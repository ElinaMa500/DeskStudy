using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace DeskStudy
{
    // Deadline entry points for the notebooks: the quick-entry chip, Enter in the DDL book,
    // the clickable status line, and the hover "+ 截止时间" on Todo rows. All open one shared picker.
    public sealed partial class NotebookForm
    {
        private Button _quickDue;
        private DueChoice _pendingDue;
        private DuePickerPopup _duePopup;
        private bool IsDeadlineBook { get { return _bookId == "ddl"; } }
        public DuePickerPanel OpenDuePanel { get { return _duePopup != null && _duePopup.IsOpen ? _duePopup.Panel : null; } }

        private void InitializeDue()
        {
            _quickDue = SmallButton("+ 截止时间", null);
            _quickDue.Name = "quick-due"; _quickDue.AccessibleName = "为新任务设置截止时间"; _quickDue.Dock = DockStyle.None; _quickDue.Visible = false;
            _quickDue.FlatStyle = FlatStyle.Flat; _quickDue.FlatAppearance.BorderSize = 0;
            _quickDue.Click += delegate
            {
                // The trailing × clears a chosen deadline; anywhere else opens the picker.
                Point at = _quickDue.PointToClient(Cursor.Position);
                if (_pendingDue != null && _quickDue.ClientRectangle.Contains(at) && at.X > _quickDue.Width - Px(18)) { _pendingDue = null; UpdateQuickDue(); _quickText.Focus(); return; }
                OpenDuePicker(_quickDue, _pendingDue, _pendingDue != null,
                    delegate(DueChoice choice, bool cleared) { _pendingDue = choice.HasDue ? choice : null; UpdateQuickDue(); _quickText.Focus(); },
                    delegate { _quickText.Focus(); });
            };
            _quickEntry.Controls.Add(_quickDue);
        }

        private DuePalette PickerPalette()
        {
            if (IsReference)
            {
                var p = Palette();
                return new DuePalette { Back = p.Back, Ink = p.Ink, Sub = p.Sub, Faint = p.Faint, Rule = p.Rule, Soft = p.Soft, Accent = p.Accent, Warm = p.Warm };
            }
            // Original and Card use the 清爽卡片 skin; Paper uses the 手账纸页 skin.
            bool journal = _appliedLayout == "Paper";
            bool dark = AppearancePainter.Dark(AppearancePainter.Background(SettingsLogic.EffectiveAppearance(App.Data, _bookId)));
            var skin = new DuePalette
            {
                Back = Hex(dark ? (journal ? "#2D2B25" : "#262A2D") : (journal ? "#FAF8F1" : "#FFFFFF")),
                Rule = Hex(dark ? (journal ? "#48453B" : "#424844") : (journal ? "#E9E4D6" : "#E8ECE8")),
                Soft = Hex(dark ? (journal ? "#3B392F" : "#313A33") : (journal ? "#EFECDF" : "#F3F6F2")),
                Ink = Hex(dark ? "#E3E9E5" : "#283732"), Sub = Hex(dark ? "#AFB9B2" : "#68736E"),
                Accent = Hex(dark ? "#A5C5AC" : "#527562"), Warm = Hex(dark ? "#F1B894" : "#925331")
            };
            skin.Faint = Blend(skin.Sub, skin.Back, .35);
            return skin;
        }

        private void OpenDuePicker(Control anchor, DueChoice initial, bool allowClear, Action<DueChoice, bool> finished, Action cancelled)
        {
            if (_duePopup != null) { _duePopup.Close(); _duePopup.Dispose(); _duePopup = null; }
            Notebook book = FindBook();
            var options = new DuePickerOptions
            {
                Palette = PickerPalette(), Font = ReferenceFont(9F, FontStyle.Regular), Initial = initial, AllowClear = allowClear,
                DefaultLead = App.Data.Settings.Reminders.DefaultLeadMinutes, DateOnlyReminderTime = App.Data.Settings.Reminders.DateOnlyReminderTime,
                WeekStartDay = App.Data.Settings.Calendar.WeekStartDay, LastDate = book == null ? "" : book.LastDueDate ?? "", LastTime = book == null ? "" : book.LastDueTime ?? ""
            };
            _duePopup = new DuePickerPopup(options, finished, cancelled);
            _duePopup.Show(anchor);
        }

        private static DueChoice ChoiceOf(TaskItem task)
        {
            DateTime due;
            if (!DateTime.TryParseExact(task.DueLocal, TimeUtil.LocalFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out due)) return null;
            return new DueChoice { HasDue = true, Date = due.Date, DateOnly = task.DueDateOnly, Time = due.TimeOfDay, LeadMinutes = task.ReminderMinutes };
        }

        // Applies a picker result to a task and remembers it as the book's "上次" date and time.
        private void SetDue(TaskItem task, DueChoice choice)
        {
            string before = task.DueLocal + "|" + task.DueDateOnly + "|" + task.ReminderMinutes;
            if (!choice.HasDue) { task.DueLocal = ""; task.DueDateOnly = false; }
            else
            {
                task.DueLocal = choice.DueLocal; task.DueDateOnly = choice.DateOnly;
                if (!choice.DateOnly) task.ReminderMinutes = choice.LeadMinutes;
            }
            if (before != task.DueLocal + "|" + task.DueDateOnly + "|" + task.ReminderMinutes)
            {
                task.TimeZoneId = TimeZoneInfo.Local.Id;
                ReminderEngine.Reset(task);
                ReminderEngine.SkipPassedDateOnlyReminder(task, App.Data.Settings, DateTime.UtcNow);
            }
            Notebook book = FindBook();
            if (!choice.HasDue || book == null) return;
            book.LastDueDate = choice.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (!choice.DateOnly) book.LastDueTime = choice.Time.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        }

        private void UpdateQuickDue()
        {
            if (_quickDue == null) return;
            _quickDue.Text = _pendingDue == null ? "+ 截止时间" : DueLabel(_pendingDue) + "  ×";
            StyleQuickDue();
            if (IsModern && _contentReady) LayoutTaskCards();
        }
        private void StyleQuickDue()
        {
            DuePalette p = PickerPalette();
            _quickDue.Visible = IsModern && IsDeadlineBook;
            _quickDue.ForeColor = _pendingDue == null ? p.Faint : p.Accent;
            _quickDue.BackColor = _quickEntry.BackColor; _quickDue.FlatAppearance.BorderSize = 0;
            _quickDue.FlatAppearance.MouseOverBackColor = p.Soft; _quickDue.FlatAppearance.BorderColor = _quickEntry.BackColor;
            _quickDue.Font = ReferenceFont(8.5F, FontStyle.Regular);
        }
        // Places the deadline chip left of the add button and returns the width it took from the text box.
        private int PlaceQuickDue(int rightEdge, int top, int height, int available)
        {
            if (!_quickDue.Visible) return 0;
            int width = TextRenderer.MeasureText(_quickDue.Text, _quickDue.Font).Width + Px(12);
            // Too narrow to show both: keep the text box usable. Enter still opens the picker.
            if (available - width < Px(110)) { _quickDue.SetBounds(0, 0, 0, 0); return 0; }
            _quickDue.SetBounds(rightEdge - width, top, width, height);
            return width + Px(4);
        }
        private static string DueLabel(DueChoice choice)
        {
            return choice.Date.Month + "/" + choice.Date.Day + " " + Lang.Weekday(choice.Date.DayOfWeek) + (choice.DateOnly ? "" : " " + choice.Time.ToString(@"hh\:mm", CultureInfo.InvariantCulture));
        }

        // In the DDL book, Enter without a chosen deadline opens the picker; a second Enter adds the task without one.
        private void SubmitQuickTask()
        {
            if (_quickText.IsComposing || String.IsNullOrWhiteSpace(_quickText.Text)) return;
            if (IsDeadlineBook && _pendingDue == null)
            {
                OpenDuePicker(_quickEntry, null, false, delegate(DueChoice choice, bool cleared) { AddQuickTask(choice); }, delegate { _quickText.Focus(); });
                return;
            }
            AddQuickTask(_pendingDue);
        }
        private void AddQuickTask(DueChoice choice)
        {
            var page = FindPage(_displayedPageId); if (page == null || String.IsNullOrWhiteSpace(_quickText.Text)) return;
            var task = new TaskItem { Text = _quickText.Text.Trim(), ReminderMinutes = App.Data.Settings.Reminders.DefaultLeadMinutes };
            if (choice != null && choice.HasDue) SetDue(task, choice);
            page.Tasks.Add(task);
            _pendingDue = null; _quickDue.Text = "+ 截止时间";
            _quickText.Clear(); Persist(); RefreshFromData(); _quickText.Focus(); _tasks.ScrollControlIntoView(_quickEntry);
        }

        private void OpenTaskDue(TaskRow row, Control anchor)
        {
            string pageId = _displayedPageId, taskId = row.Task.Id;
            DueChoice current = ChoiceOf(row.Task);
            OpenDuePicker(anchor, current, current != null, delegate(DueChoice choice, bool cleared)
            {
                NotePage page = FindPage(pageId);
                TaskItem task = page == null ? null : page.Tasks.Find(delegate(TaskItem item) { return item.Id == taskId; });
                if (task == null) return;
                SetDue(task, choice); Persist(); RefreshFromData();
            }, null);
        }

        // Status line: click to set or change the deadline. "+ 截止时间" appears on hover for rows that show no status.
        private void WireDueRow(TaskRow row)
        {
            bool hot = false;
            row.Status.Cursor = Cursors.Hand; row.Status.AccessibleName = "设置截止时间";
            row.Status.Click += delegate { OpenTaskDue(row, row.Status); };
            row.Status.MouseEnter += delegate { hot = true; if (!row.Status.IsDisposed) row.Status.Invalidate(); };
            row.Status.MouseLeave += delegate { hot = false; if (!row.Status.IsDisposed) row.Status.Invalidate(); };
            row.Status.Paint += delegate(object sender, PaintEventArgs e)
            {
                if (!hot || row.Status.Text.Length == 0) return;
                string first = row.Status.Text.Split('\r')[0];
                int width = Math.Min(row.Status.Width - 1, TextRenderer.MeasureText(first, row.Status.Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width);
                using (var pen = new Pen(row.Status.ForeColor)) e.Graphics.DrawLine(pen, 3, row.Status.Font.Height, width, row.Status.Font.Height);
            };
            row.AddDue = new Label { Text = "+ 截止时间", AutoSize = false, Visible = false, Cursor = Cursors.Hand, TextAlign = ContentAlignment.MiddleRight, UseMnemonic = false, AccessibleName = "为这项任务设置截止时间" };
            row.AddDue.Name = "add-due-" + row.Task.Id;
            row.AddDue.Click += delegate { OpenTaskDue(row, row.AddDue); };
            row.Card.Controls.Add(row.AddDue);
            // Rows are rebuilt while the pointer may still be over them; a row being torn down must not be touched.
            Func<bool> alive = delegate { return !row.Card.IsDisposed && !row.Card.Disposing && row.Card.IsHandleCreated && !row.AddDue.IsDisposed; };
            EventHandler enter = delegate { if (alive() && OffersHoverDue(row)) { row.AddDue.Visible = true; row.AddDue.BringToFront(); } };
            EventHandler leave = delegate { if (alive() && !row.Card.ClientRectangle.Contains(row.Card.PointToClient(Cursor.Position))) row.AddDue.Visible = false; };
            foreach (Control c in new Control[] { row.Card, row.Toggle, row.Title, row.More, row.AddDue }) { c.MouseEnter += enter; c.MouseLeave += leave; }
        }
        private bool OffersHoverDue(TaskRow row)
        {
            return IsModern && String.IsNullOrEmpty(row.Task.DueLocal) && !row.Status.Visible;
        }

        private string DayReminderText { get { return "当天 " + App.Data.Settings.Reminders.DateOnlyReminderTime + " 提醒"; } }
    }
}
