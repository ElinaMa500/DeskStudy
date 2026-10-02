using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace DeskStudy
{
    public sealed class AppSettings
    {
        public string NotebookLayout { get; set; }
        public AppearanceOptions GlobalAppearance { get; set; }
        public Dictionary<string, AppearanceOptions> AppearanceOverrides { get; set; }
        public CalendarOptions Calendar { get; set; }
        public ReminderOptions Reminders { get; set; }
        public int AutoSaveDelayMs { get; set; }
        public bool LaunchAtStartup { get; set; }
        public string LastBackupUtc { get; set; }
        public Dictionary<string, WindowState> SavedLayout { get; set; }
        public string SavedLayoutUtc { get; set; }
        // "Desktop": borderless widgets, off the taskbar, sinking behind other windows when not in use. "Standard": ordinary windows.
        public string WidgetMode { get; set; }
        // Desktop widgets: "Round" (default) or "Square" corners (1.5.1).
        public string WidgetCorners { get; set; }
        // Global shortcut that raises or lowers all widgets, e.g. "Ctrl+Alt+Shift+D". Empty disables it.
        public string ShowHotkey { get; set; }
        // Set when the file predates WidgetMode: its window bounds still include the system frame.
        [System.Web.Script.Serialization.ScriptIgnore] public bool FramedWindowBounds { get; set; }
        public AppSettings()
        {
            WidgetMode = "Desktop"; ShowHotkey = HotkeySpec.Default; WidgetCorners = "Round";
            NotebookLayout = "Card";
            GlobalAppearance = new AppearanceOptions();
            AppearanceOverrides = new Dictionary<string, AppearanceOptions>();
            Calendar = new CalendarOptions(); Reminders = new ReminderOptions();
            AutoSaveDelayMs = 450; LastBackupUtc = "";
            SavedLayout = new Dictionary<string, WindowState>(); SavedLayoutUtc = "";
        }
    }
    public sealed class AppearanceOptions
    {
        public string Theme { get; set; }
        public string BackgroundColor { get; set; }
        public double Opacity { get; set; }
        public float FontSize { get; set; }
        public List<string> CustomizedFields { get; set; }
        public AppearanceOptions() { Theme = "Light"; BackgroundColor = "#F7F8FB"; Opacity = 1.0; FontSize = 9F; CustomizedFields = new List<string>(); }
    }
    public sealed class CalendarOptions
    {
        public string DefaultView { get; set; }
        // 工作周 (Monday–Friday) is a week view, so DefaultView stays "Week" and older versions still read the file.
        public bool WorkWeek { get; set; }
        public int WeekStartDay { get; set; }
        public string SemesterStart { get; set; }
        public string SemesterEnd { get; set; }
        public string TeachingWeekOne { get; set; }
        public CalendarOptions()
        {
            DefaultView = "Week"; WeekStartDay = 1;
            DateTime today = DateTime.Today;
            DateTime start = today.Month >= 8 ? new DateTime(today.Year, 9, 1) :
                (today.Month == 1 ? new DateTime(today.Year - 1, 9, 1) : new DateTime(today.Year, 2, 1));
            DateTime end = start.Month == 9 ? new DateTime(start.Year + 1, 1, 31) : new DateTime(start.Year, 7, 31);
            SemesterStart = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            SemesterEnd = end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            TeachingWeekOne = start.AddDays(-(((int)start.DayOfWeek + 6) % 7)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
    }
    public sealed class ReminderOptions
    {
        public int DefaultLeadMinutes { get; set; }
        public bool QuietHoursEnabled { get; set; }
        public string QuietStart { get; set; }
        public string QuietEnd { get; set; }
        public bool SoundEnabled { get; set; }
        // When a date-only deadline reminds on its due day.
        public string DateOnlyReminderTime { get; set; }
        public ReminderOptions() { DefaultLeadMinutes = 30; QuietStart = "22:00"; QuietEnd = "08:00"; SoundEnabled = true; DateOnlyReminderTime = "09:00"; }
    }
    // A global shortcut written as "Ctrl+Alt+Shift+D". Needs Ctrl, Alt or Win so it cannot shadow ordinary typing.
    public sealed class HotkeySpec
    {
        public const string Default = "Ctrl+Alt+Shift+D";
        public const int Alt = 1, Ctrl = 2, Shift = 4, Win = 8;
        public int Modifiers { get; private set; }
        public int VirtualKey { get; private set; }
        public static bool TryParse(string text, out HotkeySpec spec)
        {
            spec = null;
            if (String.IsNullOrWhiteSpace(text)) return false;
            int modifiers = 0, key = 0;
            foreach (string raw in text.Split('+'))
            {
                string part = raw.Trim().ToUpperInvariant();
                if (part == "CTRL") modifiers |= Ctrl;
                else if (part == "ALT") modifiers |= Alt;
                else if (part == "SHIFT") modifiers |= Shift;
                else if (part == "WIN") modifiers |= Win;
                else if (key != 0) return false;
                else if (part.Length == 1 && ((part[0] >= 'A' && part[0] <= 'Z') || (part[0] >= '0' && part[0] <= '9'))) key = part[0];
                else if (part.Length >= 2 && part[0] == 'F' && Int32.TryParse(part.Substring(1), out key) && key >= 1 && key <= 12) key = 0x6F + key;
                else if (part == "`") key = 0xC0;
                else if (part == "SPACE") key = 0x20;
                else return false;
            }
            if (key == 0 || (modifiers & (Ctrl | Alt | Win)) == 0) return false;
            spec = new HotkeySpec { Modifiers = modifiers, VirtualKey = key };
            return true;
        }
        public static string Format(int modifiers, int virtualKey)
        {
            string key = virtualKey >= 0x70 && virtualKey <= 0x7B ? "F" + (virtualKey - 0x6F) : virtualKey == 0xC0 ? "`" : virtualKey == 0x20 ? "Space"
                : (virtualKey >= 'A' && virtualKey <= 'Z') || (virtualKey >= '0' && virtualKey <= '9') ? ((char)virtualKey).ToString() : "";
            if (key == "") return "";
            return ((modifiers & Ctrl) != 0 ? "Ctrl+" : "") + ((modifiers & Alt) != 0 ? "Alt+" : "") + ((modifiers & Shift) != 0 ? "Shift+" : "") + ((modifiers & Win) != 0 ? "Win+" : "") + key;
        }
    }
    public static class SettingsLogic
    {
        public static readonly string[] WidgetModes = { "Desktop", "Standard" };
        public static readonly string[] WidgetCornerStyles = { "Round", "Square" };
        // Turns bounds saved with a system frame into the same content area without one, and back.
        public static void ShiftFrame(WindowState window, int left, int top, int right, int bottom, bool removeFrame)
        {
            int sign = removeFrame ? 1 : -1;
            window.X += sign * left; window.Y += sign * top;
            window.Width -= sign * (left + right); window.Height -= sign * (top + bottom);
        }
        public static AppearanceOptions EffectiveAppearance(AppData data, string key)
        {
            AppearanceOptions value;
            bool overridden = data.Settings.AppearanceOverrides.TryGetValue(key, out value);
            // The calendar follows the notebook layout's default background too, so the three widgets match (1.5.1).
            if (key != "todo" && key != "ddl" && key != "calendar") return overridden ? value : data.Settings.GlobalAppearance;
            var result = new AppearanceOptions { BackgroundColor = LayoutBackground(data.Settings.NotebookLayout) };
            MergeAppearance(result, data.Settings.GlobalAppearance);
            if (overridden) MergeAppearance(result, value);
            return result;
        }
        public static readonly string[] NotebookLayouts = { "Original", "Card", "Paper", "Clean", "Journal" };
        public static string LayoutBackground(string layout)
        {
            switch (layout)
            {
                case "Card": return "#F4F6F3";
                case "Paper": return "#FBF5E8";
                case "Clean": return "#FFFFFF";
                case "Journal": return "#FAF8F1";
                default: return "#F7F8FB";
            }
        }
        public static void MarkAppearanceCustomized(AppearanceOptions value, string field)
        { if (value.CustomizedFields == null) value.CustomizedFields = new List<string>(); if (!value.CustomizedFields.Contains(field)) value.CustomizedFields.Add(field); }
        public static bool HasCustomAppearanceField(AppearanceOptions value, string field)
        {
            if (value.CustomizedFields != null && value.CustomizedFields.Contains(field)) return true;
            // Non-default programmatic values and data written by earlier versions remain explicit.
            return field == "Theme" ? value.Theme != "Light" : field == "BackgroundColor" ? !value.BackgroundColor.Equals("#F7F8FB", StringComparison.OrdinalIgnoreCase) : field == "FontSize" ? value.FontSize != 9F : field == "Opacity" && value.Opacity != 1;
        }
        public static AppearanceOptions CopyAppearance(AppearanceOptions value)
        { return new AppearanceOptions { Theme = value.Theme, BackgroundColor = value.BackgroundColor, FontSize = value.FontSize, Opacity = value.Opacity, CustomizedFields = new List<string>(value.CustomizedFields ?? new List<string>()) }; }
        private static void MergeAppearance(AppearanceOptions target, AppearanceOptions source)
        {
            if (HasCustomAppearanceField(source, "Theme")) { target.Theme = source.Theme; if (!HasCustomAppearanceField(source, "BackgroundColor")) target.BackgroundColor = source.Theme == "Dark" ? "#252B36" : "#F7F8FB"; }
            if (HasCustomAppearanceField(source, "BackgroundColor")) target.BackgroundColor = source.BackgroundColor;
            if (HasCustomAppearanceField(source, "FontSize")) target.FontSize = source.FontSize;
            if (HasCustomAppearanceField(source, "Opacity")) target.Opacity = source.Opacity;
        }
        public static bool IsQuietHours(AppSettings settings, DateTime localNow)
        {
            if (!settings.Reminders.QuietHoursEnabled) return false;
            TimeSpan start = DateTime.ParseExact(settings.Reminders.QuietStart, "HH:mm", CultureInfo.InvariantCulture).TimeOfDay;
            TimeSpan end = DateTime.ParseExact(settings.Reminders.QuietEnd, "HH:mm", CultureInfo.InvariantCulture).TimeOfDay;
            TimeSpan now = localNow.TimeOfDay;
            return start == end || (start < end ? now >= start && now < end : now >= start || now < end);
        }
        public static int TeachingWeek(AppSettings settings, DateTime date)
        {
            DateTime anchor = TimeUtil.ParseDate(settings.Calendar.TeachingWeekOne);
            anchor = anchor.AddDays(-(((int)anchor.DayOfWeek + 6) % 7));
            return (int)Math.Floor((date.Date - anchor).TotalDays / 7.0) + 1;
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
        private static void Timestamp(string value)
        {
            DateTime parsed;
            Require(value != null && (value == "" || DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsed)), "设置中的保存时间无效。");
        }
        private static void Appearance(AppearanceOptions value)
        {
            Require(value != null, "外观设置不能为空。");
            Require(value.CustomizedFields != null && value.CustomizedFields.Count <= 4 && value.CustomizedFields.Distinct().Count() == value.CustomizedFields.Count && value.CustomizedFields.All(f => new[] { "Theme", "BackgroundColor", "Opacity", "FontSize" }.Contains(f)), "自定义外观字段无效。");
            Require(value.Theme == "Light" || value.Theme == "Dark", "主题设置无效。");
            string color = value.BackgroundColor;
            Require(color != null && color.Length == 7 && color[0] == '#' && color.Skip(1).All(c => Uri.IsHexDigit(c)), "背景颜色应为 #RRGGBB。");
            Require(!Double.IsNaN(value.Opacity) && !Double.IsInfinity(value.Opacity) && value.Opacity >= 0.35 && value.Opacity <= 1.0, "透明度应在 35% 到 100% 之间。");
            Require(!Single.IsNaN(value.FontSize) && !Single.IsInfinity(value.FontSize) && value.FontSize >= 8F && value.FontSize <= 14F, "字号应在 8 到 14 之间。");
        }
        internal static void Check(AppSettings settings)
        {
            Require(settings != null, "缺少设置数据。"); Appearance(settings.GlobalAppearance);
            Require(NotebookLayouts.Contains(settings.NotebookLayout), "便签布局无效。");
            Require(WidgetModes.Contains(settings.WidgetMode), "窗口模式无效。");
            Require(WidgetCornerStyles.Contains(settings.WidgetCorners), "组件四角样式无效。");
            HotkeySpec hotkey; Require(settings.ShowHotkey == "" || HotkeySpec.TryParse(settings.ShowHotkey, out hotkey), "快捷键无效。");
            Require(settings.AppearanceOverrides != null && settings.AppearanceOverrides.Count <= 3, "组件外观设置无效。");
            foreach (KeyValuePair<string, AppearanceOptions> pair in settings.AppearanceOverrides)
            { Require(new[] { "calendar", "todo", "ddl" }.Contains(pair.Key), "未知的组件外观设置。"); Appearance(pair.Value); }
            CalendarOptions c = settings.Calendar;
            Require(c != null, "缺少日历设置。"); Require(c.DefaultView == "Week" || c.DefaultView == "Month", "默认视图无效。");
            Require(c.WeekStartDay >= 0 && c.WeekStartDay <= 6, "每周起始日无效。");
            DateTime start = TimeUtil.ParseDate(c.SemesterStart), end = TimeUtil.ParseDate(c.SemesterEnd);
            Require(end >= start && (end - start).TotalDays <= 36600, "学期日期范围无效。"); TimeUtil.ParseDate(c.TeachingWeekOne);
            ReminderOptions r = settings.Reminders;
            Require(r != null, "缺少提醒设置。"); Require(r.DefaultLeadMinutes >= 0 && r.DefaultLeadMinutes <= 5256000, "默认提前时间无效。");
            DateTime.ParseExact(r.QuietStart, "HH:mm", CultureInfo.InvariantCulture); DateTime.ParseExact(r.QuietEnd, "HH:mm", CultureInfo.InvariantCulture);
            DateTime.ParseExact(r.DateOnlyReminderTime, "HH:mm", CultureInfo.InvariantCulture);
            Require(settings.AutoSaveDelayMs >= 100 && settings.AutoSaveDelayMs <= 10000, "自动保存间隔应在 100 到 10000 毫秒之间。");
            Timestamp(settings.LastBackupUtc); Timestamp(settings.SavedLayoutUtc);
            Require(settings.SavedLayout != null && (settings.SavedLayout.Count == 0 || settings.SavedLayout.Count == 3), "保存的布局无效。");
            foreach (KeyValuePair<string, WindowState> pair in settings.SavedLayout)
            {
                Require(new[] { "calendar", "todo", "ddl" }.Contains(pair.Key), "保存的布局包含未知组件。");
                WindowState w = pair.Value;
                Require(w != null && w.Width >= 120 && w.Width <= 16000 && w.Height >= 100 && w.Height <= 16000 && Math.Abs((long)w.X) <= 100000 && Math.Abs((long)w.Y) <= 100000, "保存的窗口尺寸或位置无效。");
            }
        }
    }
}
