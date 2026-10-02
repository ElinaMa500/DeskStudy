using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using DeskStudy;

// Interface language, step 1: the setting, the first-run default, the settings card, dates and weekdays.
public static class LangChecks
{
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hwnd, IntPtr dc, uint flags);
    static void Capture(Form form, string file)
    {
        using (var bmp = new System.Drawing.Bitmap(form.Width, form.Height))
        {
            using (var g = System.Drawing.Graphics.FromImage(bmp)) { IntPtr dc = g.GetHdc(); PrintWindow(form.Handle, dc, 2); g.ReleaseHdc(dc); }
            bmp.Save(file);
        }
    }
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static void Pump(int ms) { var clock = Stopwatch.StartNew(); while (clock.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(10); } }
    static IEnumerable<Control> All(Control root) { foreach (Control c in root.Controls) { yield return c; foreach (Control d in All(c)) yield return d; } }
    static T Find<T>(Control root, string name) where T : Control { return (T)All(root).First(c => c.Name == name); }

    // Started with an English Windows display language on an empty data folder.
    public static void FreshEnglish(AppController app)
    {
        Assert(app.Store.IsNew && app.Data.Settings.Language == "en" && Lang.IsEnglish, "a new user on English Windows starts in English");
        Assert(app.Data.Books.First(b => b.Id == "todo").Name == "To-do" && app.Data.Books.First(b => b.Id == "ddl").Name == "Deadlines", "their notebooks are named To-do and Deadlines");
        app.Save(); Assert(app.Flush(), "saved");
    }

    public static void Run(AppController app)
    {
        Assert(app.Store.IsNew && app.Data.Settings.Language == "zh-CN" && !Lang.IsEnglish, "a new user on Chinese Windows starts in Chinese");
        Assert(app.Data.Books.First(b => b.Id == "todo").Name == "Todo" && app.Data.Books.First(b => b.Id == "ddl").Name == "DDL", "their notebooks keep the names Todo and DDL");

        // Dates and weekdays.
        var day = new DateTime(2026, 9, 29);
        Assert(Lang.MonthDay(day) == "9 月 29 日" && Lang.MonthTitle(day) == "2026 年 9 月" && Lang.Weekday(day.DayOfWeek) == "周二" && Lang.WeekdayShort(day.DayOfWeek) == "二" && Lang.WeekdayLetter(day.DayOfWeek) == "二", "Chinese dates and weekdays are unchanged");
        Lang.Use("en");
        Assert(Lang.MonthDay(day) == "Sep 29" && Lang.MonthTitle(day) == "September 2026" && Lang.Weekday(day.DayOfWeek) == "Tue" && Lang.WeekdayShort(day.DayOfWeek) == "Tue" && Lang.WeekdayLetter(day.DayOfWeek) == "T", "English dates and weekdays: Sep 29, September 2026, Tue, T");
        Assert(Lang.T("界面语言") == "Language" && Lang.T("还没有英文的文字") == "还没有英文的文字", "English text where available, the Chinese original otherwise");
        Assert(Lang.Count(1, "{0} 天", "{0} day", "{0} days") == "1 day" && Lang.Count(3, "{0} 天", "{0} day", "{0} days") == "3 days", "counted text in English uses singular and plural");
        Lang.Use("zh-CN");
        Assert(Lang.T("界面语言") == "界面语言" && Lang.Count(3, "{0} 天", "{0} day", "{0} days") == "3 天", "back in Chinese");

        // Validation.
        bool rejected = false;
        try { app.Data.Settings.Language = "fr"; SettingsLogic.Check(app.Data.Settings); } catch (Exception) { rejected = true; }
        app.Data.Settings.Language = "zh-CN";
        Assert(rejected, "an unknown language is rejected");

        // Settings center: the bilingual card; the restart button appears only while a change is pending.
        app.OpenSettings(); Pump(200);
        var settings = Application.OpenForms.OfType<SettingsForm>().Single();
        var nav = Find<ListBox>(settings, "settings-nav"); nav.SelectedIndex = Enumerable.Range(0, nav.Items.Count).First(i => nav.Items[i].ToString().Contains("外观")); Pump(150);
        var combo = Find<ComboBox>(settings, "ui-language"); var restart = Find<Button>(settings, "restart-for-language");
        Assert(combo.Items.Count == 2 && combo.SelectedIndex == 0 && !restart.Visible, "the settings center shows the language card, Chinese selected, no restart pending");
        combo.SelectedIndex = 1; Pump(150);
        Assert(app.Data.Settings.Language == "en" && restart.Visible && !Lang.IsEnglish, "choosing English saves the choice and offers a restart; the running program stays Chinese");
        Capture(settings, System.IO.Path.Combine(app.Store.DirectoryPath, "settings-language.png"));
        combo.SelectedIndex = 0; Pump(150);
        Assert(app.Data.Settings.Language == "zh-CN" && !restart.Visible, "choosing Chinese again removes the pending restart");
        settings.Close(); Pump(80);
        app.Save(); Assert(app.Flush(), "language checks saved");
    }
}
