using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using DeskStudy;

public static class WidgetSettingsChecks
{
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Console.WriteLine("PASS " + message);
    }
    private static T Field<T>(object target, string name)
    {
        return (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }
    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control nested in Descendants(child)) yield return nested;
        }
    }
    private static void ClickHiddenButton(Button button)
    {
        // Dispatch the real button's Click event without displaying a modal editor.
        typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(button, new object[] { EventArgs.Empty });
    }

    public static void Run(AppController app)
    {
        CalendarForm calendar = (CalendarForm)app.Widgets.First(w => w.WidgetKey == "calendar");
        DateTime oldFocus = Field<DateTime>(calendar, "focusDate");
        string oldView = app.Data.Settings.Calendar.DefaultView;
        int oldWeekStart = app.Data.Settings.Calendar.WeekStartDay;
        int oldLead = app.Data.Settings.Reminders.DefaultLeadMinutes;
        try
        {
            calendar.GetType().GetField("focusDate", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(calendar, new DateTime(2026, 9, 30));
            app.Data.Settings.Calendar.DefaultView = "Week";
            app.Data.Settings.Calendar.WeekStartDay = 1;
            app.SettingsChanged();
            object surface = Field<object>(calendar, "surface");
            Assert(!Field<bool>(calendar, "monthView") && !Field<bool>(surface, "monthView") &&
                Field<DateTime>(surface, "startDate") == new DateTime(2026, 9, 28),
                "calendar applies default Week mode and Monday start to the rendered range");
            app.Data.Settings.Calendar.WeekStartDay = 0;
            app.SettingsChanged();
            Assert(Field<DateTime>(surface, "startDate") == new DateTime(2026, 9, 27),
                "changing week start to Sunday updates the calendar immediately");
            app.Data.Settings.Calendar.DefaultView = "Month";
            app.SettingsChanged();
            Assert(Field<bool>(calendar, "monthView") && Field<bool>(surface, "monthView") &&
                Field<DateTime>(surface, "startDate") == new DateTime(2026, 8, 30),
                "calendar applies default Month mode with a Sunday-aligned first row");
            Button week = Descendants(calendar).OfType<Button>().First(b => b.Text == "周");
            ClickHiddenButton(week);
            app.Save();
            Assert(!Field<bool>(calendar, "monthView"),
                "unrelated content synchronization does not override the user's selected calendar view");

            app.Data.Settings.Reminders.DefaultLeadMinutes = 45;
            using (var taskEditor = new TaskEditorDialog(null, Color.SeaGreen, app.Data.Settings.Reminders.DefaultLeadMinutes))
                Assert(taskEditor.ReminderMinutes == 45, "new task editor uses the configured default reminder lead");
            using (var taskEditor = new TaskEditorDialog(new TaskItem { Text = "Existing task", ReminderMinutes = 7 }, Color.SeaGreen, 45))
                Assert(taskEditor.ReminderMinutes == 7, "editing a task retains its individual reminder lead");

            var series = new CalendarEvent {
                Title = "Anchor editor test", Date = "2026-09-07", RepeatEndDate = "2026-12-18", RepeatWeeks = 2,
                RecurrenceAnchorDate = "2026-09-07", WeekDays = new List<int> { 1 },
                ExcludedDates = new List<string> { "2026-09-21" },
                Overrides = new List<EventOverride> { new EventOverride { OriginalDate = "2026-10-05", Date = "2026-10-06", Title = "Moved class", StartTime = "11:00", EndTime = "12:00", Location = "Lab", Notes = "One occurrence", Color = "#6C9385" } }
            };
            using (var editor = new CalendarEditor(series, true, true))
            {
                DateTimePicker anchor = Field<DateTimePicker>(editor, "anchorPicker");
                Assert(anchor.Enabled && anchor.Checked, "biweekly series editor exposes its checked recurrence anchor");
                anchor.Value = new DateTime(2026, 9, 14);
                ClickHiddenButton(Descendants(editor).OfType<Button>().First(b => b.Text == "保存日程"));
                Assert(editor.Result != null && editor.Result.RecurrenceAnchorDate == "2026-09-14",
                    "calendar editor saves the course-specific biweekly anchor through its Save button");
                Assert(editor.Result.ExcludedDates.SequenceEqual(series.ExcludedDates) && editor.Result.Overrides.Count == 1 &&
                    editor.Result.Overrides[0].OriginalDate == "2026-10-05" && editor.Result.Overrides[0].Date == "2026-10-06",
                    "editing a recurrence anchor preserves cancelled and rescheduled occurrences");
            }
            using (var editor = new CalendarEditor(series, true, true))
            {
                Field<DateTimePicker>(editor, "anchorPicker").Checked = false;
                ClickHiddenButton(Descendants(editor).OfType<Button>().First(b => b.Text == "保存日程"));
                Assert(editor.Result != null && editor.Result.RecurrenceAnchorDate == "",
                    "unchecking the anchor preserves legacy recurrence based on the series start week");
            }
        }
        finally
        {
            app.Data.Settings.Calendar.DefaultView = oldView;
            app.Data.Settings.Calendar.WeekStartDay = oldWeekStart;
            app.Data.Settings.Reminders.DefaultLeadMinutes = oldLead;
            calendar.GetType().GetField("focusDate", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(calendar, oldFocus);
            app.SettingsChanged();
        }
    }
}
