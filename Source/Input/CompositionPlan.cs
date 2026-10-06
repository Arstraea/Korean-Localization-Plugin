using System;

namespace Arstraea.KoreanPatch.Input
{
    // 게임에 보낸 조합 문자열 길이만 삭제한다. IME 원문 길이는 입력 제한으로 잘렸을 수 있다.
    //
    // Delete only the composition actually inserted; native text may exceed the input limit.
    internal sealed class CompositionPlan
    {
        public int RemoveCount { get; private set; }
        public string Commit { get; private set; }
        public string Composition { get; private set; }
        public int WrittenCount { get; private set; }

        public static CompositionPlan Create(string previous, int written, int limit,
            bool hasResult, string result, bool hasComposition, string composition)
        {
            int oldCount = Math.Min((previous ?? string.Empty).Length, Math.Max(0, written));
            int committed = Math.Max(0, written - oldCount);
            int available = Math.Max(0, limit - committed);
            string commit = hasResult ? Fit(result, available) : string.Empty;
            available -= commit.Length;
            string next = hasComposition ? Fit(composition, available) : string.Empty;
            return new CompositionPlan
            {
                RemoveCount = oldCount,
                Commit = commit,
                Composition = next,
                WrittenCount = committed + commit.Length + next.Length
            };
        }

        private static string Fit(string value, int count)
        {
            if (string.IsNullOrEmpty(value) || count <= 0) return string.Empty;
            if (value.Length <= count) return value;
            if (char.IsHighSurrogate(value[count - 1]) && char.IsLowSurrogate(value[count])) count--;
            return value.Substring(0, count);
        }
    }
}
