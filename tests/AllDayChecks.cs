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

// All-day events of one's own: shown in the row under the dates (with Outlook's), created and edited there.
public static class AllDayChecks
{
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hwnd, IntPtr dc, uint flags);
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static void Pump(int ms) { var clock = Stopwatch.StartNew(); while (clock.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(10); } }
    static IEnumerable<Control> All(Control root) { foreach (Control c in root.Controls) { yield return c; foreach (Control d in All(c)) yield return d; } }
    static string shots;
    static bool sameColorOffered;
    static void Shot(string name, Control w)
    {
        using (var bmp = new Bitmap(w.Width, w.Height))
        {
            using (var g = Graphics.FromImage(bmp)) { IntPtr dc = g.GetHdc(); PrintWindow(w.Handle, dc, 2); g.ReleaseHdc(dc); }
            bmp.Save(Path.Combine(shots, name + ".png"));
        }
    }
    static CalendarEvent Course(string title, string date, string start, string end, string color)
    {
        return new CalendarEvent { Title = title, Date = date, StartTime = start, EndTime = end, Color = color, Location = "教学楼", RepeatWeeks = 0, RepeatEndDate = date };
    }
    static CalendarEvent Whole(string title, string date, int days, string color)
    {
        return new CalendarEvent { Title = title, Date = date, StartTime = "00:00", EndTime = "23:59", Color = color, AllDay = true, Days = days, RepeatWeeks = 0, RepeatEndDate = date };
    }
    // Acts on a dialog once it is open (it runs its own message loop).
    static void WhenOpen<T>(Action<T> act) where T : Form
    {
        var timer = new System.Windows.Forms.Timer { Interval = 150 };
        timer.Tick += delegate { var f = Application.OpenForms.OfType<T>().FirstOrDefault(d => d.Visible); if (f == null) return; timer.Stop(); timer.Dispose(); act(f); };
        timer.Start();
    }
    static List<Occurrence> Row(CalendarForm calendar) { return (List<Occurrence>)typeof(CalendarForm).GetField("allDay", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(calendar); }

    public static void Run(AppController app, string path)
    {
        shots = Path.Combine(path, "allday"); Directory.CreateDirectory(shots);
        var calendar = (CalendarForm)app.Widgets.First(w => w.WidgetKey == "calendar");
        DateTime monday = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));
        Func<int, string> day = n => monday.AddDays(n).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        app.Data.Events.Clear();
        app.Data.Events.Add(Course("高等数学", day(0), "08:00", "09:40", "#6C9385"));
        app.Data.Events.Add(Course("英语写作", day(2), "08:30", "10:00", "#B08968"));
        app.Data.Events.Add(Course("组会", day(3), "09:00", "10:30", "#8A7FB5"));
        app.Data.Events.Add(Whole("校运动会", day(2), 1, "#C49472"));
        app.Data.Events.Add(Whole("国庆假期", day(3), 7, "#BB818B"));
        var duty = Whole("图书馆值班", day(1), 1, "#7196B1"); duty.RepeatWeeks = 1; duty.RepeatEndDate = day(60); duty.WeekDays = new List<int> { 2 };
        app.Data.Events.Add(duty);
        app.Outlook.Events.Clear();
        app.Outlook.Events.Add(new OutlookEvent { Uid = "open", Title = "Open day", AllDay = true, Start = day(4) + "T00:00:00", End = day(5) + "T00:00:00" });
        app.Data.Settings.Calendar.Outlook.Url = "https://example.invalid/calendar.ics"; app.OutlookDownload = delegate(string unused) { throw new System.Net.WebException("offline"); };
        app.Data.Settings.NotebookLayout = "Journal"; app.Data.Settings.Calendar.DefaultView = "Week"; app.Data.Settings.Calendar.WorkWeek = false;
        app.Data.Settings.GlobalAppearance.FontSize = 9; app.Data.Settings.GlobalAppearance.Opacity = 1;
        foreach (var w in app.Widgets) app.Data.Windows[w.WidgetKey].PositionLocked = false;
        app.ShowAll(); app.SettingsChanged(); Pump(300);
        foreach (var w in app.Widgets.Where(w => w != calendar)) w.Hide();
        calendar.Bounds = new Rectangle(1074, 493, 846, 647); Pump(200);
        calendar.RefreshData(); Pump(200);
        var surface = All(calendar).OfType<CalendarSurface>().First();

        var row = Row(calendar);
        Assert(row.Count == 4 && row.Any(o => o.Title == "国庆假期" && o.Days == 7) && row.Any(o => o.Title == "图书馆值班") && row.Any(o => o.External != null), "the week's all-day row holds own events (one day, several days, weekly) and Outlook's");
        var items = ((System.Collections.IEnumerable)typeof(CalendarSurface).GetField("items", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(surface)).Cast<Occurrence>().ToList();
        Assert(!items.Any(o => o.AllDay), "all-day events are not drawn in the hour grid");
        foreach (string layout in new[] { "Journal", "Clean", "Original" })
        {
            app.Data.Settings.NotebookLayout = layout; app.SettingsChanged(); Pump(400);
            surface.AutoScrollPosition = new Point(0, surface.HourPixels * 7); Pump(200);
            Shot("week-" + layout, calendar);
        }
        app.Data.Settings.NotebookLayout = "Journal"; app.SettingsChanged(); Pump(300);
        // The next week: the holiday that began last week is still there.
        All(calendar).OfType<Button>().First(b => b.Name == "calendar-next").PerformClick(); Pump(300);
        Assert(Row(calendar).Any(o => o.Title == "国庆假期"), "a several-day event that began the week before still shows in the next week");
        Shot("next-week-Journal", calendar);
        All(calendar).OfType<Button>().First(b => b.Name == "calendar-today").PerformClick(); Pump(200);
        All(calendar).OfType<Button>().First(b => b.Name == "calendar-month").PerformClick(); Pump(300);
        Shot("month-Journal", calendar);
        All(calendar).OfType<Button>().First(b => b.Name == "calendar-week").PerformClick(); Pump(300);

        // Double-click a date: the editor opens with 全天 ticked; save a two-day event.
        var header = All(calendar).First(c => c.Name == "calendar-day-header");
        bool ticked = false; sameColorOffered = false;
        WhenOpen<CalendarEditor>(delegate(CalendarEditor f)
        {
            var box = All(f).OfType<CheckBox>().First(c => c.Name == "event-all-day"); ticked = box.Checked;
            ((NumericUpDown)All(f).First(c => c.Name == "event-days")).Value = 2;
            All(f).OfType<TextBox>().First().Text = "实习面试";
            // The color list shows the colors other events use, by name, so the very same color can be picked.
            var colors = (ComboBox)All(f).First(c => c.Name == "event-color");
            var names = colors.Items.Cast<object>().Select(i => i.ToString()).ToList();
            int same = names.FindIndex(n => n.Contains("英语写作"));
            sameColorOffered = same >= 6 && names.Last().Contains("自定义") && colors.DrawMode == DrawMode.OwnerDrawFixed;
            if (same >= 0) colors.SelectedIndex = same;
            Shot("editor", f);
            box.Checked = false; Pump(150); Shot("editor-timed", f);
            var timeRow = All(f).First(c => c.Name == "event-days").Parent;
            Console.WriteLine("time row: " + String.Join("; ", timeRow.Controls.Cast<Control>().Where(c => c.Visible).Select(c => c.GetType().Name + " " + c.Bounds)));
            box.Checked = true; Pump(150);
            All(f).OfType<Button>().First(b => b.Text == Lang.T("保存日程")).PerformClick();
        });
        float col = (surface.GridWidth - surface.TimeGutter) / 7F;
        typeof(Control).GetMethod("OnMouseDoubleClick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(header, new object[] { new MouseEventArgs(MouseButtons.Left, 2, surface.TimeGutter + (int)(col * 5.5), 8, 0) });
        Pump(300);
        var created = app.Data.Events.FirstOrDefault(e => e.Title == "实习面试");
        Assert(ticked && created != null && created.AllDay && created.Days == 2 && created.Date == day(5), "double-clicking a date opens the editor with 全天 ticked; saving makes an all-day event of that length");
        Assert(sameColorOffered && String.Equals(created.Color, "#B08968", StringComparison.OrdinalIgnoreCase), "the color list offers the colors other events use (with swatches) and saves exactly that color: " + created.Color);
        Assert(Row(calendar).Any(o => o.Title == "实习面试"), "the new all-day event shows in the row at once");
        app.Save(); Assert(app.Flush(), "all-day events saved");
        Console.WriteLine("ALL-DAY SHOTS: " + shots);
    }

    public static void Read(AppController app)
    {
        var e = app.Data.Events.First(x => x.Title == "国庆假期");
        Assert(e.AllDay && e.Days == 7 && app.Data.Events.Any(x => x.Title == "实习面试" && x.AllDay && x.Days == 2), "after a restart all-day events keep their days");
    }
}
