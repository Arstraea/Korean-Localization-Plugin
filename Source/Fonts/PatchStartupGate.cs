using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace Arstraea.KoreanPatch.Fonts
{
    // Preloader 예외는 로더가 잡고 계속 실행한다. 취소는 예외가 아닌 명시적
    // 종료로 처리하며, 게임 파일 적용/복구가 끝난 뒤에만 선택을 받는다.
    //
    // Pulsar catches preloader exceptions and continues. Cancellation must exit
    // explicitly, after file application/recovery and all installer locks finish.
    internal static class PatchStartupGate
    {
        internal static void Complete()
        {
            WriteLog(FontStartup.Status);
            bool proceed = FontStartup.LaunchAllowed.HasValue
                ? FinishDecision(FontStartup.LaunchAllowed.Value, CleanupLoaderInterface, Environment.Exit, WriteLog)
                : Decide(FontStartup.Failure, ShowFailure, CleanupLoaderInterface, Environment.Exit, WriteLog);
            if (proceed && FontStartup.Failed) FontStartup.MarkContinued();
        }

        internal static bool Decide(PatchFailure failure, Func<PatchFailure, bool> show,
            Action cleanup, Action<int> exit, Action<string> log)
        {
            if (failure == null) return true;
            bool proceed = false;
            try { proceed = show(failure); }
            catch (Exception error) { log("Failure dialog could not be shown; cancelling launch: " + error); }
            return FinishDecision(proceed, cleanup, exit, log);
        }

        private static bool FinishDecision(bool proceed, Action cleanup, Action<int> exit, Action<string> log)
        {
            if (proceed)
            {
                if (FontStartup.Failed) log("Font preparation failed; user chose RUN for this launch only.");
                return true;
            }
            log("Launch CANCELLED before game initialization.");
            try { cleanup(); }
            catch (Exception error) { log("Loader interface cleanup failed: " + error); }
            // Environment.Exit은 finally를 실행하지 않으므로 로더의 보조 UI를 먼저 정리한다.
            // Environment.Exit skips finally blocks; dispose the loader-owned UI first.
            exit(0);
            return false; // Isolated tests may supply a returning exit delegate.
        }

        private static bool ShowFailure(PatchFailure failure)
        {
            bool proceed = false;
            Exception failureToShow = null;
            // 로더 주 스레드의 apartment와 게임의 전역 WinForms 설정을 바꾸지 않는다.
            // Keep the loader apartment and the game's global WinForms settings intact.
            var thread = new Thread(() =>
            {
                try
                {
                    using (var dialog = new PatchDialog(failure))
                        proceed = dialog.ShowDialog() == DialogResult.OK;
                }
                catch (Exception error) { failureToShow = error; }
            }) { Name = "KoreanPatch startup choice", IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join(); // User choice has no implicit timeout/acceptance.
            if (failureToShow != null) throw new InvalidOperationException("Startup choice failed.", failureToShow);
            return proceed;
        }

        private static Assembly Pulsar => AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Pulsar.Shared");

        internal static void CleanupLoaderInterface()
        {
            var pulsar = Pulsar;
            if (pulsar == null) return;
            // 설치된 InterfaceClient.Dispose는 자신이 시작한 보조 프로세스만 정리한다.
            // 프로세스 이름으로 게임이나 Steam을 검색해 종료하지 않는다.
            //
            // Installed InterfaceClient.Dispose owns and closes only its helper process.
            // Never enumerate/terminate other game or Steam processes by name.
            Exception failure = null;
            var worker = new Thread(() =>
            {
                try
                {
                    var tools = pulsar.GetType("Pulsar.Shared.Tools", true);
                    var client = tools.GetProperty("Interface").GetValue(null) as IDisposable;
                    client?.Dispose();
                }
                catch (Exception error) { failure = error; }
            }) { IsBackground = true, Name = "KoreanPatch loader cleanup" };
            worker.Start();
            if (!worker.Join(8000)) WriteLog("Loader interface cleanup timed out; ending this launch.");
            else if (failure != null) WriteLog("Loader interface cleanup failed: " + failure);
            try { pulsar.GetType("Pulsar.Shared.LogFile", true).GetMethod("Dispose").Invoke(null, null); }
            catch { /* Logging cleanup must not resume a cancelled game launch. */ }
        }

        internal static void WriteLog(string message)
        {
            try
            {
                Pulsar?.GetType("Pulsar.Shared.LogFile", true).GetMethod("WriteLine")
                    .Invoke(null, new object[] { "[Arstraea.KoreanPatch] " + message, null });
            }
            catch { /* Loader logging is a best-effort external boundary. */ }
        }
    }

}
