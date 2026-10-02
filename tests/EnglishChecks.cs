using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using DeskStudy;

// English interface: every interface text has a translation, screenshots of every screen, and a list of cut-off text.
public static class EnglishChecks
{
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hwnd, IntPtr dc, uint flags);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool EnumThreadWindows(uint thread, EnumProc callback, IntPtr param);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    delegate bool EnumProc(IntPtr hwnd, IntPtr param);
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static void Pump(int ms) { var clock = Stopwatch.StartNew(); while (clock.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(10); } }
    static IEnumerable<Control> All(Control root) { foreach (Control c in root.Controls) { yield return c; foreach (Control d in All(c)) yield return d; } }
    static T Find<T>(Control root, string name) where T : Control { return (T)All(root).First(c => c.Name == name); }
    static string shots;
    static readonly List<string> cut = new List<string>();

    static void Capture(Control window, string name)
    {
        if (window.Width <= 0 || window.Height <= 0) return;
        using (var bmp = new Bitmap(window.Width, window.Height))
        {
            using (var g = Graphics.FromImage(bmp)) { IntPtr dc = g.GetHdc(); PrintWindow(window.Handle, dc, 2); g.ReleaseHdc(dc); }
            bmp.Save(Path.Combine(shots, name + ".png"));
        }
        FindCutText(window, name);
        // A horizontal scroll bar means something is wider than its list.
        foreach (var list in All(window).OfType<ScrollableControl>().Where(s => s.Visible && s.HorizontalScroll.Visible))
            foreach (Control child in All(list).Where(c => c.Visible && c.Parent != null && c.Right > c.Parent.ClientSize.Width + 1))
                cut.Add(name + " · horizontal scroll in " + (list.Name.Length > 0 ? list.Name : list.GetType().Name) + " · " + (child.Name.Length > 0 ? child.Name : child.GetType().Name) + " \"" + child.Text + "\" right " + child.Right + " > " + child.Parent.ClientSize.Width);
    }
    // Single-line text wider than the space its control gives it.
    static void FindCutText(Control root, string screen)
    {
        float dpi; using (var g = root.CreateGraphics()) dpi = g.DpiX / 96F;
        foreach (Control c in All(root).Concat(new[] { root }))
        {
            if (!c.Visible || String.IsNullOrEmpty(c.Text) || c.Text.Contains("\n") || c is TextBoxBase || c is ComboBox || c is Form || c is ListControl) continue;
            if (!(c is ButtonBase || c is Label)) continue;
            if (c is Label && ((Label)c).AutoSize && ((Label)c).MaximumSize.Width == 0) continue;
            // Icons, self-sizing check boxes and controls deliberately collapsed to nothing are not cut text.
            if (c.Text.Trim().Length <= 2 || c.Width <= 1) continue;
            var box = c as CheckBox; if (box != null && (box.AutoSize || box.Appearance == Appearance.Button)) continue;
            int lines = Math.Max(1, c.ClientSize.Height / Math.Max(1, c.Font.Height));
            int need = TextRenderer.MeasureText(c.Text, c.Font, Size.Empty, TextFormatFlags.SingleLine).Width;
            int room = (c.ClientSize.Width - c.Padding.Horizontal) * (c is Label && lines > 1 ? lines : 1) - (c is CheckBox || c is RadioButton ? (int)(20 * dpi) : 0) - (c is ButtonBase ? (int)(6 * dpi) : 0);
            if (need > room) cut.Add(screen + " · " + (String.IsNullOrEmpty(c.Name) ? c.GetType().Name : c.Name) + " · \"" + c.Text + "\" needs " + need + " px, has " + room);
        }
    }
    static List<IntPtr> ThreadWindows()
    {
        var list = new List<IntPtr>();
        EnumThreadWindows(GetCurrentThreadId(), delegate(IntPtr h, IntPtr p) { if (IsWindowVisible(h)) list.Add(h); return true; }, IntPtr.Zero);
        return list;
    }

    // Every Chinese text passed to Lang.T in the source has an English entry.
    static void CheckCoverage()
    {
        var table = (Dictionary<string, string>)typeof(Lang).GetField("En", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        string src = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "src"));
        var literal = new Regex("\"((?:[^\"\\\\\\n]|\\\\.)*)\"");
        var han = new Regex("[\\u4e00-\\u9fff]");
        var missing = new List<string>();
        foreach (string file in Directory.GetFiles(src, "*.cs"))
        {
            if (file.EndsWith("Lang.en.cs") || file.EndsWith("Lang.cs")) continue;
            foreach (string line in File.ReadAllLines(file))
            {
                if (!line.Contains("Lang.T(")) continue;
                foreach (Match m in literal.Matches(line))
                {
                    string text = Regex.Unescape(m.Groups[1].Value);
                    int count = line.LastIndexOf("Lang.Count(", m.Index, StringComparison.Ordinal);
                    if (!han.IsMatch(text) || (count >= 0 && line.IndexOf(')', count) > m.Index && line.LastIndexOf("Lang.T(", m.Index, StringComparison.Ordinal) < count)) continue;
                    if (!table.ContainsKey(text)) missing.Add(Path.GetFileName(file) + ": " + text);
                }
            }
        }
        foreach (string m in missing.Distinct()) Console.WriteLine("MISSING " + m);
        Assert(missing.Count == 0, "every interface text in the source has an English translation (" + table.Count + " entries)");
    }

    static void Seed(AppController app)
    {
        var monday = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));
        foreach (var book in app.Data.Books)
        {
            var page = book.Pages[0];
            page.Title = book.Id == "ddl" ? "October deadlines" : "This week";
            page.Text = "Questions from the lab session.";
            page.Tasks.Clear();
            page.Tasks.Add(new TaskItem { Text = "Tidy up the lecture notes", Completed = true, TimeZoneId = TimeZoneInfo.Local.Id });
            page.Tasks.Add(new TaskItem { Text = "Read chapter 3 of the paper and write down the open questions", DueLocal = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd") + "T18:00:00", ReminderMinutes = 45, TimeZoneId = TimeZoneInfo.Local.Id });
            page.Tasks.Add(new TaskItem { Text = "Submit the lab report", DueLocal = DateTime.Today.AddDays(4).ToString("yyyy-MM-dd") + "T23:59:00", DueDateOnly = true, TimeZoneId = TimeZoneInfo.Local.Id });
            page.Tasks.Add(new TaskItem { Text = "Reply to the supervisor", DueLocal = DateTime.Now.AddHours(-3).ToString("yyyy-MM-ddTHH:mm:00"), ReminderMinutes = 30, TimeZoneId = TimeZoneInfo.Local.Id });
            book.Pages.Add(new NotePage { Title = "Next week" });
        }
        string[][] events = { new[] { "Linear Algebra", "0", "09:00", "10:40" }, new[] { "Physics Lab", "2", "14:00", "16:00" }, new[] { "Group meeting", "3", "10:00", "11:00" } };
        foreach (var e in events)
            app.Data.Events.Add(new CalendarEvent { Title = e[0], Date = monday.AddDays(Int32.Parse(e[1])).ToString("yyyy-MM-dd"), StartTime = e[2], EndTime = e[3], Location = "Room 201", Color = "#6C9385", RepeatWeeks = 1, WeekDays = new List<int> { (Int32.Parse(e[1]) + 1) % 7 }, RepeatEndDate = monday.AddMonths(3).ToString("yyyy-MM-dd"), TimeZoneId = TimeZoneInfo.Local.Id });
        app.Save(); Pump(200);
    }

    public static void Run(AppController app, string path)
    {
        shots = Path.Combine(path, "english"); Directory.CreateDirectory(shots);
        Assert(Lang.IsEnglish, "running in English");
        CheckCoverage();
        Seed(app);
        app.ShowAll(); Pump(300);
        var calendar = app.Widgets.First(w => w.WidgetKey == "calendar"); var todo = app.Widgets.First(w => w.WidgetKey == "todo"); var ddl = app.Widgets.First(w => w.WidgetKey == "ddl");
        foreach (string layout in SettingsLogic.NotebookLayouts)
        {
            app.Data.Settings.NotebookLayout = layout; app.SettingsChanged(); Pump(300);
            Capture(todo, layout + "-todo"); Capture(ddl, layout + "-ddl"); Capture(calendar, layout + "-calendar");
            todo.Size = todo.MinimumSize; ddl.Size = ddl.MinimumSize; calendar.Size = calendar.MinimumSize; Pump(200);
            Capture(todo, layout + "-todo-min"); Capture(ddl, layout + "-ddl-min"); Capture(calendar, layout + "-calendar-min");
            todo.Size = new Size(560, 680); ddl.Size = new Size(560, 680); calendar.Size = new Size(1000, 760); Pump(150);
        }
        app.Data.Settings.NotebookLayout = "Clean"; app.SettingsChanged(); Pump(250);
        foreach (string view in new[] { "calendar-workweek", "calendar-month", "calendar-week" })
        { ((Button)Find<Control>(calendar, view)).PerformClick(); Pump(200); Capture(calendar, "Clean-" + view); }

        // The deadline picker.
        var quickDue = All(ddl).OfType<Button>().FirstOrDefault(b => b.Name == "quick-due");
        if (quickDue != null)
        {
            quickDue.PerformClick(); Pump(300);
            foreach (IntPtr h in ThreadWindows())
            {
                var control = Control.FromHandle(h);
                if (control is ToolStripDropDown) { Capture(control, "due-picker"); ((ToolStripDropDown)control).Close(); }
            }
            Pump(150);
        }

        // Dialogs, shown without blocking.
        var editor = new TaskEditorDialog(app.Data.Books[0].Pages[0].Tasks[1], Color.SeaGreen, 30); editor.Show(); Pump(200); Capture(editor, "dialog-task"); editor.Close();
        var eventEditor = new CalendarEditor(app.Data.Events[0], true, true); eventEditor.Show(); Pump(200); Capture(eventEditor, "dialog-event"); eventEditor.Close();
        var scope = new OccurrenceScopeDialog(); scope.Show(); Pump(150); Capture(scope, "dialog-scope"); scope.Close();
        app.OpenReminders(); Pump(250);
        var center = Application.OpenForms.OfType<ReminderCenter>().First(); Capture(center, "reminder-center"); center.Close();

        // Settings center, every page, top and bottom.
        app.OpenSettings(); Pump(300);
        var settings = Application.OpenForms.OfType<SettingsForm>().Single(); settings.Size = new Size(1250, 1000); Pump(200);
        var nav = Find<ListBox>(settings, "settings-nav");
        for (int i = 0; i < nav.Items.Count; i++)
        {
            nav.SelectedIndex = i; Pump(250);
            Capture(settings, "settings-" + i);
            var page = All(settings).OfType<Panel>().FirstOrDefault(p => p.Name == "settings-page-" + i && p.Visible);
            if (page != null && page.VerticalScroll.Visible) { page.AutoScrollPosition = new Point(0, page.VerticalScroll.Maximum); Pump(200); Capture(settings, "settings-" + i + "-bottom"); }
        }
        settings.Close(); Pump(100);

        File.WriteAllLines(Path.Combine(shots, "cut-text.txt"), cut.Distinct().ToArray(), Encoding.UTF8);
        Console.WriteLine("CUT TEXT: " + cut.Distinct().Count() + " (see english\\cut-text.txt)");
        app.Save(); Assert(app.Flush(), "English screens captured");
    }
}
