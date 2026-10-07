using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Arstraea.KoreanPatch.Localization;
using HarmonyLib;
using Sandbox.Game.Gui;
using Sandbox.Graphics;
using Sandbox.Graphics.GUI;
using VRage.Game;
using VRage.Utils;
using VRageMath;

namespace Arstraea.KoreanPatch.Gui
{
    // 게임 뉴스와 독립된 컨트롤로 구성해 뉴스 생성 자체를 생략하는 플러그인과도 동작한다.
    // 메뉴별 약한 참조를 사용하고, 원래 보이던 게임 뉴스만 잠시 숨겼다가 복원한다.
    //
    // Independent controls also work when another plugin skips stock news creation.
    // Weak menu ownership avoids retaining screens; restore only stock news we hid ourselves.
    internal static class LocalizationNews
    {
        private static readonly ConditionalWeakTable<MyGuiScreenBase, View> views = new ConditionalWeakTable<MyGuiScreenBase, View>();
        private static NewsCatalog catalog;
        private static bool installed;

        internal static void Install()
        {
            if (installed) return;
            try
            {
                var notes = new List<string>();
                string content = Fonts.FontStartup.ContentFolder;
                if (string.IsNullOrEmpty(content)) return;
                catalog = NewsCatalog.Load(Fonts.FontStartup.TranslationResult?.Release, content, notes);
                foreach (string note in notes) Fonts.PatchStartupGate.WriteLog(note);
                if (!catalog.Available) return;
                Type menu = Assembly.Load("SpaceEngineers.Game").GetType("SpaceEngineers.Game.GUI.MyGuiScreenMainMenu", true);
                var method = AccessTools.Method(menu, "RecreateControls", new[] { typeof(bool) })
                    ?? throw new MissingMethodException(menu.FullName, "RecreateControls");
                var pending = new PatchTransaction("Arstraea.KoreanPatch.LocalizationNews");
                pending.Add(method, postfix: new HarmonyMethod(typeof(LocalizationNews), nameof(AfterRecreate)) { priority = Priority.Last });
                var onShow = AccessTools.DeclaredMethod(menu, "OnShow")
                    ?? throw new MissingMethodException(menu.FullName, "OnShow");
                pending.Add(onShow, postfix: new HarmonyMethod(typeof(LocalizationNews), nameof(AfterShow)) { priority = Priority.Last });
                pending.Apply();
                installed = true;
            }
            catch (Exception error)
            {
                Fonts.PatchStartupGate.WriteLog("Localization news unavailable; core features remain active. " + error);
            }
        }

        private static void AfterShow(MyGuiScreenBase __instance)
        {
            View view;
            // 화면 재생성은 선택을 유지하지만 실제 메뉴 재진입은 미확인 뉴스부터 표시한다.
            // Preserve a choice on layout rebuild; prioritize unread news on an actual menu return.
            if (MyGuiScreenGamePlay.Static == null && catalog != null && views.TryGetValue(__instance, out view))
                view.Show(catalog.Pending);
        }

        private static void AfterRecreate(MyGuiScreenBase __instance, MyGuiControlBase ___m_newsControl)
        {
            if (MyGuiScreenGamePlay.Static != null || catalog == null) return;
            View old;
            bool? previous = views.TryGetValue(__instance, out old) ? old.ShowingKorean : (bool?)null;
            views.Remove(__instance);
            // 다른 플러그인이 건드릴 수 있는 GUI 경계는 실패해도 원래 메뉴를 유지한다.
            // Keep the stock menu functional if the external GUI contract has changed.
            View view = null;
            try
            {
                view = new View(__instance, ___m_newsControl, catalog);
                view.Show(previous ?? catalog.Pending);
                views.Add(__instance, view);
            }
            catch (Exception error)
            {
                view?.Remove();
                Fonts.PatchStartupGate.WriteLog("Localization news controls unavailable; stock menu retained. " + error);
            }
        }

        private sealed class View
        {
            internal bool ShowingKorean { get; private set; }
            private readonly MyGuiScreenBase screen;
            private readonly MyGuiControlBase gameNews;
            private readonly bool restoreGameNews;
            private readonly NewsCatalog releases;
            private readonly MyGuiControlParent panel;
            private readonly MyGuiControlButton confirm, toggle;
            private readonly List<MyGuiControlBase> added = new List<MyGuiControlBase>();

