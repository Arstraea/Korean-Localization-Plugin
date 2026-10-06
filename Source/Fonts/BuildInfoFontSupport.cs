using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Xml.Linq;
using VRage.Game.Definitions;

namespace Arstraea.KoreanPatch.Fonts
{
    // 원본 BI 영문·아이콘을 보존하고, 실제 로드된 게임 폰트에만 한글을 보충한다.
    //
    // Preserve BI's English/icons and supplement only game fonts actually loaded.
    internal static class BuildInfoFontSupport
    {
        internal const ulong ReleaseId = 514062285, TestId = 2392775805;
        private static readonly XNamespace Ns = "http://xna.microsoft.com/bitmapfont";
        private sealed class Prepared { internal string Path; }
        private static readonly object Gate = new object();
        private static ConditionalWeakTable<object, Prepared> prepared = new ConditionalWeakTable<object, Prepared>();
        private static string content, cache, outline;
        internal static string Status { get; private set; } = DiagnosticText.ModFont("Build Info", "inactive; fonts not prepared.");

        internal static void Configure(string gameContent, string cacheFolder = null, string patchFolder = null)
        {
            lock (Gate)
            {
                content = Path.GetFullPath(gameContent);
                cache = cacheFolder ?? Path.Combine(Path.GetDirectoryName(FontStartup.SettingsPath), "Fonts", "BuildInfo");
                outline = patchFolder == null ? null : Path.Combine(Path.GetFullPath(patchFolder), "Fonts", "white_outline", "FontDataPA.xml");
                prepared = new ConditionalWeakTable<object, Prepared>();
                Status = DiagnosticText.ModHookReady("Build Info", ReleaseId + "/" + TestId, "BI_Monospace/BI_SEOutlined");
            }
        }

        // 약한 참조로 월드의 정의가 해제될 수 있게 한다. 같은 정의는 다시 병합하지 않는다.
        //
        // Weak keys let world definitions expire; never merge the same definition again.
        internal static string Redirect(object definition, string root, string requested)
        {
            if (content == null) return requested;
            string name = ((MyFontDefinition)definition).Id.SubtypeName;
            string source = MatchPath(name, root, requested);
            if (source == null) return requested;
            lock (Gate)
            {
                Prepared existing;
                if (prepared.TryGetValue(definition, out existing)) return existing.Path;
                var result = new Prepared { Path = requested };
                try
                {
                    // 작은 BI 글자용 아웃라인은 일반 UI와 분리한다. 구형 배포물은 기존 공급원을 사용한다.
                    //
                    // Separate the outline for small BI text from general UI. Older packages retain legacy donors.
                    bool dedicated = outline != null && File.Exists(outline);
                    string donor = dedicated ? outline
                        : Path.Combine(content, "Fonts", name == "BI_Monospace" ? "monospace" : "white_shadow", "FontDataPA.xml");
                    int added;
                    byte[] xml = Merge(source, donor, name, out added);
                    string digest;
                    using (var sha = SHA256.Create()) digest = BitConverter.ToString(sha.ComputeHash(xml)).Replace("-", "").ToLowerInvariant();
                    string target = Path.Combine(cache, name + "_" + digest + ".xml");
                    Publish(target, xml);
                    result.Path = target;
                    Status = DiagnosticText.ModFont("Build Info", "supplemented; font=" + name + "; Hangul glyphs=" + added
                        + "; donor=" + (dedicated ? "dedicated outline" : "legacy game font (outline package unavailable)") + "; XML=" + target);
                    PatchStartupGate.WriteLog(Status);
                }
                catch (Exception error)
                {
                    Status = DiagnosticText.ModFont("Build Info", "supplementation skipped; font=" + name + "; original font retained; error=" + error);
                    PatchStartupGate.WriteLog(Status);
                }
                prepared.Add(definition, result);
                return result.Path;
            }
        }

        internal static string MatchPath(string name, string root, string requested)
        {
            if (name != "BI_Monospace" && name != "BI_SEOutlined" || string.IsNullOrWhiteSpace(root)
                || string.IsNullOrWhiteSpace(requested) || !Path.IsPathRooted(root)) return null;
            try
            {
                string expected = Path.Combine(Path.GetFullPath(root), "Fonts", name,
                    name == "BI_Monospace" ? "BIMonospace.xml" : "FontDataPA.xml");
                string full = Path.GetFullPath(Path.IsPathRooted(requested) ? requested : Path.Combine(root, requested));
                return string.Equals(full, expected, StringComparison.OrdinalIgnoreCase) ? expected : null;
            }
            catch (ArgumentException) { return null; }
            catch (NotSupportedException) { return null; }
            catch (IOException) { return null; }
            catch (System.Security.SecurityException) { return null; }
        }

