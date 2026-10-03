using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using DeskStudy;

// Turning a notebook page: fast, repainted in one pass, and the reused rows show the right tasks.
public static class PageTurnChecks
{
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static void Pump(int ms) { var clock = Stopwatch.StartNew(); while (clock.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(5); } }
    static IEnumerable<Control> All(Control root) { foreach (Control c in root.Controls) { yield return c; foreach (Control d in All(c)) yield return d; } }

    // Counts paint messages and the windows they went to.
    sealed class PaintCounter : IMessageFilter
    {
        public int Paints; public readonly HashSet<IntPtr> Windows = new HashSet<IntPtr>();
        public bool PreFilterMessage(ref Message m) { if (m.Msg == 0x000F) { Paints++; Windows.Add(m.HWnd); } return false; }
    }

    // Task texts and ticks in the order the notebook shows them.
    static List<string> Shown(NotebookForm notebook)
    {
        return All(notebook).OfType<CheckBox>().Where(c => c.Name.StartsWith("task-checkbox-") && c.Parent != null && c.Parent.Visible)
            .OrderBy(c => c.Parent.Top).Select(c => c.Parent.Controls.OfType<Label>().First(l => l.Left > c.Left && l.Top <= c.Bottom && l.Text.Length > 0).Text + (c.Checked ? " ✓" : "")).ToList();
    }

    public static void Run(AppController app, string label, bool assert)
    {
        var ddl = (NotebookForm)app.Widgets.First(w => w.WidgetKey == "ddl");
        var book = app.Data.Books.First(b => b.Id == "ddl");
        // Pages of different lengths; some tasks done.
        book.Pages.Clear();
        foreach (int count in new[] { 8, 3, 6 })
        {
            var page = new NotePage { Title = "第 " + (book.Pages.Count + 1) + " 页" };
            for (int i = 0; i < count; i++) page.Tasks.Add(new TaskItem { Text = "任务 " + (book.Pages.Count + 1) + "-" + (i + 1), DueLocal = DateTime.Today.AddDays(i + 1).ToString("yyyy-MM-dd") + "T18:00:00", Completed = i == 1 });
            book.Pages.Add(page);
        }
        book.CurrentPageId = book.Pages[0].Id; app.Data.Settings.SortDeadlines = false; app.Save();
        foreach (var w in app.Widgets) app.Data.Windows[w.WidgetKey].PositionLocked = false;
        app.ShowAll(); Pump(300);
        var next = All(ddl).OfType<Button>().First(b => b.Name == "next-page");
        var previous = All(ddl).OfType<Button>().First(b => b.Name == "previous-page");
        Func<NotePage, List<string>> expected = page => page.Tasks.Select(t => t.Text + (t.Completed ? " ✓" : "")).ToList();
        foreach (string layout in new[] { "Journal", "Clean", "Card", "Paper", "Original" })
        {
            app.Data.Settings.NotebookLayout = layout; app.SettingsChanged(); Pump(400);
            book.CurrentPageId = book.Pages[0].Id; app.Save(); Pump(300);
            var times = new List<double>(); int paints = 0, windows = 0;
            for (int turn = 0; turn < 8; turn++)
            {
                var counter = new PaintCounter(); Application.AddMessageFilter(counter);
                var clock = Stopwatch.StartNew();
                (turn % 4 < 2 ? next : previous).PerformClick();
                double handler = clock.Elapsed.TotalMilliseconds;
                Pump(250);
                Application.RemoveMessageFilter(counter);
                if (turn > 0) { times.Add(handler); paints += counter.Paints; windows += counter.Windows.Count; }
                var page = book.Pages.First(p => p.Id == book.CurrentPageId);
                if (assert && !Shown(ddl).SequenceEqual(expected(page))) throw new Exception(layout + ": after turning, the page shows " + String.Join(", ", Shown(ddl)) + " instead of " + String.Join(", ", expected(page)));
            }
            int n = times.Count;
            Console.WriteLine(String.Format("{0} {1}: turn {2:0} ms (max {3:0}), {4} paints in {5} windows per turn", label, layout, times.Average(), times.Max(), paints / n, windows / n));
            if (!assert) continue;
            Assert(true, layout + ": every turn shows exactly that page's tasks, ticks included, also between pages of 8, 3 and 6 tasks");
            Assert(times.Average() < 200, layout + ": a page turn takes " + times.Average().ToString("0") + " ms (it took 250–720 ms before rows were reused)");
            Assert(paints / n <= 40, layout + ": the notebook repaints in one pass (" + paints / n + " paints, it was over 100)");
        }

        // A reused row acts on its new task.
        app.Data.Settings.NotebookLayout = "Journal"; app.SettingsChanged(); Pump(300);
        book.CurrentPageId = book.Pages[0].Id; app.Save(); Pump(200);
        next.PerformClick(); Pump(200);
        var second = book.Pages[1];
        var box = All(ddl).OfType<CheckBox>().First(c => c.Name == "task-checkbox-" + second.Tasks[0].Id);
        box.Checked = true; Pump(200);
        Assert(second.Tasks[0].Completed && !book.Pages[0].Tasks[0].Completed, "ticking a task on a reused row completes that page's task, not the one the row showed before");
        Edits(app);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);
    // Runs one change and reports how long it took, how many paint messages followed and whether any widget was restyled.
    static void Change(string what, Action act, int maxPaints)
    {
        int restyles = WidgetForm.Restyles;
        var counter = new PaintCounter(); Application.AddMessageFilter(counter);
        var clock = Stopwatch.StartNew(); act(); double ms = clock.Elapsed.TotalMilliseconds;
        Pump(250); Application.RemoveMessageFilter(counter);
        Console.WriteLine(String.Format("   {0}: {1:0} ms, {2} paints, {3} restyles", what, ms, counter.Paints, WidgetForm.Restyles - restyles));
        Assert(WidgetForm.Restyles == restyles, what + ": no widget is restyled");
        Assert(counter.Paints <= maxPaints && ms < 250, what + ": drawn in one pass (" + counter.Paints + " paints, " + ms.ToString("0") + " ms)");
    }

