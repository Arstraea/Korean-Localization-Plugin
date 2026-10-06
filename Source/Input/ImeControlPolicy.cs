using System;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using HarmonyLib;
using Sandbox.Game.World;

namespace Arstraea.KoreanPatch.Input
{
    // ImeMode.On은 .NET Framework에서 전각 플래그까지 켠다. 한국어는 반각
    // Hangul/Alpha를 사용하고 Disable 전에 한/영 선택을 보관해 재활성화 때 복구한다.
    //
    // Framework ImeMode.On also enables full-width characters. Use half-width
    // Hangul/Alpha for Korean, preserving the user's choice across Disable/Activate.
    internal static class ImeControlPolicy
    {
        private sealed class SavedMode
        {
            public ImeMode? Value;
            public bool RestoringFocus;
            public int Epoch;
        }

        private static readonly ConditionalWeakTable<Control, SavedMode> modes =
            new ConditionalWeakTable<Control, SavedMode>();
        internal static readonly InputLanguageMemory Policy = new InputLanguageMemory();
        private static volatile InputContext pending;
        private static volatile InputContext current;
        private static WeakReference vanillaOwner;
        private static ImeMode vanillaOwnerMode;
        private static int epoch;
        private static Harmony sessionHooks;
        internal static string SessionStatus { get; private set; } = "Input state memory: session reset hooks not initialized.";

        internal static void InstallSessionHooks()
        {
            if (sessionHooks != null) return;
            try
            {
                var hooks = new PatchTransaction("Arstraea.KoreanPatch.InputSessions");
                var reset = new HarmonyMethod(typeof(ImeControlPolicy), nameof(ResetSession));
                hooks.Add(ProcessorAccess.RequireMethod(typeof(MyScriptManager), "LoadData", typeof(void)), prefix: reset);
                hooks.Add(ProcessorAccess.RequireMethod(typeof(MyScriptManager), "UnloadData", typeof(void)), postfix: reset);
                sessionHooks = hooks.Apply();
                SessionStatus = "Input state memory: reset on world load/unload.";
            }
            catch (Exception error)
            {
                if (error is PatchRollbackException) throw;
                Policy.MemoryEnabled = false;
                Policy.Reset();
                SessionStatus = "Input state memory unavailable; new fields start in English: " + error.Message;
                Fonts.PatchStartupGate.WriteLog("Input session reset hooks unavailable: " + error);
            }
        }

        internal static void Prepare(InputContext context) { pending = context; }
        // 창 스레드에서 GUI를 조회하지 않고 활성화 때 판정한 편집 종류만 사용한다.
        // Use the editor kind captured on activation; never inspect GUI controls on the window thread.
        internal static bool AllowsLineBreak { get { return current?.Multiline == true; } }

        internal static void EndScreen(VRage.IVRageGuiScreen screen)
        {
            InputContext context = current;
            if (context != null && ReferenceEquals(context.Screen.Target, screen))
            {
                Policy.EndTarget(context.Target.Target);
                current = null;
            }
        }

        // 월드 로드/해제 때 한/영 기억만 버린다. 입력창과 네이티브 컨텍스트를 재생성하지 않는다.
        //
        // World boundaries discard mode memory without rebuilding native controls or contexts.
        internal static void ResetSession()
        {
            Policy.Reset();
            System.Threading.Interlocked.Increment(ref epoch);
            pending = current = null;
        }

        internal static void EnterVanilla(Control original, InputContext context)
        {
            pending = null;
            current = context;
            if (original == null || original.IsDisposed) return;
            Action enter = () =>
            {
                Control receiver = ImeReceiver.ExistingOrOriginal(original);
                receiver.ImeMode = ImeMode.Disable;
                original.ImeMode = ImeMode.Disable;
                Control owner = original.Parent;
                if (owner == null || owner.IsDisposed) return;
                if (vanillaOwner?.Target == null)
                { vanillaOwner = new WeakReference(owner); vanillaOwnerMode = owner.ImeMode; }
                owner.ImeMode = ImeMode.Disable;
                // 입력은 게임 창의 원래 WM_CHAR 버퍼가 받는다. 외부 앱의 포커스는 뺏지 않는다.
                // The game window's original WM_CHAR buffer receives input; never steal external focus.
                if (owner.ContainsFocus && owner.CanFocus) owner.Focus();
            };
            if (original.InvokeRequired) original.Invoke(enter); else enter();
        }

