using System;
using System.Drawing;
using System.Windows.Forms;

namespace Arstraea.KoreanPatch.Fonts
{
    // 테마가 네이티브 ProgressBar 색을 덮지 않도록 게이지만 직접 그린다.
    // 총량 미확인 상태의 움직임은 진행률을 의미하지 않는다.
    //
    // Draw the gauge explicitly so OS themes cannot override its requested color.
    // Indeterminate motion is activity, not an invented completion percentage.
    internal sealed class PatchProgressBar : Control
    {
        private int position;
        internal int Percent { get; set; }
        internal bool Indeterminate { get; set; }
        internal PatchProgressBar()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(65, 65, 65);
            ForeColor = Color.FromArgb(0x5C, 0xD1, 0xE5);
            AccessibleName = "패치 작업 진행";
            AccessibleRole = AccessibleRole.ProgressBar;
        }
        internal void Advance()
        {
            position = (position + 12) % Math.Max(1, Width + Width / 4);
            AccessibleDescription = Indeterminate ? "진행률 확인 중" : Percent + "%";
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs args)
        {
            base.OnPaint(args);
            using (var fill = new SolidBrush(ForeColor))
            {
                int length = Indeterminate ? Width / 4 : (int)(Width * Math.Max(0, Math.Min(100, Percent)) / 100.0);
                args.Graphics.FillRectangle(fill, Indeterminate ? position - length : 0, 0, length, Height);
            }
        }
    }
}
