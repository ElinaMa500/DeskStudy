using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using DeskStudy;

// Outlook calendar (read-only), end to end with a stand-in download: sync, display, categories, errors, cache, reminders.
public static class OutlookChecks
{
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hwnd, IntPtr dc, uint flags);
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static void Pump(int ms) { var clock = Stopwatch.StartNew(); while (clock.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(10); } }
    static void PumpUntil(Func<bool> done, int ms) { var clock = Stopwatch.StartNew(); while (!done() && clock.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(10); } }
    static IEnumerable<Control> All(Control root) { foreach (Control c in root.Controls) { yield return c; foreach (Control d in All(c)) yield return d; } }
    static string shots;
    const string Link = "https://outlook.office365.com/owa/calendar/example/reachcalendar.ics";

    static void Shot(string name, Control w)
    {
        using (var bmp = new Bitmap(w.Width, w.Height))
        {
            using (var g = Graphics.FromImage(bmp)) { IntPtr dc = g.GetHdc(); PrintWindow(w.Handle, dc, 2); g.ReleaseHdc(dc); }
            bmp.Save(Path.Combine(shots, name + ".png"));
        }
    }
    static string L(DateTime day, string time) { return day.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "T" + time.Replace(":", "") + "00"; }
    static string Event(string uid, string title, DateTime day, string start, string end, string place, string category)
    {
        return "BEGIN:VEVENT\r\nUID:" + uid + "\r\nSUMMARY:" + title + "\r\nDTSTART;TZID=China Standard Time:" + L(day, start) + "\r\nDTEND;TZID=China Standard Time:" + L(day, end)
            + (place == "" ? "" : "\r\nLOCATION:" + place) + (category == "" ? "" : "\r\nCATEGORIES:" + category) + "\r\nEND:VEVENT\r\n";
    }
    // A week like a student's Outlook: lectures by category, a meeting, a multi-day all-day event.
    static string Calendar(DateTime monday, string zoneId)
    {
        string body = Event("tutor", "Tutor meeting", monday, "09:00", "09:30", "Teams", "")
            + "BEGIN:VEVENT\r\nUID:analysis\r\nSUMMARY:Analysis lecture\r\nLOCATION:67/1033\r\nCATEGORIES:Lectures\r\nDTSTART;TZID=China Standard Time:" + L(monday.AddDays(1), "09:00") + "\r\nDTEND;TZID=China Standard Time:" + L(monday.AddDays(1), "11:00") + "\r\nRRULE:FREQ=WEEKLY;BYDAY=TU,TH;COUNT=6\r\nEND:VEVENT\r\n"
            + Event("lab", "Lab induction", monday.AddDays(2), "10:30", "12:30", "B59 lab", "Green category")
            + Event("seminar", "Seminar", monday.AddDays(4), "08:30", "10:00", "2/1089", "Red category")
            + Event("vector", "Tutorial - Vector Calculus", monday.AddDays(3), "11:00", "12:00", "02/1089", "")
            + Event("mechanics", "PC - Classical Mechanics group work", monday.AddDays(4), "10:00", "11:00", "", "")
            + Event("wave", "Wave 5 (Computational Lab)", monday.AddDays(5), "10:00", "12:00", "B59/1257", "")
            + "BEGIN:VEVENT\r\nUID:reading\r\nSUMMARY:Reading week\r\nDTSTART;VALUE=DATE:" + monday.ToString("yyyyMMdd") + "\r\nDTEND;VALUE=DATE:" + monday.AddDays(5).ToString("yyyyMMdd") + "\r\nEND:VEVENT\r\n"
            + "BEGIN:VEVENT\r\nUID:open\r\nSUMMARY:Open day\r\nCATEGORIES:Purple category\r\nDTSTART;VALUE=DATE:" + monday.AddDays(2).ToString("yyyyMMdd") + "\r\nDTEND;VALUE=DATE:" + monday.AddDays(3).ToString("yyyyMMdd") + "\r\nEND:VEVENT\r\n";
        // In the computer's own zone the times show unchanged, whatever zone the test machine uses.
        return "BEGIN:VCALENDAR\r\nVERSION:2.0\r\n" + body.Replace("China Standard Time", zoneId) + "END:VCALENDAR\r\n";
    }
    static CalendarEvent Course(string title, string date, string start, string end, string color)
    {
        return new CalendarEvent { Title = title, Date = date, StartTime = start, EndTime = end, Color = color, Location = "教学楼", RepeatWeeks = 0, RepeatEndDate = date };
    }

    public static void Run(AppController app, string path)
    {
        shots = Path.Combine(path, "outlook"); Directory.CreateDirectory(shots);
        var calendar = (CalendarForm)app.Widgets.First(w => w.WidgetKey == "calendar");
        DateTime monday = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));
        Func<int, string> day = n => monday.AddDays(n).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string served = Calendar(monday, TimeZoneInfo.Local.Id); int downloads = 0; string asked = "";
        app.OutlookDownload = delegate(string address) { downloads++; asked = address; return served; };

        app.Data.Events.Clear();
        app.Data.Events.Add(Course("高等数学", day(0), "08:00", "09:40", "#6C9385"));
        app.Data.Events.Add(Course("英语写作", day(2), "08:30", "10:00", "#B08968"));
        app.Data.Events.Add(Course("组会", day(3), "09:00", "10:30", "#8A7FB5"));
        app.Data.Settings.NotebookLayout = "Journal"; app.Data.Settings.Calendar.DefaultView = "Week"; app.Data.Settings.Calendar.WorkWeek = false;
        app.Data.Settings.GlobalAppearance.FontSize = 9; app.Data.Settings.GlobalAppearance.Opacity = 1;
        foreach (var w in app.Widgets) app.Data.Windows[w.WidgetKey].PositionLocked = false;
        app.ShowAll(); app.SettingsChanged(); Pump(300);
        foreach (var w in app.Widgets.Where(w => w != calendar)) w.Hide();
        calendar.Bounds = new Rectangle(1074, 493, 846, 647); Pump(200);
        Assert(app.Outlook.Events.Count == 0 && downloads == 0, "without a link nothing is downloaded");

        // Settings: paste the link and save.
        app.OpenSettings(); Pump(300);
        var settings = Application.OpenForms.OfType<SettingsForm>().Single(); settings.Size = new Size(1250, 1000); Pump(200);
        ((ListBox)All(settings).First(c => c.Name == "settings-nav")).SelectedIndex = 3; Pump(300);
        var url = (TextBox)All(settings).First(c => c.Name == "outlook-url");
        url.Text = "webcal://outlook.office365.com/owa/calendar/example/reachcalendar.ics";
        ((Button)All(settings).First(c => c.Name == "outlook-save")).PerformClick();
        PumpUntil(() => app.Outlook.LastSyncUtc != "", 5000); Pump(300);
        Assert(downloads == 1 && asked.StartsWith("https://outlook.office365.com/") && app.Data.Settings.Calendar.Outlook.Url.StartsWith("webcal://"), "saving the link downloads it once (webcal fetched over https)");
        var events = app.Outlook.Events;
        Assert(events.Count(e => e.Uid == "analysis") == 6 - events.Count(e => e.Uid == "analysis" && e.StartLocal < DateTime.Today.AddDays(-OutlookLogic.DaysBack)) && events.Any(e => e.Uid == "reading" && e.AllDay), "the series is expanded and the all-day event read");
        Assert(app.Outlook.Categories.SequenceEqual(new[] { "Green category", "Lectures", "Purple category", "Red category" }.OrderBy(c => c, StringComparer.CurrentCulture)), "categories found: " + String.Join(", ", app.Outlook.Categories));
        var status = All(settings).First(c => c.Name == "outlook-status");
        Assert(status.Text.Contains(events.Count.ToString()) && !status.Text.Contains("reachcalendar"), "the status shows the count and never the link: " + status.Text);
        var swatches = All(settings).Where(c => c.Name.StartsWith("outlook-category-")).ToList();
        Assert(swatches.Count == 4 && swatches.Any(s => s.Text.Contains("Lectures")), "one color swatch per category");
        var titleSwatches = All(settings).Where(c => c.Name.StartsWith("outlook-title-")).ToList();
        Assert(titleSwatches.Count == OutlookLogic.Titles(app.Outlook).Count && titleSwatches.Any(s => s.Text.Contains("Tutorial - Vector Calculus")), "one color swatch per event name (" + titleSwatches.Count + ")");
        var page = All(settings).OfType<Panel>().First(p => p.Name == "settings-page-3" && p.Visible);
        page.ScrollControlIntoView(url); Pump(200);
        Shot("settings-outlook", settings);
        settings.Close(); Pump(100);

        // The calendar: course colors stay, Outlook events take their category's color, all-day row on top.
        var surface = All(calendar).OfType<CalendarSurface>().First();
        calendar.RefreshData(); Pump(200);
        var shown = ((System.Collections.IEnumerable)typeof(CalendarSurface).GetField("items", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(surface)).Cast<Occurrence>().ToList();
        Assert(shown.Count(o => o.External != null) >= 4 && shown.First(o => o.Title == "Seminar").Color == "#D13438" && shown.First(o => o.Title == "Lab induction").Color == "#13A10E" && shown.First(o => o.Title == "Tutor meeting").Color == OutlookLogic.ColorFor(app.Data.Settings.Calendar.Outlook, new OutlookEvent { Title = "Tutor meeting" }),
            "Outlook events use their category color (Red, Green), or Outlook blue without one");
        Assert(shown.First(o => o.Title == "Seminar").StartTime == "08:30", "times show as in Outlook");
        app.Data.Settings.Calendar.Outlook.CategoryColors["Lectures"] = "#8764B8"; app.SettingsChanged(); Pump(200);
        shown = ((System.Collections.IEnumerable)typeof(CalendarSurface).GetField("items", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(surface)).Cast<Occurrence>().ToList();
        Assert(shown.Where(o => o.Title == "Analysis lecture").All(o => o.Color == "#8764B8"), "a color chosen for a named category is used");
        app.SetOutlookTitleColor("Seminar", "#112233"); Pump(200);
        shown = ((System.Collections.IEnumerable)typeof(CalendarSurface).GetField("items", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(surface)).Cast<Occurrence>().ToList();
        Assert(shown.Where(o => o.Title == "Seminar").All(o => o.Color == "#112233"), "a color chosen for an event's name wins over its category, for every event of that name");
        app.Data.Settings.Calendar.Outlook.TitleColors.Remove("Seminar"); app.SettingsChanged(); Pump(200);
        int headerWithAllDay = All(calendar).First(c => c.Name == "calendar-day-header").Height;
        foreach (string layout in new[] { "Journal", "Clean", "Original" })
        {
            app.Data.Settings.NotebookLayout = layout; app.SettingsChanged(); Pump(400);
            surface.AutoScrollPosition = new Point(0, surface.HourPixels * 7); Pump(200);
            Shot("week-" + layout, calendar);
        }
        app.Data.Settings.NotebookLayout = "Journal"; app.SettingsChanged(); Pump(300);
        All(calendar).OfType<Button>().First(b => b.Name == "calendar-month").PerformClick(); Pump(300);
        Shot("month-Journal", calendar);
        All(calendar).OfType<Button>().First(b => b.Name == "calendar-week").PerformClick(); Pump(200);

        // Hidden: no Outlook events and no all-day row.
        app.Data.Settings.Calendar.Outlook.Show = false; app.SettingsChanged(); Pump(300);
        shown = ((System.Collections.IEnumerable)typeof(CalendarSurface).GetField("items", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(surface)).Cast<Occurrence>().ToList();
        Assert(shown.All(o => o.External == null) && All(calendar).First(c => c.Name == "calendar-day-header").Height < headerWithAllDay, "switched off, Outlook events and the all-day row disappear");
        app.Data.Settings.Calendar.Outlook.Show = true; app.SettingsChanged(); Pump(300);

        // A failed refresh keeps what was there and says why, without the link.
        int before = app.Outlook.Events.Count;
        app.OutlookDownload = delegate(string unused) { throw new WebException("offline", WebExceptionStatus.NameResolutionFailure); };
        app.SyncOutlook(); PumpUntil(() => !app.OutlookSyncing, 5000); Pump(100);
        Assert(app.Outlook.Events.Count == before && app.Outlook.LastError != "" && !app.Outlook.LastError.Contains("outlook.office365"), "offline: the last calendar stays and the reason is kept: " + app.Outlook.LastError);
        app.OutlookDownload = delegate(string unused) { return "<html>not a calendar</html>"; };
        app.SyncOutlook(); PumpUntil(() => !app.OutlookSyncing, 5000); Pump(100);
        Assert(app.Outlook.Events.Count == before && app.Outlook.LastError.Length > 0, "a page that is not a calendar is refused");
        app.OutlookDownload = delegate(string unused) { return served; };
        app.SyncOutlook(); PumpUntil(() => !app.OutlookSyncing, 5000); Pump(100);
        Assert(app.Outlook.LastError == "", "the next good download clears the error");

        // Reminders: an Outlook event starting in ten minutes is announced once, into the reminder history.
        DateTime soon = DateTime.Now.AddMinutes(10); soon = soon.AddTicks(-(soon.Ticks % TimeSpan.TicksPerMinute));
        served = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\n" + Event("soon", "Office hour", soon.Date, soon.ToString("HH:mm"), soon.AddMinutes(30).ToString("HH:mm"), "Room 1", "").Replace("China Standard Time", TimeZoneInfo.Local.Id) + "END:VCALENDAR\r\n";
        app.SyncOutlook(); PumpUntil(() => !app.OutlookSyncing, 5000); Pump(100);
        app.Data.Settings.Reminders.QuietHoursEnabled = false;
        app.CheckReminders(DateTime.UtcNow); app.CheckReminders(DateTime.UtcNow.AddSeconds(5));
        Assert(app.Data.ReminderHistory.Count(r => r.Title == "Office hour" && r.BookName == "Outlook") == 1, "an Outlook event is reminded once, lead time before it starts");
        app.Data.Settings.Calendar.Outlook.Remind = false; app.SettingsChanged();
        served = served.Replace("UID:soon", "UID:soon2").Replace("Office hour", "Second hour");
        app.SyncOutlook(); PumpUntil(() => !app.OutlookSyncing, 5000); app.CheckReminders(DateTime.UtcNow);
        Assert(!app.Data.ReminderHistory.Any(r => r.Title == "Second hour"), "with Outlook reminders off, none are shown");
        app.Data.Settings.Calendar.Outlook.Remind = true;

        // Restore the week for the restart check and save.
        served = Calendar(monday, TimeZoneInfo.Local.Id);
        app.SyncOutlook(); PumpUntil(() => !app.OutlookSyncing, 5000);
        app.Save(); Assert(app.Flush() && File.Exists(Path.Combine(app.Store.DirectoryPath, "outlook-cache.json")), "the downloaded calendar is cached beside data.json");
        Console.WriteLine("OUTLOOK SHOTS: " + shots);
    }

    // Copying the Outlook calendar into the local one: the settings button, after confirming.
    public static void Import(AppController app)
    {
        var calendar = (CalendarForm)app.Widgets.First(w => w.WidgetKey == "calendar");
        app.OutlookDownload = delegate(string unused) { throw new WebException("offline", WebExceptionStatus.ConnectFailure); };
        app.ShowAll(); Pump(300);
        app.Data.Settings.Calendar.Outlook.TitleColors["Tutor meeting"] = "#123456"; app.SettingsChanged(); Pump(200);
        DateTime monday = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));
        Func<List<string>> outlookWeek = () => OutlookLogic.Timed(app.Outlook, app.Data.Settings.Calendar.Outlook, monday, monday.AddDays(6)).Select(o => o.Date + " " + o.StartTime + "-" + o.EndTime + " " + o.Title + " " + o.Color).OrderBy(s => s).ToList();
        var before = outlookWeek(); int localBefore = app.Data.Events.Count; int backups = Directory.GetFiles(app.Store.DirectoryPath, "backup-*.json").Length;
        Assert(before.Count >= 5, "the cached Outlook week is there before copying (" + before.Count + ")");
        app.OpenSettings(); Pump(300);
        var settings = Application.OpenForms.OfType<SettingsForm>().Single();
        ((ListBox)All(settings).First(c => c.Name == "settings-nav")).SelectedIndex = 3; Pump(300);
        // Confirm the question, then close the "done" note.
        int answered = 0;
        var timer = new System.Windows.Forms.Timer { Interval = 200 };
        timer.Tick += delegate
        {
            var box = OwnMessageBox();
            if (box == IntPtr.Zero) return;
            var caption = new System.Text.StringBuilder(200); GetWindowTextW(box, caption, 200);
            var body = new System.Text.StringBuilder(2000); EnumChildWindows(box, delegate(IntPtr c, IntPtr p) { var t = new System.Text.StringBuilder(1000); GetWindowTextW(c, t, 1000); if (t.Length > 8) body.Append(t.ToString().Replace("\n", " ")); return true; }, IntPtr.Zero);
            Console.WriteLine("  [box] " + caption + ": " + body);
            // OK in an OK/Cancel box is IDOK; the lone OK button of an OK-only box has the IDCANCEL id.
            SendMessageW(box, 0x0111, new IntPtr(1), IntPtr.Zero);
            if (IsWindowVisible(box)) SendMessageW(box, 0x0111, new IntPtr(2), IntPtr.Zero);
            if (++answered == 2) { timer.Stop(); timer.Dispose(); }
        };
        timer.Start();
        ((Button)All(settings).First(c => c.Name == "outlook-import")).PerformClick();
        Pump(500);
        Assert(answered == 2, "copying asks first and reports when done");
        var options = app.Data.Settings.Calendar.Outlook;
        Assert(options.Url == "" && !options.Show && app.Outlook.Events.Count == 0 && !File.Exists(Path.Combine(app.Store.DirectoryPath, "outlook-cache.json")), "after copying, the subscription is stopped and the Outlook calendar is no longer shown");
        Assert(app.Data.Events.Count > localBefore && Directory.GetFiles(app.Store.DirectoryPath, "backup-*.json").Length > backups, "the events are now local, and a backup was made first");
        var local = CalendarEngine.GetOccurrences(app.Data, monday, monday.AddDays(6)).Where(x => !x.AllDay && x.Series.TimeZoneId != null && app.Data.Events.IndexOf(x.Series) >= localBefore)
            .Select(x => x.Date + " " + x.StartTime + "-" + x.EndTime + " " + x.Title + " " + x.Color).OrderBy(s => s).ToList();
        Assert(before.SequenceEqual(local), "this week's copied events match Outlook's: same days, times, names and colors (" + local.Count + ")");
        Assert(app.Data.Events.Any(e => e.Title == "Reading week" && e.AllDay && e.Days == 5), "the all-day event is copied as a local all-day event");
        settings.Close(); Pump(100);
        app.Save(); Assert(app.Flush(), "copied calendar saved");
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessageW(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);
    delegate bool EnumProc(IntPtr hwnd, IntPtr param);
    [DllImport("user32.dll")] static extern bool EnumThreadWindows(uint thread, EnumProc callback, IntPtr param);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassNameW(IntPtr hwnd, System.Text.StringBuilder name, int size);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowTextW(IntPtr hwnd, System.Text.StringBuilder text, int size);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent, EnumProc callback, IntPtr param);
    // A message box opened by this test's own thread, never another program's.
    static IntPtr OwnMessageBox()
    {
        IntPtr found = IntPtr.Zero;
        EnumThreadWindows(GetCurrentThreadId(), delegate(IntPtr h, IntPtr p)
        {
            var name = new System.Text.StringBuilder(64); GetClassNameW(h, name, 64);
            if (name.ToString() == "#32770" && IsWindowVisible(h)) { found = h; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    // After a restart, offline: the cached calendar shows straight away.
    public static void Read(AppController app)
    {
        app.OutlookDownload = delegate(string unused) { throw new WebException("offline", WebExceptionStatus.ConnectFailure); };
        Assert(app.Outlook.Events.Any(e => e.Uid == "reading") && app.Outlook.Categories.Count == 4 && app.Data.Settings.Calendar.Outlook.CategoryColors["Lectures"] == "#8764B8", "after a restart the cached Outlook calendar and the chosen colors are back");
    }
}
