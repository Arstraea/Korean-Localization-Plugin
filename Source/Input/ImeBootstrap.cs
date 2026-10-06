using System;
using System.Reflection;
using System.Windows.Forms;
using HarmonyLib;

namespace Arstraea.KoreanPatch.Input
{
    internal static class ImeBootstrap
    {
        private const string PatchId = "Arstraea.KoreanPatch.Ime";
        private static Harmony harmony;
        private static Type candidateType;
        private static ProcessorAccess processorAccess;
        private static PropertyInfo processorInstance;
        private static FieldInfo imeControlField;
        public static bool CompositionHookInstalled { get; private set; }
        public static bool WindowInitialized { get; private set; }
        public static string Status { get; private set; } = "Preloader has not run.";

        public static void Install()
        {
            if (harmony != null) return;
            var pending = new PatchTransaction(PatchId);
            try
            {
                // 로더의 어셈블리 해석을 사용한다. 게임/Steam/Pulsar 설치 경로를 추측하지 않는다.
                //
                // Resolve through the loader; never infer game/Steam/Pulsar installation paths.
                Assembly platform = Assembly.Load("VRage.Platform.Windows");
                Assembly graphics = Assembly.Load("Sandbox.Graphics");
                Type form = platform.GetType("VRage.Platform.Windows.Forms.MyGameForm", true);
                imeControlField = AccessTools.Field(form, "m_ImeControl");
                if (imeControlField == null) throw new MissingFieldException(form.FullName, "m_ImeControl");
                candidateType = graphics.GetType("Sandbox.Graphics.GUI.MyGuiControlContextMenu", true);
                MethodInfo init = ProcessorAccess.RequireMethod(form, "Init", typeof(void), typeof(string), typeof(Type));

                pending.Add(init, prefix: Hook(nameof(WindowPrefix)), postfix: Hook(nameof(WindowPostfix)));
                harmony = pending.Apply();
                Status = "Window IME hook installed; composition hook awaits window creation.";
            }
            catch (Exception error)
            {
                Status = "IME hooks were not installed: " + error.GetType().Name + ": " + error.Message;
                if (error is PatchRollbackException) throw;
                // 시작 전에는 게임 로그가 아직 준비되지 않았을 수 있다.
                //
                // The game log may not exist yet; Plugin.Init reports this status later.
            }
        }

        private static HarmonyMethod Hook(string method)
        {
            return new HarmonyMethod(typeof(ImeBootstrap), method);
        }

