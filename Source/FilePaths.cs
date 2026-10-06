using System;
using System.IO;

namespace Arstraea.KoreanPatch
{
    // CLR/Pulsar와 게임 로더가 공통으로 읽는 경로 범위에서 임시 파일명까지 검사한다.
    // OS의 긴 경로 설정이나 호스트 manifest 변경을 지원 전제로 삼지 않는다.
    //
    // Use the common legacy CLR/game-loader path range and reserve the temporary
    // suffix before writes. Never assume host/OS long-path settings are enabled.
    internal static class FilePaths
    {
        internal const int TemporarySuffixLength = 37; // . + 32 hexadecimal characters + .tmp
        internal static string Read(string path)
        {
            string full = Path.GetFullPath(path);
            if (full.StartsWith(@"\\?\", StringComparison.Ordinal) || full.Length >= 260)
                throw new PathTooLongException("Unsupported game/CLR file path (" + full.Length + " characters): " + full);
            foreach (string part in full.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                if (part.Length > 255) throw new PathTooLongException("File path component exceeds 255 characters: " + full);
            return full;
        }
        internal static void Write(string path, bool temporary = true)
        {
            string full = Read(path);
            int required = full.Length + (temporary ? TemporarySuffixLength : 0);
            if (required >= 260 || Path.GetDirectoryName(full).Length >= 248)
                throw new PathTooLongException("File replacement/recovery path is too long (" + required + " characters): " + full);
        }
        internal static string Temporary(string path)
        {
            Write(path);
            return path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        }
    }
}
