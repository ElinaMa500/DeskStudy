using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using DeskStudy;

// DDL tasks listed by deadline, shown on the calendar, and opened from the calendar in the DDL notebook.
public static class DeadlineChecks
{
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hwnd, IntPtr dc, uint flags);
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static void Pump(int ms) { var clock = Stopwatch.StartNew(); while (clock.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(10); } }
    static IEnumerable<Control> All(Control root) { foreach (Control c in root.Controls) { yield return c; foreach (Control d in All(c)) yield return d; } }
    static string shots;

    // The widgets' own pictures, placed as they stand on the desktop. Nothing else on screen is captured.
    static void Shot(string name, params WidgetForm[] widgets)
    {
        var list = widgets.Where(w => w.Visible).ToList();
        Rectangle all = list.Select(w => w.Bounds).Aggregate(Rectangle.Union);
        using (var canvas = new Bitmap(all.Width + 24, all.Height + 24))
        {
            using (var g = Graphics.FromImage(canvas))
            {
                g.Clear(Color.FromArgb(214, 222, 218));
                foreach (var w in list)
                    using (var bmp = new Bitmap(w.Width, w.Height))
                    {
                        using (var wg = Graphics.FromImage(bmp)) { IntPtr dc = wg.GetHdc(); PrintWindow(w.Handle, dc, 2); wg.ReleaseHdc(dc); }
                        g.DrawImage(bmp, w.Left - all.Left + 12, w.Top - all.Top + 12);
                    }
            }
            canvas.Save(Path.Combine(shots, name + ".png"));
        }
    }

    static string Day(DateTime monday, int offset) { return monday.AddDays(offset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
    static TaskItem Task(string text, string due, bool dateOnly, bool done)
    {
        var task = new TaskItem { Text = text, Completed = done };
        if (due != null) { task.DueDateOnly = dateOnly; task.DueLocal = dateOnly ? due + "T23:59:00" : due.Replace(' ', 'T') + ":00"; }
        return task;
    }
    static CalendarEvent Course(string title, string date, string start, string end, string color)
    {
        return new CalendarEvent { Title = title, Date = date, StartTime = start, EndTime = end, Color = color, Location = "教学楼", RepeatWeeks = 0, RepeatEndDate = date };
    }

    static List<object> Hits(CalendarSurface surface)
    {
        var field = typeof(CalendarSurface).GetField("hits", BindingFlags.NonPublic | BindingFlags.Instance);
        return ((System.Collections.IEnumerable)field.GetValue(surface)).Cast<object>().ToList();
    }
    static object Get(object o, string name) { return o.GetType().GetField(name).GetValue(o); }
    static void ClickMark(CalendarSurface surface, string text, bool twice)
    {
        surface.Refresh();
        var hit = Hits(surface).First(h => Get(h, "Mark") != null && ((DeadlineMark)Get(h, "Mark")).Text == text);
        Rectangle r = (Rectangle)Get(hit, "Bounds");
        Point client = new Point(r.X + r.Width / 2 + surface.AutoScrollPosition.X, r.Y + r.Height / 2 + surface.AutoScrollPosition.Y);
        var args = new MouseEventArgs(MouseButtons.Left, 1, client.X, client.Y, 0);
        string handler = twice ? "HandleDoubleClick" : "HandleClick";
        if (twice) typeof(CalendarSurface).GetMethod("HandleClick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(surface, new object[] { surface, args });
        typeof(CalendarSurface).GetMethod(handler, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(surface, new object[] { surface, args });
    }

    static List<List<DeadlineMark>> Badges(CalendarForm calendar)
    {
        var field = typeof(CalendarForm).GetField("badgeHits", BindingFlags.NonPublic | BindingFlags.Instance);
        return ((List<KeyValuePair<Rectangle, List<DeadlineMark>>>)field.GetValue(calendar)).Select(p => p.Value).ToList();
    }
    static List<DeadlineMark> Marks(CalendarSurface surface)
    {
        return (List<DeadlineMark>)typeof(CalendarSurface).GetField("marks", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(surface);
    }
    // Task ids in the order the notebook shows them, top to bottom.
    static List<string> RowOrder(NotebookForm notebook)
    {
        return All(notebook).Where(c => c.Name.StartsWith("task-checkbox-") && c.Parent != null && c.Parent.Visible)
            .OrderBy(c => c.Parent.Top).Select(c => c.Name.Substring("task-checkbox-".Length)).ToList();
    }
    // Acts on the task editor once it is open (it runs its own message loop).
    static void WhenEditor(Action<TaskEditorDialog> act)
    {
        var timer = new System.Windows.Forms.Timer { Interval = 100 };
        timer.Tick += delegate
        {
            var dialog = Application.OpenForms.OfType<TaskEditorDialog>().FirstOrDefault(d => d.Visible);
            if (dialog == null) return;
            timer.Stop(); timer.Dispose(); act(dialog);
        };
        timer.Start();
    }

    public static void Run(AppController app, string path)
    {
        shots = Path.Combine(path, "ddl"); Directory.CreateDirectory(shots);
        var calendar = (CalendarForm)app.Widgets.First(w => w.WidgetKey == "calendar"); var todo = app.Widgets.First(w => w.WidgetKey == "todo"); var ddl = (NotebookForm)app.Widgets.First(w => w.WidgetKey == "ddl");
        DateTime monday = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));

        // A week of courses and a DDL book of three pages, one of them archived. Tasks are stored out of order.
        app.Data.Events.Clear();
        app.Data.Events.Add(Course("高等数学", Day(monday, 0), "08:00", "09:40", "#6C9385"));
        app.Data.Events.Add(Course("线性代数", Day(monday, 1), "10:00", "11:40", "#7196B1"));
        app.Data.Events.Add(Course("英语写作", Day(monday, 2), "08:30", "10:00", "#B08968"));
        app.Data.Events.Add(Course("组会", Day(monday, 3), "09:00", "10:30", "#8A7FB5"));
        app.Data.Events.Add(Course("大学物理", Day(monday, 4), "08:00", "09:40", "#6C9385"));
        var book = app.Data.Books.First(b => b.Id == "ddl");
        var homework = new NotePage { Title = "课程作业" };
        homework.Tasks.AddRange(new[] {
            Task("读书笔记", null, false, false), Task("课程论文初稿", Day(monday, 3), true, false), Task("高数习题 3.2", Day(monday, 4) + " 09:00", false, false),
            Task("小测复习", Day(monday, 1) + " 14:00", false, true), Task("物理实验报告", Day(monday, 2) + " 11:30", false, false),
            Task("英语作文", Day(monday, 3), true, false), Task("组会汇报材料", Day(monday, 3) + " 10:00", false, false) });
        var internship = new NotePage { Title = "实习申请" };
        internship.Tasks.AddRange(new[] { Task("简历投递", Day(monday, 6) + " 12:00", false, false), Task("网申测评", Day(monday, 8) + " 18:00", false, false),
            Task("笔试报名", Day(monday, 5) + " 11:30", false, false), Task("面试材料", Day(monday, 5) + " 11:45", false, false), Task("补交成绩单", Day(monday, 5) + " 12:00", false, false) });
        var archived = new NotePage { Title = "上学期", Archived = true };
        archived.Tasks.Add(Task("图书馆还书", Day(monday, 5), true, false));
        book.Pages = new List<NotePage> { homework, internship, archived }; book.CurrentPageId = homework.Id;
        app.Data.Settings.NotebookLayout = "Clean"; app.Data.Settings.Calendar.DefaultView = "Week"; app.Data.Settings.Calendar.WorkWeek = false;
        app.Data.Settings.GlobalAppearance.FontSize = 9; app.Data.Settings.GlobalAppearance.Opacity = 1;
        foreach (var w in app.Widgets) app.Data.Windows[w.WidgetKey].PositionLocked = false;
        app.ShowAll(); app.SettingsChanged(); app.Save(); Pump(300);
        ddl.Bounds = new Rectangle(1074, 0, 420, 488); todo.Bounds = new Rectangle(1499, 0, 421, 488); calendar.Bounds = new Rectangle(1074, 493, 846, 647);
        Pump(300);
        var surface = All(calendar).OfType<CalendarSurface>().First();
        surface.AutoScrollPosition = new Point(0, surface.HourPixels * 7); Pump(200);

        // Sorting: by deadline, date-only after timed ones that day, no deadline after dated ones, done last.
        var order = DeadlineLogic.Ordered(homework.Tasks).Select(t => t.Text).ToArray();
        Assert(String.Join("|", order) == "物理实验报告|组会汇报材料|课程论文初稿|英语作文|高数习题 3.2|读书笔记|小测复习", "the DDL page is listed by deadline: " + String.Join(" → ", order));
        Assert(homework.Tasks[0].Text == "读书笔记", "the stored order is left as it was");
        var marks = DeadlineLogic.Marks(app.Data, monday, monday.AddDays(6));
        Assert(marks.Count == 10 && !marks.Any(m => m.Text == "小测复习") && marks.Any(m => m.Text == "图书馆还书" && m.Archived), "the calendar gets every unfinished deadline this week, archived page included");
        Shot("1-week-sorted", ddl, todo, calendar);

        // From the calendar: the DDL book turns to the task's page and tints it.
        book.CurrentPageId = internship.Id; app.Save(); Pump(300);
        Shot("2-before-click", ddl, todo, calendar);
        ClickMark(surface, "物理实验报告", false); Pump(SystemInformation.DoubleClickTime + 250);
        Assert(book.CurrentPageId == homework.Id && ddl.FlashingTaskId == homework.Tasks.First(t => t.Text == "物理实验报告").Id, "a click on a calendar deadline opens its page in the DDL notebook and tints the task");
        Shot("3-after-click", ddl, todo, calendar);
        Pump(1800);
        Assert(ddl.FlashingTaskId == null, "the tint fades after a moment");

        // Date-only deadlines: a ⚑ badge beside the date, the day row keeps its height.
        calendar.Refresh(); Pump(100);
        var badges = Badges(calendar);
        Assert(badges.Count == 5 && badges.Any(b => b.Count == 3 && b.Count(m => m.DateOnly) == 2) && badges.Any(b => b.Count == 4 && b.Any(m => m.Text == "图书馆还书")), "week view: every day with deadlines has a badge beside its date, timed and date-only alike");
        var merged = Hits(surface).Where(h => Get(h, "Group") != null).Select(h => (List<DeadlineMark>)Get(h, "Group")).ToList();
        Assert(merged.Count == 1 && merged[0].Count == 3 && merged[0][0].Text == "笔试报名", "three deadlines close together on Saturday share one tag instead of three slivers");
        var dayHeader = All(calendar).First(c => c.Name == "calendar-day-header");
        int withBadges = dayHeader.Height;

        // A deadline on an archived page has no page to turn to: its editor opens instead.
        bool archivedEditor = false;
        WhenEditor(delegate(TaskEditorDialog dialog) { archivedEditor = dialog.TaskText == "图书馆还书"; dialog.DialogResult = DialogResult.Cancel; });
        typeof(CalendarForm).GetMethod("OpenBadge", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(calendar, new object[] { badges.SelectMany(b => b).Where(m => m.Text == "图书馆还书").ToList() });
        Pump(150);
        Assert(archivedEditor && book.CurrentPageId == homework.Id && archived.Archived, "clicking an archived deadline opens its editor and leaves the page archived");

        // Double-click: edit the task from the calendar.
        WhenEditor(delegate(TaskEditorDialog dialog)
        {
            ((TextBox)typeof(TaskEditorDialog).GetField("_text", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(dialog)).Text = "组会汇报 PPT";
            dialog.DialogResult = DialogResult.OK;
        });
        ClickMark(surface, "组会汇报材料", true); Pump(SystemInformation.DoubleClickTime + 250);
        Assert(homework.Tasks.Any(t => t.Text == "组会汇报 PPT") && Marks(surface).Any(m => m.Text == "组会汇报 PPT"), "double-clicking a deadline on the calendar edits the task; notebook and calendar both update");

        // Switched off in settings: no deadlines on the calendar, and the DDL book keeps the arranged order.
        app.Data.Settings.Calendar.ShowDeadlines = false; app.SettingsChanged(); Pump(300); calendar.Refresh(); Pump(100);
        Assert(Marks(surface).Count == 0 && Badges(calendar).Count == 0 && dayHeader.Height == withBadges, "with deadlines hidden the calendar shows none, and the day row is as tall as with badges");
        app.Data.Settings.Calendar.ShowDeadlines = true; app.SettingsChanged(); Pump(300);
        Assert(Marks(surface).Count == 10, "turned back on, the deadlines return");
        Assert(RowOrder(ddl).SequenceEqual(DeadlineLogic.Ordered(homework.Tasks).Select(t => t.Id)), "the DDL notebook lists the page by deadline");
        app.Data.Settings.SortDeadlines = false; app.SettingsChanged(); Pump(300);
        Assert(RowOrder(ddl).SequenceEqual(homework.Tasks.Select(t => t.Id)), "with sorting off the DDL notebook shows the order the tasks were arranged in");
        app.Data.Settings.NotebookLayout = "Original"; app.SettingsChanged(); Pump(400);
        Assert(All(ddl).OfType<Button>().Count(b => b.Text == "↑" && b.Visible) > 0, "with sorting off the original layout offers ↑ ↓ again");
        app.Data.Settings.SortDeadlines = true; app.SettingsChanged(); Pump(400);
        Assert(!All(ddl).OfType<Button>().Any(b => (b.Text == "↑" || b.Text == "↓") && b.Visible), "while sorting by deadline the original layout hides ↑ ↓ in the DDL book");
        app.Data.Settings.NotebookLayout = "Clean"; app.SettingsChanged(); Pump(400);

        // Month view, then the original style.
        calendar.Refresh();
        var month = All(calendar).OfType<Button>().First(b => b.Name == "calendar-month"); month.PerformClick(); Pump(300);
        Shot("4-month", calendar);
        All(calendar).OfType<Button>().First(b => b.Name == "calendar-week").PerformClick(); Pump(200);
        app.Data.Settings.NotebookLayout = "Original"; app.SettingsChanged(); Pump(400);
        surface.AutoScrollPosition = new Point(0, surface.HourPixels * 7); Pump(200);
        Shot("5-original-week", ddl, todo, calendar);
        app.Data.Settings.NotebookLayout = "Journal"; app.SettingsChanged(); Pump(400);
        surface.AutoScrollPosition = new Point(0, surface.HourPixels * 7); Pump(200);
        Shot("6-journal-week", ddl, todo, calendar);
        Console.WriteLine("DDL SHOTS: " + shots);
    }
}
