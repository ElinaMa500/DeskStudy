using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace DeskStudy
{
    // Window mode, the global show/hide shortcut, and sending widgets behind other windows.
    public sealed partial class AppController
    {
        private HotkeyWindow hotkeyWindow;
        private bool hotkeyRegistered;
        public string HotkeyStatus { get; private set; }

        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
        [DllImport("user32.dll", EntryPoint = "GetWindowLong")] private static extern int GetWindowStyle(IntPtr hwnd, int index);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);

        private sealed class HotkeyWindow : NativeWindow
        {
            public Action Pressed;
            public HotkeyWindow() { CreateHandle(new CreateParams()); }
            protected override void WndProc(ref Message m)
            {
                if (m.Msg == 0x0312 && Pressed != null) Pressed();
                base.WndProc(ref m);
            }
        }

        // Bounds saved by a version with title bars are converted once, so the content keeps its size and place.
        private bool NormalizeWindowFrames()
        {
            if (!Data.Settings.FramedWindowBounds) return false;
            Data.Settings.FramedWindowBounds = false;
            if (Data.Settings.WidgetMode != "Desktop") return false;
            ShiftAllFrames(true); return true;
        }
        private void ShiftAllFrames(bool removeFrame)
        {
            Padding frame = WidgetForm.FrameInsets();
            foreach (WindowState window in Data.Windows.Values.Concat(Data.Settings.SavedLayout.Values))
                SettingsLogic.ShiftFrame(window, frame.Left, frame.Top, frame.Right, frame.Bottom, removeFrame);
        }
        public void SetWidgetMode(string mode)
        {
            if (!SettingsLogic.WidgetModes.Contains(mode)) throw new InvalidOperationException("窗口模式无效。");
            if (Data.Settings.WidgetMode == mode) return;
            foreach (var w in Widgets) w.Remember();
            Data.Settings.WidgetMode = mode;
            ShiftAllFrames(mode == "Desktop");
            foreach (var w in Widgets) w.ApplyWidgetMode();
            // Title bars take extra room, so neighbouring widgets may now touch.
            ArrangeWidgets();
            foreach (var w in Widgets) w.Remember();
            SettingsChanged();
        }

        public void SetWidgetCorners(string style)
        {
            if (!SettingsLogic.WidgetCornerStyles.Contains(style)) throw new InvalidOperationException("组件四角样式无效。");
            if (Data.Settings.WidgetCorners == style) return;
            Data.Settings.WidgetCorners = style;
            foreach (var w in Widgets) w.ApplyCorners();
            SettingsChanged();
        }

        // Only the real instance claims a system-wide shortcut; isolated test and preview runs never do.
        public void RegisterShowHotkey()
        {
            if (!UsesSystemStartup) { HotkeyStatus = "隔离数据模式下不注册全局快捷键。"; return; }
            if (hotkeyWindow == null) hotkeyWindow = new HotkeyWindow { Pressed = ToggleWidgets };
            if (hotkeyRegistered) { UnregisterHotKey(hotkeyWindow.Handle, 1); hotkeyRegistered = false; }
            HotkeySpec spec;
            if (!HotkeySpec.TryParse(Data.Settings.ShowHotkey, out spec)) { HotkeyStatus = "快捷键已停用。"; return; }
            hotkeyRegistered = RegisterHotKey(hotkeyWindow.Handle, 1, (uint)spec.Modifiers | 0x4000, (uint)spec.VirtualKey);
            HotkeyStatus = hotkeyRegistered ? "快捷键已生效。" : "这个快捷键已被其他程序占用，请换一个。";
        }
        public void SetShowHotkey(string text)
        {
            text = (text ?? "").Trim();
            HotkeySpec spec;
            if (text != "" && !HotkeySpec.TryParse(text, out spec)) throw new InvalidOperationException("快捷键需要包含 Ctrl、Alt 或 Win，并以字母、数字或 F1–F12 结尾。");
            Data.Settings.ShowHotkey = text;
            RegisterShowHotkey(); SettingsChanged();
        }
        private void ReleaseShowHotkey()
        {
            if (hotkeyWindow == null) return;
            if (hotkeyRegistered) UnregisterHotKey(hotkeyWindow.Handle, 1);
            hotkeyRegistered = false; hotkeyWindow.DestroyHandle(); hotkeyWindow = null;
        }

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hwnd);
        private IntPtr foregroundAtStart = GetForegroundWindow();
        // Started at sign-in: widgets go behind everything and the window the user was in keeps the keyboard.
        // Started by hand: widgets stay in front so the launch is visible; they sink once another program is used.
        private void FinishStartup(bool quiet)
        {
            if (UsesSystemStartup) { try { StartupRegistration.Refresh(); } catch (Exception) { } }
            RegisterShowHotkey();
            if (Data.Settings.WidgetMode != "Desktop") return;
            if (!quiet) { if (UsesSystemStartup) RaiseWidgets(); return; }
            SinkAll();
            if (foregroundAtStart != IntPtr.Zero && IsWindow(foregroundAtStart) && GetForegroundWindow() != foregroundAtStart) SetForegroundWindow(foregroundAtStart);
        }
        public void SinkAll() { foreach (var w in Widgets) w.SinkToBottom(); }
        // Brings forward the widgets that are showing; widgets the user hid stay hidden. With none showing, shows them all.
        public void RaiseWidgets()
        {
            var showing = Widgets.Where(w => w.Visible).ToList();
            if (showing.Count == 0) { ShowAll(); return; }
            foreach (var w in showing) w.Reveal();
        }
        // The shortcut brings every widget forward; pressed again while they are in front, it sends them back.
        public void ToggleWidgets()
        {
            bool inFront = Data.Settings.WidgetMode == "Desktop" && Form.ActiveForm is WidgetForm && Widgets.Any(w => w.Visible);
            if (!inFront) { RaiseWidgets(); return; }
            SinkAll(); ActivateNextWindow();
        }
        // Hands focus to the topmost ordinary window of another program.
        private static void ActivateNextWindow()
        {
            uint own = (uint)Process.GetCurrentProcess().Id; IntPtr next = IntPtr.Zero;
            EnumWindows(delegate(IntPtr hwnd, IntPtr lParam)
            {
                uint owner; GetWindowThreadProcessId(hwnd, out owner);
                int cloaked;
                if (owner == own || !IsWindowVisible(hwnd) || IsIconic(hwnd) || GetWindowTextLength(hwnd) == 0 || (GetWindowStyle(hwnd, -20) & 0x80) != 0) return true;
                if (DwmGetWindowAttribute(hwnd, 14, out cloaked, sizeof(int)) == 0 && cloaked != 0) return true;
                next = hwnd; return false;
            }, IntPtr.Zero);
            if (next != IntPtr.Zero) SetForegroundWindow(next);
        }
    }
}