        private static void WindowPrefix(ref Type __1)
        {
            // WndProc를 미리 JIT하면 MyRenderProxy의 정적 초기화까지 실행되어,
            // 아직 없는 MyVRage.Platform.Render 때문에 이후 게임 시작도 실패한다.
            // 창 생성은 렌더 준비 이후이므로 이 시점에만 입력 패치를 설치한다.
            //
            // Preparing WndProc early also initializes MyRenderProxy, poisoning its
            // static state before Platform.Render exists. Defer until window creation.
            if (!CompositionHookInstalled)
            {
                if (VRage.MyVRage.Platform == null || VRage.MyVRage.Platform.Render == null)
                {
                    Status = "Composition hook skipped: game rendering platform is not ready.";
                    return;
                }
                try
                {
                    Type processor = Assembly.Load("VRage.Platform.Windows")
                        .GetType("VRage.Platform.Windows.IME.MyImeProcessor", true);
                    processorAccess = new ProcessorAccess(processor);
                    MethodInfo wndProc = ProcessorAccess.RequireMethod(processor, "WndProc", typeof(bool), typeof(Message).MakeByRefType());
                    MethodInfo begin = ProcessorAccess.RequireMethod(processor, "EvtCompositionStart", typeof(void));
                    MethodInfo end = ProcessorAccess.RequireMethod(processor, "EvtCompositionEnd", typeof(void));
                    Type control = processor.Assembly.GetType("VRage.Platform.Windows.IME.MyGuiControlIme", true);
                    processorInstance = processor.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    if (processorInstance == null) throw new MissingMemberException(processor.FullName, "Instance");
                    Type form = processor.Assembly.GetType("VRage.Platform.Windows.Forms.MyGameForm", true);
                    var pending = new PatchTransaction(PatchId + ".Composition");
                    UnicodeMessagePump.Plan(pending);
                    MethodInfo filter = ProcessorAccess.RequireMethod(form, "PreFilterMessage", typeof(bool), typeof(Message).MakeByRefType());
                    MethodInfo activate = ProcessorAccess.RequireMethod(control, "ActivateDelegate", typeof(void));
                    MethodInfo deactivate = ProcessorAccess.RequireMethod(control, "DeactivateDelegate", typeof(void));
                    MethodInfo nativeWndProc = ProcessorAccess.RequireMethod(control, "WndProc", typeof(void), typeof(Message).MakeByRefType());
                    MethodInfo textChanged = ProcessorAccess.RequireMethod(control, "OnTextChanged", typeof(void), typeof(EventArgs));
                    MethodInfo gameActivate = ProcessorAccess.RequireMethod(processor, "Activate", typeof(void), typeof(VRage.IMyImeActiveControl));
                    MethodInfo gameDeactivate = ProcessorAccess.RequireMethod(processor, "Deactivate", typeof(void));
                    MethodInfo registerScreen = ProcessorAccess.RequireMethod(processor, "RegisterActiveScreen", typeof(void), typeof(VRage.IVRageGuiScreen));
                    MethodInfo unregisterScreen = ProcessorAccess.RequireMethod(processor, "UnregisterActiveScreen", typeof(void), typeof(VRage.IVRageGuiScreen));
                    MethodInfo caret = ProcessorAccess.RequireMethod(processor, "CaretRepositionReaction", typeof(void));
                    MethodInfo drain = ProcessorAccess.RequireMethod(processor, "ProcessInvoke", typeof(void));
                    pending.Add(drain, prefix: new HarmonyMethod(typeof(ImeQueueTrace), nameof(ImeQueueTrace.Prefix)),
                        finalizer: new HarmonyMethod(typeof(ImeQueueTrace), nameof(ImeQueueTrace.Finalizer)));
                    pending.Add(activate, prefix: new HarmonyMethod(typeof(ImeControlPolicy), nameof(ImeControlPolicy.ActivatePrefix)));
                    pending.Add(deactivate, prefix: new HarmonyMethod(typeof(ImeControlPolicy), nameof(ImeControlPolicy.DeactivatePrefix)));
                    pending.Add(wndProc, prefix: Hook(nameof(CompositionPrefix)), postfix: Hook(nameof(CharacterPostfix)));
                    pending.Add(begin, prefix: Hook(nameof(BeginPrefix)));
                    pending.Add(end, prefix: Hook(nameof(EndPrefix)));
                    pending.Add(filter, postfix: Hook(nameof(FormFocusPostfix)));
                    pending.Add(nativeWndProc, prefix: Hook(nameof(ObserveNativeMessage)));
                    pending.Add(textChanged, prefix: Hook(nameof(HiddenTextChangedPrefix)));
                    pending.Add(gameActivate, prefix: Hook(nameof(GameActivationPrefix)), postfix: Hook(nameof(GameActivationPostfix)));
                    pending.Add(registerScreen, postfix: Hook(nameof(ScreenRegisteredPostfix)));
                    pending.Add(unregisterScreen, postfix: Hook(nameof(ScreenUnregisteredPostfix)));
                    pending.Add(gameDeactivate, prefix: Hook(nameof(GameDeactivationPrefix)), postfix: Hook(nameof(GameDeactivationPostfix)));
                    pending.Add(caret, prefix: Hook(nameof(CaretPrefix)));
                    pending.Apply();
                    ImeReceiver.Configure(processorInstance, wndProc);
                    UnicodeMessagePump.Commit(form);
                    CompositionHookInstalled = true;
                    Status = "IME hooks installed at window creation; UI language is unchanged.";
                }
                catch (Exception error)
                {
                    Status = "Composition hook failed: " + error;
                    // 복구 자체가 실패했다면 남은 훅이 사용하는 참조를 버리지 않는다.
                    // 혼합된 입력 패치로 계속 시작하지 않고 창 초기화 실패를 보고한다.
                    //
                    // Failed rollback can leave live hooks: retain their state and fail
                    // window initialization rather than continue with mixed input patches.
                    if (error is PatchRollbackException) throw;
                    processorAccess = null;
                    processorInstance = null;
                    return;
                }
            }
            if (__1 == null) __1 = candidateType;
        }

        private static void WindowPostfix(Form __instance)
        {
            WindowInitialized = true;
            if (!CompositionHookInstalled) return;
            ImeStartupPreparation.Register(__instance, imeControlField.GetValue(__instance) as Control);
            if (ImeDiagnostics.WantsHealth) ImeHealthProbe.Attach(imeControlField.GetValue(__instance) as Control);
        }

        internal static void UpdateDiagnostics()
        {
            if (!CompositionHookInstalled || !ImeDiagnostics.WantsHealth) return;
            object processor = processorInstance.GetValue(null);
            if (processor != null) ImeHealthProbe.Tick(processor, processorAccess);
        }

