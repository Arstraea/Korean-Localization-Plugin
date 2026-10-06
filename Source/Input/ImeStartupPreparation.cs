using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Arstraea.KoreanPatch.Input
{
    // 메인 메뉴가 준비되면 첫 입력 전에 수신부를 교체한다. 늦은 교체에서는
    // 한국어 음절 경계가 유실됐으므로 이 순서를 유지한다. 추가 EDIT/폼 생성,
    // 포커스 이동이나 대기 없이 정상 입력됨을 R13 실사용 시험에서 확인했다.
    //
    // Replace the receiver after menu readiness, before first input. Preserve this
    // order: late replacement lost Korean syllable boundaries. R13 user tests passed
    // without auxiliary EDIT/forms, focus changes or waits.
    internal static class ImeStartupPreparation
    {
        private static Form owner;
        private static Control originalReceiver;
        private static volatile bool armed;
        private static volatile bool menuReady;
        private static MethodInfo screenWithFocus;
        private static PropertyInfo screenState;
        private static volatile bool attempted;
        internal static bool Attempted { get { return attempted; } private set { attempted = value; } }
        internal static bool Armed { get { return armed; } }

        internal static void Register(Form gameWindow, Control original = null)
        {
            if (ReferenceEquals(owner, gameWindow)) return;
            owner = gameWindow;
            originalReceiver = original;
            menuReady = false;
            Attempted = false;
            gameWindow.Disposed += (sender, args) =>
            {
                if (!ReferenceEquals(owner, gameWindow)) return;
                owner = null;
                originalReceiver = null;
            };
        }

        internal static void Arm()
        {
            try
            {
                Type manager = Assembly.Load("Sandbox.Graphics").GetType("Sandbox.Graphics.GUI.MyScreenManager", true);
                Type screenType = manager.Assembly.GetType("Sandbox.Graphics.GUI.MyGuiScreenBase", true);
                // RequireMethod는 인스턴스 전용이다. 정적 GUI 조회에는 별도 계약을 검사한다.
                //
                // RequireMethod is instance-only. Validate the static GUI query explicitly.
                screenWithFocus = manager.GetMethod("GetScreenWithFocus", BindingFlags.Public | BindingFlags.Static,
                    null, Type.EmptyTypes, null);
                screenState = screenType.GetProperty("State", BindingFlags.Public | BindingFlags.Instance);
                if (screenWithFocus == null || screenWithFocus.ReturnType != screenType
                    || screenState == null || !screenState.CanRead)
                {
                    armed = false;
                    ImeDiagnostics.Record("startup-prepare-unavailable", health: true, metadata: "gui-contract-mismatch");
                    return;
                }
                armed = true;
            }
            catch (Exception error)
            {
                armed = false;
                ImeDiagnostics.Record("startup-prepare-unavailable", health: true, metadata: error.GetType().Name);
            }
        }

        // GUI 목록은 게임 갱신 스레드에서만 읽는다. 창 스레드에는 준비 여부만 전달한다.
        //
        // Read the GUI screen list only on the game update thread; publish readiness only.
        internal static void UpdateReadiness()
        {
            // 메뉴에서 입력하지 않고 월드로 바로 들어가도 준비 기회를 잃지 않는다.
            // 배경 실행 등으로 창 스레드 처리가 늦어지면 준비 신호를 유지한다.
            //
            // Latch readiness across entry into a world. Delayed foreground/window
            // processing must not require returning to the menu or typing there first.
            if (!armed || Attempted || menuReady) return;
            try
            {
                object screen = screenWithFocus.Invoke(null, null);
                menuReady = screen != null && screen.GetType().FullName == "SpaceEngineers.Game.GUI.MyGuiScreenMainMenu"
                    && screenState.GetValue(screen).ToString() == "OPENED";
            }
            catch (Exception error)
            {
                armed = menuReady = false;
                ImeDiagnostics.Record("startup-prepare-unavailable", health: true, metadata: error.GetType().Name);
            }
        }

        // RenderLoop 소유 스레드에서만 호출한다. 기다리지 않고 기존 루프로 돌아간다.
        //
        // Called only on the RenderLoop owner thread; return to its existing pump.
        internal static void OnFrame(Control control)
        {
            if (!armed || !menuReady || Attempted || !ReferenceEquals(owner, control) || owner == null
                || owner.IsDisposed || owner.Disposing || !owner.IsHandleCreated
                || !owner.Visible || !owner.ContainsFocus || owner.OwnedForms.Length != 0
                || GetForegroundWindow() != owner.Handle) return;

            Control focused = Control.FromHandle(GetFocus());
            if (focused is ImeReceiver && focused.ImeMode != ImeMode.Disable) return;
            if (focused is TextBoxBase && !ReferenceEquals(focused, originalReceiver)) return;

            // 수신부 교체의 재진입이나 실패도 두 번째 시도를 만들지 않는다.
            //
            // Mark before replacement: reentrancy and failure must not retry.
            Attempted = true;
            ImeDiagnostics.Record("startup-receiver-start", health: true,
                metadata: "mainMenuObserved=True korean=" + NativeIme.IsKoreanLayout()
                    + " ownerForeground=True " + NativeIme.Snapshot(owner.Handle));
            try
            {
                // 초기 기본 수신창의 AutoFocusing이 준비 직후 포커스를 회수했던 기록이 있다.
                // 현재 키보드 언어와 무관하게 먼저 교체한다. 여기서 한국어 레이아웃으로
                // 바꾸거나 입력을 활성화하지 않는다. 첫 한글 입력이 월드 안이어도 된다.
                //
                // The startup receiver regained focus immediately after the prior probe.
                // Replace regardless of keyboard layout, without switching to Korean or
                // activating input. The first Korean input may occur inside a world.
                bool ready = PrepareReceiverOnly(originalReceiver);
                ImeDiagnostics.Record("startup-receiver-only", health: true,
                    metadata: "ready=" + ready + " auxiliaryEditCreated=False");
            }
            catch (Exception error)
            {
                ImeDiagnostics.Record("startup-prepare-failed", health: true, metadata: error.GetType().Name);
            }
        }

        internal static bool PrepareReceiverOnly(Control original)
        {
            return original != null && !original.IsDisposed && !original.Disposing
                && original.Parent != null && ImeReceiver.GetOrCreate(original) is ImeReceiver;
        }

        [DllImport("user32.dll", ExactSpelling = true)] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", ExactSpelling = true)] private static extern IntPtr GetFocus();

    }
}
