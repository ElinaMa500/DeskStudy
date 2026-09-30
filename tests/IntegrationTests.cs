using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using DeskStudy;

public static class IntegrationTests
{
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static IEnumerable<Control> Descendants(Control c) { foreach (Control child in c.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
    static Control Find(Form f, string name) { return Descendants(f).First(c => c.Name == name); }
    static void Pump(int ms) { var clock = Stopwatch.StartNew(); while (clock.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(15); } }
    [STAThread] public static int Main(string[] args)
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        string path = Path.GetFullPath(args[0]);
        try
        {
            using (var app = new AppController(path, null, false))
            {
                Pump(200);
                if (args[1] == "render")
                {
                    var monday = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));
                    app.Data.Events.Add(new CalendarEvent { Title = "量子力学", Date = monday.ToString("yyyy-MM-dd"), StartTime = "09:00", EndTime = "10:30", RepeatWeeks = 2, RepeatEndDate = monday.AddMonths(3).ToString("yyyy-MM-dd"), WeekDays = new List<int> { 1, 3 }, Location = "物理楼 201", Notes = "双周课程" });
                    app.Data.Events.Add(new CalendarEvent { Title = "高等数学", Date = monday.AddDays(1).ToString("yyyy-MM-dd"), StartTime = "11:00", EndTime = "12:00", Color = "#7196B1", Location = "教学楼 302" });
                    var todo = app.Data.Books.First(b => b.Id == "todo"); todo.Pages[0].Title = "本周任务"; todo.Pages[0].Text = "把重要的事留在桌面。\r\n周三课后整理课堂笔记。";
                    todo.Pages[0].Tasks.Add(new TaskItem { Text = "整理第一章课堂笔记", Completed = true });
                    todo.Pages[0].Tasks.Add(new TaskItem { Text = "完成习题集并检查推导过程", DueLocal = DateTime.Now.AddHours(5).ToString("yyyy-MM-ddTHH:mm:ss") });
                    var ddl = app.Data.Books.First(b => b.Id == "ddl"); ddl.Pages[0].Title = "十月 DDL"; ddl.Pages[0].Text = "所有旧页上的任务也会继续提醒。";
                    ddl.Pages[0].Tasks.Add(new TaskItem { Text = "提交物理实验报告", DueLocal = DateTime.Now.AddDays(2).ToString("yyyy-MM-ddTHH:mm:ss"), ReminderMinutes = 60 });
                    app.Save();
                    foreach (var w in app.Widgets)
                    {
                        w.Size = new Size(Math.Max(w.MinimumSize.Width, w.WidgetKey == "calendar" ? 1050 : 500), Math.Max(w.MinimumSize.Height, 780));
                        w.Show(); Pump(100); Capture(w, Path.Combine(path, w.WidgetKey + ".png"));
                    }
                    var calendarForm = app.Widgets.First(w => w.WidgetKey == "calendar");
                    Descendants(calendarForm).OfType<Button>().First(b => b.Text == "月").PerformClick(); Pump(100); Capture(calendarForm, Path.Combine(path, "month.png"));
                    using (var dialog = new CalendarEditor(app.Data.Events[0], true, true)) { dialog.Show(); Pump(100); Capture(dialog, Path.Combine(path, "calendar-editor.png")); dialog.Close(); }
                    using (var dialog = new TaskEditorDialog(ddl.Pages[0].Tasks[0], Ui.Accent)) { dialog.Show(); Pump(100); Capture(dialog, Path.Combine(path, "task-editor.png")); dialog.Close(); }
                    using (var g = calendarForm.CreateGraphics()) Console.WriteLine("Rendered system DPI: " + g.DpiX);
                    app.Shutdown(); Console.WriteLine("RENDER PASSED: " + path); return 0;
                }
                else if (args[1] == "write")
                {
                    var calendar = new CalendarEvent { Title = "验证：学期双周课程", Date = "2026-09-07", RepeatEndDate = "2026-12-18", RepeatWeeks = 2, WeekDays = new List<int> { 1 }, Location = "教学楼 201", Notes = "仅用于隔离测试" };
                    app.Data.Events.Add(calendar); CalendarEngine.CancelOccurrence(calendar, "2026-09-21");
                    var todo = app.Data.Books.First(b => b.Id == "todo"); todo.Name = "Todo 验证";
                    var first = todo.Pages[0]; first.Title = "本周任务"; first.Text = "第 1 页文字保持不变";
                    var completed = new TaskItem { Text = "已完成的第 1 页任务" }; first.Tasks.Add(completed);
                    var ddl = app.Data.Books.First(b => b.Id == "ddl"); ddl.Pages[0].Title = "第 1 页 DDL";
                    var pending = new TaskItem { Text = "跨页实时提醒", DueLocal = DateTime.Now.AddSeconds(10).ToString("yyyy-MM-ddTHH:mm:ss"), ReminderMinutes = 0 };
                    var canceled = new TaskItem { Text = "提前完成不提醒", DueLocal = pending.DueLocal, ReminderMinutes = 0, Completed = true };
                    ddl.Pages[0].Tasks.Add(pending); ddl.Pages[0].Tasks.Add(canceled);
                    app.Save(); Pump(150);
                    var todoForm = app.Widgets.First(w => w.WidgetKey == "todo");
                    var ddlForm = app.Widgets.First(w => w.WidgetKey == "ddl");
                    var checkbox = (CheckBox)Find(todoForm, "task-checkbox-" + completed.Id);
                    checkbox.Checked = true;
                    Assert(completed.Completed, "real checkbox updates completion state");
                    ((Button)Find(todoForm, "new-page")).PerformClick();
                    Assert(todo.Pages.Count == 2 && todo.CurrentPageId == todo.Pages[1].Id, "real new-page button retains first page and selects page 2");
                    ((Button)Find(todoForm, "previous-page")).PerformClick();
                    Assert(todo.CurrentPageId == first.Id && first.Text == "第 1 页文字保持不变" && first.Tasks[0].Completed, "real previous-page button restores content and checked state");
                    ((Button)Find(ddlForm, "new-page")).PerformClick();
                    Assert(ddl.Pages.Count == 2 && ddl.CurrentPageId == ddl.Pages[1].Id, "DDL page 2 active before first-page deadline");
                    var delivered = new List<ReminderRecord>(); app.RemindersDelivered += delegate(List<ReminderRecord> batch) { delivered.AddRange(batch); };
                    Pump(16000);
                    Assert(delivered.Count(r => r.TaskId == pending.Id) == 1, "live WinForms timer triggers one reminder from hidden page 1");
                    Assert(delivered.All(r => r.TaskId != canceled.Id), "completed task never triggers live timer reminder");
                    var expected = new List<string>();
                    int offset = 0;
                    foreach (var w in app.Widgets)
                    {
                        // Side by side: since 1.5.1 overlapping widgets are moved apart at startup.
                        w.WindowState = FormWindowState.Normal; w.Location = new Point(new[] { 40, 900, 1390 }[offset], 90 + offset * 35);
                        w.Size = new Size(Math.Max(w.MinimumSize.Width, 450 + offset * 20), Math.Max(w.MinimumSize.Height, 540));
                        w.TopMost = offset == 1; w.Remember();
                        var state = app.Data.Windows[w.WidgetKey]; expected.Add(w.WidgetKey + "|" + state.X + "|" + state.Y + "|" + state.Width + "|" + state.Height + "|" + state.TopMost);
                        offset++;
                    }
                    app.Widgets.First(w => w.WidgetKey == "calendar").Hide();
                    File.WriteAllLines(Path.Combine(path, "expected-windows.txt"), expected.ToArray());
                    app.Save(); Assert(app.Flush(), "all application data flushed before process exit");
                }
                else
                {
                    var todo = app.Data.Books.First(b => b.Id == "todo"); var ddl = app.Data.Books.First(b => b.Id == "ddl");
                    Assert(todo.Name == "Todo 验证" && todo.Pages.Count == 2 && todo.Pages[0].Text == "第 1 页文字保持不变" && todo.Pages[0].Tasks[0].Completed, "fresh process restores Todo name, pages, text and checked task");
                    Assert(todo.CurrentPageId == todo.Pages[0].Id && ddl.Pages.Count == 2 && ddl.CurrentPageId == ddl.Pages[1].Id, "fresh process restores independent notebook page selections");
                    var occurrences = CalendarEngine.GetOccurrences(app.Data, new DateTime(2026, 9, 1), new DateTime(2026, 12, 31));
                    Assert(occurrences.Count == 7 && occurrences.All(o => o.Date != "2026-09-21"), "fresh process restores term recurrence and single canceled occurrence");
                    foreach (var line in File.ReadAllLines(Path.Combine(path, "expected-windows.txt")))
                    {
                        string[] p = line.Split('|'); var w = app.Widgets.First(f => f.WidgetKey == p[0]);
                        Assert(w.Left == Int32.Parse(p[1]) && w.Top == Int32.Parse(p[2]) && w.Width == Int32.Parse(p[3]) && w.Height == Int32.Parse(p[4]) && w.TopMost == Boolean.Parse(p[5]), "fresh process restores window position, size and pin: " + p[0]);
                    }
                    Assert(!app.Widgets.First(w => w.WidgetKey == "calendar").Visible, "fresh process restores hidden calendar state");
                    int history = app.Data.ReminderHistory.Count; app.CheckReminders(DateTime.UtcNow);
                    Assert(app.Data.ReminderHistory.Count == history, "restart does not repeat already delivered reminders");
                }
                app.Shutdown();
            }
            Console.WriteLine("INTEGRATION " + args[1] + " PASSED"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    static void Capture(Form form, string path)
    {
        File.WriteAllLines(path + ".layout.txt", Descendants(form).Where(c => c is Button || c is Label || c is TableLayoutPanel).Select(c => c.GetType().Name + " | " + c.Text.Replace("\r", "").Replace("\n", " ") + " | " + c.Bounds + " | Client=" + c.ClientRectangle + " | Font=" + c.Font.SizeInPoints + " h=" + c.Font.Height + " | Padding=" + c.Padding + " | parent=" + c.Parent.Bounds).ToArray());
        using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png); }
    }
}