        private static void RestoreVanillaOwner()
        {
            var owner = vanillaOwner?.Target as Control;
            vanillaOwner = null;
            if (owner != null && !owner.IsDisposed) owner.ImeMode = vanillaOwnerMode;
        }

        internal static ImeMode HalfWidthMode(int conversion)
        {
            return (conversion & 1) != 0 ? ImeMode.Hangul : ImeMode.Alpha;
        }

        internal static bool ActivatePrefix(Control __instance)
        {
            RestoreVanillaOwner();
            Control receiver = ImeReceiver.ExistingOrOriginal(__instance);
            SavedMode saved = modes.GetOrCreateValue(__instance);
            int generation = System.Threading.Volatile.Read(ref epoch);
            if (saved.Epoch != generation) { saved.Value = null; saved.Epoch = generation; }
            int conversion = 0;
            bool hasMode = receiver.IsHandleCreated && NativeIme.TryGetConversion(receiver.Handle, out conversion);
            if (hasMode && receiver.ImeMode != ImeMode.Disable && current != null
                && (!current.Script || !PluginSettings.Current.VanillaScriptEditor))
                Policy.Remember(HalfWidthMode(conversion));
            InputContext next = pending;
            pending = null;
            if (!NativeIme.IsKoreanLayout())
            {
                if (next != null) Policy.Activate(next.Target.Target, next.Key, next.Chat, ImeMode.Alpha, next.Screen.Target);
                current = next ?? current;
                receiver = ImeReceiver.GetOrCreate(__instance);
                if (ReferenceEquals(receiver, __instance)) return true;
                receiver.ImeMode = ImeMode.On;
                FocusIfActive(receiver);
                return false;
            }
            ImeMode selected = saved.Value ?? (hasMode ? HalfWidthMode(conversion) : ImeMode.Alpha);
            if (next != null)
            {
                selected = Policy.Activate(next.Target.Target, next.Key, next.Chat, selected, next.Screen.Target);
                current = next;
            }
            saved.Value = null;
            receiver = ImeReceiver.GetOrCreate(__instance);
            receiver.ImeMode = selected;
            FocusIfActive(receiver);
            ImeHealthProbe.NativeState(receiver, "receiver-activated");
            return false;
        }

        internal static bool DeactivatePrefix(Control __instance)
        {
            Control receiver = ImeReceiver.ExistingOrOriginal(__instance);
            ImeHealthProbe.NativeState(receiver, "receiver-deactivating");
            int conversion;
            if (NativeIme.IsKoreanLayout() && receiver.IsHandleCreated
                && NativeIme.TryGetConversion(receiver.Handle, out conversion))
            {
                SavedMode saved = modes.GetOrCreateValue(__instance);
                saved.Value = HalfWidthMode(conversion);
                saved.Epoch = System.Threading.Volatile.Read(ref epoch);
                if (current != null && (!current.Script || !PluginSettings.Current.VanillaScriptEditor)
                    && receiver.ImeMode != ImeMode.Disable)
                    Policy.Remember(saved.Value.Value);
            }
            RestoreVanillaOwner();
            if (ReferenceEquals(receiver, __instance)) return true;
            receiver.ImeMode = ImeMode.Disable;
            return false;
        }

        // 게임 입력창 선택만으로 Windows 포커스가 숨겨진 수신 창으로 옮겨지지는 않는다.
        // 게임이 이미 포커스를 가진 경우에만 창 소유 스레드에서 입력 포커스를 보충한다.
        //
        // Selecting an in-game field does not necessarily focus the native receiver.
        // Restore its focus on the window thread, only while the game already owns focus.
        internal static bool FocusIfActive(Control control)
        {
            if (control == null || control.IsDisposed || control.Disposing || control.Focused
                || !control.CanFocus || control.Parent == null || !control.Parent.ContainsFocus)
                return false;
            SavedMode saved = modes.GetOrCreateValue(control);
            if (saved.RestoringFocus) return false;
            saved.RestoringFocus = true;
            try { return control.Focus(); }
            finally { saved.RestoringFocus = false; }
        }
    }
}
