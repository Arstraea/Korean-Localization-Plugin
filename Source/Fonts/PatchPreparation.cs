using Arstraea.KoreanPatch.Localization;
using System;

namespace Arstraea.KoreanPatch.Fonts
{
    internal sealed class WorkshopChangedException : Exception { }

    // 파일 잠금 안에서 대기하지 않는다. 준비 자료가 바뀌면 Apply가 빠져나와 잠금을
    // 해제한 뒤 다시 다운로드를 기다린다. 시계·입출력을 주입해 실제 다운로드 없이 검증한다.
    //
    // Never wait while holding the installer lock. Let Apply unwind before awaiting
    // changed content. Inject time and I/O so transitions can be tested without Steam.
    internal static class PatchPreparation
    {
        internal static readonly TimeSpan WaitLimit = TimeSpan.FromMinutes(10);
        internal const int MaxPreparations = 3;

        internal static FontInstaller.Result Run(IWorkshopSource source, IPatchProgress ui,
            Func<WorkshopSnapshot, Action, Action<PatchStage>, FontInstaller.Result> apply,
            Func<TimeSpan> elapsed, Action<int> wait)
        {
            TimeSpan waitStarted = elapsed();
            for (int attempt = 0; attempt < MaxPreparations; attempt++)
            {
                bool requested = false;
                WorkshopSnapshot package;
                while (true)
                {
                    ui.CheckRequests();
                    source.Pump();
                    package = source.Read();
                    if (package.Ready) break;
                    source.EnsureConnected();
                    int interval = package.Subscribed ? 1000 : 5000;
                    ui.Publish(PatchView.Waiting(package, elapsed() + TimeSpan.FromMilliseconds(interval)));
                    if (elapsed() - waitStarted >= WaitLimit)
                        throw new PatchPreparationException(KoreanText.DownloadTimedOut);
                    if (package.Subscribed && !requested)
                    {
                        source.RequestDownload();
                        requested = true;
                    }
                    wait(interval);
                }
                ui.CheckRequests();
                try
                {
                    var result = apply(package, () =>
                    {
                        ui.CheckRequests();
                        source.Pump();
                        if (!package.SamePackage(source.Read())) throw new WorkshopChangedException();
                    }, stage => ui.Publish(PatchView.Working(stage)));
                    return result;
                }
                catch (WorkshopChangedException)
                {
                    if (attempt + 1 == MaxPreparations)
                        throw new PatchPreparationException(KoreanText.PackChangedRepeatedly);
                    ui.Publish(PatchView.Working(PatchStage.Workshop, KoreanText.PackChanged));
                }
            }
            throw new InvalidOperationException("Preparation attempt limit exceeded.");
        }
    }

    internal interface IPatchProgress
    {
        void Publish(PatchView view);
        void CheckRequests();
    }

    internal sealed class PatchView
    {
        internal PatchStage Stage { get; private set; }
        internal string Message { get; private set; }
        internal string Guidance { get; private set; }
        internal WorkshopSnapshot Download { get; private set; }
        internal PatchFailure Failure { get; private set; }
        internal TimeSpan NextCheck { get; private set; }
        internal bool IsComplete { get; private set; }
        internal TimeSpan? LaunchAt { get; private set; }
        internal bool IsWaiting => Download != null;

        internal static PatchView Working(PatchStage stage, string message = null) => new PatchView
        {
            Stage = stage, Message = message ?? KoreanText.PreparingPatch,
            Guidance = KoreanText.WorkingGuidance
        };

        internal static PatchView Waiting(WorkshopSnapshot item, TimeSpan nextCheck = default(TimeSpan)) => new PatchView
        {
            Stage = PatchStage.Workshop, Download = item, NextCheck = nextCheck,
            Message = !item.Subscribed ? KoreanText.SubscriptionRequired : item.Downloading
                ? (item.Installed ? KoreanText.UpdatingPack : KoreanText.WaitingForDownload)
                : KoreanText.WaitingForDownload,
            Guidance = (!item.Subscribed
                ? KoreanText.SubscriptionGuidance
                : KoreanText.DownloadGuidance)
                + KoreanText.DownloadWaitLimit
        };

        internal static PatchView Error(PatchFailure failure) => new PatchView
        { Stage = failure.Stage, Failure = failure, Message = KoreanText.PreparationFailed, Guidance = failure.Guidance };

        internal static PatchView Completed(TimeSpan? launchAt = null) => new PatchView
        {
            Stage = PatchStage.Launch, IsComplete = true, LaunchAt = launchAt,
            Message = KoreanText.PreparationComplete,
            Guidance = KoreanText.PreparationCompleteGuidance
        };
    }
}
