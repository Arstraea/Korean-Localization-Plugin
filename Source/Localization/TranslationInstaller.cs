using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Arstraea.KoreanPatch.Fonts;

namespace Arstraea.KoreanPatch.Localization
{
    // 번역은 원문과 병합하거나 별도 사전에 보관하지 않는다. 배포 파일을 그대로
    // 캐시에 준비하고 게임의 기본 영어 → 선택 언어 로더가 읽게 한다.
    //
    // Cache published translations verbatim, without merging English or retaining a
    // translation dictionary. The native English -> selected-language loader owns text memory.
    internal static class TranslationInstaller
    {
        internal static readonly string[] RelativeFiles = {
            Path.Combine("Common", "MyCommonTexts.ko-KR.resx"), "MyTexts.ko-KR.resx",
            Path.Combine("CoreTexts", "MyCoreTexts.ko-KR.resx") };

        internal sealed class Result
        {
            internal string ResourceRoot, GameRoot;
            internal int InstalledFiles, PrimaryFiles, CopiedFiles;
            internal bool Available => PrimaryFiles > 0;
            internal readonly List<string> Notes = new List<string>();
            internal ReleaseMetadata Release;
            internal EconomyDatapadCatalog EconomyDatapads = EconomyDatapadCatalog.Empty;
            internal string Summary => "Korean translations: files=" + InstalledFiles + ", copied=" + CopiedFiles
                + ", version=" + (Release?.Version ?? "unknown") + "."
                + (Notes.Count == 0 ? "" : "\n" + string.Join("\n", Notes));
        }