            internal View(MyGuiScreenBase screen, MyGuiControlBase gameNews, NewsCatalog releases)
            {
                this.screen = screen;
                this.releases = releases;
                this.gameNews = gameNews;
                restoreGameNews = gameNews != null && gameNews.Visible && screen.Controls.Contains(gameNews);
                Vector2 size = gameNews?.Size ?? new Vector2(0.4f, 0.28f);
                Vector2 anchor = gameNews?.Position ?? MyGuiManager.ComputeFullscreenGuiCoordinate(
                    MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_BOTTOM, 54, 54) - 5f * MyGuiConstants.MENU_BUTTONS_POSITION_DELTA;
                var align = gameNews?.OriginAlign ?? MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_TOP;
                Vector2 topLeft = MyUtils.GetCoordTopLeftFromAligned(anchor, size, align);
                panel = new MyGuiControlParent(position: topLeft, size: size);
                panel.OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP;
                panel.Name = "KoreanPatchNews";
                // 기본 뉴스는 복합 텍스처 두 레이어에 흰색 마스크를 적용한다.
                // 부모에 어두운 색을 곱하면 원래 회청색 배경과 테두리가 검게 변한다.
                //
                // Stock news draws two composite-texture layers with white masks.
                // A dark parent tint turns its blue-grey background and border black.
                panel.BackgroundTexture = null;
                panel.Elements.Add(new MyGuiControlCompositePanel { Size = size,
                    ColorMask = new Vector4(1f, 1f, 1f, 0.8f), BackgroundTexture = MyGuiConstants.TEXTURE_NEWS_BACKGROUND });
                panel.Elements.Add(new MyGuiControlCompositePanel { Size = size, Position = new Vector2(size.X - 0.004f, 0f),
                    ColorMask = Vector4.One, BackgroundTexture = MyGuiConstants.TEXTURE_NEWS_BACKGROUND_BlueLine });
                float half = (size.Y - 0.024f - 0.008f) * 0.5f;
                float contentTop = -size.Y * 0.5f + 0.012f
                    + MyGuiManager.GetNormalizedSizeFromScreenSize(new Vector2(0f, -8f)).Y;
                Section("한글화 팩 업데이트 정보", releases.Pack, contentTop, half, size.X, flushTop: true);
                Section("플러그인 업데이트 정보", releases.Plugin, contentTop + half + 0.008f, half, size.X);
                // 실제 화면의 픽셀 보정값을 GUI 좌표로 변환해 버튼 행만 이동한다.
                //
                // Convert the requested screen-pixel offset for the action row only.
                Vector2 right = topLeft + new Vector2(size.X, size.Y + 0.006f)
                    + MyGuiManager.GetNormalizedSizeFromScreenSize(new Vector2(4f, 6f));
                toggle = new MyGuiControlButton(position: right, visualStyle: MyGuiControlButtonStyleEnum.Small,
                    buttonScale: 0.65f, text: new StringBuilder("한글화 소식 보기"), textScale: 0.45f,
                    originAlign: MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_TOP,
                    onButtonClick: ignored => Show(!ShowingKorean));
                float extraHeight = MyGuiManager.GetNormalizedSizeFromScreenSize(new Vector2(0f, 6f)).Y;
                toggle.Size = new Vector2(toggle.Size.X * 1.5f, toggle.Size.Y + extraHeight);
                confirm = new MyGuiControlButton(position: right - new Vector2(toggle.Size.X + 0.008f, 0f),
                    visualStyle: MyGuiControlButtonStyleEnum.Small, buttonScale: 0.65f,
                    text: new StringBuilder("확인"), textScale: 0.45f,
                    originAlign: MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_TOP, onButtonClick: Confirm);
                confirm.Size = new Vector2(confirm.Size.X * 1.5f, confirm.Size.Y + extraHeight);
                // 구성 완료 후에만 화면에 추가해 생성 실패가 메뉴에 잔여 컨트롤을 남기지 않게 한다.
                // Publish controls only after construction succeeds, keeping failures out of the menu.
                try { Add(panel); Add(toggle); Add(confirm); }
                catch { Remove(); throw; }
            }

