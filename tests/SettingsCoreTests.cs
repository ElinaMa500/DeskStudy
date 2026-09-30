using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using DeskStudy;

internal static class SettingsCoreTests
{
    private static string scratch;
    private static int passed;
    private static JavaScriptSerializer Json() { return new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 }; }
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Run(string name, Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
    private static void Reject(Action action, string message)
    {
        bool failed = false;
        try { action(); } catch (Exception) { failed = true; }
        Assert(failed, message);
    }
    private static Dictionary<string, object> Map(object value) { return (Dictionary<string, object>)value; }
    private static DateTime D(string value) { return TimeUtil.ParseDate(value); }
    private static DateTime LocalUtc(string value) { return TimeUtil.LocalToUtc(TimeUtil.ParseLocal(value), TimeZoneInfo.Local.Id); }
    private static AppStore Store(string label) { return new AppStore(Path.Combine(scratch, label)); }
    private static CalendarEvent Course()
    {
        return new CalendarEvent { Title = "Seminar", Date = "2026-08-31", RepeatEndDate = "2026-10-05", RepeatWeeks = 2, WeekDays = new List<int> { 1, 4 }, RecurrenceAnchorDate = "2026-09-14" };
    }
    private static string LegacyJson(AppData data)
    {
        Dictionary<string, object> root = Map(Json().DeserializeObject(Json().Serialize(data)));
        root["Version"] = 1; root.Remove("Settings");
        foreach (object e in (object[])root["Events"]) Map(e).Remove("RecurrenceAnchorDate");
        foreach (object w in Map(root["Windows"]).Values) Map(w).Remove("PositionLocked");
        foreach (object book in (object[])root["Books"])
        foreach (object page in (object[])Map(book)["Pages"]) Map(page).Remove("Archived");
        return Json().Serialize(root);
    }
    public static int Main()
    {
        scratch = Path.Combine(Path.GetTempPath(), "DeskStudy-SettingsCore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            Run("Version 1 migration: exact original retained before overwrite; content preserved", delegate
            {
                AppStore initial = Store("legacy"); AppData old = initial.Data;
                CalendarEvent course = Course(); course.RecurrenceAnchorDate = ""; CalendarEngine.CancelOccurrence(course, "2026-09-14"); old.Events.Add(course);
                foreach (Notebook b in old.Books)
                {
                    b.Name += " custom"; b.Pages[0].Text = "Historic text " + b.Id;
                    b.Pages[0].Tasks.Add(new TaskItem { Text = "Done", Completed = true });
                    NotePage page = new NotePage { Title = "Second page" }; b.Pages.Add(page); b.CurrentPageId = page.Id;
                }
                old.Windows["calendar"].X = -1234; old.Windows["calendar"].TopMost = true;
                string primary = Path.Combine(initial.DirectoryPath, "data.json"); string legacy = LegacyJson(old);
                File.WriteAllText(primary, legacy); byte[] original = File.ReadAllBytes(primary);
                AppStore migrated = new AppStore(initial.DirectoryPath);
                Assert(migrated.Data.Version == 2 && migrated.Data.Settings != null, "Migration did not construct settings.");
                Assert(File.ReadAllBytes(primary).SequenceEqual(original), "Loading rewrote original prematurely.");
                string[] originals = Directory.GetFiles(initial.DirectoryPath, "migration-v1-*.json");
                Assert(originals.Length == 1 && File.ReadAllBytes(originals[0]).SequenceEqual(original), "Exact legacy backup missing.");
                Assert(migrated.Data.Events[0].ExcludedDates.SequenceEqual(new[] { "2026-09-14" }), "Migration lost course exceptions.");
                Assert(migrated.Data.Events[0].RecurrenceAnchorDate == "", "Legacy recurrence changed anchor.");
                Assert(migrated.Data.Windows["calendar"].X == -1234 && migrated.Data.Windows["calendar"].TopMost && !migrated.Data.Windows["calendar"].PositionLocked, "Legacy layout changed.");
                foreach (Notebook b in migrated.Data.Books)
                    Assert(b.Pages.Count == 2 && b.CurrentPageId == b.Pages[1].Id && b.Pages[0].Tasks[0].Completed && b.Pages[0].Text == "Historic text " + b.Id && !b.Pages[0].Archived, "Legacy notebook content changed.");
                migrated.Save(); AppStore again = new AppStore(initial.DirectoryPath);
                Assert(again.Data.Version == 2 && Directory.GetFiles(initial.DirectoryPath, "migration-v1-*.json").Length == 1, "Migration repeated after save.");
                Assert(File.ReadAllBytes(originals[0]).SequenceEqual(original), "Saving altered the migration backup.");
            });
            Run("Version 1 import is supported and protects current state", delegate
            {
                AppStore destination = Store("legacy-import"); destination.Data.Books[0].Pages[0].Text = "Current data";
                AppData incoming = new AppData(); incoming.Books[0].Pages[0].Text = "Legacy incoming";
                string path = Path.Combine(scratch, "legacy-incoming.json"); File.WriteAllText(path, LegacyJson(incoming));
                destination.Import(path);
                Assert(destination.Data.Version == 2 && destination.Data.Books[0].Pages[0].Text == "Legacy incoming", "Legacy import failed.");
                Assert(File.ReadAllText(Directory.GetFiles(destination.DirectoryPath, "before-import-*.json").Single()).Contains("Current data"), "Current unsaved content was not protected.");
            });
            Run("Settings, lock, archive, selected pages and saved layout survive restart", delegate
            {
                AppStore store = Store("roundtrip"); AppSettings s = store.Data.Settings;
                s.GlobalAppearance.Theme = "Dark"; s.GlobalAppearance.BackgroundColor = "#112233"; s.GlobalAppearance.FontSize = 12F; s.GlobalAppearance.Opacity = 0.75;
                s.AppearanceOverrides["todo"] = new AppearanceOptions { BackgroundColor = "#FFEEDD", FontSize = 11F, Opacity = 0.9 };
                s.Calendar.DefaultView = "Month"; s.Calendar.WeekStartDay = 0; s.Calendar.SemesterStart = "2026-09-01"; s.Calendar.SemesterEnd = "2027-01-31"; s.Calendar.TeachingWeekOne = "2026-09-07";
                s.Reminders.QuietHoursEnabled = true; s.Reminders.SoundEnabled = false; s.Reminders.DefaultLeadMinutes = 75; s.AutoSaveDelayMs = 700; s.LaunchAtStartup = true;
                s.SavedLayoutUtc = DateTime.UtcNow.ToString("o");
                foreach (string key in new[] { "calendar", "todo", "ddl" }) s.SavedLayout[key] = new WindowState { X = 50, Y = 80, Width = 600, Height = 450, PositionLocked = true };
                store.Data.Windows["calendar"].PositionLocked = true; store.Data.Windows["todo"].Visible = false;
                foreach (Notebook b in store.Data.Books)
                {
                    b.Pages[0].Archived = true; NotePage second = new NotePage { Title = "Displayed " + b.Id }; b.Pages.Add(second); b.CurrentPageId = second.Id;
                }
                store.Save(); AppStore restarted = new AppStore(store.DirectoryPath); AppSettings r = restarted.Data.Settings;
                Assert(r.GlobalAppearance.Theme == "Dark" && r.GlobalAppearance.FontSize == 12F && r.GlobalAppearance.Opacity == 0.75, "Global appearance did not restore.");
                Assert(r.AppearanceOverrides["todo"].BackgroundColor == "#FFEEDD" && r.Calendar.DefaultView == "Month" && r.Calendar.WeekStartDay == 0, "Override/calendar settings lost.");
                Assert(r.Reminders.QuietHoursEnabled && !r.Reminders.SoundEnabled && r.Reminders.DefaultLeadMinutes == 75 && r.AutoSaveDelayMs == 700 && r.LaunchAtStartup, "Reminder/application settings lost.");
                Assert(r.SavedLayout.Count == 3 && r.SavedLayout["ddl"].PositionLocked && restarted.Data.Windows["calendar"].PositionLocked && !restarted.Data.Windows["todo"].Visible, "Saved layout/window state lost.");
                Assert(restarted.Data.Books.All(b => b.Pages[0].Archived && b.CurrentPageId == b.Pages[1].Id), "Archived or currently selected pages lost.");
            });
            Run("Appearance inheritance follows global; removing an override restores following", delegate
            {
                AppData data = new AppData(); data.Settings.GlobalAppearance.FontSize = 10F;
                Assert(SettingsLogic.EffectiveAppearance(data, "todo").FontSize == 10F, "Default does not inherit.");
                data.Settings.AppearanceOverrides["todo"] = new AppearanceOptions { FontSize = 13F };
                data.Settings.GlobalAppearance.FontSize = 11F;
                Assert(SettingsLogic.EffectiveAppearance(data, "todo").FontSize == 13F && SettingsLogic.EffectiveAppearance(data, "ddl").FontSize == 11F, "Override affected another component.");
                data.Settings.AppearanceOverrides.Remove("todo");
                Assert(SettingsLogic.EffectiveAppearance(data, "todo").FontSize == 11F, "Removing override did not restore global.");
            });
            Run("Malformed or future settings imports leave live and disk state untouched", delegate
            {
                AppStore store = Store("reject"); store.Data.Books[0].Pages[0].Text = "Preserve me"; store.Save();
                AppData live = store.Data; string primary = Path.Combine(store.DirectoryPath, "data.json"); string bytes = File.ReadAllText(primary);
                string path = Path.Combine(scratch, "invalid-settings.json");
                Action<Action<Dictionary<string, object>>> invalid = delegate(Action<Dictionary<string, object>> mutate)
                {
                    Dictionary<string, object> root = Map(Json().DeserializeObject(bytes)); mutate(root); File.WriteAllText(path, Json().Serialize(root));
                    Reject(delegate { store.Import(path); }, "Invalid settings accepted.");
                    Assert(Object.ReferenceEquals(store.Data, live) && File.ReadAllText(primary) == bytes, "Rejected import mutated data.");
                };
                invalid(delegate(Dictionary<string, object> root) { root.Remove("Settings"); });
                invalid(delegate(Dictionary<string, object> root) { Map(Map(root["Settings"])["GlobalAppearance"]).Remove("Opacity"); });
                invalid(delegate(Dictionary<string, object> root) { Map(Map(root["Settings"])["GlobalAppearance"])["Opacity"] = 0.1; });
                invalid(delegate(Dictionary<string, object> root) { Map(Map(root["Settings"])["Calendar"])["WeekStartDay"] = 7; });
                invalid(delegate(Dictionary<string, object> root) { Map(Map(root["Settings"])["Reminders"])["QuietStart"] = "25:00"; });
                invalid(delegate(Dictionary<string, object> root) { Map(root["Settings"])["AutoSaveDelayMs"] = 0; });
                invalid(delegate(Dictionary<string, object> root) { root["Version"] = 99; });
                invalid(delegate(Dictionary<string, object> root) { Map(((object[])Map(((object[])root["Books"])[0])["Pages"])[0]).Remove("Archived"); });
                Assert(Directory.GetFiles(store.DirectoryPath, "before-import-*.json").Length == 0, "Rejected import created misleading safety backup.");
            });
            Run("Quiet hours: boundaries, midnight crossing, all-day and disabled", delegate
            {
                AppSettings s = new AppSettings(); s.Reminders.QuietHoursEnabled = true;
                Assert(!SettingsLogic.IsQuietHours(s, DateTime.Today.AddHours(21).AddMinutes(59)), "Quiet started early.");
                Assert(SettingsLogic.IsQuietHours(s, DateTime.Today.AddHours(22)) && SettingsLogic.IsQuietHours(s, DateTime.Today.AddHours(7).AddMinutes(59)), "Midnight crossing incorrect.");
                Assert(!SettingsLogic.IsQuietHours(s, DateTime.Today.AddHours(8)), "Quiet end must be exclusive.");
                s.Reminders.QuietStart = "10:00"; s.Reminders.QuietEnd = "12:00";
                Assert(SettingsLogic.IsQuietHours(s, DateTime.Today.AddHours(11)) && !SettingsLogic.IsQuietHours(s, DateTime.Today.AddHours(13)), "Daytime quiet interval failed.");
                s.Reminders.QuietStart = s.Reminders.QuietEnd = "00:00";
                Assert(SettingsLogic.IsQuietHours(s, DateTime.Today.AddHours(13)), "Equal endpoints should mean all-day quiet.");
                s.Reminders.QuietHoursEnabled = false;
                Assert(!SettingsLogic.IsQuietHours(s, DateTime.Today.AddHours(13)), "Disabled quiet hours still suppress.");
            });
            Run("Hidden archived old-page DDL defers during quiet then catches up exactly once", delegate
            {
                AppData data = new AppData(); data.Settings.Reminders.QuietHoursEnabled = true;
                Notebook book = data.Books[1]; NotePage first = book.Pages[0]; first.Archived = true;
                NotePage second = new NotePage(); book.Pages.Add(second); book.CurrentPageId = second.Id; data.Windows["ddl"].Visible = false;
                TaskItem task = new TaskItem { Text = "Archived DDL", DueLocal = "2026-11-02T23:00:00", ReminderMinutes = 30, TimeZoneId = TimeZoneInfo.Local.Id }; first.Tasks.Add(task);
                Assert(ReminderEngine.Scan(data, LocalUtc("2026-11-02T22:30:00")).Count == 0, "Quiet-hour advance was emitted.");
                Assert(ReminderEngine.Scan(data, LocalUtc("2026-11-03T07:59:55")).Count == 0, "Quiet-hour deadline was emitted.");
                Assert(task.AdvanceNotifiedKey == "" && task.DueNotifiedKey == "" && data.ReminderHistory.Count == 0, "Quiet consumed keys/history.");
                List<ReminderRecord> notices = ReminderEngine.Scan(data, LocalUtc("2026-11-03T08:00:00"));
                Assert(notices.Count == 1 && notices[0].TaskId == task.Id && notices[0].Kind == "due" && notices[0].CatchUp, "Deferred reminder did not catch up after a recent scan.");
                Assert(ReminderEngine.Scan(data, LocalUtc("2026-11-03T08:00:10")).Count == 0, "Deferred reminder repeated.");
                Assert(first.Archived && book.CurrentPageId == second.Id && !data.Windows["ddl"].Visible, "Reminder changed archive/current/visibility state.");
            });
            Run("Completing during quiet cancels deferred deadline; disabling quiet still marks catch-up", delegate
            {
                AppData data = new AppData(); data.Settings.Reminders.QuietHoursEnabled = true;
                data.Settings.Reminders.QuietStart = data.Settings.Reminders.QuietEnd = "00:00";
                TaskItem completed = new TaskItem { Text = "Done", DueLocal = "2026-11-02T10:00:00", TimeZoneId = TimeZoneInfo.Local.Id };
                TaskItem pending = new TaskItem { Text = "Pending", DueLocal = "2026-11-02T10:00:00", TimeZoneId = TimeZoneInfo.Local.Id };
                data.Books[0].Pages[0].Tasks.Add(completed); data.Books[0].Pages[0].Tasks.Add(pending);
                Assert(ReminderEngine.Scan(data, LocalUtc("2026-11-02T11:00:00")).Count == 0, "All-day quiet did not defer.");
                completed.Completed = true; data.Settings.Reminders.QuietHoursEnabled = false;
                List<ReminderRecord> notices = ReminderEngine.Scan(data, LocalUtc("2026-11-02T11:00:05"));
                Assert(notices.Count == 1 && notices[0].TaskId == pending.Id && notices[0].CatchUp, "Completion/disabling quiet mishandled deferred tasks.");
            });
            Run("Teaching week anchor handles negative offsets, bounds and single cancellation", delegate
            {
                AppData data = new AppData(); CalendarEvent c = Course(); data.Events.Add(c);
                string dates = String.Join(",", CalendarEngine.GetOccurrences(data, D("2026-08-01"), D("2026-12-01")).Select(o => o.Date));
                Assert(dates == "2026-08-31,2026-09-03,2026-09-14,2026-09-17,2026-09-28,2026-10-01", "Incorrect anchor parity/bounds: " + dates);
                Assert(!CalendarEngine.IsScheduled(c, D("2026-09-10")), "Negative partial week truncated toward zero.");
                CalendarEngine.CancelOccurrence(c, "2026-09-17");
                Assert(CalendarEngine.GetOccurrences(data, D("2026-08-01"), D("2026-12-01")).Count == 5, "Cancel changed more than one occurrence.");
                data.Settings.Calendar.TeachingWeekOne = "2026-09-16";
                Assert(SettingsLogic.TeachingWeek(data.Settings, D("2026-09-14")) == 1 && SettingsLogic.TeachingWeek(data.Settings, D("2026-09-13")) == 0 && SettingsLogic.TeachingWeek(data.Settings, D("2026-09-28")) == 3, "Teaching week Monday normalization failed.");
                c.RecurrenceAnchorDate = "";
                Assert(CalendarEngine.IsScheduled(c, D("2026-08-31")), "Removing anchor did not preserve legacy start behavior.");
            });
            Run("Explicit backup and export timestamps persist; failed export leaves timestamp intact", delegate
            {
                AppStore store = Store("backups"); store.Data.Books[1].Pages[0].Text = "Protect backup";
                string backup = store.CreateBackup();
                Assert(File.Exists(backup) && Path.GetFileName(backup).StartsWith("backup-") && store.Data.Settings.LastBackupUtc != "" && store.LatestBackupUtc != "", "Explicit backup metadata missing.");
                AppStore restart = new AppStore(store.DirectoryPath);
                Assert(restart.Data.Settings.LastBackupUtc == store.Data.Settings.LastBackupUtc && restart.LatestBackupUtc != "", "Backup metadata not saved.");
                string before = store.Data.Settings.LastBackupUtc;
                Reject(delegate { store.Export(Path.Combine(scratch, "nonexistent", "bad.json")); }, "Invalid destination export succeeded.");
                Assert(store.Data.Settings.LastBackupUtc == before, "Failed backup changed timestamp.");
                store.Data.Books[1].Pages[0].Text = "Unsaved replacement"; store.Import(backup);
                Assert(store.Data.Books[1].Pages[0].Text == "Protect backup", "Backup restore did not restore data.");
                Assert(File.ReadAllText(Directory.GetFiles(store.DirectoryPath, "before-import-*.json").Single()).Contains("Unsaved replacement"), "Restore failed to preserve current unsaved data.");
            });
            Run("Rescue export succeeds while the primary file is locked; timestamp saves later", delegate
            {
                AppStore store = Store("rescue-export"); store.Save();
                string primary = Path.Combine(store.DirectoryPath, "data.json");
                string path = Path.Combine(scratch, "rescue-export.json"); store.Data.Books[0].Pages[0].Text = "Rescue unsaved content";
                using (FileStream locked = new FileStream(primary, FileMode.Open, FileAccess.Read, FileShare.None)) store.Export(path);
                Assert(File.Exists(path) && File.ReadAllText(path).Contains("Rescue unsaved content") && store.Data.Settings.LastBackupUtc != "", "Rescue export failed or lost in-memory timestamp.");
                Assert(Directory.GetFiles(store.DirectoryPath, "*.tmp").Length == 0, "Failed metadata save leaked a temporary file.");
                store.Save(); Assert(new AppStore(store.DirectoryPath).Data.Settings.LastBackupUtc == store.Data.Settings.LastBackupUtc, "Deferred timestamp did not persist.");
                string folder = store.DirectoryPath, temporaryFolder = folder + "-temporarily-moved";
                Directory.Move(folder, temporaryFolder);
                try { Assert(store.LatestBackupUtc == store.Data.Settings.LastBackupUtc, "Missing backup folder should use known timestamp."); }
                finally { Directory.Move(temporaryFolder, folder); }
            });
            Console.WriteLine("ALL " + passed + " SETTINGS CORE TESTS PASSED"); Console.WriteLine("Test data: " + scratch); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("FAIL after " + passed + " passing tests: " + ex); Console.Error.WriteLine("Retained data: " + scratch); return 1; }
    }
}
