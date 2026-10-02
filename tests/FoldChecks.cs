using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using DeskStudy;

// Folding widgets down to a header bar on the bottom edge, and unfolding them back.
public static class FoldChecks
{
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static void Pump(int ms) { var clock = Stopwatch.StartNew(); while (clock.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(10); } }
    static IEnumerable<Control> All(Control root) { foreach (Control c in root.Controls) { yield return c; foreach (Control d in All(c)) yield return d; } }
    static T Find<T>(Control root, string name) where T : Control { return (T)All(root).First(c => c.Name == name); }
    static Rectangle V(WidgetForm w) { return w.VisualBounds; }
    static void Put(WidgetForm w, Rectangle visual)
    {
        Rectangle b = w.Bounds, v = w.VisualBounds;
        w.Bounds = Rectangle.FromLTRB(visual.Left - (v.Left - b.Left), visual.Top - (v.Top - b.Top), visual.Right + (b.Right - v.Right), visual.Bottom + (b.Bottom - v.Bottom));
    }
    static void Drop(WidgetForm w, Point location) { w.Location = location; Pump(40); SendMessage(w.Handle, 0x0231, IntPtr.Zero, IntPtr.Zero); SendMessage(w.Handle, 0x0232, IntPtr.Zero, IntPtr.Zero); Pump(350); }
    static bool Apart(IEnumerable<WidgetForm> widgets)
    {
        var list = widgets.Where(w => w.Visible).ToList();
        for (int i = 0; i < list.Count; i++) for (int j = i + 1; j < list.Count; j++) if (V(list[i]).IntersectsWith(V(list[j]))) return false;
        return true;
    }
    static void Click(WidgetForm w) { Find<Button>(w, "collapse-widget").PerformClick(); Pump(350); }

