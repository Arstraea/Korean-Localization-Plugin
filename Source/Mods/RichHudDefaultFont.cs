using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Sandbox.Definitions;
using Sandbox.Game.World;
using VRage.FileSystem;
using VRage.Game;
using VRage;
using VRage.Utils;
using VRageMath;
using Atlas = VRage.MyTuple<string, VRageMath.Vector2>;
using Glyph = VRage.MyTuple<int, VRageMath.Vector2, VRageMath.Vector2, float, float>;
using Style = VRage.MyTuple<int, float, float, VRage.MyTuple<string, VRageMath.Vector2>[], System.Collections.Generic.KeyValuePair<char, VRage.MyTuple<int, VRageMath.Vector2, VRageMath.Vector2, float, float>>[], System.Collections.Generic.KeyValuePair<uint, float>[]>;
using Font = VRage.MyTuple<string, float, VRage.MyTuple<int, float, float, VRage.MyTuple<string, VRageMath.Vector2>[], System.Collections.Generic.KeyValuePair<char, VRage.MyTuple<int, VRageMath.Vector2, VRageMath.Vector2, float, float>>[], System.Collections.Generic.KeyValuePair<uint, float>[]>[]>;
using Format = VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>;

namespace Arstraea.KoreanPatch.Mods
{
    // 실제 모드 어셈블리가 등록될 때 연결하여 첫 UI 생성 전에 기본 글꼴을 보충한다.
    // 미리 변환한 한글 정보와 설치 DDS를 사용하며, 위치 보정은 추가한 글자에만 적용한다.
    //
    // Attach to identified mod assemblies before their first UI initialization.
    // Use precomputed Hangul and installed DDS; adjust layout only for supplied glyphs.
    internal static class RichHudDefaultFont
    {
        internal const string PatchId = "Arstraea.KoreanPatch.RichHudDefault";
        private const string RuntimeId = PatchId + ".World";
        internal static string Status { get; private set; } = DiagnosticText.ModFont("RichHudMaster", "waiting for mod assemblies.");
        private static Harmony bootstrap;
        private static readonly Harmony runtime = new Harmony(RuntimeId);
        private static RichHudFontData prepared;
        private static bool attempted;
        private static Assembly target;
        private static Func<object, int, char> readChar;
        private static Func<object, int, Format> readFormat;
        private static Dictionary<char, float> offsets = new Dictionary<char, float>();
        [ThreadStatic] private static int layoutDepth;

        internal static void Install()
        {
            if (bootstrap != null) return;
            var candidate = new Harmony(PatchId);
            try
            {
                candidate.Patch(AccessTools.Method(typeof(MyScriptManager), "LoadData"),
                    prefix: new HarmonyMethod(typeof(RichHudDefaultFont), nameof(Reset)));
                candidate.Patch(AccessTools.Method(typeof(MyScriptManager), "LoadScripts"),
                    prefix: new HarmonyMethod(typeof(RichHudDefaultFont), nameof(BeforeScripts)));
                var builders = AccessTools.Method(typeof(MyDefinitionManager), "GetDefinitionBuilders");
                if (builders == null || builders.ReturnType != typeof(List<Tuple<MyObjectBuilder_Definitions, string>>))
                    throw new MissingMethodException("Definition builder loading signature changed.");
                candidate.Patch(builders, prefix: new HarmonyMethod(typeof(RichHudDefaultFont), nameof(BeforeDefinitions)));
                candidate.Patch(AccessTools.Method(typeof(MyScriptManager), "AddAssembly"),
                    prefix: new HarmonyMethod(typeof(RichHudDefaultFont), nameof(AssemblyAdded)));
                candidate.Patch(AccessTools.Method(typeof(MyScriptManager), "UnloadData"),
                    postfix: new HarmonyMethod(typeof(RichHudDefaultFont), nameof(Reset)));
                bootstrap = candidate;
            }
            catch (Exception error)
            {
                candidate.UnpatchAll(PatchId);
                Report("hooks unavailable; error=" + error);
            }
        }

        private static void AssemblyAdded(object __0, Assembly __2)
        {
            if (__2 == null || TextHudFontSupport.SteamId(__0) != FontModSpec.RichHud.FrameworkId) return;
            Prepare();
            if (prepared == null) return;
            try { Observe(FontModSpec.RichHud.FrameworkId, __2); }
            catch (Exception error)
            {
                prepared = null;
                Report("connection failed; subsequent legacy loads not suppressed; error=" + error);
            }
        }

