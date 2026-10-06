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
                        throw new PatchPreparationException("한글화 팩 자료 다운로드를 오래 기다렸지만 완료되지 않았습니다.\n\nSteam에서 구독·다운로드 상태를 확인하고 잠시 후 다시 실행해 주세요.");
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
                        throw new PatchPreparationException("패치 준비 중 한글화 팩 자료가 반복해서 바뀌어 적용을 중단했습니다. Steam 업데이트가 완료된 뒤 다시 실행해 주세요.");
                    ui.Publish(PatchView.Working(PatchStage.Workshop, "한글화 팩 자료가 갱신되어 최신 상태를 다시 확인합니다."));
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
            Stage = stage, Message = message ?? "패치를 준비하고 있습니다.",
            Guidance = "현재 작업이 끝나면 자동으로 다음 단계로 진행합니다.\n\n종료를 누르면 적용·복구 중인 작업을 마친 뒤 게임 실행을 종료합니다."
        };

        internal static PatchView Waiting(WorkshopSnapshot item, TimeSpan nextCheck = default(TimeSpan)) => new PatchView
        {
            Stage = PatchStage.Workshop, Download = item, NextCheck = nextCheck,
            Message = !item.Subscribed ? "Steam 창작마당에서 한글화 팩 구독이 필요합니다." : item.Downloading
                ? (item.Installed ? "한글화 팩을 최신 자료로 업데이트하고 있습니다." : "Steam 다운로드가 완료되기를 기다리고 있습니다.")
                : "Steam 다운로드가 완료되기를 기다리고 있습니다.",
            Guidance = (!item.Subscribed
                ? "아래 버튼으로 한글화 팩 창작마당 페이지를 열어 [구독]을 눌러 주세요.\n\n구독 여부를 자동으로 확인하고 다운로드가 완료되면 한글패치 적용을 시작합니다."
                : "Steam의 한글화 팩 다운로드 상태를 자동으로 확인하고 있습니다.\n\n진행되지 않으면 Steam 다운로드 목록과 인터넷 연결을 확인해 주세요.")
                + "\n\n한글화 팩 다운로드는 최대 10분 동안 기다립니다."
        };

        internal static PatchView Error(PatchFailure failure) => new PatchView
        { Stage = failure.Stage, Failure = failure, Message = "한글패치 준비가 정상적으로 완료되지 않았습니다.", Guidance = failure.Guidance };

        internal static PatchView Completed(TimeSpan? launchAt = null) => new PatchView
        {
            Stage = PatchStage.Launch, IsComplete = true, LaunchAt = launchAt,
            Message = "한글패치 준비가 완료되었습니다.",
            Guidance = "필요한 자료 확인과 폰트·번역 준비가 문제 없이 완료되었습니다."
        };
    }
}
