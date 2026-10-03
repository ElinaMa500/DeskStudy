using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DeskStudy
{
    // Desktop widget mode: no title bar, off the taskbar and Alt+Tab, resizable from the edges,
    // and resting behind other windows until clicked.
    public partial class WidgetForm
    {
        private bool desktopMode;
        private Button closeButton;
        private Timer hoverTimer;
        private bool closeShown;
        private Color closeRest, closeHot;
        public bool DesktopMode { get { return desktopMode; } }

        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] private static extern bool AdjustWindowRectEx(ref RECT rect, int style, bool menu, int exStyle);
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
        private const uint NoSize = 0x0001, NoMove = 0x0002, NoZOrder = 0x0004, NoActivate = 0x0010, FrameChanged = 0x0020;
        private static readonly IntPtr HwndBottom = new IntPtr(1);

        // How much a standard window's frame adds around its content, used to keep the content area when switching modes.
        public static Padding FrameInsets()
        {
            var rect = new RECT();
            AdjustWindowRectEx(ref rect, 0x00CF0000, false, 0);
            return new Padding(-rect.Left, -rect.Top, rect.Right, rect.Bottom);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                if (desktopMode) { cp.ExStyle |= 0x00000080; cp.ExStyle &= ~0x00040000; }
                return cp;
            }
        }
        // Shown without taking focus, so a widget appearing at startup never interrupts typing elsewhere.
        protected override bool ShowWithoutActivation { get { return desktopMode && !TopMost; } }

        private void InitializeFrame()
        {
            closeButton = Ui.Button("✕", delegate { Hide(); });
            closeButton.Name = "close-widget"; closeButton.AccessibleName = Lang.T("隐藏这个组件"); closeButton.AutoSize = false; closeButton.Visible = false;
            closeButton.FlatStyle = FlatStyle.Flat; closeButton.FlatAppearance.BorderSize = 0; closeButton.Margin = Padding.Empty; closeButton.TabStop = false;
            // A small ✕ tucked into the top-right corner, apart from the other buttons so it is not hit by mistake.
            header.Controls.Add(closeButton);
            header.Resize += delegate { PlaceCloseButton(); };
            hoverTimer = new Timer { Interval = 150 };
            hoverTimer.Tick += delegate { UpdateCloseHover(); };
        }
        // The ✕ keeps its place in the header and only becomes visible while the pointer is over the widget.
        private void UpdateCloseHover()
        {
            if (!closeShown || IsDisposed) return;
            Color wanted = Visible && Bounds.Contains(Cursor.Position) ? closeHot : closeRest;
            if (closeButton.ForeColor != wanted) closeButton.ForeColor = wanted;
        }
        // The system drop shadow only shows while the widget is being dragged or resized (1.5.1).
        private bool shadowOn;
        private void SetShadow(bool on)
        {
            if (!desktopMode || !IsHandleCreated || IsDisposed) return;
            shadowOn = on;
            // Windows 11 draws the shadow together with its rounded corners, so the system rounds only while dragging.
            try { int corner = on ? 2 : 1; DwmSetWindowAttribute(Handle, 33, ref corner, sizeof(int)); }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
            ApplyCorners();
        }
        // At rest, "圆角" is drawn by clipping the window to a rounded shape, which casts no shadow.
        // While dragging the clip is lifted so the system's own rounded corners and shadow show.
        public void ApplyCorners()
        {
            if (!IsHandleCreated || IsDisposed) return;
            bool clip = desktopMode && !shadowOn && WindowState == FormWindowState.Normal && App != null && App.Data.Settings.WidgetCorners != "Square";
            if (!clip) { if (cornerClipped) { SetWindowRgn(Handle, IntPtr.Zero, true); cornerClipped = false; } return; }
            float dpi; using (Graphics g = CreateGraphics()) dpi = g.DpiX / 96F;
            IntPtr region = RoundedRegion(Width, Height, (int)Math.Round(8 * dpi));
            // The window owns the region after this call.
            if (SetWindowRgn(Handle, region, true) == 0) DeleteObject(region); else cornerClipped = true;
        }
        private bool cornerClipped;
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (cornerClipped || (desktopMode && !shadowOn)) ApplyCorners();
        }
        // GDI's CreateRoundRectRgn cuts the right and bottom corners differently from the left and top ones,
        // so the rounded outline is built row by row from the circle, identical at all four corners.
        internal static IntPtr RoundedRegion(int width, int height, int radius)
        {
            radius = Math.Max(0, Math.Min(radius, Math.Min(width, height) / 2));
            IntPtr region = CreateRectRgn(0, radius, width, height - radius);
            for (int row = 0; row < radius; row++)
            {
                double dy = radius - row - 0.5;
                int inset = (int)Math.Round(radius - Math.Sqrt(radius * radius - dy * dy));
                foreach (int y in new[] { row, height - 1 - row })
                {
                    IntPtr line = CreateRectRgn(inset, y, width - inset, y + 1);
                    CombineRgn(region, region, line, 2);
                    DeleteObject(line);
                }
            }
            return region;
        }
        [DllImport("gdi32.dll")] private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);
        [DllImport("gdi32.dll")] private static extern int CombineRgn(IntPtr destination, IntPtr first, IntPtr second, int mode);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr handle);
        [DllImport("user32.dll")] private static extern int SetWindowRgn(IntPtr hwnd, IntPtr region, bool redraw);
        private Font closeFont;
        // Just inside the top-right corner, outside the band used for resizing from the edges.
        private void PlaceCloseButton()
        {
            if (closeButton == null || !closeShown) return;
            float dpi; using (Graphics g = CreateGraphics()) dpi = g.DpiX / 96F;
            int inset = (int)Math.Round(7 * dpi);
            closeButton.Location = new Point(header.ClientSize.Width - closeButton.Width - inset, inset);
            closeButton.BringToFront();
        }
        private void StyleCloseButton(bool show, Color back, Color muted, Color soft, float dpi)
        {
            closeShown = show && desktopMode;
            closeButton.Visible = closeShown; hoverTimer.Enabled = closeShown;
            if (!closeShown) return;
            closeRest = back; closeHot = muted;
            closeButton.BackColor = back; closeButton.FlatAppearance.BorderSize = 0; closeButton.FlatAppearance.BorderColor = back; closeButton.FlatAppearance.MouseOverBackColor = soft;
            float size = (float)Math.Round(7.5F * SettingsLogic.EffectiveAppearance(App.Data, WidgetKey).FontSize / 9F, 2);
            if (closeFont == null || Math.Abs(closeFont.SizeInPoints - size) > .01F) { Font old = closeFont; closeFont = new Font("Microsoft YaHei UI", size); if (old != null) old.Dispose(); }
            closeButton.Font = closeFont; closeButton.Padding = Padding.Empty; closeButton.MinimumSize = Size.Empty;
            int side = (int)Math.Round(18 * dpi);
            closeButton.Size = new Size(side, side);
            // Keep the header's own buttons clear of the corner.
            header.Padding = new Padding(header.Padding.Left, header.Padding.Top, header.Padding.Right + side + (int)Math.Round(6 * dpi), header.Padding.Bottom);
            PlaceCloseButton();
            UpdateCloseHover();
        }

        // Applies the window mode chosen in settings. The window is rebuilt, so callers restore bounds afterwards.
        public void ApplyWidgetMode()
        {
            bool wanted = App.Data.Settings.WidgetMode == "Desktop";
            if (wanted == desktopMode) return;
            bool remembered = ready; ready = false;
            try
            {
                desktopMode = wanted;
                ShowInTaskbar = !wanted; MaximizeBox = !wanted; MinimizeBox = !wanted;
                if (IsHandleCreated) RecreateHandle();
            }
            finally { ready = remembered; }
            RestoreWindow(); ApplyAppearance();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            cornerClipped = false; shadowOn = false;
            if (!desktopMode) return;
            SetShadow(false);
            // Re-evaluate the frame so the title bar is removed immediately.
            SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, NoSize | NoMove | NoZOrder | NoActivate | FrameChanged);
        }

        // Hit-test code for a screen point: a resize edge or corner, otherwise client.
        public int EdgeHit(Point screen)
        {
            if (!desktopMode || IsDisposed || Disposing || !IsHandleCreated || PositionLocked || WindowState != FormWindowState.Normal) return 1;
            float dpi; using (Graphics g = CreateGraphics()) dpi = g.DpiX / 96F;
            int grip = (int)Math.Round(6 * dpi), corner = (int)Math.Round(14 * dpi);
            Rectangle b = Bounds;
            if (!b.Contains(screen)) return 1;
            bool left = screen.X < b.Left + grip, right = screen.X >= b.Right - grip, top = screen.Y < b.Top + grip, bottom = screen.Y >= b.Bottom - grip;
            bool nearLeft = screen.X < b.Left + corner, nearRight = screen.X >= b.Right - corner, nearTop = screen.Y < b.Top + corner, nearBottom = screen.Y >= b.Bottom - corner;
            int hit = left ? 10 : right ? 11 : top ? 12 : bottom ? 15 : 1;
            if ((top && nearLeft) || (left && nearTop)) hit = 13;
            else if ((top && nearRight) || (right && nearTop)) hit = 14;
            else if ((bottom && nearLeft) || (left && nearBottom)) hit = 16;
            else if ((bottom && nearRight) || (right && nearBottom)) hit = 17;
            // A folded widget is a fixed-size bar.
            if (collapsed) return 1;
            return hit;
        }
        internal static Point ScreenPoint(IntPtr lParam)
        {
            int value = unchecked((int)lParam.ToInt64());
            return new Point((short)(value & 0xFFFF), (short)((value >> 16) & 0xFFFF));
        }
        private bool FrameWndProc(ref Message m)
        {
            if (!desktopMode) return false;
            // The whole window is client area: no title bar. The thick frame style stays so Windows keeps the shadow and rounded corners.
            if (m.Msg == 0x0083) { m.Result = IntPtr.Zero; return true; }
            if (m.Msg == 0x0084) { m.Result = new IntPtr(EdgeHit(ScreenPoint(m.LParam))); return true; }
            // With a rounded window region Windows stops composing the frame and would paint the old-style
            // border over the edges of the widget (most visibly when it becomes active). There is no frame to paint.
            if (cornerClipped && m.Msg == 0x0085) { m.Result = IntPtr.Zero; return true; }
            if (cornerClipped && m.Msg == 0x0086) { m.Result = new IntPtr(1); return true; }

            return false;
        }
        private void AfterFrameWndProc(ref Message m)
        {
            // Another application came to the front: go back behind everything.
            if (desktopMode && m.Msg == 0x001C && m.WParam == IntPtr.Zero && IsHandleCreated && !IsDisposed)
                BeginInvoke(new Action(SinkToBottom));
        }
        public void SinkToBottom()
        {
            if (!desktopMode || TopMost || IsDisposed || !IsHandleCreated || !Visible) return;
            SetWindowPos(Handle, HwndBottom, 0, 0, 0, 0, NoSize | NoMove | NoActivate);
        }
    }

    // A panel that lets the owning widget's resize edges show through where it touches the window border.
    // Holds a control's drawing while it is rebuilt, then repaints it and its children in one pass,
    // instead of letting each child window repaint on its own as it changes.
    // Nested pauses on the same control count; drawing resumes when the outermost one ends.
    internal sealed class RedrawPause : IDisposable
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool RedrawWindow(IntPtr hwnd, IntPtr rect, IntPtr region, uint flags);
        private static readonly System.Collections.Generic.Dictionary<Control, int> depth = new System.Collections.Generic.Dictionary<Control, int>();
        private readonly Control control; private readonly bool paused;
        public RedrawPause(Control control)
        {
            this.control = control;
            paused = control != null && control.IsHandleCreated && control.Visible && !control.IsDisposed;
            if (!paused) return;
            int level; depth.TryGetValue(control, out level); depth[control] = level + 1;
            if (level == 0) SendMessage(control.Handle, 0x000B, IntPtr.Zero, IntPtr.Zero);
        }
        public void Dispose()
        {
            if (!paused) return;
            int level; depth.TryGetValue(control, out level);
            if (level > 1) { depth[control] = level - 1; return; }
            depth.Remove(control);
            if (control.IsDisposed || !control.IsHandleCreated) return;
            SendMessage(control.Handle, 0x000B, new IntPtr(1), IntPtr.Zero);
            // RDW_ERASE | RDW_FRAME | RDW_INVALIDATE | RDW_ALLCHILDREN | RDW_UPDATENOW
            RedrawWindow(control.Handle, IntPtr.Zero, IntPtr.Zero, 0x4 | 0x400 | 0x1 | 0x80 | 0x100);
        }
    }

    internal sealed class EdgePanel : Panel
    {
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != 0x0084) return;
            var form = FindForm() as WidgetForm;
            if (form != null && form.DesktopMode && form.EdgeHit(WidgetForm.ScreenPoint(m.LParam)) != 1) m.Result = new IntPtr(-1);
        }
    }
}
