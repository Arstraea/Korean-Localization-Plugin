using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using Arstraea.KoreanPatch.Localization;
using HarmonyLib;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Graphics;
using Sandbox.Graphics.GUI;
using VRage;
using VRage.Game.Localization;
using VRage.Utils;
using VRageMath;

namespace Arstraea.KoreanPatch.Gui
{
    // 보호는 열람자의 화면에만 적용하며 공유 아이템이나 게임의 저장 프로토콜은 변경하지 않는다.
    //
    // Protect only the reader's screen, without changing shared items or the save protocol.
    internal static class DatapadProtection
    {
        internal const string PatchId = "Arstraea.KoreanPatch.DatapadProtection";
        private static readonly ConditionalWeakTable<MyGuiScreenBase, View> views = new ConditionalWeakTable<MyGuiScreenBase, View>();
        private static MethodInfo saveMethod;
        private static MethodInfo updateBody;
        private static EconomyDatapadCatalog economy = EconomyDatapadCatalog.Empty;
        private static FieldInfo yesButton, noButton, messageBody;
        private static bool installed;

        private sealed class View
        {
            internal DatapadTranslationState Text;
            internal MyGuiControlTextbox Title;
            internal MyGuiControlMultilineEditableText Body;
            internal MyGuiControlLabel Notice;
            internal string RetainedTitle, RetainedBody;
            internal bool KeepTitle, KeepBody, PromptOpen, Approved;
        }

        internal static void Install(EconomyDatapadCatalog catalog = null)
        {
            economy = catalog ?? Fonts.FontStartup.TranslationResult?.EconomyDatapads ?? EconomyDatapadCatalog.Empty;
            if (installed) return;
            try
            {
                Type editor = Assembly.Load("Sandbox.Game").GetType("Sandbox.Game.GUI.MyGuiDatapadEditScreen", true);
                saveMethod = AccessTools.DeclaredMethod(editor, "OkButtonClicked", new[] { typeof(MyGuiControlButton) });
                updateBody = AccessTools.DeclaredMethod(editor, "MultilineTextChanged", new[] { typeof(MyGuiControlMultilineEditableText) })
                    ?? throw new MissingMethodException(editor.FullName, "MultilineTextChanged");
                yesButton = AccessTools.Field(typeof(MyGuiScreenMessageBox), "m_yesButton")
                    ?? throw new MissingFieldException("MyGuiScreenMessageBox", "m_yesButton");
                noButton = AccessTools.Field(typeof(MyGuiScreenMessageBox), "m_noButton")
                    ?? throw new MissingFieldException("MyGuiScreenMessageBox", "m_noButton");
                messageBody = AccessTools.Field(typeof(MyGuiScreenMessageBox), "m_messageBoxText")
                    ?? throw new MissingFieldException("MyGuiScreenMessageBox", "m_messageBoxText");
                var pending = new PatchTransaction(PatchId);
                pending.Add(AccessTools.DeclaredMethod(editor, "RecreateControls", new[] { typeof(bool) }),
                    prefix: new HarmonyMethod(typeof(DatapadProtection), nameof(BeforeRecreate)),
                    postfix: new HarmonyMethod(typeof(DatapadProtection), nameof(AfterRecreate)));
                pending.Add(saveMethod, prefix: new HarmonyMethod(typeof(DatapadProtection), nameof(BeforeSave)),
                    transpiler: new HarmonyMethod(typeof(DatapadProtection), nameof(PreserveSaveFields)));
                pending.Apply();
                installed = true;
            }
            catch (Exception error) { Log("Datapad protection unavailable. ", error); }
        }

        private static void BeforeRecreate(MyGuiScreenBase __instance, MyObjectBuilder_Datapad ___m_datapad)
        {
            if (___m_datapad == null) return;
            View view;
            if (!views.TryGetValue(__instance, out view))
            {
                var text = DatapadTranslationState.Capture(___m_datapad.Name, ___m_datapad.Data,
                    value => MyTexts.SubstituteTexts(value, null));
                string title, body;
                if (text == null && IsKorean() && economy.TryTranslate(___m_datapad.Name, ___m_datapad.Data,
                    key => MyTexts.GetString(MyStringId.GetOrCompute(key)), out title, out body))
                    text = DatapadTranslationState.FromEconomy(___m_datapad.Name, ___m_datapad.Data, title, body);
                if (text == null) return;
                view = new View { Text = text };
                views.Add(__instance, view);
            }
            view.KeepTitle = view.Text.Initialized && view.Title != null && view.Text.TitleChanged(view.Title.Text);
            view.KeepBody = view.Text.Initialized && view.Body != null && view.Text.BodyChanged(view.Body.Text.ToString());
            view.RetainedTitle = view.KeepTitle ? view.Title.Text : null;
            view.RetainedBody = view.KeepBody ? view.Body.Text.ToString() : null;
        }

