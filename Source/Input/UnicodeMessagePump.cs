using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using SharpDX.Win32;
using SharpDX.Windows;
using System.Diagnostics;
using System.Windows.Forms;

namespace Arstraea.KoreanPatch.Input
{
    // 게임의 Unicode 창을 ANSI 메시지 루프로 처리하면 한글 WM_CHAR가 바이트로
    // 분리된다. 실제 게임 루프를 Unicode로 연결하고 필터/순서/렌더는 유지한다.
    //
    // ANSI retrieval splits Korean WM_CHAR for Unicode windows. Replace only the
    // game's active pump, retaining filtering, ordering and rendering behavior.
    internal static class UnicodeMessagePump
    {
        private static Type gameFormType;
        private static int announced;
        [ThreadStatic] private static bool enabled;
        internal static bool Installed { get; private set; }
        internal static bool BoundedPumpInstalled { get; private set; }
        private static int formsAnnounced;
        private struct PumpScope
        {
            internal bool Enabled;
            internal bool DoEvents;
        }

        internal static void Plan(PatchTransaction transaction)
        {
            // 새 게임 루프는 SharpDX를 거치지 않는다. 두 경로 모두 같은 창 범위에서
            // 준비 작업을 수행하되 새 루프의 메시지 예산과 마우스 이동 분리는 유지한다.
            //
            // New game versions bypass SharpDX. Scope both paths to the game window,
            // retaining the new pump's message budget and separate mouse-move drain.
            Type pump = Assembly.Load("VRage.Platform.Windows").GetType("VRage.Platform.Windows.Forms.MyMessagePump");
            if (pump != null)
            {
                FieldInfo owner = AccessTools.Field(pump, "m_control");
                MethodInfo frame = ProcessorAccess.RequireMethod(pump, "Pump", typeof(bool));
                MethodInfo drain = AccessTools.DeclaredMethod(pump, "Drain", new[] { typeof(uint), typeof(uint), typeof(int) });
                if (owner == null || owner.IsStatic || owner.FieldType != typeof(Control)
                    || drain == null || !drain.IsStatic || drain.ReturnType != typeof(int))
                    throw new MissingMemberException(pump.FullName, "bounded message pump contract");
                transaction.Add(frame,
                    prefix: new HarmonyMethod(typeof(UnicodeMessagePump), nameof(BoundedPrefix)),
                    finalizer: new HarmonyMethod(typeof(UnicodeMessagePump), nameof(BoundedFinalizer)));
                transaction.Add(drain, prefix: new HarmonyMethod(typeof(UnicodeMessagePump), nameof(DrainPrefix)));
            }
            var target = ProcessorAccess.RequireMethod(typeof(RenderLoop), "NextFrame", typeof(bool));
            transaction.Add(target,
                prefix: new HarmonyMethod(typeof(UnicodeMessagePump), nameof(Prefix)),
                transpiler: new HarmonyMethod(typeof(UnicodeMessagePump), nameof(Transpile)),
                finalizer: new HarmonyMethod(typeof(UnicodeMessagePump), nameof(Finalizer)));
        }

        internal static void Commit(Type formType)
        {
            gameFormType = formType;
            BoundedPumpInstalled = formType.Assembly.GetType("VRage.Platform.Windows.Forms.MyMessagePump") != null;
            Installed = true;
        }

        private static void BoundedPrefix(Control ___m_control, out bool __state)
        {
            __state = enabled;
            enabled = gameFormType != null && gameFormType.IsInstanceOfType(___m_control);
            if (!enabled) return;
            ImeStartupPreparation.OnFrame(___m_control);
            if (System.Threading.Interlocked.Exchange(ref announced, 1) == 0)
                ImeDiagnostics.Record("unicode-pump-active", health: true, metadata: "path=MyMessagePump bounded=True");
        }

        private static void BoundedFinalizer(bool __state) { enabled = __state; }

