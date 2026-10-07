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
                case PatchStage.Paths: return "게임 설치 경로와 창작마당 한글화 팩 다운로드 위치 확인";
                case PatchStage.Workshop: return "창작마당 한글화 팩 구독 및 다운로드 완료 여부 확인";
                case PatchStage.AccessAndRecovery: return "폰트·번역 폴더 확인 후 이전에 중단된 폰트 패치 작업이 있으면 복구";
                case PatchStage.Prepare: return "폰트 설치 준비 및 번역 파일 읽기 확인";
                case PatchStage.Recheck: return "적용 전 최종 자료 확인";
                case PatchStage.Apply: return "폰트 설치, 번역 파일 적용";
                case PatchStage.Launch: return "마무리";
                default: throw new ArgumentOutOfRangeException(nameof(stage));
            }
        }
        internal static string Display(PatchStage stage) => Title(stage) + " (" + (int)stage + "/" + Count + ")";
    }
}
