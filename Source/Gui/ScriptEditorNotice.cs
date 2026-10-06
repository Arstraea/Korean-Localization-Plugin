using System;
using System.Reflection;
using HarmonyLib;
using Sandbox.Graphics.GUI;
using VRage.Utils;
using VRageMath;

namespace Arstraea.KoreanPatch.Gui
{
    // 옵션에 따른 입력 제한을 코드 편집기에서 설명한다. 입력 동작이나 코드 내용은 수정하지 않는다.
    // GUI 생성 뒤에만 라벨을 추가하며 시작 전 렌더 초기화를 피한다.
    //
    // Explain the configured input restriction in the code editor without changing code or input.
    // Add the label after GUI creation; never initialize rendering during preloading.
    internal static class ScriptEditorNotice
    {
        private static bool installed;

        internal static void Install()
        {
            if (installed) return;
            try
            {
                Type editor = Assembly.Load("Sandbox.Game").GetType("Sandbox.Game.Gui.MyGuiScreenEditor", true);
                var target = AccessTools.Method(editor, "RecreateControls", new[] { typeof(bool) })
                    ?? throw new MissingMethodException(editor.FullName, "RecreateControls");
                var pending = new PatchTransaction("Arstraea.KoreanPatch.ScriptEditorNotice");
                pending.Add(target, postfix: new HarmonyMethod(typeof(ScriptEditorNotice), nameof(AfterRecreate)));
                pending.Apply();
                installed = true;
            }
            catch (Exception error)
            {
                Fonts.PatchStartupGate.WriteLog("Script editor notice unavailable; input settings remain active. " + error);
            }
        }

        private static void AfterRecreate(MyGuiScreenBase __instance, MyGuiControlButton ___m_okButton,
            MyGuiControlCompositePanel ___m_descriptionBackgroundPanel)
        {
            if (!PluginSettings.Current.VanillaScriptEditor) return;
            if (___m_okButton == null || ___m_descriptionBackgroundPanel == null || !__instance.Size.HasValue) return;
            // 버튼 줄과 화면 하단 사이를 사용하고 본문·행 번호·게임패드 안내는 이동하지 않는다.
            //
            // Use the space below the button row without moving code, line counters or gamepad help.
            float buttonBottom = ___m_okButton.Position.Y + ___m_okButton.Size.Y * 0.5f;
            float screenBottom = __instance.Size.Value.Y * 0.5f;
            var notice = new MyGuiControlLabel(
                position: new Vector2(___m_descriptionBackgroundPanel.Position.X, (buttonBottom + screenBottom) * 0.5f),
                text: "한글화 플러그인 설정으로 인해 편집기 내에서의 한글 입력이 제한됩니다.",
                colorMask: new Color(UiTextColors.SoftRedR, UiTextColors.SoftRedG, UiTextColors.SoftRedB).ToVector4(),
                textScale: 0.65f, font: "White", originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER,
                maxWidth: ___m_descriptionBackgroundPanel.Size.X, isAutoScaleEnabled: true, minimumTextScale: 0.45f);
            notice.Name = "KoreanPatchScriptEditorNotice";
            __instance.Controls.Add(notice);
        }
    }
}
