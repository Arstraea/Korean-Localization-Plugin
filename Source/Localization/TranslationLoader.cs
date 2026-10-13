using System;
using System.IO;
using HarmonyLib;
using VRage;

namespace Arstraea.KoreanPatch.Localization
{
    // 영어 원본을 읽은 뒤 한국어 캐시만 기본 로더로 보충한다. 이전 게임 폴더의
    // ko-KR은 실행 중 읽기만 생략하며 삭제하거나 복구하지 않는다.
    //
    // Load stock English first, then cached Korean with the native loader. Ignore old
    // installed ko-KR at read time only; never remove or restore installation files.
    internal static class TranslationLoader
    {
        private static string gameRoot, resourceRoot;
        [ThreadStatic] private static bool supplementing;

        internal static void Install(Harmony harmony, TranslationInstaller.Result prepared)
        {
            gameRoot = prepared.GameRoot;
            resourceRoot = prepared.ResourceRoot;
            harmony.Patch(AccessTools.Method(typeof(MyTexts), "LoadTexts", new[] { typeof(string), typeof(string), typeof(string) }),
                postfix: new HarmonyMethod(typeof(TranslationLoader), nameof(AfterLoad)));
            harmony.Patch(AccessTools.Method(typeof(MyTexts), "IterateResFileItems"),
                prefix: new HarmonyMethod(typeof(TranslationLoader), nameof(BeforeFile)));
        }

        private static bool BeforeFile(string __0)
        {
            if (string.IsNullOrEmpty(__0) || string.IsNullOrEmpty(gameRoot)) return true;
            string full = Path.GetFullPath(__0);
            foreach (string relative in TranslationInstaller.RelativeFiles)
                if (string.Equals(full, Path.Combine(gameRoot, relative), StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        private static void AfterLoad(string __0, string __1, string __2)
        {
            if (supplementing || resourceRoot == null || __1 != "ko" || __2 != "KR" || string.IsNullOrEmpty(__0)) return;
            string path = Path.GetFullPath(__0).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string suffix;
            if (string.Equals(path, gameRoot, StringComparison.OrdinalIgnoreCase)) suffix = "";
            else if (string.Equals(path, Path.Combine(gameRoot, "CoreTexts"), StringComparison.OrdinalIgnoreCase)) suffix = "CoreTexts";
            else if (string.Equals(path, Path.Combine(gameRoot, "Common"), StringComparison.OrdinalIgnoreCase)) suffix = "Common";
            else return;
            try
            {
                supplementing = true;
                MyTexts.LoadTexts(Path.Combine(resourceRoot, suffix), "ko", "KR");
            }
            catch (Exception error) when (ReleaseMetadata.IsFileError(error))
            { Fonts.PatchStartupGate.WriteLog("Cached Korean texts could not be loaded: " + error.Message); }
            finally { supplementing = false; }
        }
    }
}
