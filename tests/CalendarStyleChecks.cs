using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using DeskStudy;

// The calendar following the notebook layout (原始 / 清爽 / 纸页), the 工作周 view and the slimmer reference widgets.
public static class CalendarStyleChecks
{
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static void Pump(int ms) { var clock = Stopwatch.StartNew(); while (clock.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(10); } }
    static IEnumerable<Control> All(Control root) { foreach (Control c in root.Controls) { yield return c; foreach (Control d in All(c)) yield return d; } }
    static T Find<T>(Control root, string name) where T : Control { return (T)All(root).First(c => c.Name == name); }
    static void Switch(AppController app, string layout) { app.Data.Settings.NotebookLayout = layout; app.SettingsChanged(); Pump(200); }
    // The whole control lies inside its parent's client area (nothing cut off).
    static bool Inside(Control c) { return c.Parent != null && new Rectangle(Point.Empty, c.Parent.ClientSize).Contains(c.Bounds); }

    public static void Run(AppController app, string path, bool afterRestart)
    {
        var calendar = (CalendarForm)app.Widgets.First(w => w.WidgetKey == "calendar");
        var todo = app.Widgets.First(w => w.WidgetKey == "todo");
        if (afterRestart)
        {
            Assert(app.Data.Settings.Calendar.WorkWeek && app.Data.Settings.Calendar.DefaultView == "Week", "the 工作周 choice was saved");
            Assert(Find<Label>(calendar, "calendar-range").Text.Contains("–") && Find<Control>(calendar, "calendar-workweek").BackColor != Find<Control>(calendar, "calendar-week").BackColor, "after a restart the calendar opens in 工作周 again");
            return;
        }
        app.ShowAll(); Pump(200);

        // Original: the calendar keeps its old look, with a 工作周 button and uncut header buttons.
        Switch(app, "Original");
        Assert(calendar.AppliedCalendarStyle == "Original" && !Find<Panel>(calendar, "calendar-reference-bar").Visible && Find<Button>(calendar, "calendar-original-workweek").Visible, "原始外观: the calendar keeps its toolbar and gains a 工作周 button");
        var hide = Find<Button>(calendar, "hide-widget"); var gear = Find<Button>(calendar, "open-settings");
        Assert(hide.Bottom <= hide.Parent.ClientSize.Height && gear.Bottom <= gear.Parent.ClientSize.Height, "原始外观: ⚙ and 隐藏 are no longer cut off at the bottom (" + hide.Bounds + " in " + hide.Parent.ClientSize + ")");
        int originalMinimum = calendar.MinimumSize.Height, originalNotebookMinimum = todo.MinimumSize.Height;
        int originalHeader = Find<Panel>(calendar, "widget-header").Height;
        Find<Button>(calendar, "calendar-original-workweek").PerformClick(); Pump(150);
        Assert(app.Data.Settings.Calendar.WorkWeek && Find<Label>(calendar, "calendar-range").Text.Length > 0, "原始外观: 工作周 works and is remembered");
        Find<Button>(calendar, "calendar-original-week").PerformClick(); Pump(100);
        Assert(!app.Data.Settings.Calendar.WorkWeek, "原始外观: 周 switches back to the whole week");

        foreach (string layout in new[] { "Card", "Clean", "Paper", "Journal" })
        {
            Switch(app, layout);
            string expected = layout == "Card" || layout == "Clean" ? "Clean" : "Journal";
            Assert(calendar.AppliedCalendarStyle == expected, layout + ": the calendar uses the " + expected + " style");
            Assert(Find<Panel>(calendar, "calendar-reference-bar").Visible && !Find<Button>(calendar, "hide-widget").Visible, layout + ": one navigation row, reference header without 隐藏");
            string background = SettingsLogic.EffectiveAppearance(app.Data, "calendar").BackgroundColor;
            Assert(String.Equals(background, SettingsLogic.LayoutBackground(layout), StringComparison.OrdinalIgnoreCase), layout + ": the calendar background follows the notebooks (" + background + ")");
        }
        Switch(app, "Clean");
        var header = Find<Panel>(calendar, "widget-header"); var pin = Find<CheckBox>(calendar, "widget-pin");
        Assert(header.Height < originalHeader, "清爽: the calendar header is slimmer (" + header.Height + " < " + originalHeader + ")");
        Assert(Inside(gear) && Inside(pin) && gear.Bottom <= gear.Parent.ClientSize.Height, "清爽: 置顶 and ⚙ are fully visible");
        Assert(calendar.MinimumSize.Height < originalMinimum, "清爽: the calendar can be made shorter (" + calendar.MinimumSize.Height + " < " + originalMinimum + ")");
        Assert(todo.MinimumSize.Height < originalNotebookMinimum, "清爽: the notebook can be made shorter thanks to the slimmer footer (" + todo.MinimumSize.Height + " < " + originalNotebookMinimum + ")");
        var notebookHeader = Find<Panel>(todo, "widget-header");
        Assert(notebookHeader.Height > header.Height, "清爽: the notebook header keeps its height (" + notebookHeader.Height + "), only the calendar's is slimmer");
        foreach (string name in new[] { "calendar-previous", "calendar-next", "calendar-today", "calendar-workweek", "calendar-week", "calendar-month", "calendar-add" })
        {
            // ‹ starts slightly left of the row on purpose so the glyph lines up with the content edge; only its height must fit.
            Control c = Find<Control>(calendar, name);
            Assert(c.Top >= 0 && c.Bottom <= c.Parent.ClientSize.Height && (name == "calendar-previous" || Inside(c)), "清爽: " + name + " is not cut off (" + c.Bounds + " in " + c.Parent.ClientSize + ")");
        }

        // 工作周: five columns from Monday, remembered; 周 and 月 switch back.
        var range = Find<Label>(calendar, "calendar-range");
        ((Button)Find<Control>(calendar, "calendar-workweek")).PerformClick(); Pump(150);
        DateTime monday = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));
        Assert(range.Text == monday.ToString("M.d") + " – " + monday.AddDays(4).ToString("M.d"), "工作周 shows Monday to Friday: " + range.Text);
        Assert(app.Data.Settings.Calendar.WorkWeek && app.Data.Settings.Calendar.DefaultView == "Week", "工作周 is saved as a week view plus a separate flag, so 1.5 can still read the file");
        app.Data.Settings.Calendar.WeekStartDay = 0; app.SettingsChanged(); Pump(150);
        Assert(range.Text == monday.ToString("M.d") + " – " + monday.AddDays(4).ToString("M.d"), "with the week starting on Sunday, 工作周 still runs Monday to Friday");
        ((Button)Find<Control>(calendar, "calendar-next")).PerformClick(); Pump(100);
        Assert(range.Text == monday.AddDays(7).ToString("M.d") + " – " + monday.AddDays(11).ToString("M.d"), "› moves 工作周 one week on");
        ((Button)Find<Control>(calendar, "calendar-today")).PerformClick(); Pump(100);
        ((Button)Find<Control>(calendar, "calendar-month")).PerformClick(); Pump(150);
        Assert(app.Data.Settings.Calendar.DefaultView == "Month" && !app.Data.Settings.Calendar.WorkWeek, "月 is remembered");
        ((Button)Find<Control>(calendar, "calendar-week")).PerformClick(); Pump(150);
        Assert(app.Data.Settings.Calendar.DefaultView == "Week" && !app.Data.Settings.Calendar.WorkWeek, "周 is remembered");
        app.Data.Settings.Calendar.WeekStartDay = 1; app.SettingsChanged(); Pump(100);

        // Settings center: the same three choices.
        app.OpenSettings(); Pump(150);
        var settings = Application.OpenForms.OfType<SettingsForm>().Single();
        var view = Find<ComboBox>(settings, "calendar-default-view");
        Assert(view.Items.Count == 3, "the settings center offers 工作周, 周 and 月");
        view.SelectedIndex = 0; Pump(150);
        Assert(app.Data.Settings.Calendar.WorkWeek && range.Text == monday.ToString("M.d") + " – " + monday.AddDays(4).ToString("M.d"), "choosing 工作周 in the settings center switches the calendar");
        settings.Close(); Pump(80);
        app.Save(); Assert(app.Flush(), "calendar style changes saved");
    }
}
