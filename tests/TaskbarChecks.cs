using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using DeskStudy;

// The one taskbar button of desktop mode: present by default, a click toggles the widgets, switchable in settings.
public static class TaskbarChecks
{
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLong")] static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] static extern IntPtr GetDesktopWindow();
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static void Pump(int ms) { var clock = Stopwatch.StartNew(); while (clock.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(10); } }

    public static void Run(AppController app, string path)
    {
        app.ShowAll(); Pump(200);
        var button = app.TaskbarButtonWindow;
        Assert(app.Data.Settings.ShowTaskbarIcon && app.Data.Settings.WidgetMode == "Desktop" && button != null && !button.IsDisposed, "desktop mode starts with the taskbar button switched on");
        int exStyle = GetWindowLong(button.Handle, -20);
        Assert(IsWindowVisible(button.Handle) && button.ShowInTaskbar && (exStyle & 0x80) == 0 && button.Text == "桌面课笺" && button.Icon != null, "it is a visible taskbar window named 桌面课笺 with the program icon (not a tool window)");
        Assert(!button.Bounds.IntersectsWith(SystemInformation.VirtualScreen) && button.Opacity == 0, "it takes no room on any screen");
        Assert(app.Widgets.All(w => !w.ShowInTaskbar), "the widgets themselves stay off the taskbar");

        // A click while another program is active: forward. A click while a widget is active: back.
        var ddl = app.Widgets.First(w => w.WidgetKey == "ddl");
        SendMessage(button.Handle, 0x0006, new IntPtr(1), GetDesktopWindow()); Pump(200);
        Assert(button.LastAction == "forward" && app.Widgets.All(w => w.Visible), "clicked while another program is in front: the widgets come forward");
        SendMessage(button.Handle, 0x0006, new IntPtr(1), ddl.Handle); Pump(200);
        Assert(button.LastAction == "back" && app.Widgets.All(w => w.Visible), "clicked again while a widget is in front: the widgets go back (and stay shown)");
        SendMessage(button.Handle, 0x0112, new IntPtr(0xF020), IntPtr.Zero); Pump(200);
        Assert(button.LastAction == "forward" && button.WindowState == FormWindowState.Normal, "a click that would minimize the button brings the widgets forward instead");

        // All widgets hidden: a click shows them again.
        foreach (var w in app.Widgets) w.Hide();
        SendMessage(button.Handle, 0x0006, new IntPtr(1), GetDesktopWindow()); Pump(250);
        Assert(app.Widgets.All(w => w.Visible), "with every widget hidden, a click shows them all");

        // Settings: switched off and on, and only in desktop mode.
        app.SetTaskbarIcon(false); Pump(100);
        Assert(app.TaskbarButtonWindow == null && button.IsDisposed && !app.Data.Settings.ShowTaskbarIcon, "switched off in settings, the taskbar button goes away");
        app.SetTaskbarIcon(true); Pump(100);
        Assert(app.TaskbarButtonWindow != null && IsWindowVisible(app.TaskbarButtonWindow.Handle), "switched on, it comes back");
        app.SetWidgetMode("Standard"); Pump(300);
        Assert(app.TaskbarButtonWindow == null && app.Widgets.All(w => w.ShowInTaskbar), "standard windows have their own taskbar buttons, so the extra one goes away");
        app.SetWidgetMode("Desktop"); Pump(300);
        Assert(app.TaskbarButtonWindow != null && app.Widgets.All(w => !w.ShowInTaskbar), "back in desktop mode, the one taskbar button returns");
        app.Save(); Assert(app.Flush(), "taskbar setting saved");
    }

    public static void Read(AppController app)
    {
        Assert(app.Data.Settings.ShowTaskbarIcon && app.TaskbarButtonWindow != null, "after a restart the taskbar button is there again");
    }
}
