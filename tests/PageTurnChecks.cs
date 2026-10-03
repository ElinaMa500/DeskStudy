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
    }
}