        private static void AfterRecreate(MyGuiScreenBase __instance, MyGuiControlTextbox ___m_textboxName,
            MyGuiControlMultilineEditableText ___m_textbox, MyGuiControlLabel ___m_characterCountLabel,
            MyGuiControlButton ___m_okButton, MyGuiControlButton ___m_createGpsCoord)
        {
            View view;
            if (!views.TryGetValue(__instance, out view)) return;
            view.Title = ___m_textboxName; view.Body = ___m_textbox;
            if (view.Title == null || view.Body == null) return;
            if (view.Text.DisplayTitle != null && IsKorean())
            {
                view.Title.Text = view.Text.DisplayTitle;
                view.Body.Text = new StringBuilder(view.Text.DisplayBody);
                updateBody.Invoke(__instance, new object[] { view.Body });
            }
            // GUI 재생성으로 편집 중인 문장을 무수정 기준으로 바꾸거나 잃지 않는다.
            //
            // Preserve edits across GUI rebuilds; never adopt edited text as a clean baseline.
            if (view.KeepTitle) view.Title.Text = view.RetainedTitle;
            else view.Text.InitialTitle = view.Title.Text;
            if (view.KeepBody) view.Body.Text = new StringBuilder(view.RetainedBody);
            else view.Text.InitialBody = view.Body.Text.ToString();
            view.Text.Initialized = true;
            if (view.Notice != null) __instance.Controls.Remove(view.Notice);
            view.Notice = null;
            if (!view.Text.Translated || ___m_characterCountLabel == null) return;
            var anchor = ___m_characterCountLabel;
            Vector2 top = MyUtils.GetCoordTopLeftFromAligned(anchor.Position, anchor.Size, anchor.OriginAlign);
            float gap = MyGuiManager.GetNormalizedSizeFromScreenSize(new Vector2(0, 5)).Y;
            float width = ___m_okButton == null ? 0.31f
                : MyUtils.GetCoordTopLeftFromAligned(___m_okButton.Position, ___m_okButton.Size, ___m_okButton.OriginAlign).X - top.X;
            if (___m_createGpsCoord != null && ___m_createGpsCoord.Visible)
                width = Math.Min(width, MyUtils.GetCoordTopLeftFromAligned(___m_createGpsCoord.Position,
                    ___m_createGpsCoord.Size, ___m_createGpsCoord.OriginAlign).X - top.X);
            width -= MyGuiManager.GetNormalizedSizeFromScreenSize(new Vector2(8, 0)).X;
            view.Notice = new MyGuiControlLabel(position: new Vector2(top.X, top.Y + anchor.Size.Y + gap),
                text: KoreanText.DatapadTranslationNotice, colorMask: SoftRed, textScale: anchor.TextScale, font: "White",
                originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP,
                maxWidth: Math.Max(0.12f, width), isAutoScaleEnabled: true, minimumTextScale: 0.45f);
            view.Notice.Name = "KoreanPatchDatapadTranslationNotice";
            __instance.Controls.Add(view.Notice);
        }

        private static bool BeforeSave(MyGuiScreenBase __instance)
        {
            View view;
            if (!views.TryGetValue(__instance, out view)) return true;
            if (!view.Text.Initialized || view.PromptOpen) return false;
            if (view.Approved) { view.Approved = false; return true; }
            string title = view.Title.Text, body = view.Body.Text.ToString();
            if (!view.Text.Changed(title, body)) { __instance.CloseScreen(); return false; }
            if (!view.Text.HasLocalizationKeys) return true;
            try
            {
                view.PromptOpen = true;
                bool handled = false;
                var popup = CreateSaveDialog(result =>
                {
                    if (handled) return;
                    handled = true; view.PromptOpen = false;
                    if (result != MyGuiScreenMessageBox.ResultEnum.YES || __instance.State == MyGuiScreenState.CLOSED
                        || __instance.State == MyGuiScreenState.CLOSING) return;
                    // 팝업을 연 뒤의 변경까지 승인하지 않는다. 마우스·게임패드도 같은 진입점으로 돌아온다.
                    //
                    // Approve only the reviewed snapshot, through the same mouse/gamepad save entry point.
                    if (!DatapadTranslationState.Same(title, view.Title.Text)
                        || !DatapadTranslationState.Same(body, view.Body.Text.ToString())) return;
                    view.Approved = true;
                    try { saveMethod.Invoke(__instance, new object[] { null }); }
                    finally { view.Approved = false; }
                }, () => view.PromptOpen = false);
                MyGuiSandbox.AddScreen(popup);
            }
            catch (Exception error) { view.PromptOpen = false; Log("Datapad save prompt failed; no save was sent. ", error); }
            return false;
        }

