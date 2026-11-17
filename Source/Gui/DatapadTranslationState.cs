using System;
using System.Text.RegularExpressions;

namespace Arstraea.KoreanPatch.Gui
{
    // 원본 키와 열람용 문장을 분리한다. 초기 표시의 잘림·치환을 사용자 편집으로
    // 보지 않는다. 키가 있는 패드는 미수정 항목을 보존하고, 거래소 패드는 사용자가
    // 수정해 저장할 때 화면의 제목·본문을 함께 저장한다.
    //
    // Keep item tokens separate from the rendered baseline. Initial substitution or
    // clipping is not an edit. Key-based pads retain untouched raw fields; an edited
    // station pad saves both displayed fields together.
    internal sealed class DatapadTranslationState
    {
        private static readonly Regex tokens = new Regex(@"\{LOC(?:G|R)?:([^{}]+)\}");
        internal readonly string RawTitle, RawBody;
        internal readonly bool Translated;
        internal readonly bool HasLocalizationKeys;
        internal string DisplayTitle { get; private set; }
        internal string DisplayBody { get; private set; }
        internal string InitialTitle, InitialBody;
        internal bool Initialized;

        private DatapadTranslationState(string title, string body, bool translated, bool hasLocalizationKeys = true)
        { RawTitle = title; RawBody = body; Translated = translated; HasLocalizationKeys = hasLocalizationKeys; }

        internal static DatapadTranslationState FromEconomy(string title, string body, string displayTitle, string displayBody)
            => new DatapadTranslationState(title, body, false, false) { DisplayTitle = displayTitle, DisplayBody = displayBody };

        internal static DatapadTranslationState Capture(string title, string body, Func<string, string> resolve)
        {
            if (!HasToken(title) && !HasToken(body)) return null;
            return new DatapadTranslationState(title, body, Resolved(title, resolve) || Resolved(body, resolve));
        }

        private static bool HasToken(string value) => value != null && tokens.IsMatch(value);

        private static bool Resolved(string value, Func<string, string> resolve)
        {
            if (value == null) return false;
            foreach (Match match in tokens.Matches(value))
            {
                string result = resolve(match.Value);
                if (!string.IsNullOrEmpty(result) && result != match.Value && result != match.Groups[1].Value
                    && !HasToken(result)) return true;
            }
            return false;
        }

        internal bool TitleChanged(string value) => !Same(InitialTitle, value);
        internal bool BodyChanged(string value) => !Same(InitialBody, value);
        internal bool Changed(string title, string body) => TitleChanged(title) || BodyChanged(body);
        // 무수정은 저장 진입점에서 이미 차단된다. 저장을 진행하는 거래소 패드는
        // 한쪽만 영문으로 남지 않도록 현재 표시값을 둘 다 전송한다.
        //
        // The save entry point already skips unmodified pads. When an edited station
        // pad reaches saving, send both display values to avoid mixed-language records.
        internal string SaveTitle(string value) => !HasLocalizationKeys || TitleChanged(value) ? value : RawTitle;
        internal string SaveBody(string value) => !HasLocalizationKeys || BodyChanged(value) ? value : RawBody;
        internal static bool Same(string left, string right) => string.Equals(left ?? "", right ?? "", StringComparison.Ordinal);
    }
}