        private static void Prepare()
        {
            if (attempted) return;
            attempted = true;
            try
            {
                prepared = RichHudFontData.Load(Fonts.FontStartup.PackageFolder,
                    System.IO.Path.Combine(MyFileSystem.ContentPath, "Fonts", "white"));
                Report("data prepared; Hangul glyphs=" + prepared.Font.Item3[0].Item5.Length
                    + "; shared DDS=" + prepared.ImagePaths.Length + ".");
            }
            catch (Exception error)
            {
                prepared = null;
                Report("preparation failed; legacy font mod not suppressed; error=" + error);
            }
        }

        private static bool BeforeScripts(object __1)
        {
            ulong id = TextHudFontSupport.SteamId(__1);
            if (id == FontModSpec.RichHud.FrameworkId || id == FontModSpec.RichHud.FontId) Prepare();
            if (id != FontModSpec.RichHud.FontId || prepared == null) return true;
            Report("legacy font mod scripts excluded (Steam: 2940288034).");
            return false;
        }

        private static bool BeforeDefinitions(object __0, ref List<Tuple<MyObjectBuilder_Definitions, string>> __result)
        {
            if (TextHudFontSupport.SteamId(__0) != FontModSpec.RichHud.FontId || prepared == null) return true;
            __result = new List<Tuple<MyObjectBuilder_Definitions, string>>();
            Report("legacy font mod definitions excluded (Steam: 2940288034).");
            return false;
        }

        internal static void Observe(ulong origin, Assembly assembly)
        {
            if (origin != FontModSpec.RichHud.FrameworkId || target == assembly) return;
            var se = assembly.GetType("RichHudFramework.UI.FontData.SeFont");
            if (se == null) return;
            if (target != null) throw new InvalidOperationException("More than one RichHud font implementation was supplied.");
            try
            {
                var data = AccessTools.Method(se, "GetFontData", Type.EmptyTypes);
                var board = assembly.GetType("RichHudFramework.UI.Rendering.Server.TextBoard", true);
                var layout = AccessTools.Method(board, "UpdateLineOffsets");
                if (data == null || data.ReturnType != typeof(Font) || layout == null || layout.GetParameters().Length != 1)
                    throw new MissingMethodException("RichHud default font/layout signature changed.");
                var code = PatchProcessor.GetOriginalInstructions(layout);
                if (!code.Any(i => i.opcode == OpCodes.Ldc_I4 && Equals(i.operand, 0x4E00))
                    || !code.Any(i => i.opcode == OpCodes.Ldc_I4 && Equals(i.operand, 0xE001))
                    || !code.Any(i => i.opcode == OpCodes.Ldc_R4 && Equals(i.operand, -4f)))
                    throw new InvalidOperationException("RichHud CJK layout has changed; recheck Hangul baseline adjustment.");
                Type line = layout.GetParameters()[0].ParameterType;
                var setOffset = AccessTools.Method(line, "SetOffsetAt", new[] { typeof(int), typeof(Vector2) });
                if (setOffset == null) throw new MissingMethodException("RichHud Line.SetOffsetAt");
                readChar = Reader<char>(line, "Chars", false);
                readFormat = Reader<Format>(line, "FormattedGlyphs", true);
                runtime.Patch(setOffset, prefix: new HarmonyMethod(typeof(RichHudDefaultFont), nameof(OffsetPrefix)));
                runtime.Patch(layout, prefix: new HarmonyMethod(typeof(RichHudDefaultFont), nameof(BeginLayout)),
                    transpiler: new HarmonyMethod(typeof(RichHudDefaultFont), nameof(BindCollectionCalls)),
                    finalizer: new HarmonyMethod(typeof(RichHudDefaultFont), nameof(EndLayout)));
                // 위치 보정 연결이 성공한 뒤 자료 병합을 활성화한다.
                // Enable merging only after the matching layout hooks are installed.
                runtime.Patch(data, postfix: new HarmonyMethod(typeof(RichHudDefaultFont), nameof(FontPostfix)));
                target = assembly;
                Report("connected; waiting for SpaceEngineers registration.");
            }
            catch
            {
                runtime.UnpatchAll(RuntimeId);
                readChar = null; readFormat = null; target = null;
                throw;
            }
        }