        internal static Result Apply(string content, string package, string statePath,
            Action recheck = null, Action<PatchStage> progress = null, string cacheFolder = null)
        {
            var result = new Result();
            if (string.IsNullOrEmpty(content) || string.IsNullOrEmpty(package))
            { result.Notes.Add("Translation installation paths unavailable."); return result; }
            cacheFolder = Path.GetFullPath(cacheFolder ?? PluginPaths.CacheFor(content));
            PluginPaths.RequireExternal(content, cacheFolder);
            string gameRoot = Path.Combine(content, "Data", "Localization");
            string targetRoot = Path.Combine(cacheFolder, "Data", "Localization");
            result.GameRoot = Path.GetFullPath(gameRoot);
            result.ResourceRoot = targetRoot;
            string sourceRoot = Path.Combine(package, "Data", "Localization");
            progress?.Invoke(PatchStage.AccessAndRecovery);
            FontInstaller.RejectLinks(targetRoot);
            // 폰트와 같은 설치별 잠금을 재사용한다. 대기 중에는 잠금을 잡지 않는다.
            // Reuse the per-installation guard, never holding it while waiting for Steam.
            using (FontInstallGuard.Acquire(cacheFolder))
            {
                progress?.Invoke(PatchStage.Prepare);
                result.Release = ReleaseMetadata.ReadOptional(Path.Combine(package,
                    ReleaseMetadata.RelativePath.Replace('/', Path.DirectorySeparatorChar)), result.Notes);
                var state = TranslationState.Load(statePath, result.Notes);
                if (result.Release != null)
                {
                    state.LastSeenVersion = result.Release.Version;
                    state.SaveIfChanged(statePath, result.Notes);
                }
                var changes = new bool[RelativeFiles.Length];
                var sources = new string[RelativeFiles.Length];
                int publishedPrimary = 0;
                for (int i = 0; i < RelativeFiles.Length; i++)
                {
                    string source = Path.Combine(sourceRoot, RelativeFiles[i]);
                    string target = Path.Combine(targetRoot, RelativeFiles[i]);
                    FilePaths.Read(source);
                    FilePaths.Read(target);
                    FontInstaller.RejectLinks(source);
                    FontInstaller.RejectLinks(target);
                    if (File.Exists(source))
                    {
                        Validate(source);
                        changes[i] = !SameBytes(source, target);
                        if (i < 2) publishedPrimary++;
                    }
                    else if (File.Exists(Path.Combine(gameRoot, RelativeFiles[i])))
                    {
                        source = Path.Combine(gameRoot, RelativeFiles[i]);
                        FilePaths.Read(source);
                        FontInstaller.RejectLinks(source);
                        Validate(source);
                        changes[i] = !SameBytes(source, target);
                        result.Notes.Add("Reusing manually installed translation: " + RelativeFiles[i]);
                    }
                    else { result.Notes.Add("Missing: " + RelativeFiles[i]); continue; }
                    result.InstalledFiles++;
                    sources[i] = source;
                    if (i < 2) result.PrimaryFiles++;
                }

                progress?.Invoke(PatchStage.Recheck);
                recheck?.Invoke();
                for (int i = 0; i < RelativeFiles.Length; i++)
                    if (changes[i]) FilePaths.Write(Path.Combine(targetRoot, RelativeFiles[i]));
                progress?.Invoke(PatchStage.Apply);
                for (int i = 0; i < RelativeFiles.Length; i++)
                {
                    if (!changes[i]) continue;
                    CopyAtomically(sources[i], Path.Combine(targetRoot, RelativeFiles[i]));
                    result.CopiedFiles++;
                }
                // 새 팩에서 없어진 파일은 캐시에만 제거한다. 게임의 과거 설치본은 보존한다.
                // Drop removed cached resources only; never delete old game-installed files.
                for (int i = 0; i < RelativeFiles.Length; i++)
                    if (sources[i] == null && File.Exists(Path.Combine(targetRoot, RelativeFiles[i])))
                        File.Delete(Path.Combine(targetRoot, RelativeFiles[i]));

                // 성공한 설치와 관측/안내한 버전은 별개다. 파일 내용이 다르면 같은 버전이라도
                // 교체하므로 버전 번호 누락/실수 때문에 파일 갱신을 건너뛰지 않는다.
                // Record applied/seen/notified versions separately. Version equality never skips
                // a differing file, so missing or unchanged metadata cannot block a real update.
                state.InstalledVersion = publishedPrimary > 0 && result.Available ? result.Release?.Version : null;
                // 경로 변경은 읽음 기록을 초기화하지 않는다. 실제 파일 비교·폰트 재병합이
                // 복원된 캐시를 현재 설치에 맞추며, 이 경로는 준비한 설치를 설명하는 기록이다.
                //
                // A moved installation does not reset acknowledgements. File comparison/font
                // merging refresh restored cache data; this path only describes the prepared source.
                state.GameContentPath = Path.GetFullPath(content);
                state.SaveIfChanged(statePath, result.Notes);
                // 기존 이름 파일은 대조용 18개 항목만 읽으며 게임 번역 로더에 넣지 않는다.
                //
                // Read only 18 source entries; never load the manual-install RESX into MyTexts.
                result.EconomyDatapads = EconomyDatapadCatalog.Load(Path.Combine(sourceRoot, "MyTexts.resx"), result.Notes);
                return result;
            }
        }

        internal static void Validate(string path)
        {
            using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
            {
                reader.MoveToContent();
                if (reader.Name != "root" || reader.NamespaceURI.Length != 0) throw new XmlException("RESX root is missing: " + path);
                while (reader.Read())
                    if (reader.NodeType == XmlNodeType.Element && reader.Depth == 1 && reader.Name == "data"
                        && reader.GetAttribute("name") == null)
                        throw new XmlException("RESX data name is missing: " + path);
            }
        }

        internal static bool SameBytes(string source, string target)
        {
            if (!File.Exists(target)) return false;
            using (var a = File.OpenRead(source))
            using (var b = File.OpenRead(target))
            {
                if (a.Length != b.Length) return false;
                var left = new byte[8192];
                var right = new byte[8192];
                int count;
                while ((count = a.Read(left, 0, left.Length)) != 0)
                {
                    int read = 0;
                    while (read < count)
                    {
                        int n = b.Read(right, read, count - read);
                        if (n == 0) return false;
                        read += n;
                    }
                    for (int i = 0; i < count; i++) if (left[i] != right[i]) return false;
                }
                return b.ReadByte() == -1;
            }
        }

        private static void CopyAtomically(string source, string target)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            string temporary = FilePaths.Temporary(target);
            try
            {
                using (var input = File.OpenRead(source))
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { input.CopyTo(output, 8192); output.Flush(true); }
                Validate(temporary);
                if (File.Exists(target)) File.Replace(temporary, target, null);
                else File.Move(temporary, target);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