        private static void GameActivationPostfix(object __instance)
        { if (ImeDiagnostics.WantsHealth) ImeDiagnostics.Record("game-activate", health: true, metadata: processorAccess.Snapshot(__instance)); }
        private static bool GameActivationPrefix(object __instance, VRage.IMyImeActiveControl __0)
        { return processorAccess.PrepareActivation(__instance, __0); }
        private static void ScreenRegisteredPostfix(object __instance, VRage.IVRageGuiScreen __0)
        { processorAccess.RestoreScreenInput(__instance, __0); }
        private static void ScreenUnregisteredPostfix(VRage.IVRageGuiScreen __0)
        { ImeControlPolicy.EndScreen(__0); }
        private static void GameDeactivationPrefix(object __instance)
        { if (ImeDiagnostics.WantsHealth) ImeDiagnostics.Record("game-deactivate-before", health: true, metadata: processorAccess.Snapshot(__instance)); }
        private static void GameDeactivationPostfix(object __instance)
        { if (ImeDiagnostics.WantsHealth) ImeDiagnostics.Record("game-deactivate-after", health: true, metadata: processorAccess.Snapshot(__instance)); }
        private static void CaretPrefix(object __instance)
        { if (ImeDiagnostics.WantsHealth) ImeDiagnostics.Record("game-caret-reposition", health: true, metadata: processorAccess.Snapshot(__instance)); }

        private static void HiddenTextChangedPrefix(Control __instance)
        { ImeHealthProbe.NativeState(__instance, "hidden-text-reset"); }

        internal static void ObserveNativeMessage(Control __instance, ref Message __0)
        {
            if (!ImeDiagnostics.WantsHealth && !ImeDiagnostics.WantsDetail) return;
            // 숨겨진 TextBox가 메시지를 버리기 전 경로를 관찰한다. 키 값은 저장하지 않는다.
            //
            // Observe before the hidden receiver can discard messages; never persist key values.
            int message = __0.Msg;
            if (message == 0x0100) ImeHealthProbe.ObserveKey(__0.WParam.ToInt64() == 0xe5);
            if (message == 7 || message == 8 || message == 0x0051 || message == 0x0281 || message == 0x0282)
                ImeHealthProbe.NativeState(__instance, "receiver-message-" + message.ToString("X"));
            if (message == 0x0288) ImeDiagnostics.Record("native-ime-request", flags: (int)__0.WParam.ToInt64());
        }

        private static bool BeginPrefix(object __instance)
        {
            if (!NativeIme.IsKoreanLayout()) return true;
            long origin = ImeDiagnostics.Record("native-start");
            processorAccess.Begin(__instance, origin);
            return false;
        }

        private static bool EndPrefix(object __instance)
        {
            if (!NativeIme.IsKoreanLayout() && !processorAccess.OwnsComposition(__instance)) return true;
            long origin = ImeDiagnostics.Record("native-end");
            processorAccess.End(__instance, origin);
            return false;
        }

        private static void FormFocusPostfix(Form __instance, ref Message __0)
        {
            // 원래 필터는 WM_SETFOCUS를 WinForms에 전달하지 않을 수 있으므로
            // GotFocus 이벤트만 기다리지 않고 이 창의 실제 메시지 처리 뒤에 연결한다.
            //
            // The native filter may bypass WinForms WM_SETFOCUS handling, so do not
            // depend solely on GotFocus events to restore the input receiver.
            if (__0.Msg != 7 && !(__0.Msg == 6 && (__0.WParam.ToInt64() & 0xffff) != 0)) return;
            object processor = processorInstance.GetValue(null);
            if (processor != null && processorAccess.IsConnected(processor))
                ImeControlPolicy.FocusIfActive(ImeReceiver.ExistingOrOriginal(imeControlField.GetValue(__instance) as Control));
        }

        private static void CharacterPostfix(object __instance, ref Message __0, ref bool __result)
        {
            // 엔진이 이미 WM_CHAR를 게임 입력 큐에 넣었다. 숨겨진 TextBox에도 넣으면
            // OnTextChanged의 Text=""가 네이티브 편집 상태를 바꾸어 다음 조합에 간섭한다.
            //
            // The engine already queued WM_CHAR. Forwarding it to the hidden TextBox
            // also runs its Text="" reset, disrupting the native edit/composition state.
            if (__0.Msg == 0x0102 && processorAccess.IsConnected(__instance) && NativeIme.IsKoreanLayout())
            {
                __0.Result = IntPtr.Zero;
                __result = false;
            }
        }

