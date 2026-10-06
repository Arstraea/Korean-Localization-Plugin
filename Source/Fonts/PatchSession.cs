using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace Arstraea.KoreanPatch.Fonts
{
    // Steam/파일 처리는 Preloader 스레드에 유지하고 UI만 별도 STA에서 구동한다.
    // UI는 상태와 요청만 교환하며 작업 스레드나 파일 교체를 강제 종료하지 않는다.
    //
    // Keep Steam/file work on the preloader thread; run only the UI on its own STA.
    // Exchange state and requests, never abort the worker or interrupt file replacement.
    internal sealed class PatchSession : IPatchProgress, IDisposable
    {
        internal static readonly TimeSpan CompletionDelay = TimeSpan.FromSeconds(10);
        private readonly AutoResetEvent wake = new AutoResetEvent(false);
        private readonly ManualResetEvent choice = new ManualResetEvent(false);
        private readonly Stopwatch elapsed = Stopwatch.StartNew();
        private readonly Thread thread;
        private readonly Func<PatchSession, PatchDialog> createDialog;
        private readonly Func<TimeSpan> clock;
        private PatchView view = PatchView.Working(PatchStage.Paths);
        private int exitRequested, stopRequested, finished, accepted;
        private Exception uiError;
        internal bool ExitRequested => Volatile.Read(ref exitRequested) != 0;
        internal bool StopRequested => Volatile.Read(ref stopRequested) != 0;
        internal PatchView View => Volatile.Read(ref view);
        internal Exception UiError => uiError;
        internal TimeSpan Elapsed => clock();

        internal PatchSession(Func<PatchSession, PatchDialog> factory = null, Func<TimeSpan> clock = null)
        {
            createDialog = factory ?? (session => new PatchDialog(session));
            this.clock = clock ?? (() => elapsed.Elapsed);
            thread = new Thread(UiMain) { IsBackground = true, Name = "KoreanPatch preparation UI" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        public void Publish(PatchView value)
        {
            var previous = View;
            Volatile.Write(ref view, value);
            if (previous.Stage != value.Stage || previous.Message != value.Message || (previous.Failure == null) != (value.Failure == null))
                PatchStartupGate.WriteLog("Preparation [" + PatchStages.Display(value.Stage) + "]: " + value.Message);
        }

        public void CheckRequests()
        {
            if (ExitRequested) throw new OperationCanceledException("User requested cancellation of game launch.");
            if (StopRequested) throw new PatchPreparationException("사용자가 한글화 팩 다운로드 대기를 중단했습니다.\n\nSteam에서 구독·다운로드를 완료한 뒤 다시 실행해 주세요.");
        }

        internal void RequestRefresh() => wake.Set();
        internal void RequestStop()
        {
            if (!View.IsWaiting) return;
            Interlocked.Exchange(ref stopRequested, 1);
            wake.Set();
        }
        internal void RequestExit()
        {
            Interlocked.Exchange(ref exitRequested, 1);
            choice.Set();
            wake.Set();
        }
        internal void ChooseRun()
        {
            if ((!View.IsComplete && View.Failure == null) || ExitRequested) return;
            Interlocked.Exchange(ref accepted, 1);
            choice.Set();
        }
        internal void Wait(int milliseconds) => wake.WaitOne(milliseconds);

        // 완료 안내와 실패 선택은 파일 잠금과 복구 작업을 빠져나온 뒤에만 게시한다.
        // Publish completion/failure only after all file locks/recovery have unwound.
        internal bool Resolve(PatchFailure failure)
        {
            if (ExitRequested) return false;
            Publish(failure == null ? PatchView.Completed() : PatchView.Error(failure));
            choice.WaitOne();
            return !ExitRequested && Volatile.Read(ref accepted) != 0;
        }

        private void UiMain()
        {
            PatchDialog dialog = null;
            try
            {
                using (var context = new ApplicationContext())
                using (var timer = new System.Windows.Forms.Timer { Interval = 100 })
                {
                    PatchView rendered = null;
                    timer.Tick += (sender, args) =>
                    {
                        try
                        {
                            if (Volatile.Read(ref finished) != 0)
                            {
                                dialog?.FinishAndClose();
                                context.ExitThread();
                                return;
                            }
                            PatchView latest = View;
                            // 창 생성 여부는 UI 스레드에서만 판정한다. 정상 무표시 실행은
                            // 즉시 통과하고, 이미 열린 창은 완료 화면을 그릴 때부터 10초를 센다.
                            //
                            // Decide visibility on the UI thread to avoid races with Show().
                            // Silent runs proceed; an existing dialog gets ten seconds from render.
                            if (latest.IsComplete && !latest.LaunchAt.HasValue)
                            {
                                if (dialog == null) { ChooseRun(); return; }
                                latest = PatchView.Completed(Elapsed + CompletionDelay);
                                Publish(latest);
                            }
                            // 정상 파일 확인·적용은 걸린 시간에 관계없이 조용히 처리한다.
                            // 대기 안내나 오류로 연 창은 이후 적용 완료까지 재사용한다.
                            //
                            // Keep normal file preparation silent regardless of duration.
                            // Reuse a window opened for waiting/errors until preparation finishes.
                            if (dialog == null && !ExitRequested && (latest.IsWaiting || latest.Failure != null))
                            {
                                dialog = createDialog(this);
                                dialog.Render(latest);
                                rendered = latest;
                                dialog.Show();
                            }
                            if (dialog != null && (rendered != latest || ExitRequested || StopRequested))
                            {
                                dialog.Render(latest);
                                rendered = latest;
                            }
                            var now = Elapsed;
                            dialog?.TickProgress(now);
                            if (latest.IsComplete && latest.LaunchAt.HasValue && now >= latest.LaunchAt.Value)
                                ChooseRun();
                        }
                        catch (Exception error)
                        {
                            uiError = error;
                            RequestExit();
                            context.ExitThread();
                        }
                    };
                    timer.Start();
                    Application.Run(context);
                }
            }
            catch (Exception error) { uiError = error; RequestExit(); }
            finally { dialog?.Dispose(); }
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref finished, 1);
            thread.Join();
            wake.Dispose(); choice.Dispose();
        }
    }
}