    // Editing tasks: double-click edit, tick, add. None of them restyles a widget or repaints piece by piece.
    static void Edits(AppController app)
    {
        var todo = (NotebookForm)app.Widgets.First(w => w.WidgetKey == "todo");
        var book = app.Data.Books.First(b => b.Id == "todo");
        var page = book.Pages.First(p => p.Id == book.CurrentPageId);
        page.Tasks.Clear();
        for (int i = 0; i < 6; i++) page.Tasks.Add(new TaskItem { Text = "要做的事 " + (i + 1) });
        app.Save(); Pump(300);
        foreach (string layout in new[] { "Journal", "Clean", "Original" })
        {
            app.Data.Settings.NotebookLayout = layout; app.SettingsChanged(); Pump(400);
            string before = page.Tasks[2].Text, after = before + " 改";
            var title = All(todo).OfType<Label>().First(l => l.Text == before && l.Visible);
            typeof(Control).GetMethod("OnMouseDoubleClick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(title, new object[] { new MouseEventArgs(MouseButtons.Left, 2, 4, title.Height / 2, 0) });
            Pump(100);
            var editor = All(todo).OfType<TextBox>().First(t => t.Name == "task-inline-editor");
            editor.Text = after;
            var seen = new List<string>();
            EventHandler watch = delegate { seen.Add(title.Text); };
            title.TextChanged += watch;
            Change(layout + " double-click edit saved", delegate { SendMessage(editor.Handle, 0x0100, new IntPtr((int)Keys.Enter), IntPtr.Zero); }, 40);
            title.TextChanged -= watch;
            Assert(page.Tasks[2].Text == after && All(todo).OfType<Label>().Any(l => l.Text == after && l.Visible) && !seen.Contains(before), layout + ": the row goes straight to the new text, never back to the old one first");
            var box = All(todo).OfType<CheckBox>().First(c => c.Name == "task-checkbox-" + page.Tasks[0].Id);
            Change(layout + " task ticked", delegate { box.Checked = !box.Checked; }, 40);
            if (layout != "Original")
            {
                var quick = All(todo).OfType<TextBox>().First(t => t.Name == "quick-task");
                quick.Text = "新任务 " + layout;
                Change(layout + " task added", delegate { SendMessage(quick.Handle, 0x0100, new IntPtr((int)Keys.Enter), IntPtr.Zero); }, 40);
                Assert(page.Tasks.Any(t => t.Text == "新任务 " + layout), layout + ": the quick entry adds the task");
            }
        }
        // Settings that change the look still restyle.
        int restyles = WidgetForm.Restyles;
        app.Data.Settings.GlobalAppearance.FontSize = 10; app.SettingsChanged(); Pump(300);
        Assert(WidgetForm.Restyles > restyles, "changing the font size still restyles the widgets");
        app.Data.Settings.GlobalAppearance.FontSize = 9; app.SettingsChanged(); Pump(300);
    }
}