        // 확정 문자열을 직접 적용한 메시지는 WinForms에 다시 전달하지 않는다.
        //
        // Do not forward handled composition to WinForms: it would generate the result a second time.
        private static bool CompositionPrefix(object __instance, ref Message __0, ref bool __result)
        {
            int message = __0.Msg;
            if (ImeDiagnostics.WantsDetail && message == 0x010e && (NativeIme.IsKoreanLayout() || processorAccess.OwnsComposition(__instance)))
                ImeDiagnostics.Record("native-end-order", metadata: "resultQueued=" + NativeIme.ResultFollowsEnd(__0.HWnd));
            if (ImeDiagnostics.WantsDetail && message == 0x0102 && processorAccess.IsConnected(__instance) && NativeIme.IsKoreanLayout())
                ImeDiagnostics.Record("native-wmchar", metadata: ImeDiagnostics.Shape(((char)__0.WParam.ToInt64()).ToString()));
            if (message != 0x010f && message != 0x0286) return true;
            if (!processorAccess.IsConnected(__instance) || !NativeIme.IsKoreanLayout())
            {
                if (ImeDiagnostics.WantsDetail) ImeDiagnostics.Record("composition-bypass", metadata: "connected=" + processorAccess.IsConnected(__instance)
                    + " korean=" + NativeIme.IsKoreanLayout());
                return true;
            }
            if (message == 0x010f)
            {
                int flags = unchecked((int)__0.LParam.ToInt64());
                bool hasResult = (flags & NativeIme.ResultString) != 0;
                bool hasComposition = (flags & NativeIme.CompositionString) != 0;
                if (hasResult || hasComposition || flags == 0)
                {
                    string result, composition;
                    if (NativeIme.TryRead(__0.HWnd, flags, out result, out composition))
                    {
                        long origin = ImeDiagnostics.WantsDetail ? ImeDiagnostics.Record("native-strings", flags, result.Length, composition.Length,
                            metadata: "resultShape=" + ImeDiagnostics.Shape(result) + " compShape=" + ImeDiagnostics.Shape(composition)
                            + ((flags & 0x2000) == 0 ? "" : " inlineCharShape="
                                + ImeDiagnostics.Shape(((char)__0.WParam.ToInt64()).ToString())
                                + " inlineMatchesComposition=" + (composition.Length == 1 && composition[0] == (char)__0.WParam.ToInt64()))) : 0;
                        processorAccess.Apply(__instance, hasResult, result, hasComposition, composition, origin);
                    }
                    else
                    {
                        // 읽기 실패 때도 원래 후보 GUI 경로를 Windows 스레드에서 실행하지 않는다.
                        // 다음 유효한 스냅샷이나 종료 메시지가 조합을 정리하도록 둔다.
                        //
                        // A failed native read must not fall through to off-thread candidate GUI.
                        // Keep state until the next valid snapshot or end message.
                        ImeDiagnostics.Record("native-read-failed", flags);
                    }
                }
                else if ((flags & 0x2000) != 0) // CS_INSERTCHAR
                {
                    // 문자열 버퍼 대신 wParam으로 온 조합 문자도 처리한다. NOMOVECARET는
                    // 다음 확정 메시지가 대체할 임시 문자이며, 게임에서는 기존 조합 범위로 추적한다.
                    //
                    // Some composition messages carry a character in wParam, not a GCS buffer.
                    // NOMOVECARET marks provisional text that a later result replaces.
                    string character = ((char)__0.WParam.ToInt64()).ToString();
                    bool provisional = (flags & 0x4000) != 0;
                    long origin = ImeDiagnostics.WantsDetail ? ImeDiagnostics.Record("native-character", flags, provisional ? 0 : 1, provisional ? 1 : 0,
                        metadata: ImeDiagnostics.Shape(character)) : 0;
                    processorAccess.Apply(__instance, !provisional, provisional ? "" : character,
                        provisional, provisional ? character : "", origin);
                }
                else ImeDiagnostics.Record("native-flags-unhandled", flags);
            }
            else if (ImeDiagnostics.WantsDetail) ImeDiagnostics.Record("native-imechar-suppressed", metadata: ImeDiagnostics.Shape(((char)__0.WParam.ToInt64()).ToString()));
            // 대체 확정 경로인 WM_IME_CHAR는 막고 일반 WM_CHAR는 원래 엔진에 맡긴다.
            //
            // Block the alternate WM_IME_CHAR commit path; ordinary WM_CHAR stays with the engine.
            __0.Result = IntPtr.Zero;
            __result = false;
            return false;
        }
    }
}
