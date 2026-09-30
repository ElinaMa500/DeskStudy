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
            Assert(app.Widgets.All(w => PlacementLogic.Contain(V(w), work) == V(w)) && Apart(app.Widgets), "saved overlapping widgets are moved apart at startup: " + string.Join(" ", app.Widgets.Select(w => V(w).ToString())));
            Assert(V(ddl).Left == work.Right - 100, "a widget the user left far off screen stays there after a restart: " + V(ddl));
            Assert(app.Widgets.All(w => Saved(app, w)), "the tidied positions are what gets saved");
            return;
        }
        app.ShowAll(); Pump(200);
        Assert(Apart(app.Widgets) && app.Widgets.All(w => work.Contains(V(w))), "a fresh start opens the widgets on screen without overlap");
        // Known starting layout: calendar top-left, Todo to its right, DDL below Todo. Notebooks are 440 × 540.
        const int W = 440, H = 540, C = PlacementLogic.EdgeCatch;
        Func<int, int, Rectangle> at = (x, y) => new Rectangle(work.Left + x, work.Top + y, W, H);
        Put(calendar, new Rectangle(work.Left, work.Top, 850, 650)); Put(todo, at(900, 0)); Put(ddl, at(900, 590)); Pump(150);
        Assert(V(todo).Size == new Size(W, H) && V(ddl) == at(900, 590), "starting layout placed: " + V(calendar) + " " + V(todo) + " " + V(ddl));

        // While the mouse is held nothing is adjusted.
        Rectangle proposal = new Rectangle(work.Right - W - 6, work.Top + 20, W, H);
        Assert(Step(todo, 0x0216, 0, proposal) == proposal, "while dragging, a widget 6 px from the edge is not pulled to it");
        proposal = new Rectangle(work.Right - W + 200, work.Top + 20, W, H);
        Assert(Step(todo, 0x0216, 0, proposal) == proposal, "while dragging, a widget past the edge is not pushed back");
        proposal = Rectangle.FromLTRB(V(todo).Left, V(todo).Top, work.Right - 4, V(todo).Bottom);
        Assert(Step(todo, 0x0214, PlacementLogic.EdgeRight, proposal) == proposal, "while resizing, the edge is not pulled either");

        // Released slightly past an edge: back to it. Far past: left there. The top always comes back.
        Drop(todo, new Rectangle(work.Right - W + 40, work.Top + 20, W, H));
        Assert(V(todo) == new Rectangle(work.Right - W, work.Top + 20, W, H), "released 40 px past the right edge: slides back flush with it, same size");
        Assert(Saved(app, todo), "the bounced position is saved");
        Drop(todo, new Rectangle(work.Right - W + 200, work.Top + 20, W, H));
        Assert(V(todo) == new Rectangle(work.Right - W + 200, work.Top + 20, W, H) && Saved(app, todo), "released 200 px past the right edge: stays partly off screen");
        Drop(todo, new Rectangle(work.Left + 1400, work.Bottom - H + 40, W, H));
        Assert(V(todo) == new Rectangle(work.Left + 1400, work.Bottom - H, W, H), "released 40 px into the taskbar: slides back above it");
        Drop(todo, new Rectangle(work.Left + 1400, work.Bottom - H + 200, W, H));
        Assert(V(todo).Top == work.Bottom - H + 200, "released 200 px into the taskbar: stays there");
        Drop(calendar, new Rectangle(work.Left - 40, work.Top, 850, 650));
        Assert(V(calendar) == new Rectangle(work.Left, work.Top, 850, 650), "released 40 px past the left edge: back to the left edge");
        Drop(calendar, new Rectangle(work.Left - 300, work.Top, 850, 650));
        Assert(V(calendar).Left == work.Left - 300, "released 300 px past the left edge: stays");
        Drop(calendar, new Rectangle(work.Left - 300, work.Top - 200, 850, 650));
        Assert(V(calendar) == new Rectangle(work.Left - 300, work.Top, 850, 650), "released far above the screen: the top always comes back");
        Drop(calendar, new Rectangle(work.Left, work.Top, 850, 650));
        Drop(ddl, new Rectangle(work.Left + 1450, work.Top - 80, W, H));
        Assert(V(ddl) == new Rectangle(work.Left + 1450, work.Top, W, H), "released past the top: back to the top");

        // Overlap: only the dropped widget moves, 5 px from the one it landed on.
        Put(todo, at(900, 0)); Put(ddl, at(900, 590)); Pump(100);
        Rectangle calendarBefore = V(calendar), todoBefore = V(todo);
        Drop(ddl, at(700, 100));
        Assert(V(calendar) == calendarBefore && V(todo) == todoBefore, "the widgets underneath stay where they are");
        Assert(Apart(app.Widgets) && work.Contains(V(ddl)), "the dropped widget is moved clear of the others: " + V(ddl));
        bool gapKept = app.Widgets.Where(w => w != ddl).All(w => !Rectangle.Inflate(V(ddl), G - 1, G - 1).IntersectsWith(V(w)));
        Assert(gapKept, "and keeps a 5 px gap from them");
        Drop(ddl, at(1300, 50));
        Assert(V(ddl) == at(900 + W + G, 50), "dropped over the right half of Todo: pushed right to 5 px beside it");

        // Snapping on release.
        Put(ddl, at(1450, 590)); Pump(100);
        Drop(ddl, new Rectangle(work.Right - W - 6, work.Top + 590, W, H));
        Assert(V(ddl).Right == work.Right, "released 6 px from the right edge: snaps to it");
        Drop(ddl, new Rectangle(work.Right - W - 15, work.Top + 590, W, H));
        Assert(V(ddl).Right == work.Right - 15, "15 px away: no snap");
        Drop(ddl, new Rectangle(V(todo).Right + 9, V(todo).Top + 3, W, H));
        Assert(V(ddl).Left == V(todo).Right + G && V(ddl).Top == V(todo).Top, "released next to Todo: snaps 5 px beside it with the tops lined up");
        Put(ddl, new Rectangle(V(todo).Left, V(todo).Bottom + 40, W, H)); Pump(80);
        Rectangle stretched = Rectangle.FromLTRB(V(ddl).Left, V(todo).Bottom + 12, V(ddl).Right, V(ddl).Bottom);
        Step(ddl, 0x0214, PlacementLogic.EdgeTop, stretched); Put(ddl, stretched); Pump(40);
        SendMessage(ddl.Handle, 0x0232, IntPtr.Zero, IntPtr.Zero); Pump(320);
        Assert(V(ddl).Top == V(todo).Bottom + G && V(ddl).Bottom == stretched.Bottom, "top edge released near Todo: snaps 5 px below it");

        // Resizing into a neighbour stops the dragged edge; the widget does not jump.
        Rectangle grown = Rectangle.FromLTRB(V(ddl).Left, V(todo).Bottom - 60, V(ddl).Right, V(ddl).Bottom);
        Step(ddl, 0x0214, PlacementLogic.EdgeTop, grown); Put(ddl, grown); Pump(40);
        SendMessage(ddl.Handle, 0x0232, IntPtr.Zero, IntPtr.Zero); Pump(320);
        Assert(V(ddl).Top == V(todo).Bottom + G && V(ddl).Bottom == grown.Bottom, "top edge dragged into Todo: stops 5 px below it, bottom edge stays");
        Rectangle wide = Rectangle.FromLTRB(V(ddl).Left, V(ddl).Top, work.Right + 40, V(ddl).Bottom);
        Step(ddl, 0x0214, PlacementLogic.EdgeRight, wide); Put(ddl, wide); Pump(40);
        SendMessage(ddl.Handle, 0x0232, IntPtr.Zero, IntPtr.Zero); Pump(320);
        Assert(V(ddl).Right == work.Right && V(ddl).Left == wide.Left, "right edge released 40 px off screen: back to the edge, left edge stays");

        // Dropped squarely on another widget (more than 2/3 covered): left stacked.
        Put(todo, at(900, 0)); Put(ddl, at(1450, 590)); Pump(60);
        Drop(ddl, at(950, 60));
        Assert(V(ddl) == at(950, 60) && V(todo) == at(900, 0) && Saved(app, ddl), "dropped covering most of Todo: both stay where they are, stacked");
        Drop(ddl, at(1150, 60));
        Assert(Apart(app.Widgets), "moved so that only about 40% is shared: pushed clear again");

        // Locked widgets never move; the other one makes way.
        Put(todo, at(900, 0)); Put(ddl, at(1450, 590)); Pump(60);
        app.Data.Windows["todo"].PositionLocked = true; Rectangle lockedAt = V(todo);
        Drop(ddl, at(1000, 100));
        Assert(V(todo) == lockedAt && !V(ddl).IntersectsWith(lockedAt), "dropped onto a locked widget: the locked one stays, the dropped one moves");
        app.Data.Windows["todo"].PositionLocked = false;

        // Hidden widgets are not obstacles; one coming back into view moves out of the way.
        Put(ddl, at(1450, 590)); Pump(60);
        app.SetWidgetVisible("ddl", false); Pump(80);
        Drop(todo, at(1150, 560));
        Rectangle todoOverHidden = V(todo);
        Assert(todoOverHidden == at(1150, 560), "a hidden widget is not an obstacle: " + todoOverHidden);
        app.SetWidgetVisible("ddl", true); Pump(300);
        Assert(V(todo) == todoOverHidden && Apart(app.Widgets), "a widget shown again on top of another moves out of the way, the other stays: " + V(ddl));

        // Settings center: typed coordinates follow the same rule.
        var state = app.Data.Windows["ddl"];
        app.UpdateWindow("ddl", new WindowState { X = work.Right + 300, Y = state.Y, Width = state.Width, Height = state.Height, Visible = true });
        Assert(V(ddl).Left == work.Right - PlacementLogic.KeepVisible && Saved(app, ddl), "an X entirely beyond the screen typed in settings: 100 px brought back into view");
        var calendarState = app.Data.Windows["calendar"];
        app.UpdateWindow("ddl", new WindowState { X = calendarState.X + 600, Y = calendarState.Y + 40, Width = state.Width, Height = state.Height, Visible = true });
        Assert(Apart(app.Widgets) && Saved(app, ddl) && V(calendar) == calendarBefore, "coordinates typed in settings that partly overlap another widget are moved clear, and settings show the result");
        app.UpdateWindow("ddl", new WindowState { X = calendarState.X + 40, Y = calendarState.Y + 40, Width = state.Width, Height = state.Height, Visible = true });
        Assert(V(calendar).Contains(V(ddl)) && Saved(app, ddl), "coordinates that put DDL entirely on the calendar are kept (stacked on purpose)");
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
        Assert(V(todo).Left == V(calendar).Right + G, "standard window dropped onto the calendar: moved to 5 px beside its visible edge (" + (V(todo).Left - V(calendar).Right) + " px)");
        Drop(todo, new Rectangle(work.Right - W + 40, work.Top + 50, W, H));
        Assert(V(todo).Right == work.Right, "standard window 40 px past the right edge: the visible edge ends flush with the screen");
        Drop(todo, new Rectangle(work.Right - W - 3, work.Top + 4, W, H));
        Assert(V(todo).Right == work.Right && V(todo).Top == work.Top, "standard windows snap to the screen edges on release too");
        app.SetWidgetMode("Desktop"); Pump(250);
        Assert(Apart(app.Widgets) && app.Widgets.All(w => PlacementLogic.Contain(V(w), work) == V(w)), "back in desktop mode everything still follows the rules and is apart");

        // Animation: the bounce glides and ends exactly at the target.
        Put(todo, at(900, 0)); Pump(60);
        Put(todo, new Rectangle(work.Right - W + 50, work.Top, W, H)); Pump(30);
        SendMessage(todo.Handle, 0x0232, IntPtr.Zero, IntPtr.Zero);
        bool gliding = todo.Sliding; Pump(320);
        Assert(!todo.Sliding && V(todo).Right == work.Right, "the bounce ends at the edge (animated: " + gliding + ")");

        // Leave an overlapping layout, with DDL far off screen, for the restart check.
        app.Save(); Assert(app.Flush(), "placement changes saved");
        Put(calendar, new Rectangle(work.Left, work.Top, 850, 650)); Put(todo, new Rectangle(work.Left + 600, work.Top + 50, W, H)); Put(ddl, new Rectangle(work.Right - 100, work.Top + 200, W, H)); Pump(100);
        Assert(V(todo).IntersectsWith(V(calendar)), "an overlapping layout is left for the restart check");
        app.QueueSave(); Assert(app.Flush(), "and saved");
    }
}
