using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Arstraea.KoreanPatch.Input
{
    // 문자 대신 종류별 개수와 상태만 기록한다. 난타로 상세 기록이 소진되어도
    // 포커스/상태 기록 예산은 남겨 입력 중단 이후를 관찰할 수 있게 한다.
    //
    // Log counts and state, never text. Reserve a separate health budget so a burst
    // cannot exhaust the diagnostics needed to inspect a later input stall.
    internal static class ImeDiagnostics
    {
        internal const int DetailLimit = 8192, HealthLimit = 4096, QueueLimit = 1024;
        private static readonly object gate = new object();
        private static readonly Queue<string> pending = new Queue<string>();
        private static readonly Stopwatch clock = Stopwatch.StartNew();
        private static long sequence;
        private static int detailCount, healthCount, dropped;
        // 공개 실행에서는 입력 추적과 주기적 네이티브 상태 조회를 하지 않는다.
        // 문제 재현용 실행에서만 환경 변수로 켜며 입력 본문은 계속 기록하지 않는다.
        //
        // Public runs omit input traces/native health polling. Opt in for reproduction;
        // typed contents remain excluded even when tracing is enabled.
        internal static bool Enabled = Environment.GetEnvironmentVariable("KOREANPATCH_IME_TRACE") == "1";

        // 한도 도달 안내 한 건까지 허용한다. 이후에는 호출자가 문자열/네이티브
        // 상태를 만들기 전에 중단한다. 실제 입력 처리를 이 조건으로 막지 않는다.
        //
        // Allow one exhaustion notice, then skip metadata work at the call site.
        // These gates must never suppress actual input processing.
        internal static bool WantsDetail { get { return Enabled && Volatile.Read(ref detailCount) <= DetailLimit; } }
        internal static bool WantsHealth { get { return Enabled && Volatile.Read(ref healthCount) <= HealthLimit; } }

        internal static long Record(string stage, int flags = 0, int resultLength = 0,
            int compositionLength = 0, int removed = 0, long origin = 0, string metadata = "", bool health = false)
        {
            if (!Enabled) return 0;
            lock (gate)
            {
                long index = ++sequence;
                int limit = health ? HealthLimit : DetailLimit;
                int count = health ? healthCount : detailCount;
                if (count > limit) return index;
                count++;
                if (health) healthCount = count; else detailCount = count;
                if (count > limit)
                {
                    if (count == limit + 1) Enqueue("[Arstraea.KoreanPatch/IME] "
                        + (health ? "Health" : "Detail") + " trace limit reached; input processing continues.");
                    return index;
                }
                Enqueue("[Arstraea.KoreanPatch/IME] #" + index + " ms=" + clock.ElapsedMilliseconds
                    + " t=" + Thread.CurrentThread.ManagedThreadId + " " + stage
                    + " flags=" + flags.ToString("X") + " result=" + resultLength
                    + " composition=" + compositionLength + " removed=" + removed
                    + " origin=" + origin + " " + metadata);
                return index;
            }
        }

        private static void Enqueue(string line)
        {
            if (pending.Count == QueueLimit) { pending.Dequeue(); dropped++; }
            pending.Enqueue(line);
        }

        internal static void Flush(Action<string> write)
        {
            string[] batch;
            int lost;
            lock (gate)
            {
                if (pending.Count == 0 && dropped == 0) return;
                batch = pending.ToArray();
                pending.Clear();
                lost = dropped;
                dropped = 0;
            }
            if (lost != 0) write("[Arstraea.KoreanPatch/IME] Pending trace overflow; omitted=" + lost);
            foreach (string line in batch) write(line);
        }

        internal static string Shape(string text)
        {
            int syllables = 0, jamo = 0, ascii = 0, fullwidth = 0, control = 0, other = 0;
            if (text != null) foreach (char c in text)
            {
                if (c >= '\uac00' && c <= '\ud7a3') syllables++;
                else if ((c >= '\u1100' && c <= '\u11ff') || (c >= '\u3130' && c <= '\u318f')
                    || (c >= '\ua960' && c <= '\ua97f') || (c >= '\ud7b0' && c <= '\ud7ff')) jamo++;
                else if (char.IsControl(c)) control++;
                else if (c >= ' ' && c <= '~') ascii++;
                else if (c == '\u3000' || (c >= '\uff01' && c <= '\uff60')) fullwidth++;
                else other++;
            }
            return "syllable:" + syllables + ",jamo:" + jamo + ",ascii:" + ascii
                + ",fullwidth:" + fullwidth + ",control:" + control + ",other:" + other;
        }
    }
}
