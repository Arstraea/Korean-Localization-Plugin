using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using VRage;
using Arstraea.KoreanPatch.Fonts;

namespace Arstraea.KoreanPatch.Localization
{
    // Pulsar 사본에 추가된 enum을 문화권/번역과 연결한다. 정상 Config 조회는
    // 게임에 맡기고, 자료 준비 실패 때만 한국어 선택을 기본 언어로 대체한다.
    //
    // Connect the enum added to Pulsar's copy to culture/text resources. Leave normal
    // Config reads to the game; fall back only when Korean resources cannot be prepared.
    internal static class LanguageSupport
    {
        internal const byte KoreanId = LanguageEnumPatch.KoreanId;
        internal const string PatchId = "Arstraea.KoreanPatch.Language";
        internal static string Status { get; private set; } = "Game language: Korean preparation pending.";
        private static readonly MyLanguagesEnum Korean = (MyLanguagesEnum)KoreanId;
        private static Harmony harmony;
        private static FieldInfo supportedLanguages;

        internal static void Install(TranslationInstaller.Result prepared)
        {
            if (harmony != null) return;
            var pending = new Harmony(PatchId);
            bool registered = false;
            try
            {
                Status = prepared?.Summary ?? "Korean translations: preparation incomplete.";
                if (prepared?.Available != true)
                {
                    InstallUnavailableFallback();
                    Status += "\nGame language: Korean resources unavailable; stock language handling retained.";
                    return;
                }
                if (!Enum.IsDefined(typeof(MyLanguagesEnum), LanguageEnumPatch.KoreanName)
                    || Enum.GetName(typeof(MyLanguagesEnum), Korean) != LanguageEnumPatch.KoreanName)
                    throw new InvalidOperationException("Preloader did not supply MyLanguagesEnum.Korean = 255; restart with the current plugin.");
                if (MyTexts.Languages.ContainsKey(Korean)
                    || Cultures.ContainsKey("ko-KR"))
                    throw new InvalidOperationException("Korean language ID/culture is already occupied; leaving existing registration intact.");

                Type language = Assembly.Load("VRage.Game").GetType("VRage.Game.Localization.MyLanguage", true);
                supportedLanguages = AccessTools.Field(language, "m_supportedLanguages") ?? throw new MissingFieldException("m_supportedLanguages");
                pending.Patch(Require(language, "Init"), postfix: Hook(nameof(InitPostfix)));
                pending.Patch(Require(language, "ConvertLangEnum", typeof(MyLanguagesEnum)), prefix: Hook(nameof(ToCulturePrefix)));
                pending.Patch(Require(language, "ConvertLangEnum", typeof(string)), prefix: Hook(nameof(FromCulturePrefix)));
                // 커뮤니티 번역으로 등록한다. OS가 한국어라는 이유만으로 최초 언어를 바꾸지 않는다.
                // Mark as community localization so OS culture alone does not select it as an official default.
                Require(typeof(MyTexts), "AddLanguage", typeof(MyLanguagesEnum), typeof(string), typeof(string),
                    typeof(string), typeof(float), typeof(bool)).Invoke(null, new object[] { Korean, "ko", "KR", "한국어 by Arstraea", 1f, true });
                registered = true;
                pending.Unpatch(ConfigGetter(), AccessTools.Method(typeof(LanguageSupport), nameof(UnavailableConfigPostfix)));
                harmony = pending;
                Status += "\nGame language: Korean registration ready (Korean = 255, ko-KR).";
            }
            catch (Exception error)
            {
                pending.UnpatchAll(PatchId);
                if (registered)
                {
                    ((IDictionary<MyLanguagesEnum, MyTexts.MyLanguageDescription>)AccessTools.Field(typeof(MyTexts), "m_languageIdToLanguage").GetValue(null)).Remove(Korean);
                    Cultures.Remove("ko-KR");
                }
                Status = "Game language: Korean support unavailable; stock language handling retained; error=" + error;
                InstallUnavailableFallback();
            }
            finally { PatchStartupGate.WriteLog(Status); }
        }

        private static IDictionary<string, MyLanguagesEnum> Cultures =>
            (IDictionary<string, MyLanguagesEnum>)AccessTools.Field(typeof(MyTexts), "m_cultureToLanguageId").GetValue(null);
        private static MethodInfo Require(Type type, string name, params Type[] parameters) =>
            AccessTools.Method(type, name, parameters) ?? throw new MissingMethodException(type.FullName, name);
        private static HarmonyMethod Hook(string name) => new HarmonyMethod(typeof(LanguageSupport), name);
        private static void InitPostfix(object __instance)
        { if (harmony != null) ((HashSet<MyLanguagesEnum>)supportedLanguages.GetValue(__instance)).Add(Korean); }
        private static bool ToCulturePrefix(MyLanguagesEnum __0, ref string __result)
        { if (__0 != Korean) return true; __result = "ko-KR"; return false; }
        private static bool FromCulturePrefix(string __0, ref MyLanguagesEnum __result)
        {
            if (!string.Equals(__0, "ko-KR", StringComparison.OrdinalIgnoreCase)) return true;
            __result = Korean; return false;
        }
        private static MethodInfo ConfigGetter() => Require(Assembly.Load("Sandbox.Game").GetType("Sandbox.Engine.Utils.MyConfig", true), "get_Language");
        private static void InstallUnavailableFallback()
        {
            // enum은 사전 로드에서 이미 존재한다. 자료 준비에 실패하면 원래 Config의
            // IsDefined 검사만으로는 한국어를 거를 수 없으므로 이 경우에만 대체한다.
            //
            // Preloading already defined the enum. Only failed resource preparation needs
            // a guard, since the stock IsDefined check now accepts the saved Korean ID.
            if (Enum.GetName(typeof(MyLanguagesEnum), Korean) != LanguageEnumPatch.KoreanName) return;
            var getter = ConfigGetter();
            var patch = AccessTools.Method(typeof(LanguageSupport), nameof(UnavailableConfigPostfix));
            var guard = new Harmony(PatchId);
            guard.Unpatch(getter, patch);
            guard.Patch(getter, postfix: new HarmonyMethod(patch));
        }
        private static void UnavailableConfigPostfix(ref MyLanguagesEnum __result)
        {
            if (__result != Korean) return;
            Type language = Assembly.Load("VRage.Game").GetType("VRage.Game.Localization.MyLanguage", true);
            __result = (MyLanguagesEnum)Require(language, "GetOsLanguageCurrentOfficial")
                .Invoke(language.GetProperty("Instance").GetValue(null), null);
        }
    }
}