            private void Add(MyGuiControlBase control) { added.Add(control); screen.Controls.Add(control); }

            private void Section(string title, ReleaseMetadata release, float y, float height, float width, bool flushTop = false)
            {
                // 제목과 버전/날짜 뒤에만 옅은 검정 띠를 그려 본문과 구분한다.
                //
                // Shade only the title/version/date band, leaving the stock body background intact.
                float bandTop = flushTop ? -panel.Size.Y * 0.5f : y - 0.003f;
                panel.Elements.Add(new MyGuiControlPanel(position: new Vector2(-width * 0.5f, bandTop),
                    size: new Vector2(width, y + 0.028f - bandTop), backgroundColor: new Vector4(0f, 0f, 0f, 0.5f),
                    texture: MyGuiConstants.BLANK_TEXTURE, originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP));
                float left = -width * 0.5f + 0.018f, right = width * 0.5f - 0.018f;
                panel.Controls.Add(new MyGuiControlLabel(position: new Vector2(left, y + 0.012f), text: title,
                    textScale: 0.70f, font: "White", colorMask: new Color(92, 209, 229).ToVector4(),
                    originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER,
                    maxWidth: 0.16f, isAutoScaleEnabled: true, minimumTextScale: 0.6f));
                Metadata(release == null ? "" : "버전: " + release.Version, right, y, width);
                float twoPixels = MyGuiManager.GetNormalizedSizeFromScreenSize(new Vector2(0f, 2f)).Y;
                Metadata(release?.PublishedDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "", right, y + 0.014f - twoPixels, width);
                string text = FormatBody(release);
                float bodyTop = 0.031f + MyGuiManager.GetNormalizedSizeFromScreenSize(new Vector2(0f, 5f)).Y;
                panel.Controls.Add(new MyGuiControlMultilineText(position: new Vector2(left + 0.011f, y + bodyTop),
                    size: new Vector2(width - 0.047f, height - bodyTop), font: "White", textScale: 0.58f,
                    textAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP,
                    textBoxAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP,
                    contents: new StringBuilder(text), drawScrollbarV: true, drawScrollbarH: false, selectable: true)
                    { OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP });
            }

            private void Metadata(string text, float right, float y, float width)
            {
                panel.Controls.Add(new MyGuiControlLabel(position: new Vector2(right, y), text: text,
                    textScale: 0.42f, font: "White", maxWidth: width - 0.20f, isAutoScaleEnabled: true,
                    minimumTextScale: 0.28f, originAlign: MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_TOP));
            }

            internal void Show(bool korean)
            {
                ShowingKorean = korean;
                panel.Visible = korean;
                confirm.Visible = korean;
                toggle.Visible = !korean || restoreGameNews;
                toggle.Text = korean ? "게임 소식 보기" : "한글화 소식 보기";
                if (restoreGameNews) gameNews.Visible = !korean;
                // 감춰진 버튼이 키보드 포커스를 붙잡지 않게 한다.
                // Do not let a hidden action retain keyboard focus.
                if (ReferenceEquals(screen.FocusedControl, confirm) || ReferenceEquals(screen.FocusedControl, toggle))
                    screen.FocusedControl = toggle.Visible ? toggle : null;
            }

            private void Confirm(MyGuiControlButton ignored)
            {
                var notes = new List<string>();
                releases.Confirm(notes);
                foreach (string note in notes) Fonts.PatchStartupGate.WriteLog(note);
                Show(false);
            }

            internal void Remove()
            {
                foreach (var control in added) screen.Controls.Remove(control);
                if (restoreGameNews) gameNews.Visible = true;
            }
        }

        internal static string FormatBody(ReleaseMetadata release)
        {
            if (release == null) return "업데이트 내역을 불러올 수 없습니다.";
            string items = release.Changes == null || release.Changes.Length == 0 ? ""
                : "* " + string.Join("\n* ", release.Changes);
            if (!string.IsNullOrEmpty(release.Introduction))
                return release.Introduction + (items.Length == 0 ? "" : "\n\n" + items);
            return items.Length == 0 ? "등록된 업데이트 내역이 없습니다." : items;
        }
    }
}
