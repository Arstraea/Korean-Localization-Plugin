using System;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Arstraea.KoreanPatch.Gui
{
    // 화면과 복사 내용은 같은 스냅샷이다. 입력 본문이나 월드/플레이어 정보는 수집하지 않는다.
    //
    // Display and copy the same snapshot, without collecting typed text or world/player data.
    internal static class PreparationReport
    {
        internal static string Snapshot()
        {
            return Format(Plugin.DisplayName + " " + typeof(Plugin).Assembly.GetName().Version + " / CLR / Windows",
                Input.ImeBootstrap.Status, Input.ImeControlPolicy.SessionStatus,
                "Input tracing: detailed=" + Input.ImeDiagnostics.Enabled + ".",
                Fonts.FontStartup.Status, Fonts.FontModCompatibility.Status,
                Fonts.BuildInfoFontSupport.Status, Localization.LanguageSupport.Status,
                Mods.TextHudFontSupport.Status, Mods.RichHudDefaultFont.Status);
        }
        internal static string Format(params string[] entries)
        {
            var report = new StringBuilder();
            foreach (string entry in entries)
            {
                bool first = true;
                foreach (string line in (entry ?? "").Replace("\r", "").Split('\n'))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (report.Length != 0) report.Append('\n');
                    report.Append(first ? "* " : "  ").Append(line);
                    first = false;
                }
            }
            return report.ToString();
        }
        internal static void Copy(string report)
        {
            // 게임 갱신 스레드의 apartment를 바꾸지 않고, 버튼을 누를 때만 STA에서 복사한다.
            //
            // Keep the game thread's apartment intact; use STA only for the explicit copy action.
            Exception failure = null;
            var worker = new Thread(() =>
            {
                try { Clipboard.SetText(report); }
                catch (Exception error) { failure = error; }
            }) { IsBackground = true, Name = "KoreanPatch report copy" };
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
            if (!worker.Join(2000)) throw new TimeoutException("Preparation report copy timed out after 2000 ms.");
            if (failure != null) throw new InvalidOperationException("Preparation report clipboard copy failed.", failure);
        }
    }
}
