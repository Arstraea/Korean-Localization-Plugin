using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using VRage;
using VRageMath;
using Atlas = VRage.MyTuple<string, VRageMath.Vector2>;
using Glyph = VRage.MyTuple<int, VRageMath.Vector2, VRageMath.Vector2, float, float>;
using Style = VRage.MyTuple<int, float, float, VRage.MyTuple<string, VRageMath.Vector2>[], System.Collections.Generic.KeyValuePair<char, VRage.MyTuple<int, VRageMath.Vector2, VRageMath.Vector2, float, float>>[], System.Collections.Generic.KeyValuePair<uint, float>[]>;
using Font = VRage.MyTuple<string, float, VRage.MyTuple<int, float, float, VRage.MyTuple<string, VRageMath.Vector2>[], System.Collections.Generic.KeyValuePair<char, VRage.MyTuple<int, VRageMath.Vector2, VRageMath.Vector2, float, float>>[], System.Collections.Generic.KeyValuePair<uint, float>[]>[]>;

namespace Arstraea.KoreanPatch.Mods
{
    // 미리 변환한 자료만 읽으며 실행 때 게임 XML을 다시 변환하거나 이미지를 만들지 않는다.
    //
    // Read precomputed data only; never reconvert game XML or generate images at runtime.
    internal sealed class RichHudFontData
    {
        internal const string RelativePath = "Korean Patch Plugin/RichHudMaster/SpaceEngineers.Hangul.xml";
        internal Font Font;
        internal string[] ImagePaths;

        internal static RichHudFontData Load(string package, string installedWhite)
        {
            // 자료는 창작마당에서만 읽어 로컬 DLL과 Pulsar 소스 배포의 동작을 동일하게 유지한다.
            // 누락·손상은 준비 실패로 알리고 기존 폰트 모드의 읽기 제외를 시작하지 않는다.
            //
            // Use Workshop data only so local DLL and source distribution behave alike.
            // Report missing/corrupt data before suppressing the legacy font mod.
            if (string.IsNullOrEmpty(package)) throw new FileNotFoundException("Korean Localization Pack location is unavailable for RichHud font support.");
            string external = FilePaths.Read(Path.Combine(package, RelativePath.Replace('/', Path.DirectorySeparatorChar)));
            using (var stream = File.OpenRead(external)) return Read(stream, installedWhite);
        }

        internal static RichHudFontData Read(Stream stream, string installedWhite)
        {
            var atlases = new List<Atlas>(); var paths = new List<string>();
            var glyphs = new List<KeyValuePair<char, Glyph>>(); var seen = new HashSet<char>();
            using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
            {
                reader.MoveToContent();
                if (reader.Name != "RichHudHangul" || reader.GetAttribute("version") != "1")
                    throw new InvalidDataException("Unsupported precomputed RichHud font format.");
                float point = Number(reader, "pointSize"), height = Number(reader, "height"), baseline = Number(reader, "baseline");
                if (point != 23 || height != 37 || baseline != 30)
                    throw new InvalidDataException("Reconvert changed RichHud source font metrics.");
                int rootDepth = reader.Depth;
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == rootDepth) break;
                    if (reader.NodeType != XmlNodeType.Element) continue;
                    if (reader.Name == "Atlases" || reader.Name == "Glyphs") continue;
                    if (reader.Name == "Atlas")
                    {
                        int id = Integer(reader, "id"); string file = reader.GetAttribute("file");
                        float width = Number(reader, "width"), atlasHeight = Number(reader, "height");
                        if (id != atlases.Count || width != 1024 || atlasHeight != 1024 || string.IsNullOrEmpty(file)
                            || file != Path.GetFileName(file) || !file.StartsWith("koKR-", StringComparison.Ordinal)
                            || !file.EndsWith(".dds", StringComparison.Ordinal))
                            throw new InvalidDataException("Invalid shared RichHud atlas.");
                        string path = Path.GetFullPath(Path.Combine(installedWhite, file));
                        if (!File.Exists(path)) throw new FileNotFoundException("Shared Korean atlas missing", path);
                        atlases.Add(new Atlas(SharedHudMaterials.Name(path).String, new Vector2(width, atlasHeight))); paths.Add(path);
                    }
                    else if (reader.Name == "Glyph")
                    {
                        int code = Integer(reader, "code"), atlas = Integer(reader, "atlas");
                        float width = Number(reader, "width"), h = Number(reader, "height"), x = Number(reader, "x"), y = Number(reader, "y");
                        float advance = Number(reader, "advance"), bearing = Number(reader, "bearing");
                        if (code < 0 || code > char.MaxValue || !RichHudDefaultFont.IsHangul((char)code) || !seen.Add((char)code)
                            || atlas < 0 || atlas >= atlases.Count || width <= 0 || h <= 0 || x < 0 || y < 0
                            || x + width > atlases[atlas].Item2.X || y + h > atlases[atlas].Item2.Y)
                            throw new InvalidDataException("Invalid precomputed Hangul glyph.");
                        glyphs.Add(new KeyValuePair<char, Glyph>((char)code, new Glyph(atlas, new Vector2(width, h), new Vector2(x, y), advance, bearing)));
                    }
                    else throw new InvalidDataException("Unexpected RichHud data element: " + reader.Name);
                }
                while (reader.Read()) { } // 끝까지 파싱하여 잘린 문서나 두 번째 루트도 거부한다.
                if (glyphs.Count == 0) throw new InvalidDataException("Precomputed RichHud data contains no Hangul.");
                return new RichHudFontData { ImagePaths = paths.ToArray(), Font = new Font("KoreanPatch_Hangul", point,
                    new[] { new Style(0, height, baseline, atlases.ToArray(), glyphs.ToArray(), new KeyValuePair<uint, float>[0]) }) };
            }
        }
        private static int Integer(XmlReader reader, string attribute) => int.Parse(reader.GetAttribute(attribute), CultureInfo.InvariantCulture);
        private static float Number(XmlReader reader, string attribute)
        {
            float value = float.Parse(reader.GetAttribute(attribute), CultureInfo.InvariantCulture);
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("Non-finite RichHud metric.");
            return value;
        }
    }
}
