using System;
using System.Threading;

namespace Arstraea.KoreanPatch.Input
{
    // 엔진 큐를 바꾸지 않고 한 번의 배출에 묶인 작업을 구분한다. 재진입 때는
    // 외부 배출 번호를 복구해 동기화 타이밍 가설을 로그로 검증할 수 있게 한다.
    //
    // Label existing queue drains without changing them. Restore the outer label
    // after reentrancy so batching hypotheses can be checked from actual traces.
    internal static class ImeQueueTrace
    {
        private static long nextBatch;
        [ThreadStatic] internal static long CurrentBatch;

        internal static void Prefix(out long __state)
        {
            __state = CurrentBatch;
            CurrentBatch = Interlocked.Increment(ref nextBatch);
        }

        internal static void Finalizer(long __state) { CurrentBatch = __state; }
    }
}
