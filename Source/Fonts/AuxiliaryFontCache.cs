using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arstraea.KoreanPatch.Mods;

namespace Arstraea.KoreanPatch.Fonts
{
    // 실행 중 Workshop 자료 교체와 겹치지 않도록 BI/RichHud 자료도 같은 캐시에 둔다.
    // 게임 자료와 팩은 읽기만 하며 공유 plain DDS는 일반 폰트 캐시를 사용한다.
    //
    // Keep BI/RichHud inputs in the same cache to avoid live Workshop replacement.
    // Read game/pack files only; use the shared plain atlases prepared for stock fonts.
    internal static class AuxiliaryFontCache
    {
        internal static void Prepare(string package, string cache, Action recheck)
        {
            using (FontInstallGuard.Acquire(cache))
            {
                // 최종 확인 뒤 공급 파일을 다시 읽지 않고 검사한 사본을 게시한다.
                //
                // Publish validated snapshots without reopening source files after the final recheck.
                var files = new List<KeyValuePair<string, byte[]>>();
                string rich = Path.Combine(package, RichHudFontData.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                string richTarget = Path.Combine(PluginPaths.ModSupportFor(cache, "RichHudMaster"), RichHudFontData.FileName);
                files.Add(new KeyValuePair<string, byte[]>(richTarget, ReadOptional(rich)));
                string outline = Path.Combine(package, "Fonts", "white_outline", "FontDataPA.xml");
                string outlineTarget = Path.Combine(cache, "Fonts", "white_outline", "FontDataPA.xml");
                byte[] outlineBytes = ReadOptional(outline);
                if (outlineBytes != null)
                {
                    var xml = FontMerge.Read(outlineBytes);
                    var ns = xml.Root.Name.Namespace;
                    foreach (var bitmap in xml.Root.Element(ns + "bitmaps").Elements())
                    {
                        string name = (string)bitmap.Attribute("name");
                        if (!FontMerge.IsLocalAtlas(name)) continue;
                        string image = Path.Combine(Path.GetDirectoryName(outline), name);
                        byte[] bytes = ReadOptional(image) ?? throw new FileNotFoundException("Build Info outline atlas missing.", image);
                        FontMerge.ValidateDds(bytes, (string)bitmap.Attribute("size"));
                        files.Add(new KeyValuePair<string, byte[]>(Path.Combine(Path.GetDirectoryName(outlineTarget), name), bytes));
                    }
                }
                files.Add(new KeyValuePair<string, byte[]>(outlineTarget, outlineBytes));
                foreach (var file in files)
                {
                    FontInstaller.RejectLinks(file.Key);
                    FilePaths.Read(file.Key);
                    if (file.Value != null && (!File.Exists(file.Key) || !File.ReadAllBytes(file.Key).SequenceEqual(file.Value)))
                        FilePaths.Write(file.Key);
                }
                recheck?.Invoke();
                foreach (var file in files) PublishOptional(file.Key, file.Value);
            }
        }

        private static byte[] ReadOptional(string source)
        {
            FontInstaller.RejectLinks(source);
            return File.Exists(source) ? File.ReadAllBytes(FilePaths.Read(source)) : null;
        }

        private static void PublishOptional(string target, byte[] bytes)
        {
            FontInstaller.RejectLinks(target);
            if (bytes == null)
            { if (File.Exists(target)) File.Delete(target); return; }
            if (File.Exists(target) && File.ReadAllBytes(target).SequenceEqual(bytes)) return;
            string temporary = FilePaths.Temporary(target);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            try
            {
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { output.Write(bytes, 0, bytes.Length); output.Flush(true); }
                if (File.Exists(target)) File.Replace(temporary, target, null); else File.Move(temporary, target);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
