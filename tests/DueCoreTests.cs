using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using DeskStudy;

// Date-only deadlines: reminder rules, display and data compatibility. No UI.
public static class DueCoreTests
{
    static int count;
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); count++; Console.WriteLine("PASS " + message); }
    static DateTime Utc(DateTime local) { return TimeUtil.LocalToUtc(local, TimeZoneInfo.Local.Id); }
    static TaskItem DateOnly(AppData data, DateTime day, string text)
    {
        var task = new TaskItem { Text = text, DueLocal = TimeUtil.DateOnlyDue(day), DueDateOnly = true, ReminderMinutes = 30 };
        data.Books[1].Pages[0].Tasks.Add(task); return task;
    }
    static int Fired(List<ReminderRecord> records, TaskItem task) { return records.Count(r => r.TaskId == task.Id); }

    public static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "DeskStudy-DueCore-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var data = new AppData(); DateTime day = DateTime.Today.AddDays(40);
            Assert(data.Settings.Reminders.DateOnlyReminderTime == "09:00" && TimeUtil.DateOnlyDue(day).EndsWith("T23:59:00"), "date-only deadlines default to a 09:00 reminder and are stored as that day's 23:59");
            var task = DateOnly(data, day, "只有日期");
            Assert(Fired(ReminderEngine.Scan(data, Utc(day.AddHours(8).AddMinutes(59))), task) == 0, "date-only: nothing before 09:00 on the due day");
            var morning = ReminderEngine.Scan(data, Utc(day.AddHours(9)));
            Assert(Fired(morning, task) == 1 && morning[0].Kind == "advance" && morning[0].DueDateOnly && !morning[0].CatchUp, "date-only: one reminder at 09:00 on the due day");
            Assert(Fired(ReminderEngine.Scan(data, Utc(day.AddHours(9).AddMinutes(5))), task) == 0, "date-only: the 09:00 reminder is not repeated");
            Assert(Fired(ReminderEngine.Scan(data, Utc(day.AddHours(23).AddMinutes(59).AddSeconds(30))), task) == 0 && Fired(ReminderEngine.Scan(data, Utc(day.AddDays(1).AddHours(10))), task) == 0, "date-only: no reminder at 23:59 or after the day has passed");

            data.Settings.Reminders.DateOnlyReminderTime = "07:30";
            Assert(Fired(ReminderEngine.Scan(data, Utc(day.AddHours(10))), task) == 0, "changing the reminder time does not re-send a delivered reminder");
            var early = DateOnly(data, day.AddDays(1), "改过提醒时间");
            Assert(Fired(ReminderEngine.Scan(data, Utc(day.AddDays(1).AddHours(7).AddMinutes(29))), early) == 0 && Fired(ReminderEngine.Scan(data, Utc(day.AddDays(1).AddHours(7).AddMinutes(30))), early) == 1, "the configured reminder time is used for later date-only deadlines");
            data.Settings.Reminders.DateOnlyReminderTime = "09:00";

            var missed = DateOnly(data, day.AddDays(3), "整天没开机");
            var late = ReminderEngine.Scan(data, Utc(day.AddDays(4).AddHours(10)));
            Assert(Fired(late, missed) == 1 && late.First(r => r.TaskId == missed.Id).Kind == "due" && late.First(r => r.TaskId == missed.Id).CatchUp, "a date-only deadline missed entirely is delivered once, late");
            Assert(Fired(ReminderEngine.Scan(data, Utc(day.AddDays(4).AddHours(11))), missed) == 0, "the late delivery is not repeated");

            var afternoon = DateOnly(data, day.AddDays(6), "当天下午才添加");
            ReminderEngine.SkipPassedDateOnlyReminder(afternoon, data.Settings, Utc(day.AddDays(6).AddHours(15)));
            Assert(afternoon.AdvanceNotifiedKey != "" && Fired(ReminderEngine.Scan(data, Utc(day.AddDays(6).AddHours(15).AddMinutes(1))), afternoon) == 0 && Fired(ReminderEngine.Scan(data, Utc(day.AddDays(7).AddHours(10))), afternoon) == 0, "a date-only deadline set after that morning's reminder time stays silent");
            var ahead = DateOnly(data, day.AddDays(8), "提前几天添加");
            ReminderEngine.SkipPassedDateOnlyReminder(ahead, data.Settings, Utc(day.AddDays(6).AddHours(15)));
            Assert(ahead.AdvanceNotifiedKey == "" && Fired(ReminderEngine.Scan(data, Utc(day.AddDays(8).AddHours(9))), ahead) == 1, "a date-only deadline set in advance still reminds that morning");

            var done = DateOnly(data, day.AddDays(9), "已完成"); done.Completed = true;
            Assert(Fired(ReminderEngine.Scan(data, Utc(day.AddDays(9).AddHours(12))), done) == 0, "a completed date-only task never reminds");

            var timed = new TaskItem { Text = "有具体时间", DueLocal = day.AddDays(10).AddHours(18).ToString(TimeUtil.LocalFormat, CultureInfo.InvariantCulture), ReminderMinutes = 60 };
            data.Books[1].Pages[0].Tasks.Add(timed);
            var advance = ReminderEngine.Scan(data, Utc(day.AddDays(10).AddHours(17)));
            var due = ReminderEngine.Scan(data, Utc(day.AddDays(10).AddHours(18)));
            Assert(Fired(advance, timed) == 1 && advance.First(r => r.TaskId == timed.Id).Kind == "advance" && Fired(due, timed) == 1 && due.First(r => r.TaskId == timed.Id).Kind == "due" && !due.First(r => r.TaskId == timed.Id).DueDateOnly, "timed deadlines still remind before and at the due time");

            Assert(TimeUtil.DueDisplay("2026-10-02T23:59:00", true) == "2026-10-02" && TimeUtil.DueDisplay("2026-10-02T18:00:00", false) == "2026-10-02 18:00" && TimeUtil.DueDisplay("", false) == "", "due display shows only the day for date-only deadlines");

            var store = new AppStore(root);
            store.Data.Books[1].LastDueDate = "2026-10-12"; store.Data.Books[1].LastDueTime = "20:00"; store.Data.Settings.Reminders.DateOnlyReminderTime = "08:15";
            store.Data.Books[1].Pages[0].Tasks.Add(new TaskItem { Text = "保存后仍是仅日期", DueLocal = TimeUtil.DateOnlyDue(day), DueDateOnly = true });
            store.Save();
            var reopened = new AppStore(root);
            Assert(reopened.LoadWarning == "" && reopened.Data.Books[1].LastDueDate == "2026-10-12" && reopened.Data.Books[1].LastDueTime == "20:00" && reopened.Data.Settings.Reminders.DateOnlyReminderTime == "08:15" && reopened.Data.Books[1].Pages[0].Tasks[0].DueDateOnly, "date-only flag, last-used date and time, and the reminder time survive a restart");

            string file = Path.Combine(root, "data.json"); var json = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };
            var map = (Dictionary<string, object>)json.DeserializeObject(File.ReadAllText(file));
            foreach (Dictionary<string, object> book in (object[])map["Books"])
            {
                book.Remove("LastDueDate"); book.Remove("LastDueTime");
                foreach (Dictionary<string, object> page in (object[])book["Pages"]) foreach (Dictionary<string, object> item in (object[])page["Tasks"]) item.Remove("DueDateOnly");
            }
            ((Dictionary<string, object>)((Dictionary<string, object>)map["Settings"])["Reminders"]).Remove("DateOnlyReminderTime");
            File.WriteAllText(file, json.Serialize(map));
            var older = new AppStore(root);
            Assert(older.LoadWarning == "" && older.Data.Books[1].LastDueDate == "" && older.Data.Books[1].LastDueTime == "" && older.Data.Settings.Reminders.DateOnlyReminderTime == "09:00" && !older.Data.Books[1].Pages[0].Tasks[0].DueDateOnly && older.Data.Books[1].Pages[0].Tasks[0].DueLocal.EndsWith("T23:59:00"), "1.3 data without the new fields opens with defaults and keeps its deadlines");

            older.Data.Books[1].Pages[0].Tasks[0].DueDateOnly = true; older.Data.Books[1].Pages[0].Tasks[0].DueLocal = "";
            bool rejected = false; try { older.Save(); } catch (InvalidDataException) { rejected = true; } catch (Exception) { rejected = true; }
            Assert(rejected, "a date-only flag without a date is rejected before saving");

            Console.WriteLine("ALL " + count + " DUE CORE CHECKS PASSED"); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}
