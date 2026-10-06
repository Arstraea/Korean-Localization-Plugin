using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using HarmonyLib;
using Sandbox.Definitions;
using Sandbox.Game.World;
using VRage.FileSystem;
using VRage.Game;
using VRage.Utils;
using VRageMath;
using VRageRender;

namespace Arstraea.KoreanPatch.Mods
{
    // 폰트 모드 전체 대신 설치된 한글 자료만 사용한다. 게임과 Pulsar 모두 거치는
    // 실제 스크립트/정의 읽기를 차단하여 저장된 모드 목록을 수정하지 않는다.
    //
    // Use installed glyph assets without executing the legacy font mod. Guard actual
    // script/definition reads shared by world and Pulsar mods; never edit saved lists.
    internal static class TextHudFontSupport
    {
        internal const string PatchId = "Arstraea.KoreanPatch.TextHudFont";
        internal const string FontName = "BI_SEOutlined";
        internal static string Status { get; private set; } = DiagnosticText.ModFont("TextHudAPI", "waiting for world load.");
        private static Harmony bootstrap;
        private static readonly Harmony runtime = new Harmony(PatchId + ".World");
        private static Data data;
        private static bool attempted, materialsReady;
        private static Assembly target;
        private static MethodInfo addCharacter;
        private static PropertyInfo characters;
        private static readonly HashSet<object> supplemented = new HashSet<object>();

        internal static void Install()
        {
            if (bootstrap != null) return;
            var candidate = new Harmony(PatchId);
            try
            {
                candidate.Patch(AccessTools.Method(typeof(MyScriptManager), "LoadData"),
                    prefix: new HarmonyMethod(typeof(TextHudFontSupport), nameof(Reset)));
                candidate.Patch(AccessTools.Method(typeof(MyScriptManager), "UnloadData"),
                    postfix: new HarmonyMethod(typeof(TextHudFontSupport), nameof(Reset)));
                candidate.Patch(AccessTools.Method(typeof(MyScriptManager), "LoadScripts"),
                    prefix: new HarmonyMethod(typeof(TextHudFontSupport), nameof(BeforeScripts)));
                candidate.Patch(AccessTools.Method(typeof(MyScriptManager), "AddAssembly"),
                    prefix: new HarmonyMethod(typeof(TextHudFontSupport), nameof(AssemblyAdded)));
                var builders = AccessTools.Method(typeof(MyDefinitionManager), "GetDefinitionBuilders");
                if (builders == null || builders.ReturnType != typeof(List<Tuple<MyObjectBuilder_Definitions, string>>))
                    throw new MissingMethodException("Definition builder loading signature changed.");
                candidate.Patch(builders, prefix: new HarmonyMethod(typeof(TextHudFontSupport), nameof(BeforeDefinitions)));
                bootstrap = candidate;
            }
            catch (Exception error)
            {
                candidate.UnpatchAll(PatchId);
                Report("hooks unavailable; error=" + error);
            }
        }

        internal static ulong SteamId(object context)
        {
            if (context == null) return 0;
            var item = AccessTools.Property(context.GetType(), "ModItem")?.GetValue(context);
            if (item == null) return 0;
            var type = item.GetType();
            if (!string.Equals(type.GetField("PublishedServiceName")?.GetValue(item) as string, "Steam", StringComparison.OrdinalIgnoreCase)) return 0;
            return type.GetField("PublishedFileId")?.GetValue(item) is ulong id ? id : 0;
        }

        private static bool BeforeScripts(object __1)
        {
            ulong id = SteamId(__1);
            if (id == FontModSpec.TextHud.FrameworkId || id == FontModSpec.TextHud.FontId) Prepare();
            if (id != FontModSpec.TextHud.FontId || data == null) return true;
            Report("legacy font mod scripts excluded (Steam: 2409196107).");
            return false;
        }

        private static bool BeforeDefinitions(object __0, ref List<Tuple<MyObjectBuilder_Definitions, string>> __result)
        {
            if (SteamId(__0) != FontModSpec.TextHud.FontId || data == null) return true;
            __result = new List<Tuple<MyObjectBuilder_Definitions, string>>();
            Report("legacy font mod definitions excluded (Steam: 2409196107).");
            return false;
        }

        private static void Prepare()
        {
            if (attempted) return;
            attempted = true;
            try
            {
                data = Read(Path.Combine(MyFileSystem.ContentPath, "Fonts", "white"));
                Report("data prepared; Hangul glyphs=" + data.Glyphs.Count + "; shared DDS=" + data.Images.Count + ".");
            }
            catch (Exception error)
            {
                data = null;
                Report("preparation failed; legacy font mod not suppressed; error=" + error);
            }
        }

        private static void AssemblyAdded(object __0, Assembly __2)
        {
            if (SteamId(__0) != FontModSpec.TextHud.FrameworkId) return;
            Prepare();
            if (data == null) return;
            try { Attach(__2); }
            catch (Exception error)
            {
                data = null;
                Report("connection failed; subsequent legacy loads not suppressed; error=" + error);
            }
        }

