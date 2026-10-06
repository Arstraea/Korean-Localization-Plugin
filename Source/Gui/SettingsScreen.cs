using System;
using System.Runtime.CompilerServices;
using System.Text;
using Arstraea.KoreanPatch.Fonts;
using Sandbox;
using Sandbox.Graphics;
using Sandbox.Graphics.GUI;
using VRage.Game;
using VRage.Utils;
using VRageMath;

namespace Arstraea.KoreanPatch.Gui
{
    // 게임 GUI는 Pulsar의 설정 콜백에서만 생성한다. 시작 전 파일 준비 UI와 분리하여
    // Preloader에서 GUI/렌더러의 정적 초기화를 유발하지 않는다.
    //
    // Construct game GUI only from Pulsar's settings callback. Keep it separate from
    // the pre-game preparation UI to avoid initializing GUI/render state in Preloader.
    internal sealed class SettingsScreen : MyGuiScreenBase
    {
        private const float Left = -0.32f;
        private const float Width = 0.64f;
        private const float Indent = 0.015f;
        private const float SectionRowOffset = 0.05f;
        private const float InputHeading = -0.29f;
        private const float LanguageRow = InputHeading + SectionRowOffset;
        private const float EditorRow = LanguageRow + 0.065f;
        private const float FontHeading = -0.035f;
        private const float FontRow = FontHeading + SectionRowOffset;
        private const float LogHeading = 0.12f;
        private const float LogTop = LogHeading + SectionRowOffset;
        private const float FooterSeparator = 0.30f;
        private const float LogBottomGap = 0.03f;
        private const float ValueLeft = -0.075f;
        private static readonly Vector4 HeadingColor = new Color(92, 209, 229).ToVector4();
        private static readonly Vector4 CopyErrorColor = new Color(UiTextColors.SoftRedR, UiTextColors.SoftRedG, UiTextColors.SoftRedB).ToVector4();
        private static SettingsScreen active;
        private MyGuiControlCheckbox hideIcons;
        private MyGuiControlCombobox inputLanguage;
        private MyGuiControlCheckbox vanillaEditor;
        private string preparationReport;
        private MyGuiControlLabel copyFeedback;

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Open()
        {
            if (active != null) return;
            var screen = new SettingsScreen();
            screen.Closed += (closed, isUnloading) => { if (ReferenceEquals(active, closed)) active = null; };
            active = screen;
            try { MyGuiSandbox.AddScreen(screen); }
            catch { active = null; throw; }
        }

        private SettingsScreen()
            : base(new Vector2(0.5f, 0.5f), MyGuiConstants.SCREEN_BACKGROUND_COLOR, new Vector2(0.72f, 0.80f),
                backgroundTransition: MySandboxGame.Config.UIBkOpacity, guiTransition: MySandboxGame.Config.UIOpacity)
        {
            EnabledBackgroundFade = true;
            CloseButtonEnabled = true;
            RecreateControls(true);
        }

        public override string GetFriendlyName() => "KoreanPatchSettings";