        private static Func<object, int, T> Reader<T>(Type line, string collection, bool format)
        {
            var instance = Expression.Parameter(typeof(object), "line");
            var index = Expression.Parameter(typeof(int), "index");
            Expression list = Expression.PropertyOrField(Expression.Convert(instance, line), collection);
            Expression value = Expression.MakeIndex(list, list.Type.GetProperty("Item"), new[] { index });
            if (format) value = Expression.PropertyOrField(Expression.PropertyOrField(value, "format"), "Data");
            return Expression.Lambda<Func<object, int, T>>(value, instance, index).Compile();
        }

        // 월드 재입장에서는 동일한 이름의 모드라도 타입 객체가 새로 생성된다. 해당 타입을
        // 포함한 IReadOnlyList 호출을 그대로 복사하면 패치된 함수에서 두 번째 로드부터
        // EntryPointNotFoundException이 재현된다. ILGenerator로 현재 MethodInfo를 직접
        // 연결해 이름 기반 타입 재해석을 피한다. 다른 모드/전역 캐시는 수정하지 않는다.
        // 연결 함수는 패치 준비 때만 만들며 HUD 표시 중 자료 변환·리플렉션은 하지 않는다.
        //
        // Reentry loads fresh types under the same mod assembly identity. Copying calls
        // to IReadOnlyList of those types reproduces EntryPointNotFoundException on reload.
        // Bind the current MethodInfo directly through ILGenerator instead of resolving
        // its type by name. Do not modify global caches or other mods. Bridges are built
        // once at patch installation; rendering does no reflection or data conversion.
        private static IEnumerable<CodeInstruction> BindCollectionCalls(IEnumerable<CodeInstruction> instructions,
            MethodBase __originalMethod)
        {
            var bridges = new Dictionary<MethodInfo, DynamicMethod>();
            foreach (var instruction in instructions)
            {
                var method = instruction.operand as MethodInfo;
                if (instruction.opcode == OpCodes.Callvirt && method != null && method.Name == "get_Item"
                    && method.DeclaringType.IsGenericType
                    && method.DeclaringType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)
                    && method.ReturnType.Assembly == __originalMethod.Module.Assembly)
                {
                    DynamicMethod bridge;
                    if (!bridges.TryGetValue(method, out bridge))
                    {
                        bridge = new DynamicMethod("RichHudCollectionItem", method.ReturnType,
                            new[] { method.DeclaringType, typeof(int) }, typeof(RichHudDefaultFont).Module, true);
                        var il = bridge.GetILGenerator();
                        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1);
                        il.Emit(OpCodes.Callvirt, method); il.Emit(OpCodes.Ret);
                        bridges.Add(method, bridge);
                    }
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = bridge;
                }
                yield return instruction;
            }
        }

        private static void FontPostfix(ref Font __result)
        {
            try
            {
                if (prepared == null) throw new InvalidOperationException("Precomputed Hangul was not prepared before initialization.");
                Dictionary<char, float> corrections;
                Font merged = Merge(__result, prepared.Font, out corrections);
                foreach (var path in prepared.ImagePaths) SharedHudMaterials.Register(path);
                __result = merged;
                offsets = corrections;
                Report("supplemented; font=SpaceEngineers; Hangul glyphs=" + offsets.Count
                    + "; precomputed data/shared DDS; legacy standalone font not registered.");
            }
            catch (Exception error)
            {
                offsets.Clear();
                Report("supplementation skipped; original font retained; error=" + error);
            }
        }

        internal static bool IsHangul(char ch) => (ch >= '\uAC00' && ch <= '\uD7A3')
            || (ch >= '\u3131' && ch <= '\u318E') || (ch >= '\u1100' && ch <= '\u11FF');

        internal static Font Merge(Font original, Font donor, out Dictionary<char, float> added)
        {
            if (original.Item1 != "SpaceEngineers" || donor.Item1 != "KoreanPatch_Hangul"
                || !Positive(original.Item2) || !Positive(donor.Item2))
                throw new InvalidOperationException("Unexpected RichHud font identity or size.");
            Style basis = original.Item3.Single(s => s.Item1 == 0 && s.Item4 != null);
            Style korean = donor.Item3.Single(s => s.Item1 == 0 && s.Item4 != null);
            float ratio = original.Item2 / donor.Item2;
            float correction = (korean.Item3 * ratio - basis.Item3) * (12f / original.Item2);
            if (!Positive(basis.Item2) || !Positive(korean.Item2) || float.IsNaN(correction) || float.IsInfinity(correction))
                throw new InvalidOperationException("Invalid RichHud font metrics.");
            added = new Dictionary<char, float>();
            var existing = new HashSet<char>(basis.Item5.Select(g => g.Key));
            var images = basis.Item4.ToList();
            var glyphs = basis.Item5.ToList();
            var remap = new Dictionary<int, int>();
            foreach (var pair in korean.Item5)
            {
                if (!IsHangul(pair.Key) || !existing.Add(pair.Key)) continue;
                Glyph g = pair.Value;
                if (g.Item1 < 0 || g.Item1 >= korean.Item4.Length || !Positive(g.Item2.X) || !Positive(g.Item2.Y))
                    throw new InvalidOperationException("Invalid Korean glyph image reference.");
                int mapped;
                if (!remap.TryGetValue(g.Item1, out mapped))
                {
                    Atlas atlas = korean.Item4[g.Item1];
                    if (string.IsNullOrEmpty(atlas.Item1) || !Positive(atlas.Item2.X) || !Positive(atlas.Item2.Y))
                        throw new InvalidOperationException("Invalid Korean atlas.");
                    mapped = images.Count; remap.Add(g.Item1, mapped);
                    images.Add(new Atlas(atlas.Item1, atlas.Item2 * ratio));
                }
                // 이미지 좌표·아틀라스 크기를 함께 조절해 UV는 유지하고 실제 표시 크기만 맞춘다.
                //
                // Scale the atlas and sprite coordinates together: preserve UVs while
                // normalizing drawn dimensions, advance and bearing to the default point size.
                glyphs.Add(new KeyValuePair<char, Glyph>(pair.Key,
                    new Glyph(mapped, g.Item2 * ratio, g.Item3 * ratio, g.Item4 * ratio, g.Item5 * ratio)));
                added.Add(pair.Key, correction);
            }
            var styles = (Style[])original.Item3.Clone();
            int slot = Array.FindIndex(styles, s => s.Item1 == 0 && s.Item4 != null);
            styles[slot] = new Style(basis.Item1, basis.Item2, basis.Item3, images.ToArray(), glyphs.ToArray(), basis.Item6);
            return new Font(original.Item1, original.Item2, styles);
        }

        private static bool Positive(float value) => value > 0 && !float.IsInfinity(value);
        private static void BeginLayout(out int __state) { __state = layoutDepth; layoutDepth++; }
        private static void EndLayout(int __state) { layoutDepth = __state; }

        private static void OffsetPrefix(object __instance, int __0, ref Vector2 __1)
        {
            if (layoutDepth == 0 || offsets.Count == 0) return;
            char ch = readChar(__instance, __0);
            float correction;
            if (!offsets.TryGetValue(ch, out correction)) return;
            Format format = readFormat(__instance, __0);
            if (format.Item3.X != 0 || (format.Item3.Y & 1) != 0) return;
            __1 = CorrectOffset(__1, ch, format.Item2, correction);
        }

        internal static Vector2 CorrectOffset(Vector2 original, char ch, float textSize, float correction)
        {
            // 원본 CJK 보정은 한글 완성형에만 걸린다. 추가 글자에 한해 이를 상쇄한 뒤
            // 동일한 기준선 보정을 적용하여 자모와 완성형이 다른 높이가 되지 않게 한다.
            //
            // Cancel the original CJK shift only for supplied Hangul, then apply the
            // common baseline correction to both syllables and compatibility jamo.
            float undoCjk = ch >= 0x4E00 && ch < 0xE001 ? 4f : 0f;
            original.Y += (correction + undoCjk) * textSize;
            return original;
        }

        internal static void Reset()
        {
            runtime.UnpatchAll(RuntimeId);
            prepared = null; attempted = false; target = null; readChar = null; readFormat = null;
            offsets = new Dictionary<char, float>(); layoutDepth = 0;
            Status = DiagnosticText.ModFont("RichHudMaster", "waiting for mod assemblies.");
        }

        private static void Report(string text) { Status = DiagnosticText.ModFont("RichHudMaster", text); MyLog.Default?.WriteLine("[Arstraea.KoreanPatch] " + Status); }
    }
}