        private static bool DrainPrefix(uint __0, uint __1, int __2, ref int __result)
        {
            if (!enabled) return true;
            int count = 0;
            NativeMessage native;
            while (count < __2 && PeekMessage(out native, IntPtr.Zero, unchecked((int)__0), unchecked((int)__1), 1) != 0)
            {
                count++;
                Message message = Message.Create(native.handle, unchecked((int)native.msg), native.wParam, native.lParam);
                if (FilterMessage(ref message)) continue;
                // 새 루프에는 WinForms 키 전처리가 없다. 우리 비편집 수신부에만
                // 적용해 Ctrl/Enter/메뉴 문자에 대한 기존 정책을 유지한다.
                //
                // The bounded loop omits WinForms key preprocessing. Apply it only to
                // our non-editing receiver to retain Ctrl/Enter/menu-character policy.
                var receiver = message.Msg >= 0x100 && message.Msg <= 0x109
                    ? Control.FromHandle(message.HWnd) as ImeReceiver : null;
                if (receiver != null && receiver.PreProcessMessage(ref message)) continue;
                TranslateMessage(ref native);
                DispatchMessage(ref native);
            }
            __result = count;
            return false;
        }

        private static void Prefix(RenderLoop __instance, out PumpScope __state)
        {
            __state = new PumpScope { Enabled = enabled, DoEvents = __instance.UseApplicationDoEvents };
            enabled = gameFormType != null && gameFormType.IsInstanceOfType(__instance.Control);
            if (enabled) ImeStartupPreparation.OnFrame(__instance.Control);
            // 입력 수신부가 활성인 동안 검증된 WinForms 메시지 경로를 유지한다.
            // 호출 후 원래 옵션을 복구하며 다른 창/루프의 처리 경로는 변경하지 않는다.
            //
            // Preserve the verified WinForms pump while our receiver is enabled.
            // Restore the caller's option even on failure, leaving other loops unchanged.
            if (enabled && HasEnabledReceiver(__instance.Control))
            {
                __instance.UseApplicationDoEvents = true;
                if (System.Threading.Interlocked.Exchange(ref formsAnnounced, 1) == 0)
                    ImeDiagnostics.Record("forms-pump-active", health: true);
            }
            if (enabled && System.Threading.Interlocked.Exchange(ref announced, 1) == 0)
                ImeDiagnostics.Record("unicode-pump-active", health: true,
                    metadata: "doEvents=" + __instance.UseApplicationDoEvents);
        }
        private static void Finalizer(RenderLoop __instance, PumpScope __state)
        {
            __instance.UseApplicationDoEvents = __state.DoEvents;
            enabled = __state.Enabled;
        }

        private static bool HasEnabledReceiver(Control owner)
        {
            if (owner == null || owner.IsDisposed) return false;
            foreach (Control child in owner.Controls)
                if (child is ImeReceiver && !child.IsDisposed && child.Enabled
                    && (child.ImeMode == ImeMode.Hangul || child.ImeMode == ImeMode.Alpha
                        || child.ImeMode == ImeMode.HangulFull || child.ImeMode == ImeMode.AlphaFull
                        || child.ImeMode == ImeMode.On)) return true;
            return false;
        }

        private static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            var counts = new Dictionary<string, int> { { "PeekMessage", 0 }, { "GetMessage", 0 }, { "DispatchMessage", 0 } };
            int filters = 0;
            foreach (CodeInstruction instruction in code)
            {
                var method = instruction.operand as MethodInfo;
                if (method != null && method.DeclaringType == typeof(Application) && method.Name == "FilterMessage")
                {
                    instruction.operand = AccessTools.Method(typeof(UnicodeMessagePump), nameof(FilterMessage));
                    filters++;
                    continue;
                }
                if (method == null || method.DeclaringType.FullName != "SharpDX.Win32Native" || !counts.ContainsKey(method.Name)) continue;
                instruction.operand = AccessTools.Method(typeof(UnicodeMessagePump), method.Name);
                counts[method.Name]++;
            }
            foreach (var count in counts)
                if (count.Value != 1) throw new InvalidOperationException("Unexpected RenderLoop call count: " + count.Key);
            if (filters != 1) throw new InvalidOperationException("Unexpected RenderLoop filter count.");
            return code;
        }