    public static void Run(AppController app, string path, bool afterRestart)
    {
        var calendar = app.Widgets.First(w => w.WidgetKey == "calendar"); var todo = app.Widgets.First(w => w.WidgetKey == "todo"); var ddl = app.Widgets.First(w => w.WidgetKey == "ddl");
        Rectangle work = Screen.FromControl(todo).WorkingArea;
        if (afterRestart)
        {
            var saved = app.Data.Windows["ddl"];
            Assert(!ddl.Collapsed && ddl.Bounds == new Rectangle(saved.X, saved.Y, saved.Width, saved.Height) && ddl.Height > 300, "after a restart a widget that was folded opens unfolded, where it was before folding: " + ddl.Bounds);
            return;
        }
        foreach (var w in app.Widgets) app.Data.Windows[w.WidgetKey].PositionLocked = false;
        app.ShowAll(); app.Data.Settings.NotebookLayout = "Clean"; app.SettingsChanged(); Pump(250);
        Put(calendar, new Rectangle(work.Left + 1000, work.Bottom - 600, 900, 600));
        Put(ddl, new Rectangle(work.Left + 1000, work.Bottom - 1100, 440, 495));
        Put(todo, new Rectangle(work.Left + 100, work.Top + 40, 440, 540)); Pump(150);
        Assert(app.Widgets.All(w => Find<Button>(w, "collapse-widget").Visible), "every widget has a fold button next to ⚙");
        Assert(Find<Button>(ddl, "collapse-widget").Text == "", "the fold button shows a down chevron");

        // Folding the calendar: straight down onto the taskbar.
        Rectangle calendarBefore = calendar.Bounds;
        Click(calendar);
        Assert(calendar.Collapsed && !calendar.Sliding, "the calendar folds");
        Assert(V(calendar).Bottom == work.Bottom && V(calendar).Left == calendarBefore.Left && V(calendar).Height < 80 && V(calendar).Width == calendarBefore.Width, "folded: only the header bar is left, resting on the taskbar, same width: " + V(calendar));
        Assert(Find<Button>(calendar, "collapse-widget").Text == "", "folded: the button shows an up chevron");
        Assert(calendar.EdgeHit(new Point(V(calendar).Left + 2, V(calendar).Top + V(calendar).Height / 2)) == 1, "folded: the bar cannot be resized");
        var calendarSaved = app.Data.Windows["calendar"];
        Assert(new Rectangle(calendarSaved.X, calendarSaved.Y, calendarSaved.Width, calendarSaved.Height) == calendarBefore, "folded: the saved position is the unfolded one");

        // Folding DDL, which sits right above the calendar's bar: it slides along the bottom instead of covering it.
        Rectangle ddlBefore = ddl.Bounds;
        Click(ddl);
        Assert(ddl.Collapsed && V(ddl).Bottom == work.Bottom && !V(ddl).IntersectsWith(V(calendar)), "a second folded widget lines up on the bottom edge beside the first: " + V(ddl) + " / " + V(calendar));
        Assert(Apart(app.Widgets), "folded bars never cover other widgets");
        Assert(V(calendar).Height == V(ddl).Height, "the folded calendar bar is exactly as tall as the folded notebook bar (" + V(calendar).Height + " / " + V(ddl).Height + ")");

        // Dragging a folded bar: it always goes back to the bottom edge; near the right edge it snaps to it.
        Drop(ddl, new Point(work.Left + 200, work.Top + 300));
        Assert(V(ddl).Bottom == work.Bottom && V(ddl).Left == work.Left + 200, "a folded bar dragged up slides back down to the bottom");
        Drop(calendar, new Point(work.Right - V(calendar).Width + 60, work.Top + 500));
        Assert(V(calendar).Right == work.Right && V(calendar).Bottom == work.Bottom, "a folded bar dragged 60 px past the right edge snaps to it");

        // Unfolding: back to where it was.
        Click(calendar);
        Assert(!calendar.Collapsed && calendar.Bounds == calendarBefore, "unfolding puts the calendar back where it was, same size: " + calendar.Bounds);
        Assert(Find<Button>(calendar, "collapse-widget").Text == "", "unfolded: the button shows a down chevron again");
        // While DDL is folded, Todo moves into its old place; unfolding DDL then makes way.
        Put(todo, new Rectangle(ddlBefore.Left + 250, ddlBefore.Top + 30, 440, 540)); Pump(100);
        Click(ddl);
        Assert(!ddl.Collapsed && app.Widgets.Where(w => w != ddl).All(w => !V(ddl).IntersectsWith(V(w))) && V(ddl).Size == ddlBefore.Size, "unfolding into a place now taken: the unfolded widget moves to the nearest free spot: " + V(ddl));

        // Double-check the header double click and the font size.
        ddl.ToggleCollapse(); Pump(350);
        app.Data.Settings.GlobalAppearance.FontSize = 11; SettingsLogic.MarkAppearanceCustomized(app.Data.Settings.GlobalAppearance, "FontSize"); app.SettingsChanged(); Pump(250);
        Assert(ddl.Collapsed && V(ddl).Bottom == work.Bottom, "with a larger font the folded bar still rests on the bottom edge");
        app.Data.Settings.GlobalAppearance.FontSize = 9; app.SettingsChanged(); Pump(200);

        // Original layout: folding works the same.
        ddl.ToggleCollapse(); Pump(350);
        app.Data.Settings.NotebookLayout = "Original"; app.SettingsChanged(); Pump(250);
        Click(todo);
        Assert(todo.Collapsed && V(todo).Bottom == work.Bottom && app.Widgets.Where(w => w != todo).All(w => !V(todo).IntersectsWith(V(w))), "原始外观: folding works the same");
        Click(todo);

        // Typing coordinates in the settings center unfolds the widget first.
        Click(todo);
        var state = app.Data.Windows["todo"];
        app.UpdateWindow("todo", new WindowState { X = state.X, Y = state.Y, Width = state.Width, Height = state.Height, Visible = true });
        Assert(!todo.Collapsed && todo.Height == state.Height, "placing a folded widget from the settings center shows it unfolded");

        // Leave DDL folded for the restart check.
        app.Data.Settings.NotebookLayout = "Clean"; app.SettingsChanged(); Pump(200);
        Rectangle beforeRestart = ddl.Bounds;
        Click(ddl);
        app.Save(); Assert(app.Flush(), "fold checks saved");
        var s = app.Data.Windows["ddl"];
        Assert(new Rectangle(s.X, s.Y, s.Width, s.Height) == beforeRestart, "a folded widget is saved at its unfolded place");
    }
}
