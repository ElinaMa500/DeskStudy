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

// Screenshots for the user guide, from sample data only (never the real screen or real data).
// Run once with a Chinese and once with an English first start: docs/images/guide/zh and .../en.
public static class GuideShots
{
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hwnd, IntPtr dc, uint flags);
    delegate bool EnumProc(IntPtr hwnd, IntPtr param);
    [DllImport("user32.dll")] static extern bool EnumThreadWindows(uint thread, EnumProc callback, IntPtr param);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    static void Pump(int ms) { var clock = Stopwatch.StartNew(); while (clock.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(10); } }
    static IEnumerable<Control> All(Control root) { foreach (Control c in root.Controls) { yield return c; foreach (Control d in All(c)) yield return d; } }
    static string folder;
    static bool en;
    static string L(string zh, string english) { return en ? english : zh; }

    static Bitmap Picture(Control w)
    {
        var bmp = new Bitmap(w.Width, w.Height);
        using (var g = Graphics.FromImage(bmp)) { IntPtr dc = g.GetHdc(); PrintWindow(w.Handle, dc, 2); g.ReleaseHdc(dc); }
        return bmp;
    }
    static void Save(Bitmap bmp, string name) { bmp.Save(Path.Combine(folder, name + ".png")); bmp.Dispose(); Console.WriteLine("SHOT " + name); }
    static void Shot(string name, Control w) { Save(Picture(w), name); }
    // Several widgets as they stand on the desktop, on a quiet background.
    static void Desk(string name, params Control[] widgets)
    {
        var list = widgets.Where(w => w.Visible).ToList();
        Rectangle all = list.Select(w => w.Bounds).Aggregate(Rectangle.Union);
        var canvas = new Bitmap(all.Width + 32, all.Height + 32);
        using (var g = Graphics.FromImage(canvas))
        {
            g.Clear(Color.FromArgb(214, 222, 218));
            foreach (var w in list) using (var b = Picture(w)) g.DrawImage(b, w.Left - all.Left + 16, w.Top - all.Top + 16);
        }
        Save(canvas, name);
    }
    static List<IntPtr> ThreadWindows() { var list = new List<IntPtr>(); EnumThreadWindows(GetCurrentThreadId(), delegate(IntPtr h, IntPtr p) { list.Add(h); return true; }, IntPtr.Zero); return list; }

    static string Day(DateTime monday, int n) { return monday.AddDays(n).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
    static CalendarEvent Course(string title, string date, string start, string end, string color, string place, int[] weekdays)
    {
        var e = new CalendarEvent { Title = title, Date = date, StartTime = start, EndTime = end, Color = color, Location = place, RepeatWeeks = weekdays == null ? 0 : 1, RepeatEndDate = weekdays == null ? date : TimeUtil.ParseDate(date).AddDays(120).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) };
        if (weekdays != null) e.WeekDays = weekdays.ToList();
        return e;
    }
    static TaskItem Task(string text, DateTime? due, bool dateOnly, bool done)
    {
        var t = new TaskItem { Text = text, Completed = done };
        if (due.HasValue) { t.DueDateOnly = dateOnly; t.DueLocal = (dateOnly ? due.Value.Date.AddHours(23).AddMinutes(59) : due.Value).ToString(TimeUtil.LocalFormat, CultureInfo.InvariantCulture); }
        return t;
    }

    public static void Run(AppController app, string path, string language)
    {
        en = language == "en";
        folder = Path.Combine(path, "guide", language); Directory.CreateDirectory(folder);
        var calendar = (CalendarForm)app.Widgets.First(w => w.WidgetKey == "calendar");
        var todo = (NotebookForm)app.Widgets.First(w => w.WidgetKey == "todo");
        var ddl = (NotebookForm)app.Widgets.First(w => w.WidgetKey == "ddl");
        // The sample week starts from tomorrow, so deadlines read as upcoming whatever day the shots are taken.
        DateTime soon = DateTime.Today.AddDays(1);
        DateTime monday = soon.AddDays(-(((int)soon.DayOfWeek + 6) % 7));
        bool nextWeek = monday > DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));

        // A student's week.
        app.Data.Events.Clear();
        app.Data.Events.Add(Course(L("高等数学", "Calculus"), Day(monday, 0), "08:00", "09:40", "#6C9385", L("教学楼 A201", "Room A201"), new[] { 1, 3 }));
        app.Data.Events.Add(Course(L("线性代数", "Linear Algebra"), Day(monday, 1), "10:00", "11:40", "#7196B1", L("教学楼 B105", "Room B105"), new[] { 2, 4 }));
        app.Data.Events.Add(Course(L("学术写作", "Academic Writing"), Day(monday, 2), "13:30", "15:00", "#C49472", L("图书馆 3 楼", "Library L3"), new[] { 3 }));
        app.Data.Events.Add(Course(L("大学物理", "Physics"), Day(monday, 4), "08:30", "10:10", "#A68EB5", L("理学楼 102", "Science 102"), new[] { 5 }));
        var meeting = Course(L("课题组会", "Group meeting"), Day(monday, 3), "15:00", "16:30", "#939C63", L("实验室", "Lab"), new[] { 4 }); meeting.RepeatWeeks = 2;
        app.Data.Events.Add(meeting);
        app.Data.Events.Add(new CalendarEvent { Title = L("校运动会", "Sports day"), Date = Day(monday, 5), StartTime = "00:00", EndTime = "23:59", AllDay = true, Days = 2, Color = "#BB818B", RepeatWeeks = 0, RepeatEndDate = Day(monday, 5) });

        var todoBook = app.Data.Books.First(b => b.Id == "todo"); var ddlBook = app.Data.Books.First(b => b.Id == "ddl");
        todoBook.Name = L("Todo", "To-do"); ddlBook.Name = L("DDL", "Deadlines");
        var todoPage = todoBook.Pages[0]; todoPage.Title = L("本周要做", "This week"); todoPage.Tasks.Clear();
        todoPage.Tasks.AddRange(new[] { Task(L("复习线性代数第 3 章", "Review Linear Algebra ch. 3"), null, false, false), Task(L("给导师发周报", "Send the weekly update"), null, false, true),
            Task(L("预约图书馆研讨室", "Book a library study room"), null, false, false), Task(L("整理实验数据", "Tidy up the lab data"), soon.AddDays(3).AddHours(17), false, false) });
        var ddlPage = ddlBook.Pages[0]; ddlPage.Title = L("课程作业", "Coursework"); ddlPage.Tasks.Clear();
        ddlPage.Tasks.AddRange(new[] { Task(L("物理实验报告", "Physics lab report"), soon.AddDays(2).AddHours(23).AddMinutes(59), false, false),
            Task(L("写作课论文初稿", "Writing essay draft"), soon.AddDays(3), true, false), Task(L("高数习题 3.2", "Calculus problem set 3.2"), soon.AddHours(12), false, false),
            Task(L("小组展示 PPT", "Group presentation slides"), soon.AddDays(8).AddHours(9), false, false) });
        var internship = new NotePage { Title = L("实习申请", "Internships") };
        internship.Tasks.Add(Task(L("网申测评", "Online assessment"), soon.AddDays(5).AddHours(18), false, false));
        ddlBook.Pages.Add(internship); ddlBook.CurrentPageId = ddlPage.Id;

        app.Data.Settings.NotebookLayout = "Journal"; app.Data.Settings.Calendar.DefaultView = "Week"; app.Data.Settings.Calendar.WorkWeek = false;
        app.Data.Settings.GlobalAppearance.FontSize = 9; app.Data.Settings.GlobalAppearance.Opacity = 1;
        foreach (var w in app.Widgets) app.Data.Windows[w.WidgetKey].PositionLocked = false;
        app.ShowAll(); app.SettingsChanged(); Pump(400);
        ddl.Bounds = new Rectangle(1074, 0, 420, 488); todo.Bounds = new Rectangle(1499, 0, 421, 488); calendar.Bounds = new Rectangle(1074, 493, 846, 647);
        Pump(400);
        var surface = All(calendar).OfType<CalendarSurface>().First();
        if (nextWeek) { All(calendar).OfType<Button>().First(b => b.Name == "calendar-next").PerformClick(); Pump(300); }
        Action morning = delegate { surface.AutoScrollPosition = new Point(0, surface.HourPixels * 7 + surface.HourPixels / 2); Pump(250); };
        morning();

        Desk("overview", ddl, todo, calendar);
        Shot("calendar-week", calendar);
        Shot("notebook-ddl", ddl);
        Shot("notebook-todo", todo);
        All(calendar).OfType<Button>().First(b => b.Name == "calendar-month").PerformClick(); Pump(300); Shot("calendar-month", calendar);
        All(calendar).OfType<Button>().First(b => b.Name == "calendar-workweek").PerformClick(); Pump(300); morning(); Shot("calendar-workweek", calendar);
        All(calendar).OfType<Button>().First(b => b.Name == "calendar-week").PerformClick(); Pump(300); morning();

        // The five layouts, the To-do notebook in each.
        var layouts = new[] { "Original", "Card", "Paper", "Clean", "Journal" };
        var pictures = new List<Bitmap>();
        foreach (string layout in layouts) { app.Data.Settings.NotebookLayout = layout; app.SettingsChanged(); Pump(400); todo.Bounds = new Rectangle(1499, 0, 421, 520); Pump(250); pictures.Add(Picture(todo)); }
        var strip = new Bitmap(pictures.Sum(p => p.Width) + 16 * (pictures.Count + 1), pictures.Max(p => p.Height) + 32);
        using (var g = Graphics.FromImage(strip)) { g.Clear(Color.FromArgb(214, 222, 218)); int x = 16; foreach (var p in pictures) { g.DrawImage(p, x, 16); x += p.Width + 16; p.Dispose(); } }
        Save(strip, "layouts");
        app.Data.Settings.NotebookLayout = "Journal"; app.SettingsChanged(); Pump(400);
        todo.Bounds = new Rectangle(1499, 0, 421, 488); Pump(200);

        // Editing a task in place.
        var title = All(todo).OfType<Label>().First(l => l.Text == todoPage.Tasks[0].Text && l.Visible);
        typeof(Control).GetMethod("OnMouseDoubleClick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(title, new object[] { new MouseEventArgs(MouseButtons.Left, 2, 30, title.Height / 2, 0) });
        Pump(250); Shot("inline-edit", todo);
        var editorBox = All(todo).OfType<TextBox>().FirstOrDefault(t => t.Name == "task-inline-editor");
        if (editorBox != null) { All(todo).OfType<TextBox>().First(t => t.Name == "quick-task").Focus(); Pump(200); }

        // The deadline picker.
        var quickDue = All(ddl).OfType<Button>().FirstOrDefault(b => b.Name == "quick-due");
        All(ddl).OfType<TextBox>().First(t => t.Name == "quick-task").Text = L("交选课确认单", "Hand in the course form");
        if (quickDue != null)
        {
            quickDue.PerformClick(); Pump(350);
            foreach (IntPtr h in ThreadWindows()) { var control = Control.FromHandle(h); if (control is ToolStripDropDown && control.Visible) { Shot("due-picker", control); ((ToolStripDropDown)control).Close(); } }
            Pump(150);
        }

        // Dialogs, shown without blocking.
        var eventEditor = new CalendarEditor(app.Data.Events[0], true, true); eventEditor.Show(); Pump(250); Shot("event-editor", eventEditor); eventEditor.Close();
        var allDayEditor = new CalendarEditor(app.Data.Events.Last(), true, true); allDayEditor.Show(); Pump(250); Shot("event-editor-allday", allDayEditor); allDayEditor.Close();
        var taskEditor = new TaskEditorDialog(ddlPage.Tasks[0], Color.FromArgb(184, 113, 75), 30); taskEditor.Show(); Pump(250); Shot("task-editor", taskEditor); taskEditor.Close();

        // Folded: the three bars along the bottom.
        foreach (var w in new WidgetForm[] { calendar, ddl, todo }) { All(w).OfType<Button>().First(b => b.Name == "collapse-widget").PerformClick(); Pump(350); }
        Desk("folded", calendar, ddl, todo);
        foreach (var w in new WidgetForm[] { calendar, ddl, todo }) { All(w).OfType<Button>().First(b => b.Name == "collapse-widget").PerformClick(); Pump(350); }

        // Outlook (sample link and events; nothing is downloaded).
        app.OutlookDownload = delegate(string unused) { throw new System.Net.WebException("offline"); };
        app.Data.Settings.Calendar.Outlook.Url = "https://outlook.office365.com/owa/calendar/…/calendar.ics";
        app.Outlook.Events.Clear();
        foreach (var o in new[] { new { t = L("学院讲座", "Department seminar"), d = 1, s = "16:00", e = "17:00" }, new { t = L("答疑时间", "Office hours"), d = 3, s = "12:00", e = "13:00" } })
            app.Outlook.Events.Add(new OutlookEvent { Uid = o.t, Title = o.t, Start = Day(monday, o.d) + "T" + o.s + ":00", End = Day(monday, o.d) + "T" + o.e + ":00", Location = L("线上", "Online") });
        app.Outlook.LastSyncUtc = DateTime.UtcNow.AddMinutes(-5).ToString("o");
        app.SettingsChanged(); Pump(300); morning();
        surface.AutoScrollPosition = new Point(0, surface.HourPixels * 11); Pump(250);
        Shot("calendar-outlook", calendar);

        // The settings center, page by page.
        app.OpenSettings(); Pump(400);
        var settings = Application.OpenForms.OfType<SettingsForm>().Single(); settings.Size = new Size(1100, 860); Pump(250);
        var nav = (ListBox)All(settings).First(c => c.Name == "settings-nav");
        string[] names = { "settings-overview", "settings-display", "settings-appearance", "settings-calendar", "settings-notebooks", "settings-reminders", "settings-data" };
        for (int i = 0; i < nav.Items.Count && i < names.Length; i++) { nav.SelectedIndex = i; Pump(350); Shot(names[i], settings); }
        nav.SelectedIndex = 3; Pump(300);
        var page = All(settings).OfType<Panel>().First(p => p.Name == "settings-page-3" && p.Visible);
        // Bring the Outlook card to the top of the page.
        var link = All(settings).First(c => c.Name == "outlook-url");
        int y = page.PointToClient(link.PointToScreen(Point.Empty)).Y - page.AutoScrollPosition.Y;
        page.AutoScrollPosition = new Point(0, Math.Max(0, y - 110)); Pump(300);
        Shot("settings-outlook", settings);
        settings.Close(); Pump(150);
        Console.WriteLine("GUIDE SHOTS: " + folder);
    }
}
