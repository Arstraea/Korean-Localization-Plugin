using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Arstraea.KoreanPatch.Fonts
{
    // 모든 자료를 먼저 검증하고 이미지를 준비한 뒤 XML을 교체한다. 중단된 XML
    // 교체는 다음 실행에서 복구한다. 바뀐 외부 파일을 과거 백업으로 덮지 않는다.
    //
    // Validate all assets before publishing XML; recover interrupted replacements
    // on next launch. Never restore an old backup over externally changed files.
    internal static class FontInstaller
    {
        internal static readonly string[] Families = { "white", "white_shadow", "monospace" };
        internal sealed class Result
        {
            internal int ChangedXml, CopiedImages, HangulCount;
            internal bool OriginalIconsUnavailable;
        }
        private sealed class Plan
        {
            internal string Family, Target;
            internal byte[] Before;
            internal FontMerge.Result Merged;
        }

        internal static Result Apply(string contentPath, string workshopPath, bool hideIcons,
            Action beforeCommit = null, Action<int> afterReplace = null, Action<PatchStage> progress = null)
        {
            progress?.Invoke(PatchStage.AccessAndRecovery);
            string fonts = Path.GetFullPath(Path.Combine(contentPath, "Fonts"));
            string transaction = Path.Combine(fonts, "KoreanPatch-Transaction");
            foreach (string family in Families) FilePaths.Read(Path.Combine(fonts, family, "FontDataPA.xml"));
            RejectLinks(fonts);
            foreach (string family in Families) RejectLinks(Path.Combine(fonts, family));
            if (!Directory.Exists(fonts)) throw new DirectoryNotFoundException("Game font folder is unavailable.");
            using (var guard = FontInstallGuard.Acquire(fonts))
            {
                RejectLinks(transaction);
                Recover(fonts, transaction);
                progress?.Invoke(PatchStage.Prepare);
                var plans = new List<Plan>();
                var result = new Result();
                var sourceImages = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                foreach (string family in Families)
                {
                    string target = Path.Combine(fonts, family, "FontDataPA.xml");
                    RejectLinks(target);
                    string sourceDirectory = Path.Combine(workshopPath, "Fonts", family);
                    RejectLinks(sourceDirectory);
                    byte[] original = File.ReadAllBytes(FilePaths.Read(target));
                    var merged = FontMerge.Merge(original, File.ReadAllBytes(FilePaths.Read(Path.Combine(sourceDirectory, "FontDataPA.xml"))),
                        name => ReadImage(sourceDirectory, name, sourceImages), hideIcons, allowSharedWhite: family == "monospace");
                    result.HangulCount += merged.HangulCount;
                    result.OriginalIconsUnavailable |= merged.OriginalIconsUnavailable;
                    plans.Add(new Plan { Family = family, Target = target, Before = original, Merged = merged });
                }
                // Workshop 다운로드가 다시 시작됐다면 준비한 세트를 게시하지 않는다.
                // Recheck Workshop readiness before publishing the prepared set.
                progress?.Invoke(PatchStage.Recheck);
                beforeCommit?.Invoke();
                var changed = plans.Where(p => !p.Before.SequenceEqual(p.Merged.Xml)).ToArray();
                var images = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                var imageWrites = new List<KeyValuePair<string, byte[]>>();
                // 모든 변경 대상과 복구 경로를 확인한 뒤에 첫 이미지를 쓴다.
                //
                // Preflight every destination/recovery name before publishing the first image.
                foreach (var plan in plans)
                    foreach (var image in plan.Merged.Images)
                    {
                        string target = FilePaths.Read(Path.Combine(fonts, plan.Family, image.Key.Replace('/', Path.DirectorySeparatorChar)));
                        RejectLinks(target);
                        byte[] previous;
                        if (images.TryGetValue(target, out previous))
                        {
                            if (!previous.SequenceEqual(image.Value)) throw new InvalidDataException("Conflicting font atlas data: " + target);
                            continue;
                        }
                        images.Add(target, image.Value);
                        if (File.Exists(target) && File.ReadAllBytes(target).SequenceEqual(image.Value)) continue;
                        FilePaths.Write(target);
                        imageWrites.Add(new KeyValuePair<string, byte[]>(target, image.Value));
                    }
                foreach (var plan in changed)
                {
                    FilePaths.Write(plan.Target);
                    FilePaths.Write(Path.Combine(transaction, plan.Family + ".before"), false);
                    FilePaths.Write(Path.Combine(transaction, plan.Family + ".after"), false);
                }
                if (changed.Length > 0) FilePaths.Write(Path.Combine(transaction, "pending"));
                progress?.Invoke(PatchStage.Apply);
                foreach (var image in imageWrites)
                {
                    RejectLinks(image.Key);
                    WriteAtomically(image.Key, image.Value);
                    result.CopiedImages++;
                }
                if (changed.Length == 0) return result;
                progress?.Invoke(PatchStage.Apply);
                Directory.CreateDirectory(transaction);
                foreach (var plan in changed)
                {
                    WriteDurable(Path.Combine(transaction, plan.Family + ".before"), plan.Before);
                    WriteDurable(Path.Combine(transaction, plan.Family + ".after"), plan.Merged.Xml);
                }
                foreach (var plan in plans)
                    if (!File.ReadAllBytes(plan.Target).SequenceEqual(plan.Before))
                        throw new IOException("Font XML changed during preparation. Restart to retry.");
                WriteAtomically(Path.Combine(transaction, "pending"), System.Text.Encoding.UTF8.GetBytes(string.Join("\n", changed.Select(p => p.Family))));
                try
                {
                    foreach (var plan in changed)
                    {
                        WriteAtomically(plan.Target, plan.Merged.Xml);
                        result.ChangedXml++;
                        afterReplace?.Invoke(result.ChangedXml);
                    }
                    File.Delete(Path.Combine(transaction, "pending"));
                }
                catch
                {
                    progress?.Invoke(PatchStage.Apply);
                    Recover(fonts, transaction);
                    throw;
                }
                CleanTransaction(transaction);
                return result;
            }
        }

        internal static void Recover(string fonts, string transaction)
        {
            string marker = Path.Combine(transaction, "pending");
            RejectLinks(marker);
            if (!File.Exists(marker)) { if (Directory.Exists(transaction)) CleanTransaction(transaction); return; }
            string[] families = File.ReadAllLines(marker);
            if (families.Length == 0 || families.Distinct().Count() != families.Length || families.Any(f => !Families.Contains(f)))
                throw new InvalidDataException("Invalid font recovery journal.");
            // 먼저 전체 대상을 확인한다. 한 파일의 충돌 때문에 나머지만 복구하지 않는다.
            // Validate every target before restoring any of them.
            foreach (string family in families)
            {
                string target = Path.Combine(fonts, family, "FontDataPA.xml");
                FilePaths.Write(target);
                RejectLinks(target);
                RejectLinks(Path.Combine(transaction, family + ".before"));
                RejectLinks(Path.Combine(transaction, family + ".after"));
                byte[] current = File.ReadAllBytes(target);
                byte[] before = File.ReadAllBytes(Path.Combine(transaction, family + ".before"));
                byte[] after = File.ReadAllBytes(Path.Combine(transaction, family + ".after"));
                if (!current.SequenceEqual(before) && !current.SequenceEqual(after))
                    throw new IOException("Font recovery conflicts with an external change: " + family);
            }
            foreach (string family in families)
            {
                string target = Path.Combine(fonts, family, "FontDataPA.xml");
                byte[] before = File.ReadAllBytes(Path.Combine(transaction, family + ".before"));
                if (!File.ReadAllBytes(target).SequenceEqual(before)) WriteAtomically(target, before);
            }
            File.Delete(marker);
            CleanTransaction(transaction);
        }

        // 같은 설치에서는 공통 DDS를 한 번만 읽고 쓴다. 기존 미참조 DDS는 복구·외부 사용을 위해 보존한다.
        //
        // Read/write shared DDS once per installation; retain old unused files for recovery/external users.
        private static byte[] ReadImage(string directory, string name, Dictionary<string, byte[]> images)
        {
            string path = FilePaths.Read(Path.Combine(directory, name.Replace('/', Path.DirectorySeparatorChar)));
            RejectLinks(path);
            byte[] bytes;
            if (!images.TryGetValue(path, out bytes)) { bytes = File.ReadAllBytes(path); images.Add(path, bytes); }
            return bytes;
        }

        private static void CleanTransaction(string directory)
        {
            foreach (string family in Families)
                foreach (string suffix in new[] { ".before", ".after" })
                    File.Delete(Path.Combine(directory, family + suffix));
            // 알 수 없는 파일은 지우지 않는다.
            // Leave unrecognized files alone.
            if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
        }
        private static void WriteDurable(string path, byte[] bytes)
        {
            RejectLinks(path);
            using (var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            { output.Write(bytes, 0, bytes.Length); output.Flush(true); }
        }
        private static void WriteAtomically(string path, byte[] bytes)
        {
            string temporary = FilePaths.Temporary(path);
            try
            {
                WriteDurable(temporary, bytes);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        internal static void RejectLinks(string path)
        {
            for (string check = Path.GetFullPath(path); !string.IsNullOrEmpty(check); check = Path.GetDirectoryName(check))
                if ((Directory.Exists(check) || File.Exists(check)) && (File.GetAttributes(check) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked font paths require explicit support: " + check);
        }
    }
}
