using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Arstraea.KoreanPatch.Fonts
{
    // 제보용 단계/예외와 해결 안내를 구분한다. 복사·페이지 열기는 시작 선택이 아니다.
    //
    // Separate report details from guidance. Copying/opening a page never accepts launch.
    internal sealed class PatchDialog : Form
    {
        internal static string WorkshopUrl => "https://steamcommunity.com/sharedfiles/filedetails/?id=" + FontStartup.WorkshopId;
        private readonly Font bodyFont, headingFont, detailFont, copyFont;
        private readonly Label intro, stageTitle, stageText, sectionTitle, guidance, questionTitle, questionText, progressText;
        private readonly TextBox details;
        private readonly Panel progressPanel;
        private readonly PatchProgressBar progressBar;
        private readonly Label countdown;
        private PatchView currentView;
        private readonly Button copy, run, cancel;
        private PatchFailure failure;
        private PatchSession session;
        private bool allowClose;

        internal PatchDialog(PatchFailure failure) : this(failure, Clipboard.SetText, OpenWorkshop) { }
        internal PatchDialog(PatchSession session) : this((PatchFailure)null, Clipboard.SetText, OpenWorkshop)
        { this.session = session; Render(session.View); }

        internal PatchDialog(PatchFailure failure, Action<string> copyReport, Action<string> openPage)
        {
            this.failure = failure;
            bodyFont = new Font(SystemFonts.MessageBoxFont.FontFamily, 10F);
            headingFont = new Font(bodyFont.FontFamily, 11F, FontStyle.Bold);
            detailFont = new Font(bodyFont.FontFamily, 9F);
            copyFont = new Font(bodyFont.FontFamily, 8.5F);
            Text = Plugin.DisplayTitle;
            Font = bodyFont;
            BackColor = Color.FromArgb(36, 36, 36);
            ForeColor = Color.FromArgb(242, 242, 242);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            ClientSize = new Size(800, 650);
            MinimumSize = new Size(720, 520);
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = false;
            // 로더 시작 화면 뒤에 가려진 채 선택을 기다리지 않게 한다.
            // Keep the choice visible above the loader splash.
            TopMost = true;

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 2 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty };
            var content = Stack();
            content.Dock = DockStyle.Top;
            intro = Label("", false, 20);
            content.Controls.Add(intro);
            stageTitle = Label("", true, 0);
            stageText = Label("", false, 0);
            content.Controls.Add(new TextPairPanel(stageTitle, stageText, 23, 23, 23, 73) { Margin = new Padding(0, 5, 0, 0) });

            var errorHeading = new Panel { Height = 25, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 6) };
            var title = sectionTitle = Label("오류 상세", true, 0);
            title.Dock = DockStyle.None;
            title.Location = new Point(0, 3);
            copy = new Button { Text = "내역 복사", Font = copyFont, Size = new Size(76, 23), Margin = Padding.Empty };
            errorHeading.Controls.Add(title);
            errorHeading.Controls.Add(copy);
            errorHeading.Layout += (sender, args) =>
                copy.Location = new Point(errorHeading.ClientSize.Width - copy.Width - Dpi(1), Dpi(2));
            content.Controls.Add(errorHeading);
            details = new TextBox { Multiline = true, ReadOnly = true,
                WordWrap = false, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Height = 105,
                Font = detailFont, BackColor = Color.FromArgb(48, 48, 48), ForeColor = ForeColor, BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(3, 0, 0, 8) };
            var statusArea = new Panel { Dock = DockStyle.Fill, Height = 105, Margin = new Padding(3, 0, 0, 8) };
            statusArea.Controls.Add(details);
            progressPanel = new Panel { Dock = DockStyle.Fill };
            progressBar = new PatchProgressBar { Dock = DockStyle.Top, Height = 18 };
            progressText = Label("", false, 0);
            progressText.Dock = DockStyle.Fill;
            progressText.Padding = new Padding(0, 10, 0, 0);
            progressPanel.Controls.Add(progressText);
            progressPanel.Controls.Add(progressBar);
            statusArea.Controls.Add(progressPanel);
            content.Controls.Add(statusArea);
            var feedback = Label("", false, 14);
            feedback.MinimumSize = new Size(0, 18);
            feedback.Font = detailFont;
            content.Controls.Add(feedback);
            guidance = Label("", false, 16);
            content.Controls.Add(guidance);
            scroll.Controls.Add(content);
            root.Controls.Add(scroll, 0, 0);

            var footer = Stack();
            footer.Controls.Add(new Panel { Height = 1, BackColor = SystemColors.ControlDark, Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 12) });
            questionTitle = Label("", true, 0);
            questionText = Label("", false, 0);
            var question = new TextPairPanel(questionTitle, questionText,
                24, 36, 41, 58) { Margin = new Padding(0, 0, 0, 18) };
            footer.Controls.Add(question);
            var buttons = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = Padding.Empty };
            buttons.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var workshop = Button("한글화 팩 창작마당 열기");
            workshop.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            workshop.Margin = Padding.Empty;
            run = Button("무시하고 실행");
            cancel = Button("종료");
            countdown = Label("", false, 0);
            countdown.Font = detailFont;
            countdown.TextAlign = ContentAlignment.MiddleRight;
            countdown.Anchor = AnchorStyles.Right;
            countdown.Margin = new Padding(8, 0, 0, 0);
            buttons.Controls.Add(workshop, 0, 0);
            buttons.Controls.Add(countdown, 1, 0);
            buttons.Controls.Add(run, 2, 0);
            buttons.Controls.Add(cancel, 3, 0);
            footer.Controls.Add(buttons);
            root.Controls.Add(footer, 0, 1);
            Controls.Add(root);
            StyleButtons(root);

            copy.Click += (sender, args) =>
            {
                if (this.failure == null) { session?.RequestRefresh(); return; }
                try { copyReport(this.failure.ReportText); feedback.Text = "패치 단계와 오류 상세를 함께 복사했습니다."; }
                catch (Exception error)
                {
                    feedback.Text = "복사하지 못했습니다. 잠시 후 [내역 복사]를 다시 눌러 주세요.";
                    PatchStartupGate.WriteLog("Could not copy failure report: " + error);
                }
            };
            workshop.Click += (sender, args) =>
            {
                try { openPage(WorkshopUrl); feedback.Text = "창작마당 페이지를 여는 요청을 보냈습니다."; }
                catch (Exception error)
                {
                    feedback.Text = "페이지를 열지 못했습니다. Steam 창작마당에서 Korean Localization Pack 한글화 팩(1843839106)을 찾아 주세요.";
                    PatchStartupGate.WriteLog("Could not open Workshop page: " + error);
                }
            };
            run.Click += (sender, args) =>
            {
                if (session == null) { DialogResult = DialogResult.OK; Close(); }
                else if (this.failure != null || currentView.IsComplete) session.ChooseRun();
                else session.RequestStop();
            };
            cancel.Click += (sender, args) =>
            {
                if (session == null) { DialogResult = DialogResult.Cancel; Close(); }
                else session.RequestExit();
            };
            AcceptButton = cancel;
            CancelButton = cancel;
            // CancelButton 속성은 DialogResult를 자동 지정하므로 통합 실행에서는 다시 비운다.
            // CancelButton assigns DialogResult automatically; keep closure under session control.
            cancel.DialogResult = DialogResult.None;
            Shown += (sender, args) => cancel.Select();
            Render(failure == null ? PatchView.Working(PatchStage.Paths) : PatchView.Error(failure));
        }

        internal void Render(PatchView view)
        {
            currentView = view;
            SuspendLayout();
            try
            {
                failure = view.Failure;
                bool failed = failure != null;
                bool completed = view.IsComplete;
                bool stopping = session != null && (session.ExitRequested || (session.StopRequested && !failed));
                intro.Text = failed || completed ? view.Message : "게임을 시작하기 전에 한글화 팩 자료를 준비합니다.";
                stageTitle.Text = failed ? "오류가 발생한 패치 단계" : "현재 진행 중인 패치 단계";
                var titleColor = failed ? Color.FromArgb(UiTextColors.SoftRedR, UiTextColors.SoftRedG, UiTextColors.SoftRedB) : Color.FromArgb(0x5C, 0xD1, 0xE5);
                stageTitle.ForeColor = sectionTitle.ForeColor = questionTitle.ForeColor = titleColor;
                stageText.Text = "▶ " + PatchStages.Display(view.Stage);
                sectionTitle.Text = failed ? "오류 상세" : "준비 상태";
                details.Visible = failed;
                progressPanel.Visible = !failed;
                if (failed) details.Text = failure.Details;
                guidance.Text = view.Guidance;
                copy.Text = failed ? "내역 복사" : "다시 확인";
                copy.Enabled = failed || (view.IsWaiting && !stopping);
                copy.Visible = !completed;
                run.Text = failed ? "무시하고 실행" : completed ? "지금 게임 시작" : "대기 중단";
                run.Enabled = !stopping && (failed || completed || view.IsWaiting);
                cancel.Enabled = session == null || !session.ExitRequested;
                questionTitle.Text = failed ? "오류를 무시하고 게임을 실행할까요?" : completed ? "준비가 완료되었습니다." : "준비가 완료되면 자동으로 계속 진행합니다.";
                questionText.Text = failed ? "패치가 일부 적용되지 않거나 정상적으로 동작하지 않을 수 있습니다.\n이 선택은 이번 실행에만 적용됩니다."
                    : "패치가 완료되면 게임이 실행됩니다.\n종료를 선택해도 Steam 다운로드 자체는 취소하지 않습니다.";
                if (!failed)
                {
                    var item = view.Download;
                    bool known = item != null && item.HasProgress;
                    progressBar.Indeterminate = !completed && !known && (item == null || item.Subscribed);
                    string number = "";
                    if (known)
                    {
                        int percent = (int)Math.Min(100, item.Downloaded * 100.0 / item.Total);
                        progressBar.Percent = percent;
                        number = "\n" + percent + "%  ·  " + (item.Downloaded / 1048576.0).ToString("0.0") + " / "
                            + (item.Total / 1048576.0).ToString("0.0") + " MiB";
                    }
                    else progressBar.Percent = completed ? 100 : 0;
                    progressText.Text = stopping ? "중단 요청을 받았습니다. 진행 중인 적용·복구가 끝날 때까지 기다려 주세요." : view.Message + number;
                }
                TickProgress(session?.Elapsed ?? TimeSpan.Zero);
            }
            finally { ResumeLayout(true); }
        }

        internal void TickProgress(TimeSpan elapsed)
        {
            var view = currentView;
            if (view == null) return;
            if (view.IsComplete)
            {
                int remaining = (int)Math.Max(0, Math.Ceiling(((view.LaunchAt ?? (elapsed + PatchSession.CompletionDelay)) - elapsed).TotalSeconds));
                questionText.Text = remaining + "초 후 이 창이 자동으로 닫히고 게임이 실행됩니다.";
                countdown.Text = "";
                progressBar.Advance();
                return;
            }
            bool waiting = view.IsWaiting && !(session?.ExitRequested ?? false) && !(session?.StopRequested ?? false);
            int seconds = (int)Math.Max(0, Math.Ceiling((view.NextCheck - elapsed).TotalSeconds));
            countdown.Text = !waiting ? "" : seconds == 0 ? "상태를 다시 확인하는 중…"
                : seconds + (view.Download.Subscribed ? "초 후 다운로드 상태 재확인" : "초 후 구독 여부 재확인");
            progressBar.Advance();
        }

        private static void StyleButtons(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                if (child is Button button)
                {
                    button.FlatStyle = FlatStyle.Flat;
                    button.BackColor = Color.FromArgb(55, 55, 55);
                    button.ForeColor = Color.FromArgb(242, 242, 242);
                    button.FlatAppearance.BorderColor = Color.FromArgb(120, 120, 120);
                    button.FlatAppearance.MouseOverBackColor = Color.FromArgb(72, 72, 72);
                }
                StyleButtons(child);
            }
        }

        protected override void OnHandleCreated(EventArgs args)
        {
            base.OnHandleCreated(args);
            // 지원되는 Windows에서는 제목 표시줄도 어둡게 한다. 구형 OS는 기본 제목줄을 유지한다.
            // Use a dark native caption where supported; retain the system caption on older Windows.
            try { int enabled = 1; if (DwmSetWindowAttribute(Handle, 20, ref enabled, 4) != 0) DwmSetWindowAttribute(Handle, 19, ref enabled, 4); }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        internal void FinishAndClose() { allowClose = true; Close(); }
        protected override void OnFormClosing(FormClosingEventArgs args)
        {
            if (session != null && !allowClose) { session.RequestExit(); args.Cancel = true; }
            base.OnFormClosing(args);
        }

        private Label Label(string text, bool bold, int bottom) => new Label
        { Text = text, AutoSize = true, Dock = DockStyle.Fill, Font = bold ? headingFont : bodyFont, Margin = new Padding(0, 0, 0, bottom), UseMnemonic = false };

        private static TableLayoutPanel Stack()
        {
            var panel = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill, ColumnCount = 1, Margin = Padding.Empty };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return panel;
        }
        private static Button Button(string text) => new Button
        { Text = text, AutoSize = true, MinimumSize = new Size(112, 36), Margin = new Padding(8, 0, 0, 0) };

        private int Dpi(int value) => (int)Math.Round(value * DeviceDpi / 96.0);

        // 승인된 96-DPI 시안의 행 간격을 유지하되 줄바꿈이 늘면 해당 영역을 확장한다.
        // 패널 안에서만 위치를 보정하므로 다른 행을 이동하거나 레이아웃을 동결하지 않는다.
        //
        // Preserve approved 96-DPI row spacing, growing for wrapped text. Offsets stay
        // inside each section; unrelated rows remain stable and layout stays responsive.
        private sealed class TextPairPanel : Panel
        {
            private readonly Label title, text;
            private readonly int textTop, textHeight, measuredTextHeight, blockHeight;

            internal TextPairPanel(Label title, Label text, int textTop, int textHeight, int measuredTextHeight, int blockHeight)
            {
                this.title = title;
                this.text = text;
                this.textTop = textTop;
                this.textHeight = textHeight;
                this.measuredTextHeight = measuredTextHeight;
                this.blockHeight = blockHeight;
                AutoSize = true;
                Dock = DockStyle.Fill;
                Margin = Padding.Empty;
                title.Dock = text.Dock = DockStyle.None;
                title.AutoSize = text.AutoSize = false;
                Controls.Add(title);
                Controls.Add(text);
            }

            private int Dpi(int value) => (int)Math.Round(value * DeviceDpi / 96.0);
            private int Extra(Label label, int width, int baseline)
                => Math.Max(0, label.GetPreferredSize(new Size(Math.Max(1, width), 0)).Height - Dpi(baseline));

            public override Size GetPreferredSize(Size proposedSize)
            {
                if (title == null || text == null) return base.GetPreferredSize(proposedSize);
                // 최소 폭 측정에서는 자연스러운 글자 폭을 반환하고, 실제 폭 측정에서 줄바꿈한다.
                //
                // TableLayoutPanel first asks for a minimum-width measurement (1 px).
                // Report natural text width then; wrapping belongs to its actual-width pass.
                int width = proposedSize.Width > 1 ? proposedSize.Width :
                    Math.Max(title.GetPreferredSize(Size.Empty).Width, text.GetPreferredSize(Size.Empty).Width);
                return new Size(width, Dpi(blockHeight) + Extra(title, width, 25) + Extra(text, width, measuredTextHeight));
            }

            protected override void OnLayout(LayoutEventArgs args)
            {
                base.OnLayout(args);
                if (title == null || text == null) return;
                int width = ClientSize.Width;
                int titleExtra = Extra(title, width, 25);
                title.SetBounds(0, 0, width, Dpi(25) + titleExtra);
                text.SetBounds(0, Dpi(textTop) + titleExtra, width, Dpi(textHeight) + Extra(text, width, measuredTextHeight));
            }
        }

        private static void OpenWorkshop(string url)
        {
            // 브라우저 설치 경로를 추측하지 않고 Windows의 기본 HTTPS 연결을 사용한다.
            // Let Windows dispatch HTTPS to the user's default browser.
            using (var process = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })) { }
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) { bodyFont?.Dispose(); headingFont?.Dispose(); detailFont?.Dispose(); copyFont?.Dispose(); }
        }
    }
}