        internal static byte[] Merge(string sourcePath, string donorPath, string name, out int added)
        {
            var target = FontMerge.Read(File.ReadAllBytes(sourcePath));
            var donor = FontMerge.Read(File.ReadAllBytes(donorPath));
            int baseline = name == "BI_Monospace" ? 29 : 30;
            if (FontMerge.Attr(target.Root, "base") != baseline.ToString(CultureInfo.InvariantCulture)
                || FontMerge.Attr(target.Root, "height") != "37"
                || FontMerge.Attr(donor.Root, "base") != "30" || FontMerge.Attr(donor.Root, "height") != "37")
                throw new InvalidDataException("Build Info font metrics changed; compatibility needs review.");
            XElement glyphs = target.Root.Element(Ns + "glyphs"), bitmaps = target.Root.Element(Ns + "bitmaps");
            var original = FontMerge.GlyphMap(glyphs);
            var incoming = FontMerge.GlyphMap(donor.Root.Element(Ns + "glyphs"));
            var maps = FontMerge.BitmapMap(bitmaps);
            var sourceMaps = FontMerge.BitmapMap(donor.Root.Element(Ns + "bitmaps"));
            foreach (var bitmap in maps.Values)
                bitmap.SetAttributeValue("name", ImagePath(sourcePath, FontMerge.Attr(bitmap, "name")));
            int next = maps.Count == 0 ? 0 : maps.Keys.Max() + 1;
            var remap = new Dictionary<int, int>();
            added = 0;
            foreach (var pair in incoming.Where(p => FontMerge.IsHangul(p.Key) && !original.ContainsKey(p.Key)).OrderBy(p => p.Key))
            {
                XElement glyph = pair.Value;
                int bitmapId = int.Parse(FontMerge.Attr(glyph, "bm"), CultureInfo.InvariantCulture);
                XElement bitmap;
                if (!sourceMaps.TryGetValue(bitmapId, out bitmap)) throw new InvalidDataException("Missing Korean bitmap.");
                FontMerge.ValidateGlyph(glyph, bitmap);
                if (!remap.ContainsKey(bitmapId))
                {
                    if (next > ushort.MaxValue) throw new InvalidDataException("Too many font bitmaps.");
                    var copy = new XElement(bitmap);
                    copy.SetAttributeValue("id", next);
                    copy.SetAttributeValue("name", ImagePath(donorPath, FontMerge.Attr(bitmap, "name")));
                    bitmaps.Add(copy);
                    remap.Add(bitmapId, next++);
                }
                var addedGlyph = new XElement(glyph);
                addedGlyph.SetAttributeValue("bm", remap[bitmapId]);
                // 한글 폭·이미지 크기는 유지하고 기준선 차이만 ho로 보정한다.
                //
                // Keep Hangul advance/image size; adjust only its vertical offset for baseline differences.
                int offset = glyph.Attribute("ho") == null ? 0 : int.Parse(FontMerge.Attr(glyph, "ho"), CultureInfo.InvariantCulture);
                addedGlyph.SetAttributeValue("ho", checked((sbyte)(offset + baseline - 30)));
                glyphs.Add(addedGlyph);
                added++;
            }
            if (!incoming.Keys.Any(FontMerge.IsHangul)) throw new InvalidDataException("Prepared game font has no Hangul.");
            if (target.Root.Attribute("bitmaps") != null) target.Root.SetAttributeValue("bitmaps", bitmaps.Elements().Count());
            return FontMerge.Serialize(target);
        }

        private static string ImagePath(string xml, string image)
        {
            if (string.IsNullOrWhiteSpace(image)) throw new InvalidDataException("Empty font image path.");
            string path = Path.GetFullPath(Path.IsPathRooted(image) ? image : Path.Combine(Path.GetDirectoryName(xml), image));
            if (!File.Exists(path)) throw new FileNotFoundException("Font image is unavailable.", path);
            return path;
        }

        // 내용별로 다른 XML 경로를 써서 렌더러가 이전 자료를 재사용하지 않게 한다.
        // 이미 참조 중인 XML이나 DDS는 덮어쓰지 않는다.
        //
        // Content-specific XML paths prevent reuse of stale renderer data.
        // Never overwrite referenced XML or DDS.
        private static void Publish(string target, byte[] xml)
        {
            if (File.Exists(target))
            {
                if (!File.ReadAllBytes(target).SequenceEqual(xml)) throw new InvalidDataException("Build Info font cache is damaged. Remove this XML and retry: " + target);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            string temporary = FilePaths.Temporary(target);
            try
            {
                File.WriteAllBytes(temporary, xml);
                try { File.Move(temporary, target); }
                catch (IOException)
                { if (!File.Exists(target) || !File.ReadAllBytes(target).SequenceEqual(xml)) throw; }
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
