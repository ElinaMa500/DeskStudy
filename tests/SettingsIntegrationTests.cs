using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using DeskStudy;

public static class SettingsIntegrationTests
{
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
    private static IEnumerable<Control> Descendants(Control control) { foreach (Control c in control.Controls) { yield return c; foreach (var child in Descendants(c)) yield return child; } }
    private static T Find<T>(Control root, string name) where T : Control { return (T)Descendants(root).First(c => c.Name == name); }
    private static void Pump(int milliseconds) { var clock = Stopwatch.StartNew(); while (clock.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(10); } }
    private static void Capture(Form form, string path) { File.WriteAllLines(path + ".layout.txt", Descendants(form).Where(c => c.Visible).Select(c => c.GetType().Name + " | " + c.Name + " | " + c.Text.Replace("\n", " ").Replace("\r", "") + " | " + c.Bounds + " | Min=" + c.MinimumSize + " | Max=" + c.MaximumSize + " | Auto=" + c.AutoSize + " | Anchor=" + c.Anchor + " | parent=" + c.Parent.Bounds).ToArray()); using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(path); } }
    [STAThread] public static int Main(string[] args)
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        string path = Path.GetFullPath(args[0]), phase = args[1];
        try
        {
            using (var app = new AppController(path, null, false))
            {
                Pump(150);
                if (phase == "notify")
                {
                    using (var icon = new NotifyIcon { Icon = app.AppIcon, Text = "课笺通知验证", Visible = true })
                    {
                        Pump(300);
                        Assert(NativeNotification.Show(icon, app.AppIcon, "桌面课笺 · 静音通知测试", "这是设置中心的系统通知验证。", false), "Windows Shell accepts native silent notification");
                        Pump(700); icon.Visible = false;
                    }
                }
                else if (phase == "read")
                {
                    Assert(app.Data.Version == 2 && app.Data.Settings.GlobalAppearance.Theme == "Dark", "fresh process restores global theme and v2 settings");
                    Assert(Math.Abs(app.Widgets.First(w => w.WidgetKey == "todo").Opacity - .95) < .01 && Math.Abs(app.Widgets.First(w => w.WidgetKey == "ddl").Opacity - .82) < .01, "fresh process restores component override and global opacity");
                    var todo = app.Data.Books.First(b => b.Id == "todo");
                    Assert(todo.Name == "设置验证 Todo" && todo.Pages[0].Archived && todo.Pages[0].Text == "归档仍然保留的内容" && todo.CurrentPageId == todo.Pages[1].Id, "fresh process restores archive, notebook name, text and displayed page");
                    Assert(todo.Pages[0].Tasks[0].Completed && app.Data.Events.Count == 1, "appearance and layout reset preserve task completion and courses");
                    Assert(app.Data.Settings.Calendar.DefaultView == "Month" && app.Data.Settings.Calendar.WeekStartDay == 0 && app.Data.Settings.Reminders.DefaultLeadMinutes == 45, "fresh process restores calendar and reminder defaults");
                    foreach (string line in File.ReadAllLines(Path.Combine(path, "settings-expected-windows.txt")))
                    {
                        var p = line.Split('|'); var w = app.Widgets.First(f => f.WidgetKey == p[0]); var saved = app.Data.Windows[p[0]];
                        Assert(w.Left == int.Parse(p[1]) && w.Top == int.Parse(p[2]) && w.Width == int.Parse(p[3]) && w.Height == int.Parse(p[4]) && w.TopMost == bool.Parse(p[5]) && w.Visible == bool.Parse(p[6]) && saved.PositionLocked == bool.Parse(p[7]), "fresh process restores layout / visibility / pin / lock: " + p[0]);
                    }
                    Assert(app.Data.Settings.SavedLayout.Count == 3 && app.Data.Settings.AutoSaveDelayMs == 1000 && app.Data.Settings.LaunchAtStartup, "fresh process restores saved layout, autosave delay and startup preference (isolated mode)");
                }
                else
                {
                    WidgetSettingsChecks.Run(app);
                    var todo = app.Data.Books.First(b => b.Id == "todo");
                    todo.Name = "设置验证 Todo"; var first = todo.Pages[0]; first.Text = "归档仍然保留的内容"; first.Tasks.Add(new TaskItem { Text = "已完成任务保持", Completed = true });
                    app.Data.Events.Add(new CalendarEvent { Title = "设置测试课程", Date = "2026-09-07", RepeatEndDate = "2026-12-18", RepeatWeeks = 2, WeekDays = new List<int> { 1 } });
                    app.Save(); app.OpenSettings(); Pump(150);
                    var settings = Application.OpenForms.OfType<SettingsForm>().Single(); var navigation = Find<ListBox>(settings, "settings-nav");
                    Assert(navigation.Items.Count == 7, "settings center exposes all seven pages");
                    Find<CheckBox>(settings, "overview-calendar-toggle").Checked = false;
                    Assert(!app.Widgets[0].Visible && !app.Data.Windows["calendar"].Visible, "overview checkbox hides actual calendar and updates model");
                    app.Widgets[1].Hide(); Pump(30);
                    Assert(!Find<CheckBox>(settings, "overview-todo-toggle").Checked, "desktop visibility synchronizes back to settings center");
                    Find<Button>(settings, "show-all").PerformClick(); Assert(app.Widgets.All(w => w.Visible), "show-all reveals three independent windows");
                    navigation.SelectedIndex = 1;
                    var calendar = app.Widgets.First(w => w.WidgetKey == "calendar");
                    calendar.Location = new Point(130, 140); Pump(50);
                    Assert((int)Find<NumericUpDown>(settings, "layout-calendar-x").Value == calendar.Left, "desktop move synchronizes numeric position editor");
                    Find<CheckBox>(settings, "layout-calendar-locked").Checked = true;
                    Find<CheckBox>(settings, "layout-calendar-pin").Checked = true;
                    Assert(calendar.PositionLocked && calendar.TopMost, "layout controls apply pin and position lock immediately");
                    SendMessage(calendar.Handle, 0x112, new IntPtr(0xF030), IntPtr.Zero);
                    Assert(calendar.WindowState == FormWindowState.Normal, "position lock rejects native maximize command");
                    app.SaveLayout(); int savedX = calendar.Left;
                    calendar.Location = new Point(230, 240); app.RestoreSavedLayout(); Assert(calendar.Left == savedX, "saved layout restores actual window geometry");
                    app.Data.Windows["calendar"].PositionLocked = false; calendar.WindowState = FormWindowState.Maximized; Pump(30);
                    app.RestoreSavedLayout(); Assert(calendar.WindowState == FormWindowState.Normal && calendar.Left == savedX && calendar.PositionLocked, "saved layout survives native restore callbacks from maximized window");
                    foreach (var w in app.Widgets) w.Location = new Point(-9000, -9000);
                    Find<Button>(settings, "rescue-windows").PerformClick(); Pump(50);
                    Assert(app.Widgets.All(w => w.Visible && Screen.AllScreens.Any(s => s.WorkingArea.Contains(w.Bounds))), "rescue retrieves all three offscreen widgets to a visible working area");
                    navigation.SelectedIndex = 2;
                    Find<NumericUpDown>(settings, "appearance-opacity").Value = 82;
                    Find<NumericUpDown>(settings, "appearance-font").Value = 11;
                    Find<ComboBox>(settings, "appearance-theme").SelectedIndex = 1;
                    Assert(app.Widgets.All(w => Math.Abs(w.Opacity - .82) < .01) && Math.Abs(calendar.Font.SizeInPoints - 11) < .1, "appearance controls preview opacity, font and theme in live widgets");
                    app.SettingsChanged(); app.SettingsChanged();
                    Assert(Math.Abs(calendar.Font.SizeInPoints - 11) < .1, "repeated synchronization never compounds font size");
                    app.Data.Settings.AppearanceOverrides["todo"] = new AppearanceOptions { Theme = "Light", BackgroundColor = "#FFF4D6", Opacity = .95, FontSize = 10 };
                    app.SettingsChanged(); Assert(Math.Abs(app.Widgets[1].Opacity - .95) < .01, "single component override is independent of global preview");
                    app.ResetAppearance("todo"); Assert(Math.Abs(app.Widgets[1].Opacity - .82) < .01, "follow-global removes per-component override");
                    app.Data.Settings.AppearanceOverrides["todo"] = new AppearanceOptions { Theme = "Light", BackgroundColor = "#FFF4D6", Opacity = .95, FontSize = 10 };
                    app.AddPage("todo"); app.SetPageArchived("todo", first.Id, true);
                    app.SettingsChanged(); settings.RefreshData();
                    Assert(first.Archived && first.Tasks[0].Completed && todo.Pages.Count == 2, "archiving preserves all content and completion state");
                    Assert(Descendants(app.Widgets[1]).OfType<ComboBox>().First(c => c.AccessibleName == "页面列表").Items.Count == 1, "archived pages are removed from desktop page selector without deleting content");
                    var ddl = app.Data.Books.First(b => b.Id == "ddl"); var due = DateTime.Now.AddMinutes(1);
                    var task = new TaskItem { Text = "归档且隐藏仍提醒", DueLocal = due.ToString("yyyy-MM-ddTHH:mm:ss"), ReminderMinutes = 0 }; ddl.Pages[0].Tasks.Add(task);
                    app.SetPageArchived("ddl", ddl.Pages[0].Id, true); app.HideAll();
                    app.Data.Settings.Reminders.QuietHoursEnabled = true; app.Data.Settings.Reminders.QuietStart = "00:00"; app.Data.Settings.Reminders.QuietEnd = "00:00";
                    var batches = new List<ReminderRecord>(); app.RemindersDelivered += delegate(List<ReminderRecord> rows) { batches.AddRange(rows); };
                    app.CheckReminders(TimeUtil.LocalToUtc(due.AddSeconds(2), task.TimeZoneId));
                    Assert(batches.Count == 0 && task.DueNotifiedKey == "", "quiet hours defer archived task without consuming reminder");
                    app.Data.Settings.Reminders.QuietHoursEnabled = false;
                    app.CheckReminders(TimeUtil.LocalToUtc(due.AddSeconds(40), task.TimeZoneId));
                    Assert(batches.Count(r => r.TaskId == task.Id) == 1, "after quiet hours hidden archived page still triggers reminder");
                    app.ResetLayout(); Assert(todo.Pages[0].Archived && todo.Pages[0].Tasks[0].Completed && app.Data.Events.Count == 1, "resetting layout preserves courses, completed tasks and archived pages");
                    app.ResetAppearance("all"); Assert(todo.Pages[0].Archived && todo.Pages[0].Tasks[0].Completed && app.Data.Events.Count == 1, "resetting all appearance preserves courses, tasks and archived pages");
                    app.Data.Settings.GlobalAppearance = new AppearanceOptions { Theme = "Dark", BackgroundColor = "#252B36", FontSize = 11, Opacity = .82 };
                    app.Data.Settings.AppearanceOverrides["todo"] = new AppearanceOptions { Theme = "Light", BackgroundColor = "#FFF4D6", Opacity = .95, FontSize = 10 };
                    app.Data.Settings.Calendar.DefaultView = "Month"; app.Data.Settings.Calendar.WeekStartDay = 0; app.Data.Settings.Reminders.DefaultLeadMinutes = 45; app.Data.Settings.AutoSaveDelayMs = 1000; app.SetStartup(true); app.SettingsChanged();
                    app.BackupNow(); string backup = Directory.GetFiles(path, "backup-*.json").OrderBy(f => f).Last();
                    app.Data.ReminderHistory.Clear(); app.OpenReminders();
                    todo.Name = "恢复前必须保留"; app.Save(); app.RestoreBackup(backup);
                    Assert(app.Data.Books[0].Name == "设置验证 Todo" && Directory.GetFiles(path, "before-import-*.json").Any(f => File.ReadAllText(f).Contains("恢复前必须保留")), "backup restore preserves current state before replacement");
                    var reminderCenter = Application.OpenForms.OfType<ReminderCenter>().Single();
                    Assert(Descendants(reminderCenter).OfType<ListView>().Single().Items[0].Text == app.Data.ReminderHistory.Last().Title, "restoring a backup refreshes an already open reminder history window");
                    reminderCenter.Close();
                    app.SaveLayout();
                    var ddlState = app.Data.Windows["ddl"]; ddlState.PositionLocked = true; ddlState.TopMost = true; app.UpdateWindow("ddl", ddlState); app.SetWidgetVisible("calendar", false);
                    if (phase == "render")
                    {
                        foreach (var book in app.Data.Books)
                        {
                            var current = book.Pages.First(p => p.Id == book.CurrentPageId);
                            current.Title = book.Id == "todo" ? "本周任务" : "十月 DDL";
                            current.Text = "历史页面完整保留，归档任务继续提醒。";
                            current.Tasks.Add(new TaskItem { Text = "完成实验报告，核对数据并整理课堂笔记", DueLocal = DateTime.Now.AddDays(2).ToString("yyyy-MM-ddTHH:mm:ss") });
                            current.Tasks.Add(new TaskItem { Text = "阅读本周教材章节", Completed = true });
                        }
                        app.Data.Events.Add(new CalendarEvent { Title = "计算机科学 · 实验课", Date = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd"), StartTime = "10:00", EndTime = "11:30", Location = "教学楼 302" });
                        app.SettingsChanged();
                        app.ShowAll(); settings.Size = new Size(1250, 900);
                        for (int i = 0; i < 7; i++)
                        {
                            navigation.SelectedIndex = i; Pump(100); Capture(settings, Path.Combine(path, "settings-" + i + ".png"));
                            var page = Find<Panel>(settings, "settings-page-" + i);
                            if (page.VerticalScroll.Visible) { page.AutoScrollPosition = new Point(0, page.VerticalScroll.Maximum); Pump(50); Capture(settings, Path.Combine(path, "settings-" + i + "-bottom.png")); }
                        }
                        foreach (var w in app.Widgets) { w.Size = new Size(Math.Max(w.MinimumSize.Width, w.WidgetKey == "calendar" ? 1100 : 510), 800); Pump(50); Capture(w, Path.Combine(path, "styled-" + w.WidgetKey + ".png")); }
                        app.Data.Settings.GlobalAppearance.FontSize = 14; app.Data.Settings.AppearanceOverrides["todo"].FontSize = 14; app.SettingsChanged();
                        foreach (var w in app.Widgets) { Pump(50); Capture(w, Path.Combine(path, "max-font-" + w.WidgetKey + ".png")); }
                        Descendants(calendar).OfType<Button>().First(b => b.Text == "周").PerformClick(); Pump(50); Capture(calendar, Path.Combine(path, "max-font-week.png"));
                    }
                    settings.Close(); Pump(30);
                    Assert(!app.Exiting && app.Widgets.All(w => !w.IsDisposed), "closing settings keeps desktop widgets and application alive");
                    app.SetWidgetVisible("todo", true); Find<Button>(app.Widgets[1], "open-settings").PerformClick(); Pump(30);
                    Assert(Application.OpenForms.OfType<SettingsForm>().Count(f => f.Visible) == 1, "component gear opens settings again after closing");
                    Application.OpenForms.OfType<SettingsForm>().Single().Close();
                    var expected = app.Widgets.Select(w => { w.Remember(); var s = app.Data.Windows[w.WidgetKey]; return w.WidgetKey + "|" + s.X + "|" + s.Y + "|" + s.Width + "|" + s.Height + "|" + s.TopMost + "|" + s.Visible + "|" + s.PositionLocked; }).ToArray();
                    File.WriteAllLines(Path.Combine(path, "settings-expected-windows.txt"), expected);
                    app.Save(); Assert(app.Flush(), "settings and content flush before process exit");
                }
                app.Shutdown();
            }
            Console.WriteLine("SETTINGS INTEGRATION " + phase + " PASSED"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
