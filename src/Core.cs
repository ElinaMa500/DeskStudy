using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace DeskStudy
{
    public sealed class AppData
    {
        public int Version { get; set; }
        public List<CalendarEvent> Events { get; set; }
        public List<Notebook> Books { get; set; }
        public Dictionary<string, WindowState> Windows { get; set; }
        public List<ReminderRecord> ReminderHistory { get; set; }
        public string LastCheckUtc { get; set; }
        public AppSettings Settings { get; set; }
        public AppData()
        {
            Version = 2; Settings = new AppSettings();
            Events = new List<CalendarEvent>();
            Books = new List<Notebook>();
            Notebook todo = new Notebook { Id = "todo", Name = "Todo" };
            Notebook ddl = new Notebook { Id = "ddl", Name = "DDL" };
            Books.Add(todo); Books.Add(ddl);
            Windows = new Dictionary<string, WindowState>();
            Windows["calendar"] = new WindowState { X = 40, Y = 70, Width = 850, Height = 650 };
            Windows["todo"] = new WindowState { X = 910, Y = 70, Width = 370, Height = 480 };
            Windows["ddl"] = new WindowState { X = 910, Y = 610, Width = 370, Height = 480 };
            ReminderHistory = new List<ReminderRecord>();
            LastCheckUtc = "";
        }
    }
    public sealed class WindowState
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool TopMost { get; set; }
        public bool Visible { get; set; }
        public bool PositionLocked { get; set; }
        public WindowState() { Width = 370; Height = 480; Visible = true; }
    }
    public sealed class Notebook
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string CurrentPageId { get; set; }
        public List<NotePage> Pages { get; set; }
        // Offered again as "上次" in the due picker: yyyy-MM-dd and HH:mm, or empty.
        public string LastDueDate { get; set; }
        public string LastDueTime { get; set; }
        public Notebook()
        {
            LastDueDate = ""; LastDueTime = "";
            Id = Guid.NewGuid().ToString("N"); Name = Lang.T("便签");
            Pages = new List<NotePage> { new NotePage() };
            CurrentPageId = Pages[0].Id;
        }
    }
    public sealed class NotePage
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string CreatedUtc { get; set; }
        public string Text { get; set; }
        public List<TaskItem> Tasks { get; set; }
        public bool Archived { get; set; }
        public NotePage()
        {
            Id = Guid.NewGuid().ToString("N"); Title = Lang.T("新的一页");
            CreatedUtc = DateTime.UtcNow.ToString("o"); Text = ""; Tasks = new List<TaskItem>();
        }
    }
    public sealed class TaskItem
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public string DueLocal { get; set; }
        public string TimeZoneId { get; set; }
        public string AdvanceNotifiedKey { get; set; }
        public string DueNotifiedKey { get; set; }
        public int ReminderMinutes { get; set; }
        public bool Completed { get; set; }
        // Due on a day with no specific time: DueLocal holds that day's 23:59 and the only
        // reminder fires that morning (ReminderOptions.DateOnlyReminderTime).
        public bool DueDateOnly { get; set; }
        public TaskItem()
        {
            Id = Guid.NewGuid().ToString("N"); Text = ""; DueLocal = "";
            TimeZoneId = TimeZoneInfo.Local.Id; ReminderMinutes = 15;
            AdvanceNotifiedKey = ""; DueNotifiedKey = "";
        }
    }
    public sealed class CalendarEvent
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Date { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public string Location { get; set; }
        public string Notes { get; set; }
        public string Color { get; set; }
        public string TimeZoneId { get; set; }
        public string RepeatEndDate { get; set; }
        public string RecurrenceAnchorDate { get; set; }
        public int RepeatWeeks { get; set; }
        public List<int> WeekDays { get; set; }
        public List<string> ExcludedDates { get; set; }
        public List<EventOverride> Overrides { get; set; }
        public CalendarEvent()
        {
            Id = Guid.NewGuid().ToString("N"); Title = Lang.T("新日程");
            Date = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); StartTime = "09:00"; EndTime = "10:00";
            Location = ""; Notes = ""; Color = "#6A85B6"; TimeZoneId = TimeZoneInfo.Local.Id;
            RepeatEndDate = ""; RecurrenceAnchorDate = ""; WeekDays = new List<int>(); ExcludedDates = new List<string>();
            Overrides = new List<EventOverride>();
        }
    }
    public sealed class EventOverride
    {
        public string OriginalDate { get; set; }
        public string Date { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public string Title { get; set; }
        public string Location { get; set; }
        public string Notes { get; set; }
        public string Color { get; set; }
    }
    public sealed class Occurrence
    {
        public CalendarEvent Series { get; set; }
        public string KeyDate { get; set; }
        public string Date { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public string Title { get; set; }
        public string Location { get; set; }
        public string Notes { get; set; }
        public string Color { get; set; }
        // Set for an Outlook occurrence: read-only, not part of any series here.
        [System.Web.Script.Serialization.ScriptIgnore] public OutlookEvent External { get; set; }
    }
    public sealed class ReminderRecord
    {
        public string Id { get; set; }
        public string TaskId { get; set; }
        public string Title { get; set; }
        public string BookName { get; set; }
        public string PageTitle { get; set; }
        public string DueLocal { get; set; }
        public string FiredUtc { get; set; }
        public string Kind { get; set; }
        public bool CatchUp { get; set; }
        public bool DueDateOnly { get; set; }
    }

    public static class TimeUtil
    {
        public const string LocalFormat = "yyyy-MM-dd'T'HH:mm:ss";
        public static string DateOnlyDue(DateTime date) { return date.Date.AddHours(23).AddMinutes(59).ToString(LocalFormat, CultureInfo.InvariantCulture); }
        // "2026-10-02 18:00", or just the date for a date-only deadline.
        public static string DueDisplay(string dueLocal, bool dateOnly)
        {
            if (String.IsNullOrEmpty(dueLocal)) return "";
            return dateOnly ? dueLocal.Substring(0, Math.Min(10, dueLocal.Length)) : dueLocal.Replace("T", " ").Substring(0, Math.Min(16, dueLocal.Length));
        }
        public static DateTime ParseDate(string value)
        {
            return DateTime.SpecifyKind(DateTime.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None), DateTimeKind.Unspecified);
        }
        public static DateTime ParseLocal(string value)
        {
            return DateTime.SpecifyKind(DateTime.ParseExact(value, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None), DateTimeKind.Unspecified);
        }
        public static DateTime LocalToUtc(DateTime wall, string zone)
        {
            TimeZoneInfo tz = String.IsNullOrWhiteSpace(zone) ? TimeZoneInfo.Local : TimeZoneInfo.FindSystemTimeZoneById(zone);
            wall = DateTime.SpecifyKind(wall, DateTimeKind.Unspecified);
            // Keep civil time in stored records. A nonexistent spring-forward time moves to
            // the first valid minute; a repeated autumn time chooses the later UTC instant.
            int attempts = 0;
            while (tz.IsInvalidTime(wall) && attempts++ < 180) wall = wall.AddMinutes(1);
            if (tz.IsInvalidTime(wall)) throw new ArgumentException(Lang.T("此时区的时间无法转换。"));
            if (tz.IsAmbiguousTime(wall))
            {
                TimeSpan offset = tz.GetAmbiguousTimeOffsets(wall).Min();
                return DateTime.SpecifyKind(wall.Subtract(offset), DateTimeKind.Utc);
            }
            return TimeZoneInfo.ConvertTimeToUtc(wall, tz);
        }
    }

    // A deadline from the DDL book as the calendar shows it, in this computer's local time.
    public sealed class DeadlineMark
    {
        public string TaskId { get; set; }
        public string PageId { get; set; }
        public string PageTitle { get; set; }
        public bool Archived { get; set; }
        public string Text { get; set; }
        public string Date { get; set; }
        public string Time { get; set; }
        public bool DateOnly { get; set; }
        public int Minutes { get { return DateOnly ? 24 * 60 : (int)TimeSpan.ParseExact(Time, @"hh\:mm", CultureInfo.InvariantCulture).TotalMinutes; } }
    }

    public static class DeadlineLogic
    {
        // The DDL book's display order: unfinished first, then by deadline (date-only counts as 23:59 that day),
        // tasks without a deadline after the dated ones, ties in the order they were added.
        public static List<TaskItem> Ordered(IEnumerable<TaskItem> tasks)
        {
            return tasks.Select((task, index) => new { task, index, due = DueUtc(task) })
                .OrderBy(x => x.task.Completed ? 1 : 0).ThenBy(x => x.due.HasValue ? 0 : 1)
                .ThenBy(x => x.due ?? DateTime.MaxValue).ThenBy(x => x.index).Select(x => x.task).ToList();
        }

        public static DateTime? DueUtc(TaskItem task)
        {
            if (task == null || String.IsNullOrEmpty(task.DueLocal)) return null;
            try
            {
                DateTime wall = TimeUtil.ParseLocal(task.DueLocal);
                try { return TimeUtil.LocalToUtc(wall, task.TimeZoneId); }
                catch (TimeZoneNotFoundException) { return TimeUtil.LocalToUtc(wall, null); }
                catch (InvalidTimeZoneException) { return TimeUtil.LocalToUtc(wall, null); }
            }
            catch (FormatException) { return null; }
            catch (ArgumentException) { return null; }
        }

        // Unfinished DDL tasks with a deadline between the two dates (inclusive), archived pages included.
        public static List<DeadlineMark> Marks(AppData data, DateTime from, DateTime to)
        {
            var marks = new List<DeadlineMark>();
            Notebook book = data == null || data.Books == null ? null : data.Books.FirstOrDefault(b => b != null && b.Id == "ddl");
            if (book == null || book.Pages == null) return marks;
            foreach (NotePage page in book.Pages)
            {
                if (page == null || page.Tasks == null) continue;
                foreach (TaskItem task in page.Tasks)
                {
                    if (task == null || task.Completed) continue;
                    DateTime? utc = DueUtc(task);
                    if (!utc.HasValue) continue;
                    // A date-only deadline belongs to its calendar day wherever the computer is now.
                    DateTime local = task.DueDateOnly ? TimeUtil.ParseLocal(task.DueLocal) : utc.Value.ToLocalTime();
                    if (local.Date < from.Date || local.Date > to.Date) continue;
                    marks.Add(new DeadlineMark {
                        TaskId = task.Id, PageId = page.Id, PageTitle = page.Title ?? "", Archived = page.Archived, Text = task.Text ?? "",
                        Date = local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Time = local.ToString("HH:mm", CultureInfo.InvariantCulture),
                        DateOnly = task.DueDateOnly
                    });
                }
            }
            return marks.OrderBy(m => m.Date, StringComparer.Ordinal).ThenBy(m => m.Minutes).ToList();
        }
    }

    public static class CalendarEngine
    {
        public static List<Occurrence> GetOccurrences(AppData data, DateTime from, DateTime to)
        {
            from = from.Date; to = to.Date;
            if (to < from) return new List<Occurrence>();
            if ((to - from).TotalDays > 36600) throw new ArgumentException(Lang.T("查询日期范围过大。"));
            List<Occurrence> result = new List<Occurrence>();
            foreach (CalendarEvent e in data.Events)
            {
                DateTime start = TimeUtil.ParseDate(e.Date);
                DateTime end = e.RepeatWeeks == 0 ? start : TimeUtil.ParseDate(e.RepeatEndDate);
                DateTime first = from > start ? from : start;
                DateTime last = to < end ? to : end;
                for (DateTime day = first; day <= last; day = day.AddDays(1))
                {
                    string key = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    if (!IsScheduled(e, day) || e.ExcludedDates.Contains(key) || e.Overrides.Any(o => o.OriginalDate == key)) continue;
                    result.Add(new Occurrence { Series = e, KeyDate = key, Date = key, StartTime = e.StartTime, EndTime = e.EndTime, Title = e.Title, Location = e.Location, Notes = e.Notes, Color = e.Color });
                }
                // Overrides may move into the requested window from another week/month.
                foreach (EventOverride o in e.Overrides)
                {
                    DateTime original = TimeUtil.ParseDate(o.OriginalDate);
                    DateTime date = TimeUtil.ParseDate(o.Date);
                    if (date < from || date > to || !IsScheduled(e, original) || e.ExcludedDates.Contains(o.OriginalDate)) continue;
                    result.Add(new Occurrence { Series = e, KeyDate = o.OriginalDate, Date = o.Date, StartTime = o.StartTime, EndTime = o.EndTime, Title = o.Title, Location = o.Location, Notes = o.Notes, Color = o.Color });
                }
            }
            return result.OrderBy(o => o.Date, StringComparer.Ordinal).ThenBy(o => o.StartTime, StringComparer.Ordinal).ThenBy(o => o.Title, StringComparer.Ordinal).ToList();
        }
        public static bool IsScheduled(CalendarEvent e, DateTime date)
        {
            date = date.Date;
            DateTime start = TimeUtil.ParseDate(e.Date);
            if (e.RepeatWeeks == 0) return date == start;
            if (date < start || date > TimeUtil.ParseDate(e.RepeatEndDate)) return false;
            List<int> days = e.WeekDays;
            bool selectedDay = days.Count == 0 ? date.DayOfWeek == start.DayOfWeek : days.Contains((int)date.DayOfWeek);
            DateTime anchor = String.IsNullOrEmpty(e.RecurrenceAnchorDate) ? start : TimeUtil.ParseDate(e.RecurrenceAnchorDate);
            DateTime monday = anchor.AddDays(-(((int)anchor.DayOfWeek + 6) % 7));
            int week = (int)Math.Floor((date - monday).TotalDays / 7.0);
            return selectedDay && ((week % e.RepeatWeeks) + e.RepeatWeeks) % e.RepeatWeeks == 0;
        }
        public static void CancelOccurrence(CalendarEvent e, string originalDate)
        {
            if (!IsScheduled(e, TimeUtil.ParseDate(originalDate))) throw new ArgumentException(Lang.T("所选日期不属于此日程系列。"));
            if (!e.ExcludedDates.Contains(originalDate)) e.ExcludedDates.Add(originalDate);
            e.Overrides.RemoveAll(o => o.OriginalDate == originalDate);
        }
        public static void UpdateOccurrence(CalendarEvent e, EventOverride value)
        {
            if (value == null || !IsScheduled(e, TimeUtil.ParseDate(value.OriginalDate))) throw new ArgumentException(Lang.T("所选日期不属于此日程系列。"));
            Validation.CheckOverride(value);
            e.ExcludedDates.Remove(value.OriginalDate);
            e.Overrides.RemoveAll(o => o.OriginalDate == value.OriginalDate);
            e.Overrides.Add(value);
        }
    }

    public static class ReminderEngine
    {
        public static void Reset(TaskItem task) { task.AdvanceNotifiedKey = ""; task.DueNotifiedKey = ""; }
        // Independent of the reminder time, so changing that setting never re-sends a delivered reminder.
        public static string DateOnlyKey(TaskItem task)
        {
            return task.DueLocal + "|" + task.TimeZoneId + "|day";
        }
        // The morning of the due day, in the task's own time zone.
        public static DateTime DateOnlyReminderUtc(TaskItem task, AppSettings settings)
        {
            TimeSpan time = DateTime.ParseExact(settings.Reminders.DateOnlyReminderTime, "HH:mm", CultureInfo.InvariantCulture).TimeOfDay;
            return TimeUtil.LocalToUtc(TimeUtil.ParseLocal(task.DueLocal).Date.Add(time), task.TimeZoneId);
        }
        // A date-only deadline set after that morning's reminder time stays silent: the user just chose it.
        public static void SkipPassedDateOnlyReminder(TaskItem task, AppSettings settings, DateTime utcNow)
        {
            if (!task.DueDateOnly || String.IsNullOrWhiteSpace(task.DueLocal) || utcNow < DateOnlyReminderUtc(task, settings)) return;
            task.AdvanceNotifiedKey = task.DueNotifiedKey = DateOnlyKey(task);
        }
        public static List<ReminderRecord> Scan(AppData data, DateTime utcNow)
        {
            utcNow = utcNow.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(utcNow, DateTimeKind.Utc) : utcNow.ToUniversalTime();
            List<ReminderRecord> result = new List<ReminderRecord>();
            if (SettingsLogic.IsQuietHours(data.Settings, TimeZoneInfo.ConvertTimeFromUtc(utcNow, TimeZoneInfo.Local)))
            {
                // Defer without consuming notification keys. Every page is scanned again later.
                data.LastCheckUtc = utcNow.ToString("o");
                return result;
            }
            foreach (Notebook book in data.Books)
            foreach (NotePage page in book.Pages)
            foreach (TaskItem task in page.Tasks)
            {
                if (task.Completed || String.IsNullOrWhiteSpace(task.DueLocal)) continue;
                DateTime due = TimeUtil.LocalToUtc(TimeUtil.ParseLocal(task.DueLocal), task.TimeZoneId);
                string dueKey = task.DueLocal + "|" + task.TimeZoneId;
                string advanceKey = dueKey + "|" + task.ReminderMinutes.ToString(CultureInfo.InvariantCulture);
                DateTime threshold = due;
                string kind = null;
                if (task.DueDateOnly)
                {
                    // One reminder that morning and none at 23:59. If the whole day was missed it is delivered late, once.
                    string dayKey = DateOnlyKey(task);
                    DateTime remind = DateOnlyReminderUtc(task, data.Settings);
                    if (utcNow >= remind && task.AdvanceNotifiedKey != dayKey)
                    {
                        task.AdvanceNotifiedKey = task.DueNotifiedKey = dayKey; kind = utcNow >= due ? "due" : "advance"; threshold = remind;
                    }
                }
                else if (utcNow >= due)
                {
                    // Consume both stages if the application missed the due time.
                    task.AdvanceNotifiedKey = advanceKey;
                    if (task.DueNotifiedKey != dueKey) { task.DueNotifiedKey = dueKey; kind = "due"; }
                }
                else if (task.ReminderMinutes > 0 && utcNow >= due.AddMinutes(-task.ReminderMinutes) && task.AdvanceNotifiedKey != advanceKey)
                {
                    task.AdvanceNotifiedKey = advanceKey; kind = "advance"; threshold = due.AddMinutes(-task.ReminderMinutes);
                }
                if (kind == null) continue;
                ReminderRecord record = new ReminderRecord
                {
                    Id = Guid.NewGuid().ToString("N"), TaskId = task.Id, Title = task.Text, BookName = book.Name, PageTitle = page.Title,
                    DueLocal = task.DueLocal, FiredUtc = utcNow.ToString("o"), Kind = kind, DueDateOnly = task.DueDateOnly,
                    CatchUp = (utcNow - threshold).TotalSeconds > 30
                };
                result.Add(record); data.ReminderHistory.Add(record);
            }
            if (data.ReminderHistory.Count > 2000) data.ReminderHistory.RemoveRange(0, data.ReminderHistory.Count - 2000);
            data.LastCheckUtc = utcNow.ToString("o");
            return result;
        }
    }

    public sealed class AppStore
    {
        private const int MaxBytes = 32 * 1024 * 1024;
        public string DirectoryPath { get; private set; }
        public string LoadWarning { get; private set; }
        public AppData Data { get; private set; }
        // No data file yet: the very first start.
        public bool IsNew { get; private set; }
        public string LatestBackupUtc
        {
            get
            {
                DateTime latest;
                if (!DateTime.TryParse(Data.Settings.LastBackupUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out latest)) latest = DateTime.MinValue;
                else latest = latest.ToUniversalTime();
                try
                {
                    foreach (string pattern in new[] { "backup-*.json", "before-import-*.json", "migration-v1-*.json", "migration-notebook-layouts-*.json" })
                    foreach (string file in Directory.GetFiles(DirectoryPath, pattern))
                    {
                        DateTime changed = File.GetLastWriteTimeUtc(file);
                        if (changed > latest) latest = changed;
                    }
                }
                catch (IOException) { } // A temporarily unavailable folder must not break settings refresh.
                catch (UnauthorizedAccessException) { }
                return latest == DateTime.MinValue ? "" : latest.ToString("o");
            }
        }
        private string DataPath { get { return Path.Combine(DirectoryPath, "data.json"); } }
        private string BackupPath { get { return Path.Combine(DirectoryPath, "data.previous.json"); } }
        public AppStore(string directory)
        {
            DirectoryPath = Path.GetFullPath(directory); Directory.CreateDirectory(DirectoryPath);
            LoadWarning = "";
            if (!File.Exists(DataPath)) { Data = new AppData(); IsNew = true; return; }
            string migrationPrefix = "";
            try { Data = Read(DataPath, out migrationPrefix); }
            catch (NotSupportedException) { throw; } // Never downgrade a newer app's data.
            catch (Exception ex)
            {
                // Preserve even a syntactically valid but incompatible primary before recovery.
                string recovery = Path.Combine(DirectoryPath, "data.unreadable-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".json");
                File.Copy(DataPath, recovery, false);
                try
                {
                    Data = Read(BackupPath);
                    LoadWarning = Lang.T("主数据文件无法读取，已从上一份备份恢复。原文件已保留：{0}。原因：{1}", recovery, ex.Message);
                }
                catch
                {
                    Data = new AppData();
                    LoadWarning = Lang.T("无法读取原数据，已保留损坏文件：{0}。当前显示空白数据。原因：{1}", recovery, ex.Message);
                }
                // Repair the primary without rotating the damaged file into the good backup.
                WriteAtomic(DataPath, SerializeData(Data), null);
            }
            if (migrationPrefix.Length > 0)
            {
                string migration = UniqueBackupPath(migrationPrefix);
                File.Copy(DataPath, migration, false);
                LoadWarning = Lang.T("数据已兼容新版设置。更新前的原始数据已保留：{0}", migration);
            }
        }
        private static JavaScriptSerializer Serializer()
        {
            return new JavaScriptSerializer { MaxJsonLength = MaxBytes, RecursionLimit = 100 };
        }
        private static AppData Read(string path)
        {
            string legacy;
            return Read(path, out legacy);
        }
        private static AppData Read(string path, out string legacy)
        {
            legacy = "";
            FileInfo info = new FileInfo(path);
            if (!info.Exists) throw new FileNotFoundException(Lang.T("找不到数据文件。"), path);
            if (info.Length > MaxBytes) throw new InvalidDataException(Lang.T("数据文件超过 32 MB 限制。"));
            string json = File.ReadAllText(path, Encoding.UTF8);
            JavaScriptSerializer serializer = Serializer();
            Dictionary<string, object> root = serializer.DeserializeObject(json) as Dictionary<string, object>;
            if (root == null || !(root.ContainsKey("Version") && root["Version"] is int) ||
                !new[] { "Events", "Books", "Windows", "ReminderHistory", "LastCheckUtc" }.All(root.ContainsKey))
                throw new InvalidDataException(Lang.T("这不是完整的 DeskStudy 备份文件（缺少版本或必要数据字段）。"));
            int version = (int)root["Version"];
            if (version < 1 || version > 3) throw new NotSupportedException(Lang.T("不支持此数据版本，请使用兼容的应用版本。原数据保持不变。"));
            CheckShape(root, version);
            AppData data = serializer.Deserialize<AppData>(json);
            data.Version = 2;
            // Files written before 1.5 stored window bounds that include the system title bar and frame.
            data.Settings.FramedWindowBounds = version < 2 || !((Dictionary<string, object>)root["Settings"]).ContainsKey("WidgetMode");
            if (version >= 2)
            {
                var settings = (Dictionary<string, object>)root["Settings"];
                if (!settings.ContainsKey("NotebookLayout")) legacy = "migration-notebook-layouts-";
                PreserveLegacyAppearance(data.Settings.GlobalAppearance, (Dictionary<string, object>)settings["GlobalAppearance"]);
                foreach (var pair in (Dictionary<string, object>)settings["AppearanceOverrides"])
                    PreserveLegacyAppearance(data.Settings.AppearanceOverrides[pair.Key], (Dictionary<string, object>)pair.Value);
            }
            Validation.Check(data); if (version == 1) legacy = "migration-v1-"; return data;
        }
        private static void PreserveLegacyAppearance(AppearanceOptions value, Dictionary<string, object> fields)
        {
            // Prior files cannot distinguish an explicitly selected default from an untouched one.
            // Preserve every stored value conservatively; reset appearance opts back into layout defaults.
            if (!fields.ContainsKey("CustomizedFields"))
                value.CustomizedFields = new List<string> { "Theme", "BackgroundColor", "Opacity", "FontSize" };
        }
        private static Dictionary<string, object> ShapeObject(object value, string keys)
        {
            Dictionary<string, object> map = value as Dictionary<string, object>;
            if (map == null || !keys.Split(',').All(map.ContainsKey)) throw new InvalidDataException(Lang.T("备份中的记录缺少必要字段，未导入。"));
            return map;
        }
        private static System.Collections.IList ShapeList(object value)
        {
            System.Collections.IList list = value as System.Collections.IList;
            if (list == null) throw new InvalidDataException(Lang.T("备份中的列表格式无效，未导入。"));
            return list;
        }
        private static void CheckShape(Dictionary<string, object> root, int version)
        {
            foreach (object item in ShapeList(root["Events"]))
            {
                Dictionary<string, object> e = ShapeObject(item, "Id,Title,Date,StartTime,EndTime,Location,Notes,Color,TimeZoneId,RepeatEndDate,RepeatWeeks,WeekDays,ExcludedDates,Overrides");
                if (version >= 2) ShapeObject(item, "RecurrenceAnchorDate");
                foreach (object itemOverride in ShapeList(e["Overrides"])) ShapeObject(itemOverride, "OriginalDate,Date,StartTime,EndTime,Title,Location,Notes,Color");
            }
            foreach (object item in ShapeList(root["Books"]))
            {
                Dictionary<string, object> book = ShapeObject(item, "Id,Name,CurrentPageId,Pages");
                foreach (object itemPage in ShapeList(book["Pages"]))
                {
                    Dictionary<string, object> page = ShapeObject(itemPage, "Id,Title,CreatedUtc,Text,Tasks");
                    if (version >= 2) ShapeObject(itemPage, "Archived");
                    foreach (object task in ShapeList(page["Tasks"])) ShapeObject(task, "Id,Text,DueLocal,TimeZoneId,AdvanceNotifiedKey,DueNotifiedKey,ReminderMinutes,Completed");
                }
            }
            Dictionary<string, object> windows = ShapeObject(root["Windows"], "calendar,todo,ddl");
            foreach (object window in windows.Values) ShapeObject(window, "X,Y,Width,Height,TopMost,Visible" + (version >= 2 ? ",PositionLocked" : ""));
            foreach (object record in ShapeList(root["ReminderHistory"])) ShapeObject(record, "Id,TaskId,Title,BookName,PageTitle,DueLocal,FiredUtc,Kind,CatchUp");
            if (version >= 2)
            {
                if (!root.ContainsKey("Settings")) throw new InvalidDataException(Lang.T("版本 {0} 备份缺少设置，未导入。", version));
                Dictionary<string, object> settings = ShapeObject(root["Settings"], "GlobalAppearance,AppearanceOverrides,Calendar,Reminders,AutoSaveDelayMs,LaunchAtStartup,LastBackupUtc,SavedLayout,SavedLayoutUtc");
                ShapeObject(settings["GlobalAppearance"], "Theme,BackgroundColor,Opacity,FontSize");
                Dictionary<string, object> appearances = settings["AppearanceOverrides"] as Dictionary<string, object>;
                if (appearances == null) throw new InvalidDataException(Lang.T("组件外观数据无效。"));
                foreach (object appearance in appearances.Values) ShapeObject(appearance, "Theme,BackgroundColor,Opacity,FontSize");
                ShapeObject(settings["Calendar"], "DefaultView,WeekStartDay,SemesterStart,SemesterEnd,TeachingWeekOne");
                ShapeObject(settings["Reminders"], "DefaultLeadMinutes,QuietHoursEnabled,QuietStart,QuietEnd,SoundEnabled");
                Dictionary<string, object> layout = settings["SavedLayout"] as Dictionary<string, object>;
                if (layout == null) throw new InvalidDataException(Lang.T("保存的布局数据无效。"));
                foreach (object window in layout.Values) ShapeObject(window, "X,Y,Width,Height,TopMost,Visible,PositionLocked");
            }
        }
        // Data that selects a 1.3-only notebook layout is written as version 3, so 1.2 refuses it
        // ("不支持此数据版本") instead of treating it as unreadable and starting blank.
        private static string SerializeData(AppData data)
        {
            int stored = data.Version;
            string layout = data.Settings == null ? null : data.Settings.NotebookLayout;
            data.Version = layout == "Clean" || layout == "Journal" ? 3 : 2;
            try { return Serializer().Serialize(data); }
            finally { data.Version = stored; }
        }
        private string UniqueBackupPath(string prefix)
        {
            return Path.Combine(DirectoryPath, prefix + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".json");
        }
        public void Save()
        {
            Validation.Check(Data);
            WriteAtomic(DataPath, SerializeData(Data), BackupPath);
        }
        public void Import(string path)
        {
            AppData imported = Read(path); // Complete validation before touching live data or files.
            string json = SerializeData(imported);
            // Preserve the current in-memory state even if it was edited since the last save.
            Validation.Check(Data);
            string safety = UniqueBackupPath("before-import-");
            WriteAtomic(safety, SerializeData(Data), null);
            WriteAtomic(DataPath, json, BackupPath);
            Data = imported;
        }
        public string CreateBackup()
        {
            string path = UniqueBackupPath("backup-");
            Export(path);
            return path;
        }
        public void Export(string path)
        {
            Validation.Check(Data);
            string full = Path.GetFullPath(path);
            if (String.Equals(full, DataPath, StringComparison.OrdinalIgnoreCase) || String.Equals(full, BackupPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(Lang.T("请选择数据目录以外的备份文件名。"));
            WriteAtomic(full, SerializeData(Data), null);
            Data.Settings.LastBackupUtc = DateTime.UtcNow.ToString("o");
            // Export can be a rescue when normal saving is unavailable. The external
            // backup is already durable; keep its timestamp in memory for a later save.
            try { Save(); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        private static void WriteAtomic(string path, string json, string backup)
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(json);
            if (bytes.Length > MaxBytes) throw new InvalidDataException(Lang.T("数据超过 32 MB 限制。"));
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (FileStream stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(temp, path, backup, true);
                else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }

    internal static class Validation
    {
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
        private static void Text(string value, int max, string label, bool nonempty)
        {
            Require(value != null && value.Length <= max && (!nonempty || !String.IsNullOrWhiteSpace(value)), Lang.T("{0}为空或过长。", Lang.T(label)));
        }
        private static void Date(string value) { TimeUtil.ParseDate(value); }
        private static void Zone(string value)
        {
            Text(value, 200, Lang.T("时区"), true); TimeZoneInfo.FindSystemTimeZoneById(value);
        }
        private static void Times(string start, string end)
        {
            DateTime a = DateTime.ParseExact(start, "HH:mm", CultureInfo.InvariantCulture);
            DateTime b = DateTime.ParseExact(end, "HH:mm", CultureInfo.InvariantCulture);
            Require(b > a, Lang.T("结束时间必须晚于开始时间（单次日程不跨午夜）。"));
        }
        private static void Color(string value)
        {
            Require(value != null && value.Length == 7 && value[0] == '#' && value.Skip(1).All(c => Uri.IsHexDigit(c)), Lang.T("颜色应为 #RRGGBB。"));
        }
        public static void CheckOverride(EventOverride o)
        {
            Require(o != null, Lang.T("日程例外不能为空。")); Date(o.OriginalDate); Date(o.Date); Times(o.StartTime, o.EndTime);
            Text(o.Title, 1000, Lang.T("日程名称"), true); Text(o.Location, 4000, Lang.T("地点"), false); Text(o.Notes, 100000, Lang.T("备注"), false); Color(o.Color);
        }
        public static void Check(AppData data)
        {
            Require(data != null, Lang.T("无效的数据文件。")); Require(data.Version == 2, Lang.T("不支持此数据版本，请使用兼容的应用版本。"));
            SettingsLogic.Check(data.Settings);
            Require(data.Events != null && data.Events.Count <= 20000, Lang.T("日程列表无效或数量过多。"));
            Require(data.Books != null && data.Books.Count == 2 && data.Books.All(b => b != null) && data.Books.Any(b => b.Id == "todo") && data.Books.Any(b => b.Id == "ddl"), Lang.T("数据必须包含 Todo 和 DDL 两本便签。"));
            Require(data.Windows != null && data.Windows.Count <= 20, Lang.T("窗口信息无效。"));
            foreach (string key in new[] { "calendar", "todo", "ddl" }) Require(data.Windows.ContainsKey(key), Lang.T("缺少窗口信息：{0}", key));
            foreach (WindowState window in data.Windows.Values)
                Require(window != null && window.Width >= 120 && window.Width <= 16000 && window.Height >= 100 && window.Height <= 16000 && Math.Abs((long)window.X) <= 100000 && Math.Abs((long)window.Y) <= 100000, Lang.T("窗口尺寸或位置无效。"));
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (CalendarEvent e in data.Events)
            {
                Require(e != null, Lang.T("日程不能为空。")); Id(e.Id, ids); Text(e.Title, 1000, Lang.T("日程名称"), true); Date(e.Date); Times(e.StartTime, e.EndTime);
                Text(e.Location, 4000, Lang.T("地点"), false); Text(e.Notes, 100000, Lang.T("备注"), false); Color(e.Color); Zone(e.TimeZoneId);
                Require(e.RepeatWeeks == 0 || e.RepeatWeeks == 1 || e.RepeatWeeks == 2, Lang.T("循环周期无效。"));
                Text(e.RecurrenceAnchorDate, 10, Lang.T("循环教学周起点"), false);
                if (e.RecurrenceAnchorDate.Length > 0) Date(e.RecurrenceAnchorDate);
                Require(e.WeekDays != null && e.WeekDays.Count <= 7 && e.WeekDays.All(d => d >= 0 && d <= 6) && e.WeekDays.Distinct().Count() == e.WeekDays.Count, Lang.T("循环星期设置无效。"));
                if (e.RepeatWeeks > 0)
                {
                    Date(e.RepeatEndDate);
                    Require(TimeUtil.ParseDate(e.RepeatEndDate) >= TimeUtil.ParseDate(e.Date), Lang.T("循环结束日期早于开始日期。"));
                    Require((TimeUtil.ParseDate(e.RepeatEndDate) - TimeUtil.ParseDate(e.Date)).TotalDays <= 36600, Lang.T("循环跨度不能超过 100 年。"));
                }
                Require(e.ExcludedDates != null && e.ExcludedDates.Count <= 40000 && e.ExcludedDates.Distinct().Count() == e.ExcludedDates.Count, Lang.T("取消记录无效。"));
                foreach (string value in e.ExcludedDates) Date(value);
                Require(e.Overrides != null && e.Overrides.Count <= 40000, Lang.T("日程例外列表无效。"));
                HashSet<string> overrides = new HashSet<string>();
                foreach (EventOverride o in e.Overrides) { CheckOverride(o); Require(overrides.Add(o.OriginalDate), Lang.T("同一次日程有重复例外。")); }
            }
            int totalTasks = 0;
            foreach (Notebook book in data.Books)
            {
                Text(book.Name, 200, Lang.T("便签名称"), true); Require(book.Pages != null && book.Pages.Count > 0 && book.Pages.Count <= 20000, Lang.T("便签页面列表无效。"));
                Require(book.Pages.Any(p => p != null && p.Id == book.CurrentPageId), Lang.T("当前便签页不存在。"));
                if (!String.IsNullOrEmpty(book.LastDueDate)) Date(book.LastDueDate);
                if (!String.IsNullOrEmpty(book.LastDueTime)) DateTime.ParseExact(book.LastDueTime, "HH:mm", CultureInfo.InvariantCulture);
                foreach (NotePage page in book.Pages)
                {
                    Require(page != null, Lang.T("页面不能为空。")); Id(page.Id, ids); Text(page.Title, 1000, Lang.T("页面标题"), false); Text(page.Text, 1000000, Lang.T("页面内容"), false); Utc(page.CreatedUtc, false);
                    Require(page.Tasks != null && page.Tasks.Count <= 20000, Lang.T("任务列表无效。")); totalTasks += page.Tasks.Count;
                    Require(totalTasks <= 100000, Lang.T("任务数量超过 100000 条限制。"));
                    foreach (TaskItem task in page.Tasks)
                    {
                        Require(task != null, Lang.T("任务不能为空。")); Id(task.Id, ids); Text(task.Text, 10000, Lang.T("任务内容"), false); Zone(task.TimeZoneId);
                        Require(task.ReminderMinutes >= 0 && task.ReminderMinutes <= 5256000, Lang.T("提前提醒时间无效。"));
                        Text(task.DueLocal, 40, Lang.T("截止时间"), false); if (task.DueLocal.Length > 0) TimeUtil.ParseLocal(task.DueLocal);
                        Require(!task.DueDateOnly || task.DueLocal.Length > 0, Lang.T("仅日期的截止时间缺少日期。"));
                        Text(task.AdvanceNotifiedKey, 500, Lang.T("提醒状态"), false); Text(task.DueNotifiedKey, 500, Lang.T("提醒状态"), false);
                    }
                }
            }
            Require(data.ReminderHistory != null && data.ReminderHistory.Count <= 2000, Lang.T("提醒历史无效。"));
            foreach (ReminderRecord r in data.ReminderHistory)
            {
                Require(r != null, Lang.T("提醒记录不能为空。")); Text(r.Id, 100, Lang.T("提醒 ID"), true); Text(r.TaskId, 100, Lang.T("任务 ID"), true);
                Text(r.Title, 10000, Lang.T("提醒内容"), false); Text(r.BookName, 200, Lang.T("便签名称"), false); Text(r.PageTitle, 1000, Lang.T("页面标题"), false);
                TimeUtil.ParseLocal(r.DueLocal); Utc(r.FiredUtc, false); Require(r.Kind == "due" || r.Kind == "advance", Lang.T("提醒类型无效。"));
            }
            Utc(data.LastCheckUtc, true);
        }
        private static void Id(string value, HashSet<string> ids) { Text(value, 100, "ID", true); Require(ids.Add(value), Lang.T("数据包含重复 ID。")); }
        private static void Utc(string value, bool emptyAllowed)
        {
            if (emptyAllowed && value == "") return;
            DateTime parsed; Require(!String.IsNullOrWhiteSpace(value) && DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsed), Lang.T("时间格式无效。"));
        }
    }
}
