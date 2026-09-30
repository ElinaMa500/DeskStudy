using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using DeskStudy;

// Real widget windows: snapping while dragging (WM_MOVING / WM_SIZING) and settling after a drag (WM_EXITSIZEMOVE).
public static class PlacementChecks
{
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static void Pump(int ms) { var clock = Stopwatch.StartNew(); while (clock.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(10); } }
    const int G = PlacementLogic.Gap;

    static Rectangle V(WidgetForm w) { return w.VisualBounds; }
    // Places a widget so its visible area is the given rectangle, the way the end of a drag would leave it.
    static void Put(WidgetForm w, Rectangle visual)
    {
        Rectangle b = w.Bounds, v = w.VisualBounds;
        w.Bounds = Rectangle.FromLTRB(visual.Left - (v.Left - b.Left), visual.Top - (v.Top - b.Top), visual.Right + (b.Right - v.Right), visual.Bottom + (b.Bottom - v.Bottom));
    }
    static void Drop(WidgetForm w, Rectangle visual) { Put(w, visual); Pump(40); SendMessage(w.Handle, 0x0231, IntPtr.Zero, IntPtr.Zero); SendMessage(w.Handle, 0x0232, IntPtr.Zero, IntPtr.Zero); Pump(320); }
    // Sends a move or resize step with a proposed window rectangle and returns what the widget answered.
    static Rectangle Step(WidgetForm w, int msg, int edge, Rectangle visual)
    {
        Rectangle b = w.Bounds, v = w.VisualBounds;
        Padding p = new Padding(v.Left - b.Left, v.Top - b.Top, b.Right - v.Right, b.Bottom - v.Bottom);
        var rect = new RECT { Left = visual.Left - p.Left, Top = visual.Top - p.Top, Right = visual.Right + p.Right, Bottom = visual.Bottom + p.Bottom };
        IntPtr memory = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(RECT)));
        try
        {
            Marshal.StructureToPtr(rect, memory, false);
            SendMessage(w.Handle, msg, new IntPtr(edge), memory);
            rect = (RECT)Marshal.PtrToStructure(memory, typeof(RECT));
        }
        finally { Marshal.FreeHGlobal(memory); }
        return Rectangle.FromLTRB(rect.Left + p.Left, rect.Top + p.Top, rect.Right - p.Right, rect.Bottom - p.Bottom);
    }
    static bool Apart(IEnumerable<WidgetForm> widgets)
    {
        var list = widgets.Where(w => w.Visible).ToList();
        for (int i = 0; i < list.Count; i++) for (int j = i + 1; j < list.Count; j++) if (V(list[i]).IntersectsWith(V(list[j]))) return false;
        return true;
    }
    static bool Saved(AppController app, WidgetForm w) { var s = app.Data.Windows[w.WidgetKey]; return s.X == w.Left && s.Y == w.Top && s.Width == w.Width && s.Height == w.Height; }

    public static void Run(AppController app, string path, bool afterRestart)
    {
        var calendar = app.Widgets.First(w => w.WidgetKey == "calendar"); var todo = app.Widgets.First(w => w.WidgetKey == "todo"); var ddl = app.Widgets.First(w => w.WidgetKey == "ddl");
        Rectangle work = Screen.FromControl(todo).WorkingArea;
        if (afterRestart)
        {
            Assert(app.Widgets.All(w => work.Contains(V(w))) && Apart(app.Widgets), "saved overlapping and off-screen widgets are tidied at startup: " + string.Join(" ", app.Widgets.Select(w => V(w).ToString())));
            Assert(app.Widgets.All(w => Saved(app, w)), "the tidied positions are what gets saved");
            return;
        }
        app.ShowAll(); Pump(200);
        Assert(Apart(app.Widgets) && app.Widgets.All(w => work.Contains(V(w))), "a fresh start opens the widgets on screen without overlap");
        // Known starting layout: calendar top-left, Todo to its right, DDL below Todo. Notebooks are 440 × 540.
        const int W = 440, H = 540;
        Func<int, int, Rectangle> at = (x, y) => new Rectangle(work.Left + x, work.Top + y, W, H);
        Put(calendar, new Rectangle(work.Left, work.Top, 850, 650)); Put(todo, at(900, 0)); Put(ddl, at(900, 590)); Pump(150);
        Assert(V(todo).Size == new Size(W, H) && V(ddl) == at(900, 590), "starting layout placed: " + V(calendar) + " " + V(todo) + " " + V(ddl));

        // Bounce back from every edge; the size stays.
        Drop(todo, new Rectangle(work.Right - 200, work.Top + 20, W, H));
        Assert(V(todo) == new Rectangle(work.Right - W, work.Top + 20, W, H), "dropped past the right edge: slides back flush with it, same size");
        Assert(Saved(app, todo), "the bounced position is saved");
        Drop(todo, new Rectangle(work.Left + 1400, work.Bottom - 200, W, H));
        Assert(V(todo) == new Rectangle(work.Left + 1400, work.Bottom - H, W, H) && Apart(app.Widgets), "dropped into the taskbar: slides back above it");
        Drop(calendar, new Rectangle(work.Left - 150, work.Top, 850, 650));
        Assert(V(calendar) == new Rectangle(work.Left, work.Top, 850, 650), "dropped past the left edge: back to the left edge");
        Drop(ddl, new Rectangle(work.Left + 1450, work.Top - 80, W, H));
        Assert(V(ddl) == new Rectangle(work.Left + 1450, work.Top, W, H), "dropped past the top: back to the top");

        // Overlap: only the dropped widget moves, 8 px from the one it landed on.
        Put(todo, at(900, 0)); Put(ddl, at(900, 590)); Pump(100);
        Rectangle calendarBefore = V(calendar), todoBefore = V(todo);
        Drop(ddl, at(700, 100));
        Assert(V(calendar) == calendarBefore && V(todo) == todoBefore, "the widgets underneath stay where they are");
        Assert(Apart(app.Widgets) && work.Contains(V(ddl)), "the dropped widget is moved clear of the others: " + V(ddl));
        bool gapKept = app.Widgets.Where(w => w != ddl).All(w => !Rectangle.Inflate(V(ddl), G - 1, G - 1).IntersectsWith(V(w)));
        Assert(gapKept, "and keeps an 8 px gap from them");
        Drop(ddl, at(1300, 50));
        Assert(V(ddl) == at(900 + W + G, 50), "dropped over the right half of Todo: pushed right to 8 px beside it");

        // Snapping while dragging and resizing.
        Put(ddl, at(1450, 590)); Pump(100);
        Rectangle snapped = Step(ddl, 0x0216, 0, new Rectangle(work.Right - W - 6, work.Top + 590, W, H));
        Assert(snapped.Right == work.Right, "dragged within 6 px of the right edge: snaps to it");
        snapped = Step(ddl, 0x0216, 0, new Rectangle(work.Right - W - 15, work.Top + 590, W, H));
        Assert(snapped.Right == work.Right - 15, "15 px away: no snap");
        snapped = Step(ddl, 0x0216, 0, new Rectangle(V(todo).Right + 5, V(todo).Top + 3, W, H));
        Assert(snapped.Left == V(todo).Right + G && snapped.Top == V(todo).Top, "dragged next to Todo: snaps 8 px beside it with the tops lined up");
        Put(ddl, new Rectangle(V(todo).Left, V(todo).Bottom + 40, W, H)); Pump(80);
        snapped = Step(ddl, 0x0214, PlacementLogic.EdgeTop, Rectangle.FromLTRB(V(ddl).Left, V(todo).Bottom + 12, V(ddl).Right, V(ddl).Bottom));
        Assert(snapped.Top == V(todo).Bottom + G, "resizing the top edge up to Todo snaps 8 px below it");

        // Resizing into a neighbour stops the dragged edge; the widget does not jump.
        Rectangle grown = Rectangle.FromLTRB(V(ddl).Left, V(todo).Bottom - 60, V(ddl).Right, V(ddl).Bottom);
        Step(ddl, 0x0214, PlacementLogic.EdgeTop, grown); Put(ddl, grown); Pump(40);
        SendMessage(ddl.Handle, 0x0232, IntPtr.Zero, IntPtr.Zero); Pump(320);
        Assert(V(ddl).Top == V(todo).Bottom + G && V(ddl).Bottom == grown.Bottom, "top edge dragged into Todo: stops 8 px below it, bottom edge stays");
        Rectangle wide = Rectangle.FromLTRB(V(ddl).Left, V(ddl).Top, work.Right + 90, V(ddl).Bottom);
        Step(ddl, 0x0214, PlacementLogic.EdgeRight, wide); Put(ddl, wide); Pump(40);
        SendMessage(ddl.Handle, 0x0232, IntPtr.Zero, IntPtr.Zero); Pump(320);
        Assert(V(ddl).Right == work.Right && V(ddl).Left == wide.Left, "right edge dragged off screen: pulled back to the edge, left edge stays");

        // Locked widgets never move; the other one makes way.
        Put(todo, at(900, 0)); Put(ddl, at(1450, 590)); Pump(60);
        app.Data.Windows["todo"].PositionLocked = true; Rectangle lockedAt = V(todo);
        Drop(ddl, at(1000, 100));
        Assert(V(todo) == lockedAt && !V(ddl).IntersectsWith(lockedAt), "dropped onto a locked widget: the locked one stays, the dropped one moves");
        app.Data.Windows["todo"].PositionLocked = false;

        // Hidden widgets are not obstacles; one coming back into view moves out of the way.
        Put(ddl, at(1450, 590)); Pump(60);
        app.SetWidgetVisible("ddl", false); Pump(80);
        Drop(todo, at(1400, 560));
        Rectangle todoOverHidden = V(todo);
        Assert(todoOverHidden == at(1400, 560), "a hidden widget is not an obstacle: " + todoOverHidden);
        app.SetWidgetVisible("ddl", true); Pump(300);
        Assert(V(todo) == todoOverHidden && Apart(app.Widgets), "a widget shown again on top of another moves out of the way, the other stays: " + V(ddl));

        // Settings center: typed coordinates are corrected too.
        var state = app.Data.Windows["ddl"];
        app.UpdateWindow("ddl", new WindowState { X = work.Right + 300, Y = state.Y, Width = state.Width, Height = state.Height, Visible = true });
        Assert(work.Contains(V(ddl)) && Saved(app, ddl), "an X beyond the screen typed in settings is pulled back on screen");
        var calendarState = app.Data.Windows["calendar"];
        app.UpdateWindow("ddl", new WindowState { X = calendarState.X + 40, Y = calendarState.Y + 40, Width = state.Width, Height = state.Height, Visible = true });
        Assert(Apart(app.Widgets) && Saved(app, ddl) && V(calendar) == calendarBefore, "coordinates typed in settings that overlap another widget are moved clear, and settings show the result");
        app.ResetLayout(); Pump(200);
        Assert(Apart(app.Widgets) && app.Widgets.All(w => work.Contains(V(w))), "恢复默认布局 lays the three widgets out without overlap: " + string.Join(" ", app.Widgets.Select(w => V(w).ToString())));
        app.RescueWindows(); Pump(200);
        Assert(Apart(app.Widgets) && app.Widgets.All(w => work.Contains(V(w))), "找回当前屏幕 gathers them on screen without overlap");

        // Standard windows: the same rules apply to the visible frame, not the invisible resize border.
        app.SetWidgetMode("Standard"); Pump(250);
        Assert(Apart(app.Widgets), "switching to standard windows keeps the widgets apart");
        Padding border = new Padding(V(todo).Left - todo.Left, V(todo).Top - todo.Top, todo.Right - V(todo).Right, todo.Bottom - V(todo).Bottom);
        Console.WriteLine("standard window invisible border: " + border);
        Put(calendar, new Rectangle(work.Left, work.Top, 850, 650)); Put(todo, at(900, 0)); Put(ddl, at(1450, 590)); Pump(100);
        Drop(todo, at(800, 50));
        Assert(V(todo).Left == V(calendar).Right + G, "standard window dropped onto the calendar: moved to 8 px beside its visible edge (" + (V(todo).Left - V(calendar).Right) + " px)");
        Drop(todo, new Rectangle(work.Right - 100, work.Top + 50, W, H));
        Assert(V(todo).Right == work.Right, "standard window past the right edge: the visible edge ends flush with the screen");
        snapped = Step(todo, 0x0216, 0, new Rectangle(work.Left + 3, work.Bottom - H - 5, W, H));
        Assert(snapped.Left == work.Left && snapped.Bottom == work.Bottom, "standard windows snap to the screen edges too");
        app.SetWidgetMode("Desktop"); Pump(250);
        Assert(Apart(app.Widgets) && app.Widgets.All(w => work.Contains(V(w))), "back in desktop mode everything is still on screen and apart");

        // Animation: the drop glides and ends exactly at the target.
        Put(todo, new Rectangle(work.Left + 900, work.Top, 440, 520)); Pump(60);
        Put(todo, new Rectangle(work.Right - 150, work.Top, 440, 520)); Pump(30);
        SendMessage(todo.Handle, 0x0232, IntPtr.Zero, IntPtr.Zero);
        bool gliding = todo.Sliding; Pump(320);
        Assert(!todo.Sliding && V(todo).Right == work.Right, "the bounce ends at the edge (animated: " + gliding + ")");

        // Leave an overlapping, off-screen layout in the file for the restart check.
        app.Save(); Assert(app.Flush(), "placement changes saved");
        Put(todo, new Rectangle(V(calendar).Left + 50, V(calendar).Top + 50, 440, 520)); Put(ddl, new Rectangle(work.Right - 100, work.Top + 200, 440, 500)); Pump(100);
        Assert(V(todo).IntersectsWith(V(calendar)), "an overlapping, off-screen layout is left for the restart check");
        app.QueueSave(); Assert(app.Flush(), "and saved");
    }
}
