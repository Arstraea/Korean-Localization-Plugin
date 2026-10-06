using System;
using System.Text;
using Sandbox.Game.Gui;
using Sandbox.Graphics.GUI;
using VRage;

namespace Arstraea.KoreanPatch.Input
{
    // 표시 텍스트/블록 ID 대신 화면 종류와 컨트롤 구조로 입력 용도를 구분한다.
    // 초기 포커스가 화면 등록보다 빠르므로 등록 화면이 아닌 실제 Owner를 우선한다.
    //
    // Identify roles by screen/control structure, never displayed text or block IDs.
    // Use the actual owner because initial focus can precede screen registration.
    internal sealed class InputContext
    {
        internal readonly WeakReference Target, Screen;
        internal readonly string Key;
        internal readonly bool Chat, Script, Multiline;

        internal InputContext(IMyImeActiveControl target, IVRageGuiScreen fallback)
        {
            Target = new WeakReference(target);
            Multiline = target is MyGuiControlMultilineEditableText;
            var control = target as MyGuiControlBase;
            var path = new StringBuilder();
            MyGuiScreenBase screen = null;
            for (int depth = 0; control != null && depth < 24; depth++)
            {
                path.Insert(0, "/" + control.GetType().Name + ":" + control.Name + "#" + Ordinal(control));
                if (control.Owner is MyGuiScreenBase owner) { screen = owner; break; }
                control = control.Owner as MyGuiControlBase;
            }
            object identified = screen ?? (object)fallback;
            string type = identified?.GetType().FullName ?? "UnknownScreen";
            Screen = new WeakReference(identified);
            Chat = type == "Sandbox.Game.Gui.MyGuiScreenChat";
            // 같은 여러 줄 컨트롤을 쓰는 LCD/사용자 지정 데이터는 제외하지 않는다.
            // Do not exclude LCD/custom data merely because they use the same control.
            Script = screen is MyGuiScreenEditor editor && ReferenceEquals(editor.Description, target);
            Key = Chat ? "Chat" : type + (path.Length == 0 ? "/" + target.GetType().FullName : path.ToString());
        }

        private static int Ordinal(MyGuiControlBase control)
        {
            // 동일한 기본 Name을 가진 검색/이름 칸도 서로 구분하며 화면 재생성 후에는 같은 키를 쓴다.
            // Distinguish siblings with default names while keeping keys stable across screen recreation.
            MyGuiControls siblings = (control.Owner as MyGuiScreenBase)?.Controls
                ?? (control.Owner as MyGuiControlBase)?.Elements;
            int index = 0;
            if (siblings != null)
                foreach (var sibling in siblings)
                {
                    if (ReferenceEquals(sibling, control)) break;
                    if (sibling.GetType() == control.GetType() && sibling.Name == control.Name) index++;
                }
            return index;
        }
    }
}
