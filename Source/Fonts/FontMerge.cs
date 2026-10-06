using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Arstraea.KoreanPatch.Fonts
{
    // 최신 게임 정의를 기준으로 한글만 병합한다. 플랫폼 아이콘 원형은 XML의 별도
    // 메타데이터에 보존한다. 게임 로더는 bitmaps/glyphs/kernpairs만 읽는다.
    //
    // Merge Hangul into current game definitions. Preserve platform glyph originals
    // in metadata outside the three sections consumed by the game's font loader.
    internal static class FontMerge
    {
        private const string LegacyAssetDirectory = "KoreanPatch";
        private static readonly XNamespace Ns = "http://xna.microsoft.com/bitmapfont";
        private static readonly XNamespace StateNs = "urn:arstraea:koreanpatch:font:1";
        internal sealed class Result
        {
            internal byte[] Xml;
            internal readonly Dictionary<string, byte[]> Images = new Dictionary<string, byte[]>();
            internal int HangulCount;
            internal bool OriginalIconsUnavailable;
        }

        internal static XDocument Read(byte[] bytes)
        {
            using (var input = new MemoryStream(bytes))
            using (var reader = XmlReader.Create(input, new XmlReaderSettings
            { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16 * 1024 * 1024 }))
            {
                var doc = XDocument.Load(reader);
                if (doc.Root == null || doc.Root.Name != Ns + "font") throw new InvalidDataException("Unknown font XML format.");
                return doc;
            }
        }

        internal static Result Merge(byte[] current, byte[] patch, Func<string, byte[]> readImage, bool hideIcons,
            bool allowSharedWhite = false)
        {
            var target = Read(current);
            var source = Read(patch);
            foreach (string metric in new[] { "base", "height" })
                if (Attr(target.Root, metric) != Attr(source.Root, metric))
                    throw new InvalidDataException("Font metrics changed: " + metric + ". Update the Korean font package.");
            XElement glyphs = Section(target, "glyphs"), bitmaps = Section(target, "bitmaps");
            XElement sourceGlyphs = Section(source, "glyphs"), sourceBitmaps = Section(source, "bitmaps");
            var oldGlyphs = GlyphMap(glyphs);
            var incoming = GlyphMap(sourceGlyphs).Where(pair => IsHangul(pair.Key)).OrderBy(pair => pair.Key).ToArray();
            if (incoming.Length == 0) throw new InvalidDataException("Package has no Hangul glyphs.");
            var sourceMaps = BitmapMap(sourceBitmaps);
            var maps = BitmapMap(bitmaps);
            var result = new Result { HangulCount = incoming.Length };

            // 이전 적용의 한글만 치환한다. 비한글이 쓰는 bitmap은 삭제하지 않는다.
            //
            // Replace previous Hangul; retain any bitmap still used by non-Hangul.
            foreach (var pair in oldGlyphs.Where(p => IsHangul(p.Key)).ToArray()) pair.Value.Remove();
            var retainedIds = new HashSet<int>(glyphs.Elements(Ns + "glyph").Select(g => Number(g, "bm")));
            foreach (var pair in maps.ToArray())
            {
                string name = Attr(pair.Value, "name");
                if (!retainedIds.Contains(pair.Key) && (name.StartsWith(LegacyAssetDirectory + "/", StringComparison.Ordinal)
                    || IsLocalAtlas(name) || IsSharedWhiteAtlas(name)))
                { pair.Value.Remove(); maps.Remove(pair.Key); }
            }
            int nextId = maps.Count == 0 ? 0 : maps.Keys.Max() + 1;
            var remap = new Dictionary<int, int>();
            foreach (var pair in incoming)
            {
                XElement g = pair.Value;
                int sourceId = Number(g, "bm");
                XElement bitmap;
                if (!sourceMaps.TryGetValue(sourceId, out bitmap)) throw new InvalidDataException("Missing Hangul bitmap definition.");
                if (!remap.ContainsKey(sourceId))
                {
                    string name = Attr(bitmap, "name");
                    if (!IsLocalAtlas(name) && !(allowSharedWhite && IsSharedWhiteAtlas(name)))
                        throw new InvalidDataException("Unexpected Hangul image name: " + name);
                    // monospace만 white의 같은 DDS를 참조하도록 허용한다. 다른 상위 경로는 거부한다.
                    //
                    // Only monospace may reuse white atlases; reject arbitrary parent paths.
                    if (IsSharedWhiteAtlas(name)) name = "../white/koKR-" + Path.GetFileName(name).Substring(5).ToLowerInvariant();
                    byte[] data = readImage(name);
                    ValidateDds(data, Attr(bitmap, "size"));
                    // 수동 한글패치와 같은 위치를 써서 기존 이미지를 중복 설치하지 않는다.
                    // Use the manual patch's layout so existing atlases can be reused.
                    string installedName = name;
                    if (nextId > ushort.MaxValue) throw new InvalidDataException("Too many font bitmaps.");
                    var added = new XElement(bitmap);
                    added.SetAttributeValue("id", nextId);
                    added.SetAttributeValue("name", installedName);
                    bitmaps.Add(added);
                    remap.Add(sourceId, nextId++);
                    result.Images[installedName] = data;
                }
                ValidateGlyph(g, bitmap);
                var copy = new XElement(g);
                copy.SetAttributeValue("bm", remap[sourceId]);
                glyphs.Add(copy);
            }
            XElement kern = target.Root.Element(Ns + "kernpairs");
            if (kern != null)
                foreach (var pair in kern.Elements().Where(HasHangulPair).ToArray()) pair.Remove();
            XElement incomingKern = source.Root.Element(Ns + "kernpairs");
            if (incomingKern != null)
                foreach (var pair in incomingKern.Elements().Where(HasHangulPair))
                {
                    if (kern == null) { kern = new XElement(Ns + "kernpairs"); target.Root.Add(kern); }
                    kern.Add(new XElement(pair));
                }

            XElement state = target.Root.Element(StateNs + "KoreanPatchFontState");
            if (state == null) { state = new XElement(StateNs + "KoreanPatchFontState"); target.Root.Add(state); }
            foreach (int code in new[] { 0xe030, 0xe031, 0xe032 })
            {
                XElement glyph;
                if (!oldGlyphs.TryGetValue(code, out glyph)) continue;
                XElement saved = state.Elements(Ns + "glyph").FirstOrDefault(g => Code(g) == code);
                if (!IsHidden(glyph))
                {
                    // 게임 갱신이나 외부 변경으로 들어온 새 원형을 우선한다.
                    // Prefer the current original after game updates or external changes.
                    if (saved != null) saved.Remove();
                    saved = new XElement(glyph); state.Add(saved);
                }
                if (hideIcons)
                { glyph.SetAttributeValue("size", "1x1"); glyph.SetAttributeValue("aw", "0"); glyph.SetAttributeValue("lsb", "0"); }
                else if (saved != null) glyph.ReplaceWith(new XElement(saved.Name,
                    saved.Attributes().Where(a => !a.IsNamespaceDeclaration), saved.Nodes()));
                else throw new InvalidDataException("Original platform icons are unavailable. Verify game files once before enabling platform icons.");
                if (saved == null) result.OriginalIconsUnavailable = true;
            }
            if (target.Root.Attribute("bitmaps") != null) target.Root.SetAttributeValue("bitmaps", bitmaps.Elements(Ns + "bitmap").Count());
            result.Xml = Serialize(target);
            return result;
        }

        private static bool IsHidden(XElement g) { return Attr(g, "size") == "1x1" && Attr(g, "aw") == "0"; }
        internal static bool IsLocalAtlas(string name)
        { return System.Text.RegularExpressions.Regex.IsMatch(name, @"\AkoKR-\d+\.dds\z", System.Text.RegularExpressions.RegexOptions.IgnoreCase); }
        internal static bool IsSharedWhiteAtlas(string name)
        { return System.Text.RegularExpressions.Regex.IsMatch(name, @"\A\.\./white/koKR-(?:[1-9]|1[0-9]|2[0-2])\.dds\z", System.Text.RegularExpressions.RegexOptions.IgnoreCase); }
        internal static bool IsHangul(int c)
        { return c >= 0xac00 && c <= 0xd7a3 || c >= 0x3130 && c <= 0x318f || c >= 0x1100 && c <= 0x11ff || c >= 0xa960 && c <= 0xa97f || c >= 0xd7b0 && c <= 0xd7ff; }
        private static bool HasHangulPair(XElement p)
        { return Attr(p, "left").Any(c => IsHangul(c)) || Attr(p, "right").Any(c => IsHangul(c)); }
        private static XElement Section(XDocument d, string name)
        { return d.Root.Element(Ns + name) ?? throw new InvalidDataException("Missing " + name); }
        internal static string Attr(XElement e, string a) { return (string)e.Attribute(a) ?? ""; }
        private static int Number(XElement e, string a) { return int.Parse(Attr(e, a), CultureInfo.InvariantCulture); }
        private static int Code(XElement g)
        {
            int code = int.Parse(Attr(g, "code"), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            string ch = Attr(g, "ch");
            if (ch.Length != 1 || ch[0] != code) throw new InvalidDataException("Glyph code and character differ.");
            return code;
        }
        internal static Dictionary<int, XElement> GlyphMap(XElement section) { return section.Elements(Ns + "glyph").ToDictionary(Code); }
        internal static Dictionary<int, XElement> BitmapMap(XElement section) { return section.Elements(Ns + "bitmap").ToDictionary(b => Number(b, "id")); }
        private static int[] Pair(string value, char split) { return value.Split(split).Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToArray(); }
        internal static void ValidateGlyph(XElement g, XElement bitmap)
        {
            int[] xy = Pair(Attr(g, "origin"), ','), size = Pair(Attr(g, "size"), 'x'), map = Pair(Attr(bitmap, "size"), 'x');
            if (xy.Length != 2 || size.Length != 2 || map.Length != 2 || xy.Any(v => v < 0 || v > ushort.MaxValue)
                || size.Any(v => v < 0 || v > byte.MaxValue) || xy[0] + size[0] > map[0] || xy[1] + size[1] > map[1])
                throw new InvalidDataException("Hangul glyph is outside its image.");
            byte.Parse(Attr(g, "aw"), CultureInfo.InvariantCulture);
            sbyte.Parse(Attr(g, "lsb"), CultureInfo.InvariantCulture);
            if (g.Attribute("ho") != null) sbyte.Parse(Attr(g, "ho"), CultureInfo.InvariantCulture);
        }
        private static void ValidateDds(byte[] bytes, string size)
        {
            int[] dimensions = Pair(size, 'x');
            if (bytes.Length < 128 || bytes[0] != 'D' || bytes[1] != 'D' || bytes[2] != 'S' || bytes[3] != ' '
                || BitConverter.ToInt32(bytes, 4) != 124 || dimensions.Length != 2
                || BitConverter.ToInt32(bytes, 12) != dimensions[1] || BitConverter.ToInt32(bytes, 16) != dimensions[0])
                throw new InvalidDataException("Invalid or mismatching DDS header.");
            int width = dimensions[0], height = dimensions[1], header = 128, block;
            if (width <= 0 || height <= 0 || width > 16384 || height > 16384 || BitConverter.ToInt32(bytes, 76) != 32)
                throw new InvalidDataException("Invalid DDS dimensions or pixel format header.");
            string format = Encoding.ASCII.GetString(bytes, 84, 4);
            if (format == "DXT1") block = 8;
            else if (format == "DXT3" || format == "DXT5") block = 16;
            else if (format == "DX10")
            {
                if (bytes.Length < 148 || BitConverter.ToInt32(bytes, 132) != 3 || BitConverter.ToInt32(bytes, 140) != 1
                    || (BitConverter.ToInt32(bytes, 136) & 4) != 0)
                    throw new InvalidDataException("Expected a single 2D DDS image.");
                int dxgi = BitConverter.ToInt32(bytes, 128);
                if (dxgi >= 70 && dxgi <= 72 || dxgi >= 79 && dxgi <= 81) block = 8;
                else if (dxgi >= 73 && dxgi <= 78 || dxgi >= 82 && dxgi <= 84 || dxgi >= 94 && dxgi <= 99) block = 16;
                else throw new InvalidDataException("Unsupported DDS font compression: " + dxgi);
                header = 148;
            }
            else throw new InvalidDataException("Unsupported DDS font compression: " + format);
            int levels = Math.Max(1, BitConverter.ToInt32(bytes, 28));
            int maxLevels = 1;
            for (int edge = Math.Max(width, height); edge > 1; edge /= 2) maxLevels++;
            if (levels > maxLevels) throw new InvalidDataException("Invalid DDS mip count.");
            long required = header;
            for (int level = 0; level < levels; level++)
            {
                required += (long)Math.Max(1, (width + 3) / 4) * Math.Max(1, (height + 3) / 4) * block;
                width = Math.Max(1, width / 2); height = Math.Max(1, height / 2);
            }
            if (bytes.LongLength < required) throw new InvalidDataException("Incomplete DDS pixel data.");
        }
        internal static byte[] Serialize(XDocument doc)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true, NewLineChars = "\n" })) doc.Save(writer);
                return stream.ToArray();
            }
        }
    }
}
