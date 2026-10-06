using System;
using System.IO;
using System.Reflection;
using System.Security;
using System.Xml;

namespace Arstraea.KoreanPatch.Fonts
{
    // 예상 가능한 준비 실패에는 사용자 행동을 담고, 상세 예외는 별도로 보존한다.
    //
    // Keep actionable preparation failures separate from their diagnostic details.
    internal sealed class PatchPreparationException : InvalidOperationException
    {
        internal PatchPreparationException(string message) : base(message) { }
        internal PatchPreparationException(string message, Exception inner) : base(message, inner) { }
    }

    internal sealed class PatchFailure
    {
        internal PatchStage Stage { get; }
        internal string StageDisplay => PatchStages.Display(Stage);
        internal string Guidance { get; }
        internal string Details { get; }

        private PatchFailure(PatchStage stage, string guidance, string details)
        { Stage = stage; Guidance = guidance; Details = details; }

        internal string ReportText => Plugin.DisplayName + " — 한글화 플러그인 적용 오류\r\n\r\n오류가 발생한 패치 단계\r\n"
            + StageDisplay + "\r\n\r\n오류 상세\r\n" + Details;

        internal static PatchFailure FromException(PatchStage stage, Exception error)
        {
            while (error is TargetInvocationException && error.InnerException != null) error = error.InnerException;
            string guidance;
            if (error is PatchPreparationException) guidance = error.Message;
            else if (error is UnauthorizedAccessException || error is SecurityException)
                guidance = "파일 접근 권한이 부족합니다.\n\n게임과 Pulsar를 종료한 뒤, Pulsar 실행 파일 또는 바로가기를 마우스 오른쪽 버튼으로 눌러 ‘관리자 권한으로 실행’을 선택해 주세요.";
            else if (error is PathTooLongException)
                guidance = "설치 경로가 너무 길어 파일을 처리하지 못했습니다.\n\nSteam의 저장 공간 설정에서 더 짧은 경로의 라이브러리로 게임을 이동한 뒤 다시 실행해 주세요.";
            else if (error is FileNotFoundException || error is DirectoryNotFoundException)
                guidance = "필요한 파일이나 폴더를 찾지 못했습니다.\n\nSteam에서 게임 무결성 검사와 창작마당 한글화 팩 다운로드 완료를 확인한 뒤 다시 실행해 주세요.";
            else if (error is InvalidDataException || error is XmlException || error is FormatException)
                guidance = "폰트·번역 자료를 읽지 못했거나 현재 게임 폰트와 호환되지 않습니다.\n\nSteam 다운로드가 완료됐는지 확인하고, 한글화 팩 창작마당 페이지에서 제작자에게 문의하세요.";
            else if (error is IOException && ((error.HResult & 0xffff) == 32 || (error.HResult & 0xffff) == 33))
                guidance = "한글패치 적용에 필요한 파일을 다른 프로그램에서 사용하고 있어 작업을 진행하지 못했습니다.\n\n다른 게임 또는 Pulsar 실행을 모두 종료한 뒤 다시 시도해 주세요.";
            else if (error is IOException)
                guidance = "파일을 읽거나 적용·복구하지 못했습니다.\n\n다른 게임 또는 Pulsar 실행을 모두 종료한 뒤 다시 시도해 주세요. 복구에 실패한 경우 일부 파일이 변경된 상태일 수 있습니다.";
            else
                guidance = "한글패치 작업 중 오류가 발생했습니다.\n\n게임과 Pulsar를 종료한 뒤 다시 실행해 주세요.";
            guidance += "\n\n오류가 반복되면 [내역 복사]를 눌러 복사한 내용을 한글화 팩 창작마당 페이지에서 제작자에게 알려 주세요.";
            return new PatchFailure(stage, guidance, error.ToString());
        }
    }
}
