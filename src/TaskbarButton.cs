using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DeskStudy
{
    // Desktop widgets stay off the taskbar, so the program keeps one taskbar button of its own:
    // an invisible window whose only job is to be clicked. Clicked while a widget is in front,
    // the widgets go back behind other windows; otherwise they come forward.
    internal sealed class TaskbarButton : Form
    {
        private readonly AppController app;
        // What the last click did: "forward" or "back" (read by the tests).
        public string LastAction { get; private set; }

        public TaskbarButton(AppController app)
        {
            this.app = app;
            Text = Lang.T("桌面课笺"); Icon = app.AppIcon; Name = "taskbar-button";
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = true; StartPosition = FormStartPosition.Manual;
            // Far off screen and fully transparent: it exists only as the taskbar button (and in Alt+Tab).
            Bounds = new Rectangle(-20000, -20000, 1, 1); Opacity = 0;
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

        protected override void WndProc(ref Message m)
        {
            const int WM_ACTIVATE = 0x0006, WM_SYSCOMMAND = 0x0112, SC_MINIMIZE = 0xF020;
            // A click on the button of the active window would minimize it; this window is only active
            // when the widgets are not, so bring them forward instead.
            if (m.Msg == WM_SYSCOMMAND && (unchecked((int)m.WParam.ToInt64()) & 0xFFF0) == SC_MINIMIZE) { Act(false); return; }
            base.WndProc(ref m);
            if (m.Msg == WM_ACTIVATE && (unchecked((int)m.WParam.ToInt64()) & 0xFFFF) != 0) ButtonActivated(m.LParam);
        }

        // previous: the window that was active before this one.
        private void ButtonActivated(IntPtr previous)
        {
            if (app.Widgets.Any(w => w.Visible && w.IsHandleCreated && w.Handle == previous)) { Act(true); return; }
            uint owner = 0;
            if (previous != IntPtr.Zero) GetWindowThreadProcessId(previous, out owner);
            bool ours = owner == (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            // Windows also hands activation here when one of our own windows closes (settings, a hidden widget).
            // That is no click, unless the pointer is on the taskbar.
            if (ours && !OnTaskbar(Cursor.Position)) return;
            Act(false);
        }

        private static bool OnTaskbar(Point point)
        {
            Screen screen = Screen.FromPoint(point);
            return screen.Bounds.Contains(point) && !screen.WorkingArea.Contains(point);
        }

        private void Act(bool sendBack)
        {
            LastAction = sendBack ? "back" : "forward";
            BeginInvoke(new Action(delegate
            {
                if (IsDisposed) return;
                if (WindowState != FormWindowState.Normal) WindowState = FormWindowState.Normal;
                if (sendBack) app.SendWidgetsBack(); else app.RaiseWidgets();
            }));
        }
    }

    public sealed partial class AppController
    {
        private TaskbarButton taskbarButton;
        internal TaskbarButton TaskbarButtonWindow { get { return taskbarButton; } }

        // One taskbar button in desktop mode (standard windows have their own buttons).
        public void UpdateTaskbarButton()
        {
            bool wanted = !Exiting && Data.Settings.ShowTaskbarIcon && Data.Settings.WidgetMode == "Desktop";
            if (!wanted) { if (taskbarButton != null) { taskbarButton.Dispose(); taskbarButton = null; } return; }
            if (taskbarButton == null || taskbarButton.IsDisposed) { taskbarButton = new TaskbarButton(this); taskbarButton.Show(); }
        }
        public void SetTaskbarIcon(bool on)
        {
            if (Data.Settings.ShowTaskbarIcon == on) return;
            Data.Settings.ShowTaskbarIcon = on; UpdateTaskbarButton(); SettingsChanged();
        }
        public void SendWidgetsBack() { SinkAll(); ActivateNextWindow(); }
    }
}
