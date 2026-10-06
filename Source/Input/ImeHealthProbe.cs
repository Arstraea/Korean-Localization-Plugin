using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;

namespace Arstraea.KoreanPatch.Input
{
    // 게임 UI와 Windows 창은 다른 스레드다. 주기 관찰은 비동기로 요청하고
    // 한 요청만 대기시켜 창 스레드 정체 때 진단 요청이 쌓이지 않도록 한다.
    //
    // Observe the two owners asynchronously. Keep at most one window probe pending
    // so an unresponsive native thread cannot accumulate diagnostic callbacks.
    internal static class ImeHealthProbe
    {
        private sealed class Identity { public int Value; }
        private static readonly ConditionalWeakTable<object, Identity> identities = new ConditionalWeakTable<object, Identity>();
        private static int nextIdentity;
        private static Control receiver;
        private static readonly Stopwatch clock = Stopwatch.StartNew();
        private static long nextProbe;
        private static int pending, keys, processKeys;

        internal static int Id(object value)
        {
            return value == null ? 0 : identities.GetValue(value,
                ignored => new Identity { Value = Interlocked.Increment(ref nextIdentity) }).Value;
        }

        internal static void Attach(Control control) { Volatile.Write(ref receiver, control); }

        internal static void ObserveKey(bool processed)
        {
            if (!ImeDiagnostics.WantsHealth) return;
            Interlocked.Increment(ref keys);
            if (processed) Interlocked.Increment(ref processKeys);
        }

        internal static void NativeState(Control control, string stage)
        {
            if (!ImeDiagnostics.WantsHealth || control == null || control.IsDisposed || !control.IsHandleCreated) return;
            ImeDiagnostics.Record(stage, health: true, metadata: "receiver=" + Id(control)
                + " focused=" + control.Focused + " parentFocused=" + (control.Parent != null && control.Parent.ContainsFocus)
                + " enabled=" + control.Enabled + " mode=" + control.ImeMode
                + " apartment=" + Thread.CurrentThread.GetApartmentState()
                + " korean=" + NativeIme.IsKoreanLayout() + " " + NativeIme.Snapshot(control.Handle));
        }

        internal static void Tick(object processor, ProcessorAccess access)
        {
            if (!ImeDiagnostics.WantsHealth) return;
            long now = clock.ElapsedMilliseconds;
            if (now < nextProbe) return;
            nextProbe = now + 2000;
            if (!access.IsConnected(processor) && Volatile.Read(ref pending) == 0) return;
            ImeDiagnostics.Record("health-game", health: true, metadata: access.Snapshot(processor)
                + " probePending=" + Volatile.Read(ref pending));
            Control control = Volatile.Read(ref receiver);
            RequestNativeProbe(control);
        }

        internal static bool ProbePending { get { return Volatile.Read(ref pending) != 0; } }

        internal static bool RequestNativeProbe(Control control)
        {
            if (!ImeDiagnostics.WantsHealth || control == null || control.IsDisposed || !control.IsHandleCreated) return false;
            if (Interlocked.CompareExchange(ref pending, 1, 0) != 0) return false;
            try
            {
                control.BeginInvoke((Action)(() =>
                {
                    try
                    {
                        NativeState(control, "health-native");
                        ImeDiagnostics.Record("health-keys", health: true, metadata: "keydown="
                            + Interlocked.Exchange(ref keys, 0) + " processKey=" + Interlocked.Exchange(ref processKeys, 0));
                    }
                    finally { Interlocked.Exchange(ref pending, 0); }
                }));
                return true;
            }
            catch (InvalidOperationException)
            {
                // 상태 확인과 비동기 요청 사이에도 입력창 핸들이 폐기될 수 있다.
                //
                // The receiver can be disposed between the checks and BeginInvoke.
                Interlocked.Exchange(ref pending, 0);
                ImeDiagnostics.Record("health-receiver-unavailable", health: true);
                return false;
            }
        }
    }
}