        private static int PeekMessage(out NativeMessage message, IntPtr window, int first, int last, int remove)
        {
            long started = Stopwatch.GetTimestamp();
            int result = enabled ? PeekMessageW(out message, window, first, last, remove) : PeekMessageA(out message, window, first, last, remove);
            TraceNative("peek", message, started, result > 0);
            return result;
        }
        private static int GetMessage(out NativeMessage message, IntPtr window, int first, int last)
        {
            long started = Stopwatch.GetTimestamp();
            int result = enabled ? GetMessageW(out message, window, first, last) : GetMessageA(out message, window, first, last);
            TraceNative("get", message, started, result > 0);
            return result;
        }
        private static int DispatchMessage(ref NativeMessage message)
        {
            long started = Stopwatch.GetTimestamp();
            TraceNative("dispatch-enter", message, started, true);
            try { return unchecked((int)(enabled ? DispatchMessageW(ref message) : DispatchMessageA(ref message)).ToInt64()); }
            finally { TraceNative("dispatch-exit", message, started, true); }
        }

        // 수신창에 도착하기 전의 누락/지연을 구분한다. 실제 키·문자·주소는 기록하지
        // 않고 대상 수신창의 종류와 PROCESSKEY 여부만 보관한다. 필터 결과는 바꾸지 않는다.
        //
        // Observe loss/delay before WndProc without recording keys, text or addresses.
        // Preserve filter behavior and restrict detailed traces to our receiver.
        private static bool FilterMessage(ref Message message)
        {
            bool trace = ImeDiagnostics.WantsDetail && IsReceiverInput(message.HWnd, (uint)message.Msg);
            int kind = message.Msg;
            long started = Stopwatch.GetTimestamp();
            bool result = Application.FilterMessage(ref message);
            if (trace) ImeDiagnostics.Record("pump-filter", flags: kind,
                metadata: "consumed=" + result + " elapsedMs=" + Elapsed(started));
            return result;
        }

        private static bool IsReceiverInput(IntPtr window, uint kind)
        {
            return enabled && (kind == 0x100 || kind == 0x101 || kind == 0x102
                || kind == 0x10d || kind == 0x10e || kind == 0x10f || kind == 0x286)
                && Control.FromHandle(window) is ImeReceiver;
        }

        private static long Elapsed(long started)
        { return (Stopwatch.GetTimestamp() - started) * 1000 / Stopwatch.Frequency; }

        private static void TraceNative(string stage, NativeMessage message, long started, bool valid)
        {
            if (!enabled) return;
            long elapsed = Elapsed(started);
            if (ImeDiagnostics.WantsDetail && valid && IsReceiverInput(message.handle, message.msg))
                ImeDiagnostics.Record("pump-" + stage, flags: (int)message.msg,
                    metadata: "elapsedMs=" + elapsed
                        + " queueAgeMs=" + unchecked((uint)Environment.TickCount - message.time)
                        + " processKey=" + (message.msg == 0x100 && message.wParam.ToInt64() == 0xe5));
            else if (elapsed >= 100 && ImeDiagnostics.WantsHealth)
                ImeDiagnostics.Record("pump-slow-" + stage, health: true,
                    metadata: "elapsedMs=" + elapsed);
        }

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] private static extern int PeekMessageW(out NativeMessage message, IntPtr window, int first, int last, int remove);
        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] private static extern int PeekMessageA(out NativeMessage message, IntPtr window, int first, int last, int remove);
        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] private static extern int GetMessageW(out NativeMessage message, IntPtr window, int first, int last);
        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] private static extern int GetMessageA(out NativeMessage message, IntPtr window, int first, int last);
        [DllImport("user32.dll", ExactSpelling = true)] private static extern IntPtr DispatchMessageW(ref NativeMessage message);
        [DllImport("user32.dll", ExactSpelling = true)] private static extern IntPtr DispatchMessageA(ref NativeMessage message);
        [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool TranslateMessage(ref NativeMessage message);
    }
}
