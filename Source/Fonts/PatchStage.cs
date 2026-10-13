using Arstraea.KoreanPatch.Localization;
using System;

namespace Arstraea.KoreanPatch.Fonts
{
    // SPECIFICATION.md의 단계 번호와 제목을 사용한다. 내부 함수 호출 수가 아니다.
    //
    // Match SPECIFICATION.md stages, not the number of internal function calls.
    internal enum PatchStage
    {
        Paths = 1,
        Workshop = 2,
        AccessAndRecovery = 3,
        Prepare = 4,
        Recheck = 5,
        Apply = 6,
        Launch = 7
    }

    internal static class PatchStages
    {
        internal const int Count = 7;
        internal static string Title(PatchStage stage)
        {
            switch (stage)
            {
                case PatchStage.Paths: return KoreanText.StagePaths;
                case PatchStage.Workshop: return KoreanText.StageWorkshop;
                case PatchStage.AccessAndRecovery: return KoreanText.StageAccessAndRecovery;
                case PatchStage.Prepare: return KoreanText.StagePrepare;
                case PatchStage.Recheck: return KoreanText.StageRecheck;
                case PatchStage.Apply: return KoreanText.StageApply;
                case PatchStage.Launch: return KoreanText.StageLaunch;
                default: throw new ArgumentOutOfRangeException(nameof(stage));
            }
        }
        internal static string Display(PatchStage stage) => Title(stage) + " (" + (int)stage + "/" + Count + ")";
    }
}
