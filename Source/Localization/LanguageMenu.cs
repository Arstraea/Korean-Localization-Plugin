using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Sandbox.Graphics.GUI;
using VRage;

namespace Arstraea.KoreanPatch.Localization
{
    internal static class LanguageMenu
    {
        private static bool installed;

        // GUI 함수의 패치 설치 자체가 렌더 정적 초기화를 유발한다. Preloader에서 호출하지 않는다.
        // Patching GUI methods can itself initialize rendering statics. Install after game startup.
        internal static void Install()
        {
            if (installed || !MyTexts.Languages.ContainsKey((MyLanguagesEnum)LanguageSupport.KoreanId)) return;
            try
            {
                Type options = Assembly.Load("Sandbox.Game").GetType("Sandbox.Game.Gui.MyGuiScreenOptionsGame", true);
                var target = AccessTools.Method(options, "CreateTab1Menu", new[] { typeof(bool) })
                    ?? throw new MissingMethodException(options.FullName, "CreateTab1Menu");
                new Harmony(LanguageSupport.PatchId).Patch(target,
                    postfix: new HarmonyMethod(typeof(LanguageMenu), nameof(OrderPostfix)));
                installed = true;
            }
            catch (Exception error)
            {
                Fonts.PatchStartupGate.WriteLog("Language menu order unchanged; translations remain available. " + error);
            }
        }

        // 플러그인을 사용하는 한국인이 한국어를 빠르게 찾을 수 있도록, 사용자 요청에 따라
        // 한국어를 English 바로 다음에 진열한다. ID/지원 언어/다른 언어끼리의 순서는 보존한다.
        // 이 메서드는 게임 GUI 생성 후 호출되며 Preloader에서 컨트롤을 만들지 않는다.
        //
        // At the user's request, place Korean immediately after English so Korean players using
        // the plugin can find it quickly. Preserve IDs, supported languages and all other relative order.
        // Called after game GUI creation; never construct controls in the Preloader.
        internal static void OrderPostfix(MyGuiControlCombobox ___m_languageCombobox)
        {
            var combo = ___m_languageCombobox;
            if (combo == null) return;
            var english = combo.TryGetItemByKey((long)MyLanguagesEnum.English);
            var korean = combo.TryGetItemByKey(LanguageSupport.KoreanId);
            if (english == null || korean == null) return;

            var items = new List<MyGuiControlCombobox.Item>();
            for (int i = 0; i < combo.GetItemsCount(); i++) items.Add(combo.GetItemByIndex(i));
            items.Remove(korean);
            items.Insert(items.IndexOf(english) + 1, korean);
            var order = new Dictionary<MyGuiControlCombobox.Item, int>();
            for (int i = 0; i < items.Count; i++) order.Add(items[i], i);
            long selected = combo.GetSelectedKey();
            bool hasSelection = combo.GetSelectedIndex() >= 0;
            combo.CustomSortItems((a, b) => order[a].CompareTo(order[b]));
            if (hasSelection)
            {
                // 동일 항목을 그대로 선택하면 엔진이 키보드 위치 갱신을 생략한다.
                // Clear first so the engine refreshes keyboard/scroll indices without firing events.
                combo.SelectItemByIndex(-1);
                combo.SelectItemByKey(selected, false);
            }
        }
    }
}
