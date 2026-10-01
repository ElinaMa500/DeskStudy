using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DeskStudy
{
    // Folding a widget (1.5.1): the button next to ⚙, or a double click on the header, folds it down to its
    // header bar resting on the taskbar; unfolding puts it back exactly where it was. Folding is not saved.
    public partial class WidgetForm
    {
        private Button collapseButton;
        private ToolTip collapseTip;
        private bool collapsed;
        private Rectangle foldedFrom;
        private Size expandedMinimum, expandedMaximum;
        private Font iconFont;
        public bool Collapsed { get { return collapsed; } }

        private void InitializeCollapse()
        {
            collapseButton = Ui.Button("−", delegate { ToggleCollapse(); });
            collapseButton.Name = "collapse-widget"; collapseButton.AccessibleName = "折叠"; collapseButton.AutoSize = false; collapseButton.Width = 40;
            collapseTip = new ToolTip();
            Disposed += delegate { collapseTip.Dispose(); if (iconFont != null) iconFont.Dispose(); };
        }

        // Matches the ⚙ next to it in every header style.
        private void StyleCollapseButton()
        {
            Button gear = settingsButton;
            collapseButton.MinimumSize = Size.Empty; collapseButton.Padding = Padding.Empty;
            collapseButton.Size = gear.Size; collapseButton.Margin = gear.Margin;
            collapseButton.BackColor = gear.BackColor; collapseButton.ForeColor = gear.ForeColor;
            collapseButton.FlatAppearance.BorderSize = gear.FlatAppearance.BorderSize;
            collapseButton.FlatAppearance.BorderColor = gear.FlatAppearance.BorderColor;
            collapseButton.FlatAppearance.MouseOverBackColor = gear.FlatAppearance.MouseOverBackColor;
            {
                // Chevrons ⌄ / ⌃ from Windows' own icon font (Segoe Fluent Icons on 11, Segoe MDL2 Assets on 10).
                float size = (float)Math.Round(gear.Font.SizeInPoints * .8F, 2);
                if (iconFont == null || Math.Abs(iconFont.SizeInPoints - size) > .01F)
                {
                    Font old = iconFont;
                    string family = FontFamily.Families.Any(f => f.Name == "Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
                    iconFont = new Font(family, size); if (old != null) old.Dispose();
                }
                collapseButton.Font = iconFont;
                collapseButton.Text = collapsed ? "" : "";
            }
            collapseButton.AccessibleName = collapsed ? "展开" : "折叠";
            collapseTip.SetToolTip(collapseButton, collapsed ? "展开，回到原来的位置（双击顶栏也可以）" : "折叠到屏幕底部（双击顶栏也可以）");
            UpdateHeaderActionsWidth();
        }

        public void ToggleCollapse() { if (collapsed) Unfold(true); else Fold(); }

        // The minimum size layouts ask for applies to the unfolded widget.
        protected Size ExpandedMinimumSize { get { return collapsed ? expandedMinimum : MinimumSize; } }
        protected void SetExpandedMinimumSize(Size size)
        {
            if (collapsed) expandedMinimum = size;
            else if (MinimumSize != size) MinimumSize = size;
        }

        private int FoldedHeight { get { return header.Bottom + (Height - ClientSize.Height); } }

        // Down to the header bar, resting on the taskbar (or the bottom of the screen).
        public void Fold()
        {
            if (collapsed || IsDisposed || WindowState != FormWindowState.Normal) return;
            foldedFrom = Bounds; expandedMinimum = MinimumSize; expandedMaximum = MaximumSize;
            collapsed = true;
            Body.Visible = false;
            int height = FoldedHeight;
            // Fixed size while folded: no resizing from the edges.
            MinimumSize = new Size(Width, height); MaximumSize = new Size(Width, height);
            StyleCollapseButton();
            if (App != null) App.SettleWidget(this, 0, true);
            if (App != null) App.NotifyWindowStateChanged();
        }

        // Back to the place and size it had before folding; if another widget has moved there meanwhile, the usual no-overlap rule applies.
        public void Unfold(bool animate)
        {
            if (!collapsed || IsDisposed) return;
            collapsed = false;
            MaximumSize = expandedMaximum; MinimumSize = expandedMinimum;
            Body.Visible = true;
            StyleCollapseButton();
            Padding insets = VisualInsets();
            Rectangle target = Rectangle.FromLTRB(foldedFrom.Left + insets.Left, foldedFrom.Top + insets.Top, foldedFrom.Right - insets.Right, foldedFrom.Bottom - insets.Bottom);
            if (App != null) target = App.ResolveFor(this, target);
            SlideTo(target, animate);
            if (App != null) App.NotifyWindowStateChanged();
        }
        // Used before the widget is placed from saved or typed coordinates.
        private void UnfoldInPlace()
        {
            if (!collapsed) return;
            collapsed = false;
            MaximumSize = expandedMaximum; MinimumSize = expandedMinimum;
            Body.Visible = true;
            StyleCollapseButton();
        }

        // The header can change height with the font size; the folded bar follows it and stays on the bottom edge.
        private void FitCollapsed()
        {
            if (!collapsed) return;
            int height = FoldedHeight;
            if (Height == height) return;
            int bottom = Bottom;
            MinimumSize = new Size(Width, height); MaximumSize = new Size(Width, height);
            Bounds = new Rectangle(Left, bottom - height, Width, height);
        }
    }
}
