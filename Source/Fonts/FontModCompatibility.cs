using System;
using System.IO;
using System.Reflection;
using System.Security;
using System.Threading;
using HarmonyLib;

namespace Arstraea.KoreanPatch.Fonts
{
    // 확인된 모드의 폰트 Path만 연결한다. 정의나 원본 파일을 수정하지 않아
    // 다른 월드/모드의 정의와 섞이지 않고, 측정·렌더가 동일한 경로를 받는다.
    //
    // Redirect only recognized mods' font Path getters. Leave definitions/files intact
    // so measurement and rendering agree without leaking changes across worlds.
    internal static class FontModCompatibility
    {
        internal const ulong WorkshopId = 3413495763;
        internal const string PatchId = "Arstraea.KoreanPatch.FontMod";
        internal static string Status { get; private set; } = DiagnosticText.ModFont("Korean Font", "inactive; fonts not prepared.");
        private static Harmony harmony;
        private static string[] targets;
        private static FieldInfo contextField, itemId, itemService;
        private static PropertyInfo contextItem, contextPath;
        private static readonly int[] reported = new int[3];

        // 폰트 적용 성공 후, 게임의 폰트 초기화가 JIT되기 전에 설치한다.
        // Install after successful preparation, before game font callers can be JIT-inlined.
        internal static void Install(string content)
        {
            if (harmony != null) return;
            var prepared = new string[FontInstaller.Families.Length];
            for (int i = 0; i < prepared.Length; i++)
            {
                prepared[i] = Path.GetFullPath(Path.Combine(content, "Fonts", FontInstaller.Families[i], "FontDataPA.xml"));
                if (!File.Exists(prepared[i])) throw new FileNotFoundException("Prepared font XML is unavailable.", prepared[i]);
            }
            var pending = new Harmony(PatchId);
            try
            {
                var assembly = Assembly.Load("VRage.Game");
                var font = assembly.GetType("VRage.Game.Definitions.MyFontDefinition", true);
                contextField = AccessTools.Field(font, "Context") ?? throw new MissingFieldException(font.FullName, "Context");
                contextItem = RequireProperty(contextField.FieldType, "ModItem");
                contextPath = RequireProperty(contextField.FieldType, "ModPath");
                itemId = contextItem.PropertyType.GetField("PublishedFileId");
                itemService = contextItem.PropertyType.GetField("PublishedServiceName");
                if (itemId?.FieldType != typeof(ulong) || itemService?.FieldType != typeof(string))
                    throw new MissingFieldException("Workshop font origin fields have changed.");
                var getter = RequireProperty(font, "CompatibilityPath").GetGetMethod();
                if (getter == null || getter.IsStatic || getter.ReturnType != typeof(string))
                    throw new MissingMethodException(font.FullName, "get_CompatibilityPath");
                targets = prepared;
                BuildInfoFontSupport.Configure(content, patchFolder: FontStartup.PackageFolder);
                pending.Patch(getter, postfix: new HarmonyMethod(typeof(FontModCompatibility), nameof(PathPostfix)) { priority = Priority.Last });
                harmony = pending;
                Status = DiagnosticText.ModHookReady("Korean Font", WorkshopId.ToString(), "white/white_shadow/monospace");
                PatchStartupGate.WriteLog(Status);
            }
            catch
            {
                targets = null;
                pending.UnpatchAll(PatchId);
                Status = DiagnosticText.ModFont("Korean Font", "hook installation failed.");
                throw;
            }
        }

        private static PropertyInfo RequireProperty(Type type, string name)
        { return type.GetProperty(name) ?? throw new MissingMemberException(type.FullName, name); }

        private static void PathPostfix(object __instance, ref string __result)
        {
            if (targets == null || string.IsNullOrEmpty(__result)) return;
            object context = contextField.GetValue(__instance);
            if (context == null) return;
            object item = contextItem.GetValue(context);
            if (!string.Equals(itemService.GetValue(item) as string, "Steam", StringComparison.OrdinalIgnoreCase)) return;
            ulong origin = (ulong)itemId.GetValue(item);
            string root = contextPath.GetValue(context) as string;
            if (origin == BuildInfoFontSupport.ReleaseId || origin == BuildInfoFontSupport.TestId)
            {
                __result = BuildInfoFontSupport.Redirect(__instance, root, __result);
                return;
            }
            int family = origin == WorkshopId ? MatchFamily(root, __result)
                : origin == Mods.FontModSpec.TextHud.FontId && IsTextHudDebugPath(root, __result) ? 1 : -1;
            if (family < 0) return;
            __result = targets[family];
            // 폰트 경로를 얻을 때마다 로그를 반복하지 않는다. 게임 실행당 계열별 1회만 기록한다.
            // Log each family once per process, rather than on every path lookup.
            if (Interlocked.Exchange(ref reported[family], 1) == 0)
            {
                Status = DiagnosticText.ModFont("Korean Font", "redirected; family=" + FontInstaller.Families[family] + "; XML=" + __result);
                PatchStartupGate.WriteLog(Status);
            }
        }

        // API용 모드에 함께 포함된 구형 Debug 폰트만 현재 준비한 white_shadow로 연결한다.
        // API 재질/글자 등록은 유지하고 최신 게임 아이콘과 플랫폼 아이콘 설정을 보존한다.
        //
        // Redirect only the API mod's bundled legacy Debug font to prepared white_shadow.
        // Keep API materials/registration while preserving current game icons and preferences.
        internal static bool IsTextHudDebugPath(string root, string requested)
        {
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(requested) || !Path.IsPathRooted(root)) return false;
            try
            {
                return string.Equals(Path.GetFullPath(Path.IsPathRooted(requested) ? requested : Path.Combine(root, requested)),
                    Path.Combine(Path.GetFullPath(root), "Fonts", "koKR", "FontDataKR.xml"), StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException) { return false; }
            catch (NotSupportedException) { return false; }
            catch (PathTooLongException) { return false; }
            catch (SecurityException) { return false; }
        }

        internal static int MatchFamily(string modRoot, string requested)
        {
            if (string.IsNullOrWhiteSpace(modRoot) || string.IsNullOrWhiteSpace(requested)) return -1;
            try
            {
                if (!Path.IsPathRooted(modRoot)) return -1;
                string root = Path.GetFullPath(modRoot);
                string full = Path.GetFullPath(Path.IsPathRooted(requested) ? requested : Path.Combine(root, requested));
                for (int i = 0; i < FontInstaller.Families.Length; i++)
                    if (string.Equals(full, Path.Combine(root, "Fonts", FontInstaller.Families[i], "FontDataPA.xml"), StringComparison.OrdinalIgnoreCase)) return i;
            }
            catch (ArgumentException) { }
            catch (NotSupportedException) { }
            catch (PathTooLongException) { }
            catch (SecurityException) { }
            // 인식하지 못한 모드 경로는 기존 게임의 처리를 유지한다.
            // Leave unrecognized mod paths to the game's original handling.
            return -1;
        }
    }
}
