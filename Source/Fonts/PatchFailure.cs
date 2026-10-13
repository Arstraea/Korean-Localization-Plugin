using Arstraea.KoreanPatch.Localization;
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

        internal string ReportText => string.Format(KoreanText.FailureReport, Plugin.DisplayName, StageDisplay, Details);

        internal static PatchFailure FromException(PatchStage stage, Exception error)
        {
            while (error is TargetInvocationException && error.InnerException != null) error = error.InnerException;
            string guidance;
            if (error is PatchPreparationException) guidance = error.Message;
            else if (error is UnauthorizedAccessException || error is SecurityException)
                guidance = KoreanText.FileAccessDenied;
            else if (error is PathTooLongException)
                guidance = KoreanText.FilePathTooLong;
            else if (error is FileNotFoundException || error is DirectoryNotFoundException)
                guidance = KoreanText.RequiredFilesMissing;
            else if (error is InvalidDataException || error is XmlException || error is FormatException)
                guidance = KoreanText.InvalidFontOrTranslation;
            else if (error is IOException && ((error.HResult & 0xffff) == 32 || (error.HResult & 0xffff) == 33))
                guidance = KoreanText.FileInUse;
            else if (error is IOException)
                guidance = KoreanText.CachePreparationFailed;
            else
                guidance = KoreanText.UnexpectedPatchError;
            guidance += KoreanText.FailureContactHint;
            return new PatchFailure(stage, guidance, error.ToString());
        }
    }
}
