using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Arstraea.KoreanPatch.Input
{
    internal static class NativeIme
    {
        public const int CompositionString = 0x0008;
        public const int ResultString = 0x0800;

        // 다음 메시지를 소비하지 않고 한국어 END→RESULT 예외의 발생 여부만 관찰한다.
        // 실제 로그가 확보되기 전에는 시간 지연이나 메시지 재정렬을 적용하지 않는다.
        //
        // Observe END-before-RESULT without consuming or reordering messages.
        // Actual traces must justify any later deferral policy.
        internal static bool ResultFollowsEnd(IntPtr window)
        {
            if (window == IntPtr.Zero) return false;
            NativeMessage next;
            return PeekMessageW(out next, window, 0x010d, 0x010f, 0)
                && next.Message == 0x010f && (next.LParam.ToInt64() & ResultString) != 0;
        }

        internal static string Snapshot(IntPtr window)
        {
            if (window == IntPtr.Zero) return "context=0";
            IntPtr context = ImmGetContext(window);
            if (context == IntPtr.Zero) return "context=0";
            try
            {
                int conversion, sentence;
                bool valid = ImmGetConversionStatus(context, out conversion, out sentence);
                int bytes = ImmGetCompositionStringW(context, CompositionString, null, 0);
                return "context=1 open=" + ImmGetOpenStatus(context) + " conversion="
                    + (valid ? conversion.ToString("X") : "unknown") + " nativeBytes=" + bytes;
            }
            finally { ImmReleaseContext(window, context); }
        }

        public static bool IsKoreanLayout()
        {
            return (GetKeyboardLayout(0).ToInt64() & 0x3ff) == 0x12;
        }

        public static bool TryGetConversion(IntPtr window, out int conversion)
        {
            conversion = 0;
            IntPtr context = ImmGetContext(window);
            if (context == IntPtr.Zero) return false;
            try
            {
                int sentence;
                return ImmGetConversionStatus(context, out conversion, out sentence);
            }
            finally { ImmReleaseContext(window, context); }
        }

        // 모든 원시 문자열을 읽은 뒤에만 게임의 조합 상태를 변경한다.
        //
        // Finish all native reads before changing the game's composition state.
        public static bool TryRead(IntPtr window, int flags, out string result, out string composition)
        {
            result = composition = string.Empty;
            IntPtr context = ImmGetContext(window);
            if (context == IntPtr.Zero) return false;
            try
            {
                return ((flags & ResultString) == 0 || TryReadString(context, ResultString, out result))
                    && ((flags & CompositionString) == 0 || TryReadString(context, CompositionString, out composition));
            }
            finally { ImmReleaseContext(window, context); }
        }

        private static bool TryReadString(IntPtr context, int kind, out string text)
        {
            text = string.Empty;
            int bytes = ImmGetCompositionStringW(context, kind, null, 0);
            if (bytes < 0 || bytes > 1024 * 1024 || (bytes & 1) != 0) return false;
            if (bytes == 0) return true;
            byte[] buffer = new byte[bytes];
            int read = ImmGetCompositionStringW(context, kind, buffer, buffer.Length);
            if (read < 0 || read > bytes || (read & 1) != 0) return false;
            text = Encoding.Unicode.GetString(buffer, 0, read);
            return true;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMessage
        {
            public IntPtr Window;
            public uint Message;
            public UIntPtr WParam;
            public IntPtr LParam;
            public uint Time;
            public int X, Y;
            public uint Private;
        }

        [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PeekMessageW(out NativeMessage message, IntPtr window, uint first, uint last, uint remove);
        [DllImport("imm32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ImmGetOpenStatus(IntPtr context);
        [DllImport("user32.dll")] private static extern IntPtr GetKeyboardLayout(uint threadId);
        [DllImport("imm32.dll")] private static extern IntPtr ImmGetContext(IntPtr window);
        [DllImport("imm32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ImmReleaseContext(IntPtr window, IntPtr context);
        [DllImport("imm32.dll", ExactSpelling = true)]
        private static extern int ImmGetCompositionStringW(IntPtr context, int kind, byte[] buffer, int length);
        [DllImport("imm32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ImmGetConversionStatus(IntPtr context, out int conversion, out int sentence);
    }
}