        public override void RecreateControls(bool constructor)
        {
            // 해상도/GUI 재구성 시에도 저장하지 않은 체크 상태를 유지한다.
            // Preserve the unsaved selection across GUI recreation (e.g. resolution changes).
            bool selected = hideIcons == null ? FontStartup.HidePlatformIcons : hideIcons.IsChecked;
            long language = inputLanguage == null ? (long)PluginSettings.Current.InputLanguage : inputLanguage.GetSelectedKey();
            bool vanilla = vanillaEditor == null ? PluginSettings.Current.VanillaScriptEditor : vanillaEditor.IsChecked;
            base.RecreateControls(constructor);
            AddCaption(Plugin.DisplayTitle, captionScale: 0.75f);
            Heading("한글 입력 지원 관련 설정", InputHeading);
            const string languageHelp = "입력창을 열 때 사용할 한/영 상태를 기억할 방식을 선택하세요.\n"
                + "게임에서 사용하는 UI 표시 언어와 별개로 작동합니다.\n"
                + "* 기억한 상태가 없는 최초 입력은 영어 상태로 간주합니다.\n"
                + "* 상태 기억은 게임 단위로 임시 저장되어 월드 입장과 퇴장 시 초기화됩니다.\n"
                + "* 옵션 변경 시 바로 다음 입력창 진입부터 적용됩니다.";
            var languageLabel = Label("한/영 입력 상태 기억 방식", LanguageRow, 0.78f);
            languageLabel.Position += new Vector2(Indent, 0f);
            languageLabel.OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER;
            languageLabel.SetToolTip(languageHelp);
            const string editorHelp = "스크립트 코드 편집기에서는 기존 게임처럼 한글 입력이 되지 않도록 막습니다.\n"
                + "* 기존처럼 붙여넣기로 넣는 한글 입력은 가능합니다.\n"
                + "* 한글 입력으로 스크립트가 망가지는 걸 방지하기 위한 기능으로,\n"
                + "  오직 스크립트 코드 편집기에서만 적용되는 기능입니다.";
            vanillaEditor = new MyGuiControlCheckbox(position: new Vector2(0.29f, EditorRow), isChecked: vanilla, toolTip: editorHelp);
            // 설명과 선택은 한 줄로 배치하고 입력 컨트롤의 오른쪽 끝을 체크박스와 맞춘다.
            //
            // Keep the label and selector on one row, with their value column ending at the checkbox edge.
            float valueRight = vanillaEditor.Position.X + vanillaEditor.Size.X * 0.5f;
            inputLanguage = new MyGuiControlCombobox(position: new Vector2(valueRight, LanguageRow),
                size: new Vector2(valueRight - ValueLeft, 0.04f), isAutoscaleEnabled: true, minTextScale: 0.48f);
            inputLanguage.OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_CENTER;
            inputLanguage.AddItem(0, new StringBuilder("이전 한/영 입력 상태 기억"));
            inputLanguage.AddItem(1, new StringBuilder("입력창별 한/영 입력 상태 기억"));
            inputLanguage.AddItem(2, new StringBuilder("채팅창에선 한/영 입력 상태 기억  |  그 외엔 항상 영어로 입력 시작"));
            inputLanguage.AddItem(3, new StringBuilder("모든 곳에서 항상 영어로 입력 시작"));
            inputLanguage.SelectItemByKey(language);
            inputLanguage.SetToolTip(languageHelp);
            Controls.Add(inputLanguage);
            var editorLabel = Label("스크립트 코드 편집기에서 한글 입력 무시", EditorRow, 0.72f);
            editorLabel.Position += new Vector2(Indent, 0f);
            editorLabel.OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER;
            editorLabel.SetToolTip(editorHelp);
            Controls.Add(vanillaEditor);
            Heading("폰트 설정", FontHeading);
            const string iconHelp = "채팅 등에서 이름 옆에 표시되는 플랫폼 아이콘을 숨깁니다.\n* 변경 후 게임을 재시작해야 적용됩니다.";
            var option = Label("플랫폼 아이콘 숨기기 (재시작 필요)", FontRow, 0.78f);
            option.Position += new Vector2(Indent, 0f);
            option.OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER;
            option.SetToolTip(iconHelp);
            hideIcons = new MyGuiControlCheckbox(position: new Vector2(0.29f, FontRow),
                isChecked: selected, toolTip: iconHelp);
            Controls.Add(hideIcons);

            float logRuleY = Heading("이번 실행 관련 로그", LogHeading);

            var details = new MyGuiControlMultilineText(position: new Vector2(Left + Indent, LogTop),
                size: new Vector2(valueRight - Left - Indent, FooterSeparator - LogBottomGap - LogTop), font: "White", textScale: 0.58f,
                textAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP,
                textBoxAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP,
                drawScrollbarV: true, drawScrollbarH: false, selectable: true);
            details.OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP;
            preparationReport = PreparationReport.Snapshot();
            details.Text = new StringBuilder(preparationReport);
            Controls.Add(details);
            // 세로 스크롤바는 텍스트 영역 상단에서 시작한다. 구분선과 그 상단의 중간을
            // 기준으로 하되 실제 화면 검토에 따라 왼쪽 8px·아래 3px를 보정한다.
            // 픽셀을 현재 GUI 좌표로 변환하여 해상도가 바뀌어도 같은 이동량을 사용한다.
            //
            // Start from the midpoint of the heading rule and the text area's scrollbar top,
            // then apply the visually reviewed offset: 8px left and 3px down. Convert pixels
            // to current GUI coordinates so a resolution change preserves the displacement.
            Vector2 copyBasePosition = new Vector2(valueRight, (logRuleY + details.Position.Y) * 0.5f);
            Vector2 copyPosition = copyBasePosition
                + MyGuiManager.GetNormalizedSizeFromScreenSize(new Vector2(-8f, 3f));
            // 기본 버튼은 스타일의 최소 크기로 다시 커진다. 작은 스타일을 축소하고
            // 오른쪽 원점으로 정렬해 호버·문구 변경 때도 값 영역 끝을 유지한다.
            //
            // The default style restores its minimum size. Scale the small style and
            // right-anchor it so hover/text updates retain the common value-column edge.
            var copy = new MyGuiControlButton(position: copyPosition,
                visualStyle: MyGuiControlButtonStyleEnum.Small, buttonScale: 0.65f,
                text: new StringBuilder("내역 복사"), textScale: 0.45f,
                originAlign: MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_CENTER,
                onButtonClick: CopyPreparationReport);
            Controls.Add(copy);
            // 결과 문구는 기존 위치를 유지한다. 버튼의 시각적 보정을 함께 적용하지 않는다.
            //
            // Keep feedback at its existing position, independent of the button's visual offset.
            copyFeedback = Label("", copyBasePosition.Y, 0.45f);
            copyFeedback.Position = copyBasePosition - new Vector2(copy.Size.X + 0.012f, 0f);
            copyFeedback.OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_CENTER;

            var separator = new MyGuiControlSeparatorList();
            separator.AddHorizontal(new Vector2(Left, FooterSeparator), Width);
            Controls.Add(separator);
            var save = new MyGuiControlButton(position: new Vector2(-0.14f, 0.35f),
                text: new StringBuilder("저장"), textScale: 0.72f, onButtonClick: Save);
            Controls.Add(save);
            Controls.Add(new MyGuiControlButton(position: new Vector2(0.14f, 0.35f),
                text: new StringBuilder("취소"), textScale: 0.72f,
                onButtonClick: button => CloseScreen()));
            FocusedControl = inputLanguage;
        }