        internal static void Attach(Assembly assembly)
        {
            if (target == assembly) return;
            if (target != null) throw new InvalidOperationException("Multiple TextHudAPI assemblies.");
            var font = assembly.GetType("UIFun.Definition.FontDefinition", true);
            var texture = assembly.GetType("UIFun.FontTexture", true);
            var get = AccessTools.Method(texture, "GetFont", new[] { typeof(MyStringId) });
            addCharacter = AccessTools.Method(font, "TryAddCharacter", new[] { typeof(char), typeof(MyStringId), typeof(int), typeof(string),
                typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(bool) });
            characters = AccessTools.Property(font, "Characters");
            if (get == null || get.ReturnType != font || addCharacter == null || characters == null)
                throw new MissingMethodException("TextHudAPI font signature changed.");
            runtime.Patch(get, postfix: new HarmonyMethod(typeof(TextHudFontSupport), nameof(FontRequested)));
            target = assembly;
            Report("connected; waiting for BI_SEOutlined registration.");
        }

        private static void FontRequested(MyStringId __0, object __result)
        {
            if (__0.String != FontName || __result == null || data == null || supplemented.Contains(__result)) return;
            try
            {
                RegisterMaterials(data);
                int added = Supplement(__result, data, characters, addCharacter);
                supplemented.Add(__result);
                Report("supplemented; font=BI_SEOutlined; Hangul glyphs=" + added + "; existing characters/metrics preserved.");
            }
            catch (Exception error)
            {
                // 같은 렌더 경로에서 실패를 반복하지 않는다. 다음 월드에서 다시 준비한다.
                // Do not retry/log on every font request after a runtime failure.
                supplemented.Add(__result);
                Report("registration failed; error=" + error);
            }
        }

        internal static int Supplement(object font, Data source, PropertyInfo glyphProperty, MethodInfo add)
        {
            var glyphs = (IDictionary)glyphProperty.GetValue(font);
            int count = 0;
            foreach (var g in source.Glyphs)
            {
                if (glyphs.Contains(g.Character)) continue;
                if ((bool)add.Invoke(font, new object[] { g.Character, source.Images[g.Image].Material, 1024,
                    ((int)g.Character).ToString("x4"), g.X, g.Y, g.Width, g.Height, g.Advance, g.Bearing, false })) count++;
            }
            return count;
        }

        private static void RegisterMaterials(Data source)
        {
            if (materialsReady) return;
            foreach (var image in source.Images.Values) SharedHudMaterials.Register(image.Path);
            materialsReady = true;
        }

        internal sealed class Data
        {
            internal readonly Dictionary<int, Image> Images = new Dictionary<int, Image>();
            internal readonly List<Glyph> Glyphs = new List<Glyph>();
        }
        internal sealed class Image { internal string Path; internal MyStringId Material; }
        internal sealed class Glyph { internal char Character; internal int Image, X, Y, Width, Height, Advance, Bearing; }

        internal static Data Read(string folder)
        {
            var result = new Data();
            XDocument xml;
            using (var reader = XmlReader.Create(Path.Combine(folder, "FontDataPA.xml"), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                xml = XDocument.Load(reader);
            var root = xml.Root;
            XNamespace ns = root.Name.Namespace;
            var maps = root.Element(ns + "bitmaps").Elements(ns + "bitmap").ToDictionary(e => Int(e, "id"));
            var seen = new HashSet<char>();
            foreach (var e in root.Element(ns + "glyphs").Elements(ns + "glyph"))
            {
                string ch = (string)e.Attribute("ch");
                if (ch == null || ch.Length != 1 || !RichHudDefaultFont.IsHangul(ch[0])) continue;
                var xy = ((string)e.Attribute("origin")).Split(',');
                var wh = ((string)e.Attribute("size")).Split('x');
                var g = new Glyph { Character = ch[0], Image = Int(e, "bm"), X = Number(xy[0]), Y = Number(xy[1]),
                    Width = Number(wh[0]), Height = Number(wh[1]), Advance = Int(e, "aw"), Bearing = Int(e, "lsb") };
                if (!seen.Add(g.Character) || g.X < 0 || g.Y < 0 || g.Width <= 0 || g.Height <= 6
                    || (long)g.X + g.Width > 1024 || (long)g.Y + g.Height > 1024)
                    throw new InvalidDataException("Invalid Korean glyph bounds/duplicate.");
                if (!result.Images.ContainsKey(g.Image))
                {
                    var bitmap = maps[g.Image];
                    string name = (string)bitmap.Attribute("name");
                    if ((string)bitmap.Attribute("size") != "1024x1024" || string.IsNullOrEmpty(name)
                        || name != Path.GetFileName(name) || !name.StartsWith("koKR-", StringComparison.OrdinalIgnoreCase)
                        || !name.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Expected installed Korean atlas filename.");
                    string path = Path.GetFullPath(Path.Combine(folder, name));
                    if (!File.Exists(path)) throw new FileNotFoundException("Korean atlas missing", path);
                    result.Images.Add(g.Image, new Image { Path = path, Material = SharedHudMaterials.Name(path) });
                }
                result.Glyphs.Add(g);
            }
            if (result.Glyphs.Count == 0) throw new InvalidDataException("Installed font contains no Hangul.");
            return result;
        }
        private static int Int(XElement e, string attr) => Number((string)e.Attribute(attr));
        private static int Number(string value) => int.Parse(value, CultureInfo.InvariantCulture);

        internal static void Reset()
        {
            runtime.UnpatchAll(runtime.Id);
            target = null; addCharacter = null; characters = null; data = null;
            attempted = false; materialsReady = false; supplemented.Clear();
            Status = DiagnosticText.ModFont("TextHudAPI", "waiting for world load.");
        }
        private static void Report(string value) { Status = DiagnosticText.ModFont("TextHudAPI", value); MyLog.Default?.WriteLine("[Arstraea.KoreanPatch] " + Status); }
    }
}
