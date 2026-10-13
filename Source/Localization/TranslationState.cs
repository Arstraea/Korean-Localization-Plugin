using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace Arstraea.KoreanPatch.Localization
{
    // 사용자 옵션과 분리해 저장하여 설정창 저장으로 배포 이력이 사라지지 않게 한다.
    // 설치 경로는 XML에만 기록해 PC 교체 후에도 같은 확인 이력을 읽는다.
    //
    // Keep release state separate from options, so saving settings cannot erase it.
    // Store the installation path as metadata; keep acknowledgements across PC moves.
    internal sealed class TranslationState
    {
        internal string LastSeenVersion, InstalledVersion, LastNotifiedVersion, LastConfirmedPluginVersion;
        internal string GameContentPath;

        internal static string PathFor(string content)
        {
            PluginPaths.RequireExternal(content, PluginPaths.Root);
            return Path.Combine(PluginPaths.CacheFor(content), "localization-state.xml");
        }

        internal static TranslationState Load(string path, List<string> notes)
        {
            try
            {
                if (!File.Exists(path)) return new TranslationState();
                var root = ReleaseMetadata.ReadXml(path);
                if (root.Name != "KoreanPatchTranslationState" || (string)root.Attribute("schemaVersion") != "1")
                    throw new InvalidDataException("Unsupported translation state schema.");
                return new TranslationState { GameContentPath = Value(root, "GameContentPath"), LastSeenVersion = Value(root, "LastSeenVersion"),
                    InstalledVersion = Value(root, "InstalledVersion"), LastNotifiedVersion = Value(root, "LastNotifiedVersion"),
                    LastConfirmedPluginVersion = Value(root, "LastConfirmedPluginVersion") };
            }
            catch (Exception error) when (ReleaseMetadata.IsFileError(error))
            { notes.Add("Translation version state could not be read: " + error.Message); return new TranslationState(); }
        }

        internal bool HasPendingNews(ReleaseMetadata release) => release != null
            && string.Equals(InstalledVersion, release.Version, StringComparison.Ordinal)
            && !string.Equals(LastNotifiedVersion, release.Version, StringComparison.Ordinal);

        // 확인 버튼만 기록한다. 표시·스크롤·게임 뉴스로 전환은 확인이 아니다.
        // Only explicit confirmation records this; presentation, scrolling and toggling do not.
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
                if (GameContentPath != null) root.Add(new XElement("GameContentPath", GameContentPath));
                if (LastConfirmedPluginVersion != null)
                    root.Add(new XElement("LastConfirmedPluginVersion", LastConfirmedPluginVersion));
                string xml = root.ToString();
                if (File.Exists(path) && File.ReadAllText(path) == xml) return;
                // 설치 경로·배포 기록이 모두 없으면 빈 추적 파일을 굳이 만들지 않는다.
                // Skip an empty file when neither source path nor release bookkeeping is available.
                if (!File.Exists(path) && GameContentPath == null && LastSeenVersion == null && InstalledVersion == null && LastNotifiedVersion == null
                    && LastConfirmedPluginVersion == null) return;
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
