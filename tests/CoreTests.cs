using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DeskStudy;

internal static class CoreTests
{
    private static int passed;
    private static string scratch;
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Run(string name, Action test)
    {
        test(); passed++; Console.WriteLine("PASS " + name);
    }
    private static DateTime D(string value) { return TimeUtil.ParseDate(value); }
    private static DateTime U(string value) { return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal); }
    private static CalendarEvent Course()
    {
        return new CalendarEvent { Title = "Data structures", Date = "2026-09-07", StartTime = "09:00", EndTime = "10:30", RepeatWeeks = 2, RepeatEndDate = "2026-12-18", WeekDays = new List<int> { 1, 3 }, Location = "Room 203", Notes = "Autumn semester", TimeZoneId = "GMT Standard Time" };
    }
    private static TaskItem Deadline(string local, int lead)
    {
        return new TaskItem { Text = "Essay submission", DueLocal = local, TimeZoneId = "GMT Standard Time", ReminderMinutes = lead };
    }
    private static AppStore NewStore(string test)
    {
        return new AppStore(Path.Combine(scratch, test));
    }
    private static void ExpectReject(Action action, string message)
    {
        bool threw = false;
        try { action(); } catch (Exception) { threw = true; }
        Assert(threw, message);
    }
    public static int Main()
    {
        scratch = Path.Combine(Path.GetTempPath(), "DeskStudy-CoreTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            Run("Biweekly semester: cancel one occurrence, retain all others", delegate
            {
                AppData data = new AppData(); CalendarEvent c = Course(); data.Events.Add(c);
                List<Occurrence> before = CalendarEngine.GetOccurrences(data, D("2026-09-01"), D("2026-12-31"));
                Assert(before.Count == 16, "Expected 8 teaching weeks with two days each.");
                CalendarEngine.CancelOccurrence(c, "2026-09-21");
                CalendarEngine.CancelOccurrence(c, "2026-09-21");
                List<Occurrence> after = CalendarEngine.GetOccurrences(data, D("2026-09-01"), D("2026-12-31"));
                Assert(after.Count == 15, "Exactly one occurrence must be removed, cancellation is idempotent.");
                Assert(!after.Any(o => o.KeyDate == "2026-09-21"), "Canceled course remains.");
                Assert(after.Any(o => o.KeyDate == "2026-09-23") && after.Any(o => o.KeyDate == "2026-10-05"), "Other course dates changed.");
                Assert(after.All(o => o.StartTime == "09:00"), "Civil class time changed across DST.");
            });
            Run("Recurrence: inclusive semester limits, Monday anchor, multiple weekdays", delegate
            {
                AppData data = new AppData(); CalendarEvent c = Course();
                c.Date = "2026-09-09"; c.RepeatEndDate = "2026-10-07"; data.Events.Add(c);
                string dates = String.Join(",", CalendarEngine.GetOccurrences(data, D("2026-09-01"), D("2026-11-01")).Select(o => o.Date));
                Assert(dates == "2026-09-09,2026-09-21,2026-09-23,2026-10-05,2026-10-07", "Incorrect biweekly anchoring: " + dates);
                c.RepeatWeeks = 1; c.WeekDays = new List<int> { 0 }; c.Date = "2026-09-06"; c.RepeatEndDate = "2026-09-20";
                Assert(CalendarEngine.GetOccurrences(data, D("2026-09-01"), D("2026-09-30")).Count == 3, "Sunday recurrence mismatch.");
                c.RepeatWeeks = 0;
                Assert(CalendarEngine.GetOccurrences(data, D("2026-09-01"), D("2026-09-30")).Count == 1, "Single event repeated.");
            });
            Run("Occurrence edit: move across month and update/cancel only the original", delegate
            {
                AppData data = new AppData(); CalendarEvent c = Course(); data.Events.Add(c);
                EventOverride moved = new EventOverride { OriginalDate = "2026-09-21", Date = "2026-10-02", StartTime = "14:00", EndTime = "16:00", Title = "Rescheduled lecture", Location = "Room 9", Notes = "One time", Color = "#123456" };
                CalendarEngine.UpdateOccurrence(c, moved);
                List<Occurrence> october = CalendarEngine.GetOccurrences(data, D("2026-10-01"), D("2026-10-02"));
                Assert(october.Count == 1 && october[0].KeyDate == "2026-09-21" && october[0].Title == "Rescheduled lecture", "Moved occurrence missing from destination range.");
                Assert(!CalendarEngine.GetOccurrences(data, D("2026-09-21"), D("2026-09-21")).Any(), "Original occurrence still present.");
                CalendarEngine.UpdateOccurrence(c, moved);
                Assert(c.Overrides.Count == 1, "Editing created duplicate overrides.");
                CalendarEngine.CancelOccurrence(c, moved.OriginalDate);
                Assert(CalendarEngine.GetOccurrences(data, D("2026-10-02"), D("2026-10-02")).Count == 0, "Cancel moved occurrence failed.");
                Assert(c.Overrides.Count == 0 && c.ExcludedDates.Contains("2026-09-21"), "Cancel did not resolve override state.");
            });
            Run("DST: 09:00 stays local, gap shifts forward, fold uses later instant", delegate
            {
                DateTime summer = TimeUtil.LocalToUtc(new DateTime(2026, 10, 19, 9, 0, 0), "GMT Standard Time");
                DateTime winter = TimeUtil.LocalToUtc(new DateTime(2026, 10, 26, 9, 0, 0), "GMT Standard Time");
                Assert(summer.Hour == 8 && winter.Hour == 9, "Class wall time did not survive DST transition.");
                DateTime gap = TimeUtil.LocalToUtc(new DateTime(2026, 3, 29, 1, 30, 0), "GMT Standard Time");
                Assert(gap == U("2026-03-29T01:00:00Z"), "Spring gap policy mismatch.");
                DateTime fold = TimeUtil.LocalToUtc(new DateTime(2026, 10, 25, 1, 30, 0), "GMT Standard Time");
                Assert(fold == U("2026-10-25T01:30:00Z"), "Autumn fold must choose later UTC occurrence.");
            });
            Run("Notebook pages: completion and text survive new page and return", delegate
            {
                AppStore store = NewStore("pages"); Notebook book = store.Data.Books[0]; NotePage page1 = book.Pages[0];
                page1.Title = "This week"; page1.Text = "Keep this note.";
                TaskItem task = new TaskItem { Text = "Read chapter", Completed = true }; page1.Tasks.Add(task);
                NotePage page2 = new NotePage { Title = "Next week", Text = "Separate content" }; book.Pages.Add(page2); book.CurrentPageId = page2.Id; store.Save();
                book.CurrentPageId = page1.Id;
                Assert(book.Pages.Count == 2 && book.Pages[0].Text == "Keep this note." && book.Pages[0].Tasks[0].Completed, "Adding page lost old content/state.");
                book.Pages[0].Tasks[0].Completed = false; store.Save();
                AppStore reloaded = new AppStore(store.DirectoryPath);
                Assert(!reloaded.Data.Books[0].Pages[0].Tasks[0].Completed, "Unchecking failed to persist.");
                Assert(reloaded.Data.Books[0].CurrentPageId == page1.Id && reloaded.Data.Books[0].Pages[1].Text == "Separate content", "Page navigation/content failed to persist.");
            });
            Run("Old-page DDL: advance/due reminders while viewing page 2", delegate
            {
                AppData data = new AppData(); Notebook book = data.Books[1]; NotePage page1 = book.Pages[0];
                TaskItem task = Deadline("2026-11-02T10:00:00", 15); page1.Tasks.Add(task);
                NotePage page2 = new NotePage(); book.Pages.Add(page2); book.CurrentPageId = page2.Id;
                Assert(ReminderEngine.Scan(data, U("2026-11-02T09:44:50Z")).Count == 0, "Reminder too early.");
                List<ReminderRecord> advance = ReminderEngine.Scan(data, U("2026-11-02T09:45:00Z"));
                Assert(advance.Count == 1 && advance[0].Kind == "advance" && advance[0].TaskId == task.Id && !advance[0].CatchUp, "Old-page advance reminder missing.");
                Assert(ReminderEngine.Scan(data, U("2026-11-02T09:45:10Z")).Count == 0, "Advance reminder duplicated.");
                data.LastCheckUtc = "2026-11-02T09:59:50.0000000Z";
                List<ReminderRecord> due = ReminderEngine.Scan(data, U("2026-11-02T10:00:00Z"));
                Assert(due.Count == 1 && due[0].Kind == "due" && !due[0].CatchUp, "Old-page due reminder missing.");
                Assert(ReminderEngine.Scan(data, U("2026-11-02T10:00:10Z")).Count == 0, "Due reminder duplicated.");
            });
            Run("Completion cancels pending reminder; cancellation can be undone", delegate
            {
                AppData data = new AppData(); TaskItem task = Deadline("2026-11-02T10:00:00", 15); data.Books[1].Pages[0].Tasks.Add(task); task.Completed = true;
                Assert(ReminderEngine.Scan(data, U("2026-11-02T09:45:00Z")).Count == 0, "Completed task got advance reminder.");
                Assert(ReminderEngine.Scan(data, U("2026-11-02T10:05:00Z")).Count == 0, "Completed task got due reminder.");
                task.Completed = false;
                Assert(ReminderEngine.Scan(data, U("2026-11-02T10:05:10Z")).Count == 1, "Reopened task was not eligible for reminder.");
                ReminderEngine.Reset(task); task.DueLocal = "2026-11-03T10:00:00";
                Assert(ReminderEngine.Scan(data, U("2026-11-03T09:45:00Z")).Count == 1, "Edited schedule not rearmed.");
            });
            Run("Completion after advance reminder cancels due reminder across both notebooks", delegate
            {
                AppData data = new AppData();
                TaskItem todo = Deadline("2026-11-02T10:00:00", 15); TaskItem ddl = Deadline("2026-11-02T10:00:00", 15);
                data.Books[0].Pages[0].Tasks.Add(todo); data.Books[1].Pages[0].Tasks.Add(ddl);
                Assert(ReminderEngine.Scan(data, U("2026-11-02T09:45:00Z")).Count == 2, "Tasks in both notebooks must remind.");
                ddl.Completed = true;
                List<ReminderRecord> due = ReminderEngine.Scan(data, U("2026-11-02T10:00:00Z"));
                Assert(due.Count == 1 && due[0].TaskId == todo.Id, "Completed task reminded again after its advance notification.");
            });
            Run("Sleep/exit catch-up: one reminder per overdue task, persisted deduplication", delegate
            {
                AppStore store = NewStore("catchup"); TaskItem task = Deadline("2026-11-02T10:00:00", 30); store.Data.Books[1].Pages[0].Tasks.Add(task);
                ReminderEngine.Scan(store.Data, U("2026-11-02T08:00:00Z")); store.Save();
                AppStore restart = new AppStore(store.DirectoryPath);
                List<ReminderRecord> notices = ReminderEngine.Scan(restart.Data, U("2026-11-02T12:00:00Z"));
                Assert(notices.Count == 1 && notices[0].Kind == "due" && notices[0].CatchUp, "Missed reminder should be one due catch-up."); restart.Save();
                AppStore again = new AppStore(store.DirectoryPath);
                Assert(ReminderEngine.Scan(again.Data, U("2026-11-02T12:05:00Z")).Count == 0, "Restart duplicated old reminder.");
                Assert(again.Data.ReminderHistory.Count == 1, "Reminder history lost.");
            });
            Run("Full restart: course exceptions, two notebooks, pages, text and window positions", delegate
            {
                AppStore store = NewStore("restart"); CalendarEvent c = Course(); CalendarEngine.CancelOccurrence(c, "2026-09-21"); store.Data.Events.Add(c);
                foreach (Notebook book in store.Data.Books)
                {
                    book.Name += " renamed"; book.Pages[0].Text = "Retained " + book.Id;
                    book.Pages[0].Tasks.Add(new TaskItem { Text = "Finished " + book.Id, Completed = true });
                    NotePage second = new NotePage { Title = "Second " + book.Id }; book.Pages.Add(second); book.CurrentPageId = second.Id;
                }
                int x = 111;
                foreach (WindowState w in store.Data.Windows.Values) { w.X = x; w.Y = x + 50; w.Width = 701; w.Height = 509; w.TopMost = true; w.Visible = false; x += 100; }
                store.Save(); AppStore restored = new AppStore(store.DirectoryPath);
                Assert(restored.Data.Events.Count == 1 && restored.Data.Events[0].ExcludedDates.SequenceEqual(new[] { "2026-09-21" }), "Course/cancellation not restored.");
                Assert(CalendarEngine.GetOccurrences(restored.Data, D("2026-09-01"), D("2026-12-31")).Count == 15, "Restored recurrence changed.");
                foreach (Notebook book in restored.Data.Books)
                {
                    Assert(book.Pages.Count == 2 && book.CurrentPageId == book.Pages[1].Id && book.Name.EndsWith("renamed"), "Notebook navigation/name not restored.");
                    Assert(book.Pages[0].Text == "Retained " + book.Id && book.Pages[0].Tasks[0].Completed, "Old page content/completion not restored.");
                }
                Assert(restored.Data.Windows["calendar"].X == 111 && restored.Data.Windows["todo"].X == 211 && restored.Data.Windows["ddl"].X == 311, "Window positions not restored.");
                Assert(restored.Data.Windows.Values.All(w => w.Width == 701 && w.Height == 509 && w.TopMost && !w.Visible), "Window size/visibility/topmost not restored.");
            });
            Run("Export/import: backup before replacement, all content preserved", delegate
            {
                AppStore source = NewStore("export-source"); source.Data.Events.Add(Course()); source.Data.Books[0].Pages[0].Text = "Imported text";
                string file = Path.Combine(scratch, "export.json"); source.Export(file);
                AppStore destination = NewStore("export-target"); destination.Data.Books[0].Pages[0].Text = "Original text"; destination.Save(); destination.Import(file);
                Assert(destination.Data.Events.Count == 1 && destination.Data.Books[0].Pages[0].Text == "Imported text", "Import did not apply.");
                Assert(new AppStore(destination.DirectoryPath).Data.Events.Count == 1, "Import not saved.");
                string[] backups = Directory.GetFiles(destination.DirectoryPath, "before-import-*.json");
                Assert(backups.Length == 1 && File.ReadAllText(backups[0]).Contains("Original text"), "Import did not preserve previous content.");
            });
            Run("DDL order: by deadline across time zones, date-only after timed, undated next, done last, ties stable", delegate
            {
                var tokyo = new TaskItem { Text = "tokyo 10:00", DueLocal = "2027-01-15T10:00:00", TimeZoneId = "Tokyo Standard Time" };
                var london = new TaskItem { Text = "london 09:00", DueLocal = "2027-01-15T09:00:00", TimeZoneId = "GMT Standard Time" };
                var dayOnly = new TaskItem { Text = "day only", DueLocal = "2027-01-15T23:59:00", DueDateOnly = true, TimeZoneId = "GMT Standard Time" };
                var late = new TaskItem { Text = "london 22:00", DueLocal = "2027-01-15T22:00:00", TimeZoneId = "GMT Standard Time" };
                var undatedA = new TaskItem { Text = "undated A" }; var undatedB = new TaskItem { Text = "undated B" };
                var done = new TaskItem { Text = "done early", DueLocal = "2027-01-01T08:00:00", TimeZoneId = "GMT Standard Time", Completed = true };
                var broken = new TaskItem { Text = "broken due", DueLocal = "not a date" };
                var stored = new List<TaskItem> { undatedA, done, dayOnly, late, broken, london, undatedB, tokyo };
                string order = String.Join("|", DeadlineLogic.Ordered(stored).Select(t => t.Text));
                Assert(order == "tokyo 10:00|london 09:00|london 22:00|day only|undated A|broken due|undated B|done early", "Unexpected DDL order: " + order);
                Assert(stored[0] == undatedA && stored[7] == tokyo, "Sorting must not change the stored order.");
            });
            Run("DDL on the calendar: unfinished, in range, archived pages included, local time", delegate
            {
                AppData data = new AppData(); Notebook book = data.Books.First(b => b.Id == "ddl");
                var page = book.Pages[0]; page.Title = "Homework";
                var archived = new NotePage { Title = "Last term", Archived = true }; book.Pages.Add(archived);
                page.Tasks.Add(new TaskItem { Text = "timed", DueLocal = "2027-03-10T15:00:00", TimeZoneId = "GMT Standard Time" });
                page.Tasks.Add(new TaskItem { Text = "finished", DueLocal = "2027-03-10T12:00:00", TimeZoneId = "GMT Standard Time", Completed = true });
                page.Tasks.Add(new TaskItem { Text = "no due" });
                page.Tasks.Add(new TaskItem { Text = "next month", DueLocal = "2027-04-20T12:00:00", TimeZoneId = "GMT Standard Time" });
                archived.Tasks.Add(new TaskItem { Text = "library", DueLocal = "2027-03-12T23:59:00", DueDateOnly = true, TimeZoneId = "Tokyo Standard Time" });
                data.Books.First(b => b.Id == "todo").Pages[0].Tasks.Add(new TaskItem { Text = "todo item", DueLocal = "2027-03-11T09:00:00", TimeZoneId = "GMT Standard Time" });
                List<DeadlineMark> marks = DeadlineLogic.Marks(data, D("2027-03-08"), D("2027-03-14"));
                Assert(String.Join("|", marks.Select(m => m.Text)) == "timed|library", "Only unfinished DDL deadlines in range belong on the calendar.");
                DateTime local = TimeZoneInfo.ConvertTimeFromUtc(U("2027-03-10T15:00:00Z"), TimeZoneInfo.Local);
                Assert(marks[0].Date == local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) && marks[0].Time == local.ToString("HH:mm", CultureInfo.InvariantCulture) && !marks[0].DateOnly && marks[0].PageTitle == "Homework", "A timed deadline is shown at this computer's local time.");
                Assert(marks[1].Date == "2027-03-12" && marks[1].DateOnly && marks[1].Archived && marks[1].Minutes == 24 * 60, "A date-only deadline keeps its day, wherever it was set; archived pages are included.");
                Assert(DeadlineLogic.Marks(data, D("2027-03-13"), D("2027-03-13")).Count == 0, "The range is by day and excludes other days.");
            });
            Run("DDL settings: on by default, also for files written before they existed", delegate
            {
                AppSettings fresh = new AppSettings();
                Assert(fresh.SortDeadlines && fresh.Calendar.ShowDeadlines, "Both DDL options start switched on.");
                var json = new System.Web.Script.Serialization.JavaScriptSerializer();
                AppSettings old = json.Deserialize<AppSettings>("{\"WidgetMode\":\"Desktop\",\"Calendar\":{\"DefaultView\":\"Month\"}}");
                Assert(old.SortDeadlines && old.Calendar.ShowDeadlines && old.Calendar.DefaultView == "Month", "Older files without the options read as switched on.");
                AppSettings off = json.Deserialize<AppSettings>(json.Serialize(new AppSettings { SortDeadlines = false, Calendar = new CalendarOptions { ShowDeadlines = false } }));
                Assert(!off.SortDeadlines && !off.Calendar.ShowDeadlines, "Switched-off options survive a save and load.");
            });
            Run("Invalid/future import: reject without mutating live data or on-disk data", delegate
            {
                AppStore store = NewStore("invalid-import"); store.Data.Books[0].Pages[0].Text = "Do not change"; store.Save();
                AppData original = store.Data; string originalBytes = File.ReadAllText(Path.Combine(store.DirectoryPath, "data.json"));
                string invalid = Path.Combine(scratch, "invalid.json"); File.WriteAllText(invalid, "{not-json");
                ExpectReject(delegate { store.Import(invalid); }, "Malformed JSON accepted.");
                File.WriteAllText(invalid, "{}");
                ExpectReject(delegate { store.Import(invalid); }, "Empty unrelated JSON accepted.");
                File.WriteAllText(invalid, "{\"Version\":1}");
                ExpectReject(delegate { store.Import(invalid); }, "Incomplete backup accepted.");
                File.WriteAllText(invalid, originalBytes.Replace("\"Completed\":false", "\"Ignored\":false").Replace("\"CreatedUtc\":", "\"IgnoredDate\":"));
                ExpectReject(delegate { store.Import(invalid); }, "Incomplete nested record accepted.");
                File.WriteAllText(invalid, originalBytes.Replace("\"Version\":2", "\"Version\":99"));
                ExpectReject(delegate { store.Import(invalid); }, "Future version accepted.");
                File.WriteAllText(invalid, originalBytes.Replace("\"Width\":850", "\"Width\":-1"));
                ExpectReject(delegate { store.Import(invalid); }, "Invalid window size accepted.");
                Assert(Object.ReferenceEquals(store.Data, original), "Failed import replaced live object.");
                Assert(File.ReadAllText(Path.Combine(store.DirectoryPath, "data.json")) == originalBytes, "Failed import changed disk data.");
            });
            Run("Future primary: refuse startup without overwriting or downgrading data", delegate
            {
                AppStore store = NewStore("future-primary"); store.Save(); store.Save();
                string path = Path.Combine(store.DirectoryPath, "data.json");
                string future = File.ReadAllText(path).Replace("\"Version\":2", "\"Version\":99"); File.WriteAllText(path, future);
                ExpectReject(delegate { new AppStore(store.DirectoryPath); }, "Newer data loaded with an old application.");
                Assert(File.ReadAllText(path) == future, "Startup downgraded future data.");
            });
            Run("Atomic save: previous backup and damaged primary recovery", delegate
            {
                AppStore store = NewStore("backup"); store.Data.Books[0].Pages[0].Text = "Version one"; store.Save();
                store.Data.Books[0].Pages[0].Text = "Version two"; store.Save();
                Assert(File.Exists(Path.Combine(store.DirectoryPath, "data.previous.json")), "Previous backup missing.");
                File.WriteAllText(Path.Combine(store.DirectoryPath, "data.json"), "corrupt");
                AppStore restored = new AppStore(store.DirectoryPath);
                Assert(restored.LoadWarning.Length > 0 && restored.Data.Books[0].Pages[0].Text == "Version one", "Backup recovery failed.");
                string[] preserved = Directory.GetFiles(store.DirectoryPath, "data.unreadable-*.json");
                Assert(preserved.Length == 1 && File.ReadAllText(preserved[0]) == "corrupt", "Damaged primary was not preserved for recovery.");
                restored.Save();
                Assert(File.ReadAllText(Path.Combine(store.DirectoryPath, "data.previous.json")).Contains("Version one"), "First save after recovery destroyed the valid backup.");
                Assert(Directory.GetFiles(store.DirectoryPath, "*.tmp").Length == 0, "Atomic-save temp files leaked.");
            });
            Console.WriteLine("ALL " + passed + " CORE TESTS PASSED");
            Console.WriteLine("Test data: " + scratch);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL after " + passed + " passing tests: " + ex.ToString());
            Console.Error.WriteLine("Retained test data: " + scratch); return 1;
        }
    }
}
