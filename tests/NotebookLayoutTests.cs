using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using DeskStudy;

// Runs the application's real forms against an isolated data directory. No user data or startup registration.
public static class NotebookLayoutTests
{
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);
    static readonly string[] Layouts = { "Original", "Card", "Paper", "Clean", "Journal" };
    static bool IsReference(string layout) { return layout == "Clean" || layout == "Journal"; }
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static IEnumerable<Control> Descendants(Control c) { foreach (Control child in c.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
    static T Find<T>(Control root, string name) where T : Control { return (T)Descendants(root).First(c => c.Name == name); }
    static void Pump(int ms) { var clock = Stopwatch.StartNew(); while (clock.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(10); } }
    static Notebook Book(AppController app, string key) { return app.Data.Books.First(b => b.Id == key); }
    static NotebookForm Form(AppController app, string key) { return (NotebookForm)app.Widgets.First(w => w.WidgetKey == key); }
    static string Applied(NotebookForm form) { return Convert.ToString(form.GetType().GetProperty("AppliedNotebookLayout").GetValue(form, null)); }
    static void Switch(AppController app, string layout) { app.Data.Settings.NotebookLayout = layout; app.SettingsChanged(); Pump(180); }
    static Rectangle ScreenBounds(Control control) { return new Rectangle(control.PointToScreen(Point.Empty), control.ClientSize); }
    static void Capture(Form form, string path)
    {
        File.WriteAllLines(path + ".layout.txt", Descendants(form).Where(c => c.Visible).Select(c => c.GetType().Name + " | " + c.Name + " | " + c.Text.Replace("\n", " ").Replace("\r", "") + " | " + c.Bounds + " | Font=" + c.Font.SizeInPoints + " | parent=" + c.Parent.Bounds).ToArray());
        using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png); }
    }
    static void Seed(AppController app)
    {
        foreach (var book in app.Data.Books)
        {
            book.Pages.Clear();
            var first = new NotePage { Title = book.Id == "todo" ? "本周任务" : "十月 DDL", Text = "第一段备注必须完整保留。\r\n中文备注：实验的原始测量记录和课堂疑问，下次继续整理。" };
            first.Tasks.Add(new TaskItem { Text = "整理课堂笔记，核对每一步推导和引用文献", Completed = true });
            first.Tasks.Add(new TaskItem { Text = "完成这段很长的中文任务：整理实验数据、检查误差分析、补充计算步骤并撰写讨论，确保窗口缩小时也可以完整阅读内容并继续勾选任务。", DueLocal = DateTime.Now.AddDays(1).ToString("yyyy-MM-ddTHH:mm:ss"), ReminderMinutes = 45 });
            first.Tasks.Add(new TaskItem { Text = "提交课程报告与附件", DueLocal = DateTime.Now.AddHours(-2).ToString("yyyy-MM-ddTHH:mm:ss"), Completed = true });
            var second = new NotePage { Title = "第 2 页 · 独立内容", Text = "第 2 页的备注，不能被第 1 页覆盖。" };
            second.Tasks.Add(new TaskItem { Text = "跨布局保持完成状态" });
            second.Tasks.Add(new TaskItem { Text = "本页第二项任务", DueLocal = DateTime.Now.AddDays(3).ToString("yyyy-MM-ddTHH:mm:ss"), ReminderMinutes = 30 });
            book.Pages.Add(first); book.Pages.Add(second); book.CurrentPageId = second.Id;
        }
        app.Data.Events.Add(new CalendarEvent { Title = "布局回归 · 双周课程", Date = "2026-09-07", RepeatEndDate = "2026-12-18", RepeatWeeks = 2, WeekDays = new List<int> { 1 } });
        CalendarEngine.CancelOccurrence(app.Data.Events[0], "2026-09-21");
        app.Save(); Pump(100);
    }
    static void AssertFooterUsable(NotebookForm form, string layout)
    {
        string[] names = layout == "Original" ? new[] { "previous-page", "next-page", "new-page" } : new[] { "previous-page", "next-page", "new-page", "page-directory" };
        Rectangle client = ScreenBounds(form);
        foreach (string name in names)
        {
            var c = Find<Control>(form, name);
            Assert(c.Visible && c.Width > 15 && c.Height >= 15 && client.Contains(ScreenBounds(c)), layout + " navigation remains visible and inside small window: " + name);
        }
        var title = Find<TextBox>(form, "page-title");
        Assert(title.Visible && title.Height >= title.Font.Height && client.Contains(ScreenBounds(title)), layout + " editable page title fits current font and window");
        if (layout != "Original")
        {
            var quick = Find<TextBox>(form, "quick-task");
            var scroll = quick.Parent.Parent as ScrollableControl;
            if (scroll != null) { scroll.ScrollControlIntoView(quick.Parent); Pump(30); }
            Assert(quick.Visible && quick.Width >= 60 && quick.Height >= quick.Font.Height && client.Contains(ScreenBounds(quick)), layout + " task input stays usable at small size");
            Assert(Find<Button>(form, "page-directory").Bottom <= Find<Button>(form, "page-directory").Parent.Height, layout + " footer is contained in its fixed row");
        }
    }
    static void WriteChecks(AppController app, string path, bool cardOnly)
    {
        Assert(app.Data.Settings.NotebookLayout == "Card", "new configuration defaults to recommended Card layout");
        Seed(app);
        var todo = Book(app, "todo"); var ddl = Book(app, "ddl"); var form = Form(app, "todo"); var ddlForm = Form(app, "ddl");
        var second = todo.Pages[1]; var task = second.Tasks[0];
        ((CheckBox)Find<Control>(form, "task-checkbox-" + task.Id)).Checked = true;
        var title = Find<TextBox>(form, "page-title"); var notes = Find<TextBox>(form, "page-text"); var quick = Find<TextBox>(form, "quick-task");
        title.Text = "标题草稿 · 正在编辑"; notes.Text = "备注草稿一\r\n备注草稿二；保持全部中文内容。"; quick.Text = "未提交的连续录入草稿"; quick.SelectionStart = 4; quick.SelectionLength = 3;
        var controls = new[] { title, notes, quick };
        form.Bounds = new Rectangle(110, 120, Math.Max(form.MinimumSize.Width, 500), 700); form.TopMost = true; form.Remember(); app.Data.Windows["todo"].PositionLocked = true;
        ddlForm.Hide();
        var windows = app.Widgets.ToDictionary(w => w.WidgetKey, w => w.Bounds);
        var windowHandles = app.Widgets.ToDictionary(w => w.WidgetKey, w => w.Handle);
        string[] layouts = cardOnly ? new[] { "Original", "Card" } : Layouts;
        foreach (var layout in layouts)
        {
            Switch(app, layout);
            Assert(Applied(form) == layout && Applied(ddlForm) == layout, "both notebooks apply layout " + layout);
            Assert(todo.CurrentPageId == second.Id && ddl.CurrentPageId == ddl.Pages[1].Id && second.Tasks[0].Completed && ((CheckBox)Find<Control>(form, "task-checkbox-" + task.Id)).Checked, layout + " keeps independent pages and shared completion");
            Assert(second.Title == title.Text && second.Text == notes.Text && notes.Text.Contains("备注草稿二"), layout + " keeps title and complete notes");
            Assert(quick.Text == "未提交的连续录入草稿" && quick.SelectionStart == 4 && quick.SelectionLength == 3, layout + " retains unsubmitted task text and selection");
            Assert(controls.All(c => !c.IsDisposed) && Object.ReferenceEquals(title, Find<TextBox>(form, "page-title")) && Object.ReferenceEquals(notes, Find<TextBox>(form, "page-text")), layout + " reuses edit controls and keeps their draft contents");
            Assert(app.Widgets.All(w => w.Handle == windowHandles[w.WidgetKey] && w.Bounds == windows[w.WidgetKey]) && form.TopMost && form.PositionLocked && !ddlForm.Visible, layout + " preserves native windows, geometry, pin, lock and hidden state");
            var previous = Find<Button>(form, "previous-page"); var newPage = Find<Button>(form, "new-page");
            Assert((previous.PointToScreen(Point.Empty).Y < title.PointToScreen(Point.Empty).Y) == (layout == "Original"), layout + " restores original top navigation or modern bottom navigation");
            Assert(newPage.Visible && Find<Button>(form, "open-settings").Visible, layout + " keeps navigation and settings entrance");
            bool reference = IsReference(layout);
            Assert(Find<Control>(form, "widget-accent").Visible != reference && Find<Button>(form, "hide-widget").Visible != reference && Find<CheckBox>(form, "widget-pin").Visible
                && Find<Label>(form, "widget-heading").Text.EndsWith(" · 待办本") == reference, layout + " uses the matching header (reference: dot, 置顶, ⚙; ✕ hides)");
            if (reference)
            {
                Assert(Find<Label>(form, "widget-heading").Text == "Todo · 待办本" && Find<Label>(ddlForm, "widget-heading").Text == "DDL · 截止本", layout + " header names both books");
                var summary = ((Label)typeof(NotebookForm).GetField("_taskCount", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form)).Text;
                var ddlSummary = ((Label)typeof(NotebookForm).GetField("_taskCount", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ddlForm)).Text;
                Assert(System.Text.RegularExpressions.Regex.IsMatch(summary, @"^\d+ 月 \d+ 日 · 1/2 完成$") && System.Text.RegularExpressions.Regex.IsMatch(ddlSummary, @"^\d+ 月 \d+ 日 · \d+ 项待完成$"), layout + " summary shows created date and real counts: " + summary + " / " + ddlSummary);
                var ddlDue = Find<CheckBox>(ddlForm, "task-checkbox-" + ddl.Pages[1].Tasks[1].Id).Parent.Controls.OfType<Label>().Last().Text;
                Assert(ddlDue.StartsWith("剩余 3 天 · ") && ddlDue.EndsWith(" · 提前 30 分钟"), layout + " DDL due line merges remaining days and reminder: " + ddlDue);
                Assert(!Find<Label>(form, "save-status").Visible, layout + " hides normal save status");
            }
        }
        Switch(app, "Original");
        Assert(Find<Button>(form, "hide-widget").Visible && Find<Control>(form, "widget-accent").Visible && Find<Label>(form, "widget-heading").Text == "Todo", "switching back restores the original header and 隐藏 button");
        Switch(app, "Card");
        quick.Text = "连续录入第一项"; Find<Button>(form, "quick-add").PerformClick();
        Assert(second.Tasks.Last().Text == "连续录入第一项" && quick.Text == "", "inline task submit uses current page and clears input for next item");
        quick.Text = "连续录入第二项"; Find<Button>(form, "quick-add").PerformClick();
        Assert(second.Tasks.Last().Text == "连续录入第二项", "inline task input supports consecutive additions");
        var firstId = second.Tasks[0].Id;
        var firstCard = Find<CheckBox>(form, "task-checkbox-" + firstId).Parent;
        firstCard.Controls.OfType<Button>().First(b => b.AccessibleName == "任务操作").PerformClick();
        var menu = (ContextMenuStrip)typeof(NotebookForm).GetField("_taskMenu", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form);
        menu.Items.Cast<ToolStripItem>().First(i => i.Text == "下移任务").PerformClick(); menu.Close(); Pump(40);
        Assert(second.Tasks[1].Id == firstId, "modern task action menu moves a task using shared task order");
        firstCard = Find<CheckBox>(form, "task-checkbox-" + firstId).Parent;
        firstCard.Controls.OfType<Button>().First(b => b.AccessibleName == "任务操作").PerformClick();
        menu = (ContextMenuStrip)typeof(NotebookForm).GetField("_taskMenu", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form);
        menu.Items.Cast<ToolStripItem>().First(i => i.Text == "上移任务").PerformClick(); menu.Close(); Pump(40);
        Assert(second.Tasks[0].Id == firstId && second.Tasks[0].Completed, "modern action menu restores order without changing completion");
        Find<Button>(form, "previous-page").PerformClick(); Pump(50);
        Assert(todo.CurrentPageId == todo.Pages[0].Id && Find<TextBox>(form, "page-text").Text == todo.Pages[0].Text, "previous-page restores first page notes");
        Find<Button>(form, "next-page").PerformClick(); Pump(50);
        Assert(todo.CurrentPageId == second.Id && second.Tasks[0].Completed, "next-page restores current content and completion");

        Switch(app, "Card"); quick.Focus(); IntPtr composingHandle = quick.Handle; SendMessage(quick.Handle, 0x10D, IntPtr.Zero, IntPtr.Zero);
        app.Data.Settings.NotebookLayout = "Original"; app.SettingsChanged(); Pump(160);
        Assert(Applied(form) == "Card" && quick.Handle == composingHandle, "simulated IME composition defers structural layout change and preserves edit handle");
        SendMessage(quick.Handle, 0x10E, IntPtr.Zero, IntPtr.Zero); Pump(400);
        Assert(Applied(form) == "Original", "deferred layout applies after simulated composition ends");

        app.Data.Settings.GlobalAppearance = new AppearanceOptions { BackgroundColor = "#E7EDF7", Theme = "Light", FontSize = 12, Opacity = .91 };
        app.Data.Settings.AppearanceOverrides["todo"] = new AppearanceOptions { BackgroundColor = "#F3E5D6", Theme = "Light", FontSize = 14, Opacity = .87 };
        foreach (var layout in layouts)
        {
            Switch(app, layout);
            Assert(app.Data.Settings.AppearanceOverrides["todo"].BackgroundColor == "#F3E5D6" && app.Data.Settings.AppearanceOverrides["todo"].FontSize == 14 && app.Data.Settings.GlobalAppearance.BackgroundColor == "#E7EDF7" && app.Data.Settings.GlobalAppearance.FontSize == 12, layout + " leaves custom global and component values unchanged");
            Assert(Math.Abs(form.Font.SizeInPoints - 14) < .1 && Math.Abs(form.Opacity - .87) < .01 && Math.Abs(ddlForm.Font.SizeInPoints - 12) < .1 && Math.Abs(ddlForm.Opacity - .91) < .01, layout + " applies custom font and opacity with existing priority");
            Assert(form.BackColor.ToArgb() == ColorTranslator.FromHtml("#F3E5D6").ToArgb() && ddlForm.BackColor.ToArgb() == ColorTranslator.FromHtml("#E7EDF7").ToArgb(), layout + " applies explicit background colors");
        }
        if (cardOnly) return;

        app.OpenSettings(); Pump(80);
        var settings = Application.OpenForms.OfType<SettingsForm>().Single(); Find<ListBox>(settings, "settings-nav").SelectedIndex = 2;
        foreach (var layout in Layouts)
        {
            Find<Button>(settings, "notebook-layout-" + layout.ToLowerInvariant()).PerformClick(); Pump(180);
            Assert(app.Data.Settings.NotebookLayout == layout && Applied(form) == layout && Applied(ddlForm) == layout, "settings selection card synchronizes both actual notebook windows: " + layout);
        }
        settings.Close(); Assert(!app.Exiting && !form.IsDisposed, "closing appearance settings leaves desktop widgets running");

        var delivered = new List<ReminderRecord>(); app.RemindersDelivered += delegate(List<ReminderRecord> batch) { delivered.AddRange(batch); };
        var deadline = DateTime.Now.AddSeconds(9).ToString("yyyy-MM-ddTHH:mm:ss");
        var pending = new TaskItem { Text = "隐藏旧页的实时提醒只触发一次", DueLocal = deadline, ReminderMinutes = 0 };
        var completed = new TaskItem { Text = "提前完成不再提醒", DueLocal = deadline, ReminderMinutes = 0, Completed = true };
        ddl.Pages[0].Tasks.Add(pending); ddl.Pages[0].Tasks.Add(completed); ddl.CurrentPageId = ddl.Pages[1].Id; app.Save();
        for (int i = 0; i < 10; i++) Switch(app, Layouts[i % Layouts.Length]);
        app.SetPageArchived("ddl", ddl.Pages[0].Id, true); ddlForm.Hide(); Pump(14000);
        Assert(delivered.Count(r => r.TaskId == pending.Id) == 1, "live 5-second timer still reminds once after repeated layout switches, page change, archive and hide");
        Assert(delivered.All(r => r.TaskId != completed.Id), "completed hidden-page task produces no reminder");
        app.CheckReminders(DateTime.UtcNow); app.CheckReminders(DateTime.UtcNow.AddSeconds(2));
        Assert(delivered.Count(r => r.TaskId == pending.Id) == 1, "repeated scans do not duplicate delivered reminder");
        var occurrences = CalendarEngine.GetOccurrences(app.Data, new DateTime(2026, 9, 1), new DateTime(2026, 12, 31));
        Assert(occurrences.Count == 7 && occurrences.All(o => o.Date != "2026-09-21"), "calendar recurrence and single cancellation remain unchanged");
        Switch(app, "Paper"); app.SetWidgetVisible("calendar", false);
        var expected = app.Widgets.Select(w => { w.Remember(); var s = app.Data.Windows[w.WidgetKey]; return w.WidgetKey + "|" + s.X + "|" + s.Y + "|" + s.Width + "|" + s.Height + "|" + s.TopMost + "|" + s.Visible + "|" + s.PositionLocked; }).ToArray();
        File.WriteAllLines(Path.Combine(path, "layout-windows.txt"), expected);
        File.WriteAllText(Path.Combine(path, "reminder-id.txt"), pending.Id);
        app.Save(); Assert(app.Flush(), "layout, content and windows saved before real process exit");
    }
    static void ReadChecks(AppController app, string path)
    {
        Assert(app.Data.Settings.NotebookLayout == "Paper" && Applied(Form(app, "todo")) == "Paper" && Applied(Form(app, "ddl")) == "Paper", "fresh process restores saved layout for both notebooks");
        var todo = Book(app, "todo"); var ddl = Book(app, "ddl");
        Assert(todo.CurrentPageId == todo.Pages[1].Id && ddl.CurrentPageId == ddl.Pages[1].Id && todo.Pages[1].Title == "标题草稿 · 正在编辑" && todo.Pages[1].Text.Contains("备注草稿二") && todo.Pages[1].Tasks[0].Completed, "fresh process restores independent current pages, title, notes and completion");
        Assert(todo.Pages[1].Tasks.Count == 4 && todo.Pages[1].Tasks.Last().Text == "连续录入第二项" && todo.Pages[1].Tasks[1].ReminderMinutes == 30 && todo.Pages[1].Tasks[1].DueLocal != "", "fresh process keeps task order, additions, due dates and reminder settings");
        foreach (var line in File.ReadAllLines(Path.Combine(path, "layout-windows.txt")))
        {
            var p = line.Split('|'); var w = app.Widgets.First(f => f.WidgetKey == p[0]);
            Assert(w.Left == int.Parse(p[1]) && w.Top == int.Parse(p[2]) && w.Width == int.Parse(p[3]) && w.Height == int.Parse(p[4]) && w.TopMost == bool.Parse(p[5]) && w.Visible == bool.Parse(p[6]) && w.PositionLocked == bool.Parse(p[7]), "fresh process restores window state: " + p[0]);
        }
        Assert(app.Data.Settings.AppearanceOverrides["todo"].BackgroundColor == "#F3E5D6" && app.Data.Settings.AppearanceOverrides["todo"].FontSize == 14 && app.Data.Settings.GlobalAppearance.FontSize == 12, "fresh process restores unchanged individual appearance settings");
        string reminderId = File.ReadAllText(Path.Combine(path, "reminder-id.txt")); int count = app.Data.ReminderHistory.Count(r => r.TaskId == reminderId);
        app.CheckReminders(DateTime.UtcNow);
        Assert(count == 1 && app.Data.ReminderHistory.Count(r => r.TaskId == reminderId) == count, "restart does not redeliver prior-page deadline");
    }
    static void RenderChecks(AppController app, string path)
    {
        Seed(app);
        foreach (var book in app.Data.Books)
        {
            book.CurrentPageId = book.Pages[0].Id;
            for (int i = 3; i <= 28; i++) book.Pages.Add(new NotePage { Title = "第 " + i + " 页 · 学习计划" });
        }
        app.SettingsChanged(); app.ShowAll();
        foreach (var layout in Layouts)
        {
            Switch(app, layout);
            if (layout == "Paper" || layout == "Journal")
            {
                var form = Form(app, "todo"); var tabs = Find<ListBox>(form, "paper-page-tabs");
                Assert(tabs.Visible && tabs.Items.Count == 28, layout + " directory holds many pages without creating more windows");
                tabs.SelectedIndex = 27; Pump(40); Assert(Book(app, "todo").CurrentPageId == Book(app, "todo").Pages[27].Id, layout + " page rail selects a distant page");
                tabs.SelectedIndex = 0; Pump(40);
            }
            foreach (string key in new[] { "todo", "ddl" })
            {
                var form = Form(app, key); form.Size = new Size(Math.Max(form.MinimumSize.Width, 500), 780); Pump(100);
                Find<FlowLayoutPanel>(form, "task-list").AutoScrollPosition = Point.Empty; Pump(30);
                Capture(form, Path.Combine(path, layout.ToLowerInvariant() + "-" + key + ".png"));
            }
            foreach (float font in new[] { 8F, 14F })
            {
                app.Data.Settings.GlobalAppearance.FontSize = font; app.SettingsChanged();
                foreach (string key in new[] { "todo", "ddl" })
                {
                    var form = Form(app, key); form.Size = form.MinimumSize; Pump(100); AssertFooterUsable(form, layout);
                    Capture(form, Path.Combine(path, layout.ToLowerInvariant() + "-" + key + "-small-font" + font.ToString("0") + ".png"));
                }
            }
            app.Data.Settings.GlobalAppearance.FontSize = 9; app.SettingsChanged();
        }
        app.OpenSettings(); Pump(100);
        var settings = Application.OpenForms.OfType<SettingsForm>().Single(); settings.Size = new Size(1250, 900); Find<ListBox>(settings, "settings-nav").SelectedIndex = 2; Pump(100); Capture(settings, Path.Combine(path, "settings-appearance.png"));
        var page = Find<Panel>(settings, "settings-page-2"); if (page.VerticalScroll.Visible) { page.AutoScrollPosition = new Point(0, page.VerticalScroll.Maximum); Pump(100); Capture(settings, Path.Combine(path, "settings-appearance-bottom.png")); }
        settings.Close();
        var emptyBook = Book(app, "todo"); emptyBook.CurrentPageId = emptyBook.Pages.Last().Id;
        foreach (var layout in Layouts) { Switch(app, layout); Assert(emptyBook.CurrentPageId == emptyBook.Pages.Last().Id, layout + " switch keeps the chosen current page (no stale page-tab write)"); var form = Form(app, "todo"); form.RefreshData(); form.Size = new Size(Math.Max(form.MinimumSize.Width, 500), 780); Pump(100); Capture(form, Path.Combine(path, layout.ToLowerInvariant() + "-empty.png")); }
        using (var g = Form(app, "todo").CreateGraphics()) Console.WriteLine("Rendered actual system DPI: " + g.DpiX);
        File.WriteAllText(Path.Combine(path, "CAPTURE-NOTES.txt"), "Images are DrawToBitmap renders of actual running WinForms controls, with isolated test data. They are not full desktop screenshots. Actual system DPI is logged. Genuine IME candidate selection, OS notification presentation, and other display scales still require manual checks.");
    }
    [STAThread] public static int Main(string[] args)
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        string path = Path.GetFullPath(args[0]), mode = args[1]; Directory.CreateDirectory(path);
        try
        {
            using (var app = new AppController(path, null, false))
            {
                Pump(150);
                if (mode == "read") ReadChecks(app, path);
                else if (mode == "render") RenderChecks(app, path);
                else WriteChecks(app, path, mode == "card");
                app.Shutdown();
            }
            Console.WriteLine("NOTEBOOK LAYOUT " + mode + " PASSED: " + path); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
