using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DeskStudy
{
    public sealed class AppearanceBaseline
    {
        public float Size;
        public FontStyle Style;
        public string Family;
        public Color Background;
        public Font AppliedFont;
    }

    public static class AppearancePainter
    {
        public static Color Background(AppearanceOptions a)
        {
            if (a.Theme == "Dark" && a.BackgroundColor.Equals("#F7F8FB", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb(31, 39, 51);
            return ColorTranslator.FromHtml(a.BackgroundColor);
        }
        public static bool Dark(Color c) { return c.R * .299 + c.G * .587 + c.B * .114 < 145; }
        public static Color Foreground(AppearanceOptions a) { return Dark(Background(a)) ? Color.FromArgb(235, 240, 246) : Ui.Text; }
        public static Color Surface(AppearanceOptions a)
        {
            Color c = Background(a); int amount = Dark(c) ? 16 : 7;
            return Color.FromArgb(Math.Min(255, c.R + amount), Math.Min(255, c.G + amount), Math.Min(255, c.B + amount));
        }
        public static void Apply(Control root, AppearanceOptions a, Dictionary<Control, AppearanceBaseline> baselines)
        {
            foreach (Control disposed in baselines.Keys.Where(c => c.IsDisposed).ToArray())
            { if (baselines[disposed].AppliedFont != null) baselines[disposed].AppliedFont.Dispose(); baselines.Remove(disposed); }
            Capture(root, baselines);
            ApplyControl(root, a, baselines);
            root.Invalidate(true);
        }
        private static void Capture(Control c, Dictionary<Control, AppearanceBaseline> baselines)
        {
            if (!baselines.ContainsKey(c))
            {
                var property = System.ComponentModel.TypeDescriptor.GetProperties(c)["Font"];
                bool explicitFont = property != null && property.ShouldSerializeValue(c);
                float baselineSize = c is Form || explicitFont ? c.Font.SizeInPoints : 9F;
                baselines[c] = new AppearanceBaseline { Size = baselineSize, Style = c.Font.Style, Family = c.Font.FontFamily.Name, Background = c.BackColor };
            }
            foreach (Control child in c.Controls) Capture(child, baselines);
        }
        private static void ApplyControl(Control c, AppearanceOptions a, Dictionary<Control, AppearanceBaseline> baselines)
        {
            AppearanceBaseline original;
            if (!baselines.TryGetValue(c, out original))
            {
                original = new AppearanceBaseline { Size = c.Font.SizeInPoints, Style = c.Font.Style, Family = c.Font.FontFamily.Name, Background = c.BackColor };
                baselines[c] = original;
            }
            bool customFont = (c.Tag as string) == "appearance-custom-font";
            float desiredSize = original.Size * a.FontSize / 9F;
            if (!customFont && (Math.Abs(c.Font.SizeInPoints - desiredSize) > .05F || c.Font.Style != original.Style || c.Font.FontFamily.Name != original.Family))
            {
                Font old = original.AppliedFont;
                original.AppliedFont = new Font(original.Family, desiredSize, original.Style);
                c.Font = original.AppliedFont;
                if (old != null) old.Dispose();
            }
            if (c.Name != "widget-accent")
            {
                Color bg = Background(a), surface = Surface(a), text = Foreground(a);
                c.ForeColor = text;
                if (c is TextBoxBase || c is ListControl || c is UpDownBase || c is DateTimePicker || c is ListView || c is Button)
                    c.BackColor = surface;
                else if (c is Label || c is CheckBox) c.BackColor = Color.Transparent;
                else c.BackColor = original.Background == Color.White ? surface : bg;
                Button button = c as Button;
                if (button != null) button.FlatAppearance.BorderColor = Dark(bg) ? Color.FromArgb(82, 94, 110) : Ui.Border;
            }
            foreach (Control child in c.Controls) ApplyControl(child, a, baselines);
        }
    }

    public sealed partial class AppController
    {
        private SettingsForm settingsCenter;
        private bool publishing;
        public event Action StateChanged;
        public void NotifyWindowStateChanged() { if (!Exiting && StateChanged != null) StateChanged(); }
        private void PublishChanges()
        {
            if (publishing || Exiting) return;
            publishing = true;
            try
            {
                if (DataChanged != null) DataChanged();
                foreach (var w in Widgets) w.ApplyAppearance();
                NotifyWindowStateChanged();
            }
            finally { publishing = false; }
        }
        // Only the notebook's current page changed: save it, and let an open settings center catch up.
        // The other widgets do not depend on it, so they are not refreshed or restyled.
        public void SavePageTurn()
        {
            QueueSave();
            if (settingsCenter != null && !settingsCenter.IsDisposed && settingsCenter.Visible) settingsCenter.RefreshData();
        }
        public void SettingsChanged()
        {
            saveTimer.Interval = Data.Settings.AutoSaveDelayMs;
            Save();
        }
        public void OpenSettings()
        {
            if (settingsCenter == null || settingsCenter.IsDisposed) settingsCenter = new SettingsForm(this);
            settingsCenter.RefreshData(); settingsCenter.Show(); settingsCenter.WindowState = FormWindowState.Normal; settingsCenter.Activate();
        }
        public void SetWidgetVisible(string key, bool visible)
        {
            var w = Widgets.First(f => f.WidgetKey == key);
            Data.Windows[key].Visible = visible;
            if (visible) w.Reveal(); else w.Hide();
            QueueSave(); NotifyWindowStateChanged();
        }
        public void HideAll() { foreach (var w in Widgets) SetWidgetVisible(w.WidgetKey, false); }
        private Rectangle CurrentWorkArea()
        {
            return settingsCenter != null && settingsCenter.Visible ? Screen.FromControl(settingsCenter).WorkingArea : Screen.FromPoint(Cursor.Position).WorkingArea;
        }
        private static WindowState CopyWindow(WindowState w)
        {
            return new WindowState { X = w.X, Y = w.Y, Width = w.Width, Height = w.Height, Visible = w.Visible, TopMost = w.TopMost, PositionLocked = w.PositionLocked };
        }
        public void UpdateWindow(string key, WindowState value)
        {
            WindowState requested = CopyWindow(value);
            var w = Widgets.First(f => f.WidgetKey == key);
            w.WindowState = FormWindowState.Normal;
            Data.Windows[key] = requested;
            w.RestoreWindow();
            if (requested.Visible) { w.Show(); SettleWidget(w, 0, false); }
            // A never-shown hidden form has not enabled Remember yet. Still publish
            // the actual clamped bounds so settings cannot display stale geometry.
            Data.Windows[key].X = w.Left; Data.Windows[key].Y = w.Top;
            Data.Windows[key].Width = w.Width; Data.Windows[key].Height = w.Height;
            if (!requested.Visible) w.Hide();
            w.Remember(); QueueSave(); NotifyWindowStateChanged();
        }
        public void LocateWidget(string key)
        {
            var area = CurrentWorkArea(); var w = Widgets.First(f => f.WidgetKey == key);
            var state = CopyWindow(Data.Windows[key]);
            state.X = area.Left + Math.Max(0, (area.Width - w.Width) / 2);
            state.Y = area.Top + Math.Max(0, (area.Height - w.Height) / 2);
            state.Visible = true;
            UpdateWindow(key, state); w.Reveal();
        }
        // From the calendar: open the DDL book on the task's page and tint the task. A task on an archived page
        // has no page to turn to, so its editor opens instead.
        public void ShowDeadline(IWin32Window owner, string pageId, string taskId)
        {
            var book = Book("ddl"); var page = book.Pages.FirstOrDefault(p => p.Id == pageId);
            if (page == null || !page.Tasks.Any(t => t.Id == taskId)) return;
            if (page.Archived) { EditDeadline(owner, pageId, taskId); return; }
            var w = (NotebookForm)Widgets.First(f => f.WidgetKey == "ddl");
            if (book.CurrentPageId != pageId) { book.CurrentPageId = pageId; Save(); }
            if (!w.Visible) { var state = CopyWindow(Data.Windows["ddl"]); state.Visible = true; UpdateWindow("ddl", state); }
            if (w.Collapsed) w.Unfold(true);
            w.Reveal(); w.RefreshData();
            w.HighlightTask(taskId, true);
        }

        public void EditDeadline(IWin32Window owner, string pageId, string taskId)
        {
            var page = Book("ddl").Pages.FirstOrDefault(p => p.Id == pageId);
            var task = page == null ? null : page.Tasks.FirstOrDefault(t => t.Id == taskId);
            if (task == null) return;
            using (var dialog = new TaskEditorDialog(task, Color.FromArgb(184, 113, 75), Data.Settings.Reminders.DefaultLeadMinutes))
            {
                if (dialog.ShowDialog(owner) != DialogResult.OK) return;
                page = Book("ddl").Pages.FirstOrDefault(p => p.Id == pageId);
                task = page == null ? null : page.Tasks.FirstOrDefault(t => t.Id == taskId);
                if (task == null) return;
                NotebookForm.ApplyEditor(task, dialog, Data.Settings, false);
                Save();
            }
        }

        // Calendar at the top left, the notebooks along the right edge: DDL under Todo when it fits, otherwise beside it.
        // Anything that still collides (small screens) is sorted out by the placement rules afterwards.
        private Dictionary<string, Point> Tiled(Rectangle area, Dictionary<string, Size> sizes)
        {
            const int margin = 24; int gap = PlacementLogic.Gap;
            Size todo = sizes["todo"], ddl = sizes["ddl"];
            int column = area.Height - 2 * margin - gap;
            if (todo.Height + ddl.Height > column)
            {
                // Shorten the two notebooks so they fit one above the other, as long as neither goes below its minimum.
                int todoMin = Widgets.First(w => w.WidgetKey == "todo").MinimumSize.Height, ddlMin = Widgets.First(w => w.WidgetKey == "ddl").MinimumSize.Height;
                int half = column / 2;
                if (half >= todoMin && column - half >= ddlMin)
                {
                    todo.Height = Math.Min(todo.Height, half); ddl.Height = Math.Min(ddl.Height, column - todo.Height);
                    sizes["todo"] = todo; sizes["ddl"] = ddl;
                }
            }
            var todoAt = new Point(area.Right - margin - todo.Width, area.Top + margin);
            var ddlAt = area.Top + margin + todo.Height + gap + ddl.Height <= area.Bottom - margin
                ? new Point(area.Right - margin - ddl.Width, todoAt.Y + todo.Height + gap)
                : new Point(todoAt.X - gap - ddl.Width, area.Top + margin);
            return new Dictionary<string, Point> { { "calendar", new Point(area.Left + margin, area.Top + margin) }, { "todo", todoAt }, { "ddl", ddlAt } };
        }
        private Size Clamped(WidgetForm w, int width, int height, Rectangle area)
        {
            return new Size(Math.Min(Math.Max(w.MinimumSize.Width, width), area.Width), Math.Min(Math.Max(w.MinimumSize.Height, height), area.Height));
        }
        public void RescueWindows()
        {
            var area = CurrentWorkArea();
            var sizes = Widgets.ToDictionary(w => w.WidgetKey, w => Clamped(w, Data.Windows[w.WidgetKey].Width, Data.Windows[w.WidgetKey].Height, area));
            var places = Tiled(area, sizes);
            WithPlacementSuspended(delegate
            {
                foreach (var w in Widgets)
                {
                    var state = CopyWindow(Data.Windows[w.WidgetKey]);
                    state.Width = sizes[w.WidgetKey].Width; state.Height = sizes[w.WidgetKey].Height;
                    state.X = places[w.WidgetKey].X; state.Y = places[w.WidgetKey].Y;
                    state.Visible = true; UpdateWindow(w.WidgetKey, state);
                }
            });
            SettingsChanged();
        }
        public void SaveLayout()
        {
            foreach (var w in Widgets) w.Remember();
            Data.Settings.SavedLayout = Data.Windows.ToDictionary(p => p.Key, p => CopyWindow(p.Value));
            Data.Settings.SavedLayoutUtc = DateTime.UtcNow.ToString("o"); SettingsChanged(); Flush();
        }
        public void RestoreSavedLayout()
        {
            if (Data.Settings.SavedLayout.Count == 0) return;
            var snapshot = Data.Settings.SavedLayout.ToDictionary(p => p.Key, p => CopyWindow(p.Value));
            WithPlacementSuspended(delegate { foreach (var w in Widgets) if (snapshot.ContainsKey(w.WidgetKey)) UpdateWindow(w.WidgetKey, snapshot[w.WidgetKey]); });
            SettingsChanged();
        }
        public void ResetLayout()
        {
            Rectangle area = CurrentWorkArea();
            var sizes = Widgets.ToDictionary(w => w.WidgetKey, w => Clamped(w, w.WidgetKey == "calendar" ? 950 : 440, w.WidgetKey == "calendar" ? 740 : 650, area));
            var places = Tiled(area, sizes);
            WithPlacementSuspended(delegate
            {
                foreach (var w in Widgets)
                {
                    var initial = new WindowState { Width = sizes[w.WidgetKey].Width, Height = sizes[w.WidgetKey].Height, X = places[w.WidgetKey].X, Y = places[w.WidgetKey].Y, Visible = true, TopMost = false, PositionLocked = false };
                    UpdateWindow(w.WidgetKey, initial);
                }
                foreach (var w in Widgets.OfType<CalendarForm>()) w.FitDefaultHeight();
            });
            SettingsChanged();
        }
        public void ResetAppearance(string key)
        {
            if (key == null || key == "all") Data.Settings.GlobalAppearance = new AppearanceOptions();
            if (key == "all") Data.Settings.AppearanceOverrides.Clear();
            else if (key != null) Data.Settings.AppearanceOverrides.Remove(key);
            SettingsChanged();
        }
        private Notebook Book(string id) { return Data.Books.First(b => b.Id == id); }
        public void RenameBook(string id, string name)
        {
            if (String.IsNullOrWhiteSpace(name)) throw new ArgumentException(Lang.T("便签名称不能为空。"));
            Book(id).Name = name.Trim(); Save();
        }
        public void AddPage(string bookId)
        {
            var book = Book(bookId); var page = new NotePage { Title = Lang.T("未命名页") };
            book.Pages.Add(page); book.CurrentPageId = page.Id; Save();
        }
        public void RenamePage(string bookId, string pageId, string title)
        {
            if (String.IsNullOrWhiteSpace(title)) throw new ArgumentException(Lang.T("页面名称不能为空。"));
            Book(bookId).Pages.First(p => p.Id == pageId).Title = title.Trim(); Save();
        }
        public void MovePage(string bookId, string pageId, int delta)
        {
            var book = Book(bookId); int index = book.Pages.FindIndex(p => p.Id == pageId); int target = index + delta;
            if (index < 0 || target < 0 || target >= book.Pages.Count) return;
            var page = book.Pages[index]; book.Pages.RemoveAt(index); book.Pages.Insert(target, page); Save();
        }
        public void SelectPage(string bookId, string pageId)
        {
            var book = Book(bookId); var page = book.Pages.First(p => p.Id == pageId);
            page.Archived = false; book.CurrentPageId = page.Id; Save();
        }
        public void SetPageArchived(string bookId, string pageId, bool archived)
        {
            var book = Book(bookId); var page = book.Pages.First(p => p.Id == pageId); page.Archived = archived;
            if (archived && book.CurrentPageId == pageId)
            {
                var active = book.Pages.FirstOrDefault(p => !p.Archived);
                if (active == null) { active = new NotePage { Title = Lang.T("未命名页") }; book.Pages.Add(active); }
                book.CurrentPageId = active.Id;
            }
            Save();
        }
        public void EditCalendarSeries(string id)
        {
            CalendarEvent original = id == null ? null : Data.Events.FirstOrDefault(e => e.Id == id);
            if (id != null && original == null) return;
            CalendarOptions settings = Data.Settings.Calendar;
            CalendarEvent source = original ?? new CalendarEvent { Title = "", Date = settings.SemesterStart, RepeatEndDate = settings.SemesterEnd, RecurrenceAnchorDate = settings.TeachingWeekOne };
            using (var dialog = new CalendarEditor(source, true, original != null))
            {
                Form owner = settingsCenter != null && settingsCenter.Visible ? (Form)settingsCenter : Widgets[0];
                if (dialog.ShowDialog(owner) != DialogResult.OK) return;
                if (dialog.DeleteRequested) { if (original != null) Data.Events.Remove(original); }
                else if (original == null) Data.Events.Add(dialog.Result);
                else Data.Events[Data.Events.IndexOf(original)] = dialog.Result;
                Save();
            }
        }
        public void BackupNow() { Store.CreateBackup(); SettingsChanged(); }
        public void RestoreBackup(string path)
        {
            // Import reads and validates the source before rotating data.previous.json.
            Store.Import(path);
            pendingDelivery.Clear();
            // A backup from before 1.5 carries framed bounds, and any backup may select the other window mode.
            NormalizeWindowFrames();
            foreach (var w in Widgets) w.ApplyWidgetMode();
            RegisterShowHotkey();
            var state = Data.Windows.ToDictionary(p => p.Key, p => CopyWindow(p.Value));
            WithPlacementSuspended(delegate { foreach (var w in Widgets) UpdateWindow(w.WidgetKey, state[w.WidgetKey]); });
            SettingsChanged();
            if (center != null && !center.IsDisposed) center.RefreshData();
            if (UsesSystemStartup)
            {
                try { StartupRegistration.Set(Data.Settings.LaunchAtStartup); StartupWarning = ""; }
                catch (Exception ex)
                {
                    if (!(ex is UnauthorizedAccessException || ex is System.Security.SecurityException || ex is IOException)) throw;
                    StartupWarning = Lang.T("数据已恢复，但 Windows 未允许更新开机启动项。请检查权限后重新设置。");
                    MessageBox.Show(StartupWarning + "\n\n" + ex.Message, Lang.T("开机启动未应用"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            CheckReminders(DateTime.UtcNow);
        }
        private bool UsesSystemStartup
        {
            get { return notifications && String.Equals(Store.DirectoryPath.TrimEnd(Path.DirectorySeparatorChar), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeskStudy"), StringComparison.OrdinalIgnoreCase); }
        }
        public string StartupWarning { get; private set; }
        public bool StartupEnabled
        {
            get
            {
                if (!UsesSystemStartup) return Data.Settings.LaunchAtStartup;
                try { return StartupRegistration.Enabled; }
                catch (Exception ex)
                {
                    if (!(ex is UnauthorizedAccessException || ex is System.Security.SecurityException || ex is IOException)) throw;
                    StartupWarning = Lang.T("无法读取 Windows 开机启动项；此处显示已保存的偏好。");
                    return Data.Settings.LaunchAtStartup;
                }
            }
        }
        public void SetStartup(bool enabled)
        {
            if (UsesSystemStartup) StartupRegistration.Set(enabled);
            StartupWarning = "";
            Data.Settings.LaunchAtStartup = enabled; SettingsChanged();
        }
        public void SendTestNotification()
        {
            if (notifications && !NativeNotification.Show(tray, AppIcon, Lang.T("桌面课笺 · 测试通知"), Lang.T("通知测试。测试按钮不受应用免打扰时段限制，Windows 勿扰仍可能影响横幅。"), Data.Settings.Reminders.SoundEnabled))
                throw new InvalidOperationException(Lang.T("Windows 未接受静音系统通知。提醒记录仍保留在提醒中心。"));
        }
    }

    internal static class StartupRegistration
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public static bool Enabled { get { using (var key = Registry.CurrentUser.OpenSubKey(RunKey)) return key != null && key.GetValue("DeskStudy") != null; } }
        public static void Set(bool enabled)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enabled) key.SetValue("DeskStudy", Command, RegistryValueKind.String);
                else key.DeleteValue("DeskStudy", false);
            }
        }
        // --startup tells the app it was launched at sign-in, so it starts quietly behind other windows.
        private static string Command { get { return "\"" + Application.ExecutablePath + "\" --startup"; } }
        // Keeps an enabled entry pointing at the running program, including after the folder moved or a newer version was started.
        public static void Refresh()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                if (key != null && key.GetValue("DeskStudy") != null && !String.Equals(key.GetValue("DeskStudy") as string, Command, StringComparison.OrdinalIgnoreCase))
                    key.SetValue("DeskStudy", Command, RegistryValueKind.String);
        }
    }

    internal static class NativeNotification
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct IconData
        {
            public int cbSize; public IntPtr hWnd; public uint uID, uFlags, uCallbackMessage; public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
            public uint dwState, dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
            public uint uTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
            public uint dwInfoFlags; public Guid guidItem; public IntPtr hBalloonIcon;
        }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern bool Shell_NotifyIcon(uint message, ref IconData data);
        public static bool Show(NotifyIcon tray, Icon icon, string title, string message, bool sound)
        {
            // .NET Framework's NotifyIcon has no public NIIF_NOSOUND option. Send NIF_INFO
            // to the same native icon so sound can be disabled without a second tray icon.
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo windowField = typeof(NotifyIcon).GetField("window", flags) ?? typeof(NotifyIcon).GetField("_window", flags);
            FieldInfo idField = typeof(NotifyIcon).GetField("id", flags) ?? typeof(NotifyIcon).GetField("_id", flags);
            NativeWindow window = windowField == null ? null : windowField.GetValue(tray) as NativeWindow;
            if (window != null && idField != null)
            {
                var data = new IconData { cbSize = Marshal.SizeOf(typeof(IconData)), hWnd = window.Handle, uID = Convert.ToUInt32(idField.GetValue(tray)), uFlags = 0x10, szTip = "", szInfo = message.Length > 255 ? message.Substring(0, 255) : message, szInfoTitle = title.Length > 63 ? title.Substring(0, 63) : title, uTimeoutOrVersion = 10000, dwInfoFlags = 1u | (sound ? 0u : 0x10u) };
                if (Shell_NotifyIcon(1, ref data)) return true;
            }
            if (sound) { tray.ShowBalloonTip(10000, title, message, ToolTipIcon.Info); return true; }
            return false;
        }
        public static void Dispose() { }
    }
}
