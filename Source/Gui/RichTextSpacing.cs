using System;
using System.Collections;
using System.Linq.Expressions;
using System.Text;
using HarmonyLib;
using Sandbox.Graphics;
using Sandbox.Graphics.GUI;
using VRageMath;

namespace Arstraea.KoreanPatch.Gui
{
    // 게임은 높이가 다른 글자를 나눠 그리지만 각 묶음의 마지막 간격을 생략한다.
    // 커서가 사용하는 전체 문자열 폭과 맞도록 생성 시 묶음 경계만 보충한다.
    //
    // Height runs omit their final spacing when measured separately. Restore only
    // the run boundaries at construction so drawing agrees with whole-text caret measurement.
    internal static class RichTextSpacing
    {
        internal const string PatchId = "Arstraea.KoreanPatch.RichTextSpacing";
        private static bool installed;
        private static Func<object, StringBuilder> getText;
        private static Func<object, string> getFont;
        private static Func<object, float> getScale;
        private static Func<object, IList> getRuns;
        private static Func<object, Vector2> getSize;
        private static Action<object, Vector2> setSize;

        internal static void Install()
        {
            if (installed) return;
            try
            {
                var assembly = typeof(MyGuiControlMultilineEditableText).Assembly;
                var labelType = assembly.GetType("Sandbox.Graphics.GUI.MyRichLabelText", true);
                var partType = assembly.GetType("Sandbox.Graphics.GUI.MyRichLabelPart", true);
                getText = Getter<StringBuilder>(labelType, "Text");
                getFont = Getter<string>(labelType, "Font");
                getScale = Getter<float>(labelType, "Scale");
                getSize = Getter<Vector2>(partType, "Size");
                var instance = Expression.Parameter(typeof(object), "instance");
                getRuns = Expression.Lambda<Func<object, IList>>(Expression.Convert(
                    Expression.Field(Expression.Convert(instance, labelType), "SubtextsByLineHeight"), typeof(IList)), instance).Compile();
                var size = Expression.Parameter(typeof(Vector2), "size");
                var setter = AccessTools.PropertySetter(partType, "Size")
                    ?? throw new MissingMethodException(partType.FullName, "set_Size");
                setSize = Expression.Lambda<Action<object, Vector2>>(
                    Expression.Call(Expression.Convert(instance, partType), setter, size), instance, size).Compile();
                var target = AccessTools.Method(labelType, "RecalculateByLineHeights", Type.EmptyTypes)
                    ?? throw new MissingMethodException(labelType.FullName, "RecalculateByLineHeights");
                new Harmony(PatchId).Patch(target, postfix: new HarmonyMethod(typeof(RichTextSpacing), nameof(AfterSplit)));
                installed = true;
            }
            catch (Exception error)
            {
                Fonts.PatchStartupGate.WriteLog("Rich text spacing correction unavailable; original layout retained. " + error);
            }
        }

        private static Func<object, T> Getter<T>(Type type, string name)
        {
            var getter = AccessTools.PropertyGetter(type, name)
                ?? throw new MissingMethodException(type.FullName, "get_" + name);
            var instance = Expression.Parameter(typeof(object), "instance");
            return Expression.Lambda<Func<object, T>>(
                Expression.Call(Expression.Convert(instance, type), getter), instance).Compile();
        }

        private static void AfterSplit(object __instance)
        {
            var runs = getRuns(__instance);
            if (runs == null || runs.Count < 2 || !HasHangul(getText(__instance))) return;
            string font = getFont(__instance);
            float scale = getScale(__instance);
            for (int i = 1; i < runs.Count; i++)
            {
                var left = runs[i - 1];
                var right = runs[i];
                var leftText = getText(left);
                var rightText = getText(right);
                if (leftText.Length == 0 || rightText.Length == 0) continue;
                string last = leftText[leftText.Length - 1].ToString();
                string first = rightText[0].ToString();
                float boundary = MyGuiManager.MeasureString(font, last + first, scale).X
                    - MyGuiManager.MeasureString(font, last, scale).X
                    - MyGuiManager.MeasureString(font, first, scale).X;
                var size = getSize(left);
                size.X += boundary;
                setSize(left, size);
            }
        }

        private static bool HasHangul(StringBuilder text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if ((c >= '\uac00' && c <= '\ud7a3') || (c >= '\u3130' && c <= '\u318f')
                    || (c >= '\u1100' && c <= '\u11ff')) return true;
            }
            return false;
        }
    }
}
