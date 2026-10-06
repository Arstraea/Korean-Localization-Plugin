using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace Arstraea.KoreanPatch.Localization
{
    // 사용자 옵션과 분리해 저장하여 설정창 저장으로 배포 이력이 사라지지 않게 한다.
    // 게임 설치 경로별로 구분하고 본문/전체 번역은 보관하지 않는다.
    //
    // Keep per-installation release state separate from user options, so saving settings
    // cannot erase it. Never persist a second translation catalog here.
    internal sealed class TranslationState
    {
        internal string LastSeenVersion, InstalledVersion, LastNotifiedVersion;

        internal static string PathFor(string content)
        {
            string canonical = Path.GetFullPath(content).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant();
            string key;
            using (var hash = SHA256.Create()) key = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(canonical))).Replace("-", "");
            return Path.Combine(Path.GetDirectoryName(Fonts.FontStartup.SettingsPath), "Installations", key, "localization-state.xml");
        }

        internal static TranslationState Load(string path, List<string> notes)
        {
            try
            {
                if (!File.Exists(path)) return new TranslationState();
                var root = ReleaseMetadata.ReadXml(path);
                if (root.Name != "KoreanPatchTranslationState" || (string)root.Attribute("schemaVersion") != "1")
                    throw new InvalidDataException("Unsupported translation state schema.");
                return new TranslationState { LastSeenVersion = Value(root, "LastSeenVersion"),
                    InstalledVersion = Value(root, "InstalledVersion"), LastNotifiedVersion = Value(root, "LastNotifiedVersion") };
            }
            catch (Exception error) when (ReleaseMetadata.IsFileError(error))
            { notes.Add("Translation version state could not be read: " + error.Message); return new TranslationState(); }
        }

        internal bool HasPendingNews(ReleaseMetadata release) => release != null
            && string.Equals(InstalledVersion, release.Version, StringComparison.Ordinal)
            && !string.Equals(LastNotifiedVersion, release.Version, StringComparison.Ordinal);

        // 후속 UI는 실제로 안내한 뒤에만 이 값을 저장해야 한다. 현재 실행 경로에서는 호출하지 않는다.
        // A future UI must save this only after presentation; current startup never calls it.
        internal void MarkNotified(ReleaseMetadata release)
        { if (HasPendingNews(release)) LastNotifiedVersion = release.Version; }

        internal void SaveIfChanged(string path, List<string> notes)
        {
            string temporary = null;
            try
            {
                var root = new XElement("KoreanPatchTranslationState", new XAttribute("schemaVersion", "1"),
                    new XElement("LastSeenVersion", LastSeenVersion ?? ""), new XElement("InstalledVersion", InstalledVersion ?? ""),
                    new XElement("LastNotifiedVersion", LastNotifiedVersion ?? ""));
                string xml = root.ToString();
                if (File.Exists(path) && File.ReadAllText(path) == xml) return;
                // 새 메타데이터가 없으면 비어 있는 추적 파일을 굳이 만들지 않는다.
                // Older packages need no empty bookkeeping file.
                if (!File.Exists(path) && LastSeenVersion == null && InstalledVersion == null && LastNotifiedVersion == null) return;
                temporary = FilePaths.Temporary(path);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(temporary, xml, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            catch (Exception error) when (ReleaseMetadata.IsFileError(error))
            { notes.Add("Translation version state could not be saved (installation unaffected): " + error.Message); }
            finally
            {
                try { if (temporary != null && File.Exists(temporary)) File.Delete(temporary); }
                catch (Exception error) when (ReleaseMetadata.IsFileError(error)) { notes.Add("State temporary file cleanup failed: " + error.Message); }
            }
        }
        private static string Value(XElement root, string name)
        { string text = ((string)root.Element(name))?.Trim(); return string.IsNullOrEmpty(text) ? null : text; }
    }
}