        private float Heading(string text, float y)
        {
            var label = Label(text, y, 0.8f, HeadingColor);
            // 실제 글자 폭 뒤에서 선을 시작하여 폰트/GUI 배율에 따라 제목과 겹치지 않게 한다.
            //
            // Start after the measured label so font/GUI scaling cannot put the rule through the title.
            float offset = label.Size.X + 0.012f;
            float ruleY = y + label.Size.Y * 0.5f;
            if (offset >= Width) return ruleY;
            var rule = new MyGuiControlSeparatorList();
            rule.AddHorizontal(new Vector2(Left + offset, ruleY), Width - offset);
            Controls.Add(rule);
            return ruleY;
        }

        private MyGuiControlLabel Label(string text, float y, float scale, Vector4? color = null)
        {
            var label = new MyGuiControlLabel(position: new Vector2(Left, y), text: text,
                font: "White", textScale: scale, colorMask: color,
                originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP);
            Controls.Add(label);
            return label;
        }

        private void Save(MyGuiControlButton button)
        {
            // 입력 설정은 다음 진입에 사용하고 폰트는 다음 실행 때 준비한다. 저장 실패 시 편집을 유지한다.
            //
            // Apply input preferences on next entry; prepare fonts on next launch. Keep edits on failure.
            try
            {
                PluginSettings.Save(new PluginSettings { HidePlatformIcons = hideIcons.IsChecked,
                    InputLanguage = (Input.InputLanguagePolicy)inputLanguage.GetSelectedKey(),
                    VanillaScriptEditor = vanillaEditor.IsChecked });
            }
            catch (Exception error)
            {
                MyLog.Default.WriteLine("[Arstraea.KoreanPatch] Settings save failed: " + error);
                MyGuiSandbox.AddScreen(MyGuiSandbox.CreateMessageBox(
                    messageCaption: new StringBuilder(Plugin.DisplayName),
                    messageText: new StringBuilder("설정 저장에 실패했습니다.\n\n" + error.Message)));
                return;
            }
            CloseScreen();
        }

        private void CopyPreparationReport(MyGuiControlButton button)
        {
            try
            {
                PreparationReport.Copy(preparationReport);
                copyFeedback.ColorMask = Vector4.One;
                copyFeedback.Text = "로그를 복사했습니다.";
            }
            catch (Exception error)
            {
                copyFeedback.ColorMask = CopyErrorColor;
                copyFeedback.Text = "로그 복사에 실패했습니다. 다시 시도해 주세요.";
                MyLog.Default.WriteLine("[Arstraea.KoreanPatch] Preparation report copy failed: " + error);
            }
        }
    }
}