        private static Vector4 SoftRed => new Color(UiTextColors.SoftRedR, UiTextColors.SoftRedG, UiTextColors.SoftRedB).ToVector4();

        private static bool IsKorean() => (MyLanguage.Instance as MyLanguage)?.CurrentLanguage == (MyLanguagesEnum)LanguageSupport.KoreanId;

        internal static MyGuiScreenMessageBox CreateSaveDialog(Action<MyGuiScreenMessageBox.ResultEnum> callback, Action onClosing)
        {
            var popup = MyGuiSandbox.CreateMessageBox(buttonType: MyMessageBoxButtonsType.YES_NO,
                messageText: new StringBuilder(KoreanText.DatapadSaveExplanation + "\n\n" + KoreanText.DatapadSaveWarning
                    + "\n\n" + KoreanText.DatapadSaveQuestion),
                messageCaption: new StringBuilder(KoreanText.DatapadSaveTitle), callback: callback,
                focusedResult: MyGuiScreenMessageBox.ResultEnum.NO, size: new Vector2(0.839375f, 0.42f), onClosing: onClosing);
            // 같은 공용 창을 재생성하더라도 우리 본문의 색상만 다시 구성한다.
            // 다른 메시지 박스에는 전역 패치를 설치하지 않는다.
            //
            // Reapply colored runs after this dialog rebuilds; do not globally patch message boxes.
            popup.Controls.CollectionChanged += controls => FormatSaveDialog(popup);
            FormatSaveDialog(popup);
            return popup;
        }

        private static void FormatSaveDialog(MyGuiScreenMessageBox popup)
        {
            var text = messageBody.GetValue(popup) as MyGuiControlMultilineText;
            if (text != null && popup.Controls.Contains(text))
            {
                text.TextAlign = MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER;
                text.TextBoxAlign = MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER;
                text.Clear();
                text.AppendText(KoreanText.DatapadSaveExplanation + "\n\n", "Blue", text.TextScale, Vector4.One);
                text.AppendText(KoreanText.DatapadSaveWarning, "White", text.TextScale, SoftRed);
                text.AppendText("\n\n" + KoreanText.DatapadSaveQuestion, "Blue", text.TextScale, Vector4.One);
            }
            var save = yesButton.GetValue(popup) as MyGuiControlButton;
            var cancel = noButton.GetValue(popup) as MyGuiControlButton;
            if (save != null) save.Text = KoreanText.Save;
            if (cancel != null) cancel.Text = KoreanText.Cancel;
        }

        private static string SaveTitle(string current, MyGuiScreenBase screen)
        { View view; return views.TryGetValue(screen, out view) ? view.Text.SaveTitle(current) : current; }
        private static string SaveBody(string current, MyGuiScreenBase screen)
        { View view; return views.TryGetValue(screen, out view) ? view.Text.SaveBody(current) : current; }

        // 전송 직전의 값만 선택한다. 원본 아이템·편집 컨트롤을 임시로 변조하지 않는다.
        //
        // Select only outgoing values; never temporarily overwrite the item or editor controls.
        private static IEnumerable<CodeInstruction> PreserveSaveFields(IEnumerable<CodeInstruction> source)
        {
            var code = source.ToList();
            var titleGetter = AccessTools.PropertyGetter(typeof(MyGuiControlTextbox), "Text");
            var toString = AccessTools.Method(typeof(object), "ToString", Type.EmptyTypes);
            if (code.Count(c => c.Calls(titleGetter)) != 1 || code.Count(c => c.Calls(toString)) != 1)
                throw new InvalidOperationException("Datapad save getters changed; refusing an incomplete protection patch.");
            foreach (var instruction in code)
            {
                yield return instruction;
                if (instruction.Calls(titleGetter) || instruction.Calls(toString))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(DatapadProtection),
                        instruction.Calls(titleGetter) ? nameof(SaveTitle) : nameof(SaveBody)));
                }
            }
        }

        private static void Log(string message, Exception error) => Fonts.PatchStartupGate.WriteLog(message + error);
    }
}
