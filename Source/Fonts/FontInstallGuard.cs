using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Arstraea.KoreanPatch.Fonts
{
    // 검사만 하는 실행도 게임 폴더에 잠금 파일을 쓰던 문제를 피한다.
    // 경로별 Windows 잠금은 파일 권한과 무관하며, 중단된 소유자는 XML 복구로 이어진다.
    //
    // Serialize by installation without writing a lock file on read-only checks.
    // An abandoned owner still proceeds through the installer's journal recovery.
    internal sealed class FontInstallGuard : IDisposable
    {
        private Mutex mutex;
        private bool acquired;
        private FileStream legacyLock;

        internal static FontInstallGuard Acquire(string fonts)
        {
            var guard = new FontInstallGuard();
            try
            {
                string canonical = Path.GetFullPath(fonts).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant();
                string key;
                // 경로를 일정 길이의 잠금 이름으로만 변환한다. 이미지 해시는 아니다.
                // Only bound the installation lock name; this does not hash font assets.
                using (var hash = SHA256.Create())
                    key = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(canonical))).Replace("-", "");
                guard.mutex = new Mutex(false, @"Global\Arstraea.KoreanPatch.Fonts." + key);
                try { guard.acquired = guard.mutex.WaitOne(0); }
                catch (AbandonedMutexException) { guard.acquired = true; }
                if (!guard.acquired) throw new PatchPreparationException("다른 실행에서 같은 게임의 한글패치 폰트를 준비하고 있습니다.\n\n다른 게임 또는 Pulsar 실행을 종료한 뒤 다시 시도해 주세요.");

                // 이전 빌드 또는 진단 도구가 이미 만든 잠금은 존중하되 새로 만들지 않는다.
                // Honor an existing legacy/diagnostic lock without creating one.
                string legacy = Path.Combine(fonts, "KoreanPatch.lock");
                FontInstaller.RejectLinks(legacy);
                if (File.Exists(legacy))
                {
                    try { guard.legacyLock = new FileStream(legacy, FileMode.Open, FileAccess.Read, FileShare.None); }
                    catch (FileNotFoundException) { } // The legacy owner may have just removed it.
                }
                return guard;
            }
            catch { guard.Dispose(); throw; }
        }

        public void Dispose()
        {
            try { legacyLock?.Dispose(); }
            finally
            {
                try { if (acquired) { mutex.ReleaseMutex(); acquired = false; } }
                finally { mutex?.Dispose(); }
            }
        }
    }
}
