using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DeskStudy
{
    // A keyboard-accessible choice button. Its schematic mirrors the real control structure.
    public sealed class NotebookLayoutPreview : Button
    {
        public string LayoutId { get; private set; }
        public string Caption { get; private set; }
        public string Description { get; private set; }
        private bool selected;
        public bool SelectedLayout { get { return selected; } set { selected = value; AccessibleDescription = Description + (value ? Lang.T("，当前已选中") : Lang.T("，点击选择")); Invalidate(); } }
        public NotebookLayoutPreview(string id, string caption, string description)
        {
            LayoutId = id; Caption = caption; Description = description;
            Name = "notebook-layout-" + id.ToLowerInvariant(); Text = caption; AccessibleName = caption;
            AutoSize = false; Size = new Size(192, 238); Margin = new Padding(0, 0, 12, 8);
            FlatStyle = FlatStyle.Flat; Cursor = Cursors.Hand; SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            float d = g.DpiX / 96F; int p = (int)(12 * d);
            g.Clear(selected ? Color.FromArgb(243, 247, 255) : Color.White);
            using (var pen = new Pen(selected ? Ui.Accent : Ui.Border, selected ? 2 : 1)) g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
            using (var bold = new Font(Font, FontStyle.Bold)) TextRenderer.DrawText(g, Caption, bold, new Rectangle(p, p, Width - p * 2, (int)(25 * d)), Ui.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, selected ? Lang.T("✓ 已选中") : LayoutId == "Card" ? Lang.T("默认推荐") : Lang.T("点击使用"), Font, new Rectangle(p, Height - (int)(30 * d), Width - p * 2, (int)(22 * d)), selected ? Ui.Accent : Ui.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, Description, Font, new Rectangle(p, (int)(155 * d), Width - p * 2, (int)(42 * d)), Ui.Muted, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            var state = g.Save(); g.TranslateTransform(p, 43 * d); g.ScaleTransform((Width - p * 2) / 168F, 102 * d / 102F);
            if (LayoutId == "Clean" || LayoutId == "Journal") { DrawReferenceSchematic(g, LayoutId == "Journal"); g.Restore(state); DrawFocus(g); return; }
            Color paper = LayoutId == "Paper" ? Color.FromArgb(251, 245, 232) : LayoutId == "Card" ? Color.FromArgb(244, 246, 243) : Ui.Background;
            using (var bg = new SolidBrush(paper)) g.FillRectangle(bg, 0, 0, 168, 102);
            using (var line = new Pen(Color.FromArgb(198, 208, 211)))
            using (var ink = new Pen(Color.FromArgb(111, 133, 137), 2))
            using (var accent = new SolidBrush(Color.FromArgb(85, 134, 116)))
            {
                g.DrawRectangle(line, 0, 0, 167, 101); g.FillRectangle(accent, 0, 0, 168, 2);
                g.DrawLine(ink, 8, 9, 30, 9); g.DrawLine(line, 134, 9, 159, 9);
                int x = LayoutId == "Paper" ? 27 : 8;
                if (LayoutId == "Original")
                {
                    g.DrawRectangle(line, 8, 17, 150, 10); g.DrawLine(ink, 13, 22, 72, 22);
                    g.DrawRectangle(line, 8, 31, 150, 11); g.DrawLine(ink, 12, 36, 66, 36);
                    g.DrawRectangle(line, 8, 46, 150, 20); g.DrawLine(line, 13, 51, 121, 51); g.DrawLine(line, 13, 57, 97, 57);
                    for (int y = 71; y < 96; y += 17) { g.DrawRectangle(line, 10, y, 5, 5); g.DrawLine(ink, 22, y + 3, 108, y + 3); for (int a = 22; a < 78; a += 19) g.DrawRectangle(line, a, y + 8, 15, 5); }
                }
                else
                {
                    if (LayoutId == "Paper") { g.DrawLine(line, 21, 17, 21, 98); for (int y = 21; y < 85; y += 15) g.DrawRectangle(line, 6, y, 9, 8); g.FillRectangle(accent, 6, 21, 9, 8); }
                    g.DrawLine(ink, x, 22, x + 67, 22); g.DrawLine(line, x, 31, x + 50, 31);
                    for (int y = 41; y < 81; y += 22)
                    {
                        if (LayoutId == "Card") { using (var white = new SolidBrush(Color.White)) g.FillRectangle(white, x, y - 4, 150, 20); g.DrawRectangle(line, x, y - 4, 150, 20); }
                        else g.DrawLine(line, x, y + 15, 156, y + 15);
                        g.DrawRectangle(line, x + 6, y, 6, 6); g.DrawLine(ink, x + 18, y + 3, 132, y + 3); g.DrawLine(line, x + 18, y + 10, 99, y + 10);
                    }
                    g.DrawLine(line, x, 87, x + 65, 87); g.DrawLine(ink, x, 96, x + 6, 96); g.DrawLine(line, 77, 96, 98, 96); g.DrawLine(ink, 142, 96, 157, 96);
                }
            }
            g.Restore(state);
            DrawFocus(g);
        }
        private void DrawFocus(Graphics g) { if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, new Rectangle(5, 5, Width - 10, Height - 10)); }
        // Mirrors the 清爽卡片 / 手账纸页 structure: dot header, large title, ruled task rows, footer pager.
        private static void DrawReferenceSchematic(Graphics g, bool journal)
        {
            Color back = journal ? ColorTranslator.FromHtml("#FAF8F1") : Color.White;
            Color rule = journal ? ColorTranslator.FromHtml("#E9E4D6") : ColorTranslator.FromHtml("#E8ECE8");
            Color soft = journal ? ColorTranslator.FromHtml("#EFECDF") : ColorTranslator.FromHtml("#F3F6F2");
            using (var bg = new SolidBrush(back)) g.FillRectangle(bg, 0, 0, 168, 102);
            using (var line = new Pen(rule))
            using (var border = new Pen(Color.FromArgb(214, 219, 216)))
            using (var ink = new Pen(ColorTranslator.FromHtml("#283732"), 2.4F))
            using (var sub = new Pen(ColorTranslator.FromHtml("#9AA39F"), 1.4F))
            using (var text = new Pen(ColorTranslator.FromHtml("#283732"), 1.4F))
            using (var dot = new SolidBrush(ColorTranslator.FromHtml("#527562")))
            using (var accent = new SolidBrush(ColorTranslator.FromHtml("#527562")))
            {
                g.DrawRectangle(border, 0, 0, 167, 101);
                g.FillEllipse(dot, 7, 6, 4, 4); g.DrawLine(sub, 14, 8, 38, 8); g.DrawLine(sub, 146, 8, 159, 8);
                int x = 9;
                if (journal)
                {
                    g.DrawLine(line, 0, 15, 168, 15);
                    using (var spine = new SolidBrush(soft)) g.FillRectangle(spine, 1, 16, 17, 73);
                    g.DrawLine(line, 18, 16, 18, 89);
                    using (var chip = new SolidBrush(back)) g.FillRectangle(chip, 4, 21, 11, 8);
                    g.DrawLine(sub, 7, 25, 12, 25); g.DrawLine(sub, 7, 36, 12, 36); g.DrawLine(sub, 7, 47, 12, 47);
                    x = 25;
                }
                g.DrawLine(ink, x, journal ? 24 : 20, x + 58, journal ? 24 : 20); g.DrawLine(sub, x, journal ? 31 : 27, x + 44, journal ? 31 : 27);
                for (int i = 0, y = journal ? 40 : 37; i < 3; i++, y += 14)
                {
                    if (i == 2) g.FillRectangle(accent, x, y - 3, 6, 6); else g.DrawRectangle(sub, x, y - 3, 6, 6);
                    g.DrawLine(i == 2 ? sub : text, x + 11, y, x + 70 - i * 9, y);
                    g.DrawLine(line, x, y + 7, 160, y + 7);
                }
                g.DrawLine(sub, x + 2, 81, x + 40, 81);
                g.DrawLine(line, 0, 89, 168, 89);
                g.DrawLine(sub, 8, 96, 36, 96); g.DrawLine(sub, 138, 96, 159, 96);
            }
        }
    }
}
