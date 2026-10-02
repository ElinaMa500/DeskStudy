using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace DeskStudy
{
    // What the due picker hands back. No date means "no deadline"; a date without a time is date-only.
    public sealed class DueChoice
    {
        public bool HasDue;
        public DateTime Date;
        public bool DateOnly;
        public TimeSpan Time;
        public int LeadMinutes;
        public string DueLocal
        {
            get { return !HasDue ? "" : DateOnly ? TimeUtil.DateOnlyDue(Date) : Date.Date.Add(Time).ToString(TimeUtil.LocalFormat, CultureInfo.InvariantCulture); }
        }
    }

    public struct DuePalette { public Color Back, Ink, Sub, Faint, Rule, Soft, Accent, Warm; }

    public sealed class DuePickerOptions
    {
        public DuePalette Palette;
        public Font Font;
        public int DefaultLead = 30;
        public string DateOnlyReminderTime = "09:00";
        public int WeekStartDay = 1;
        public string LastDate = "", LastTime = "";
        public bool AllowClear;
        public DueChoice Initial;
    }

    // A pill that stays highlighted while its value is the selected one.
    internal sealed class ChipButton : Button
    {
        public DuePalette Palette { get; set; }
        public bool Selected { get; set; }
        public object Value { get; set; }
        public ChipButton(string text)
        {
            Text = text; AutoSize = false; FlatStyle = FlatStyle.Flat; Cursor = Cursors.Hand; Margin = Padding.Empty; UseMnemonic = false;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
        }
        private bool hot;
        protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hot = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.Clear(Palette.Back); g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = DuePickerPanel.Round(rect, Height / 2))
            {
                using (var fill = new SolidBrush(Selected ? Palette.Accent : hot ? Palette.Soft : Palette.Back)) g.FillPath(fill, path);
                if (!Selected) using (var pen = new Pen(Palette.Rule)) g.DrawPath(pen, path);
                if (Focused && ShowFocusCues) using (var pen = new Pen(Palette.Accent) { DashStyle = DashStyle.Dot }) g.DrawPath(pen, path);
            }
            TextRenderer.DrawText(g, Text, Font, rect, Selected ? Palette.Back : Enabled ? Palette.Ink : Palette.Faint, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    // Month view drawn to match the notebook. Arrow keys move and select in one step, so Enter can always mean "done".
    internal sealed class MonthGrid : Control
    {
        public DuePalette Palette { get; set; }
        public int WeekStartDay { get; set; }
        public DateTime Today { get; private set; }
        public DateTime Month { get; set; }
        public DateTime? Selected { get; set; }
        public event EventHandler SelectionChanged;
        private DateTime cursor;
        private int header, week, cell;

        public MonthGrid()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
            TabStop = true; AccessibleName = "选择截止日期"; AccessibleRole = AccessibleRole.Table;
            WeekStartDay = 1; Today = DateTime.Today;
            Month = new DateTime(Today.Year, Today.Month, 1); cursor = Today;
        }
        public void Measure(int columnWidth, int rowHeight)
        {
            cell = rowHeight; header = rowHeight + 2; week = rowHeight - 4;
            Size = new Size(columnWidth * 7, header + week + cell * 6);
        }
        public void Select(DateTime? date)
        {
            Selected = date.HasValue ? (DateTime?)date.Value.Date : null;
            if (date.HasValue) { cursor = date.Value.Date; Month = new DateTime(cursor.Year, cursor.Month, 1); }
            Invalidate();
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
        }
        private DateTime First()
        {
            int offset = ((int)Month.DayOfWeek - WeekStartDay + 7) % 7;
            return Month.AddDays(-offset);
        }
        private void ShiftMonth(int months) { Month = Month.AddMonths(months); cursor = Month; Invalidate(); }
        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            return key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down || base.IsInputKey(keyData);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            int days = e.KeyCode == Keys.Left ? -1 : e.KeyCode == Keys.Right ? 1 : e.KeyCode == Keys.Up ? -7 : e.KeyCode == Keys.Down ? 7 : 0;
            if (days != 0) { Select((Selected ?? cursor).AddDays(days)); e.Handled = true; }
            else if (e.KeyCode == Keys.PageUp) { Select((Selected ?? cursor).AddMonths(-1)); e.Handled = true; }
            else if (e.KeyCode == Keys.PageDown) { Select((Selected ?? cursor).AddMonths(1)); e.Handled = true; }
            else if (e.KeyCode == Keys.Space) { Select(cursor); e.Handled = true; }
            base.OnKeyDown(e);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            if (e.Y < header)
            {
                if (e.X < header * 2) ShiftMonth(-1); else if (e.X > Width - header * 2) ShiftMonth(1);
                return;
            }
            int row = (e.Y - header - week) / Math.Max(1, cell), column = e.X / Math.Max(1, Width / 7);
            if (e.Y < header + week || row < 0 || row > 5 || column < 0 || column > 6) return;
            Select(First().AddDays(row * 7 + column));
        }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.Clear(Palette.Back); g.SmoothingMode = SmoothingMode.AntiAlias;
            int column = Width / 7;
            using (var bold = new Font(Font, FontStyle.Bold))
                TextRenderer.DrawText(g, Month.Year + " 年 " + Month.Month + " 月", bold, new Rectangle(0, 0, Width, header), Palette.Ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, "‹", Font, new Rectangle(0, 0, header * 2, header), Palette.Sub, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, "›", Font, new Rectangle(Width - header * 2, 0, header * 2, header), Palette.Sub, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            for (int i = 0; i < 7; i++)
                TextRenderer.DrawText(g, Lang.WeekdayLetter((DayOfWeek)((WeekStartDay + i) % 7)), Font, new Rectangle(i * column, header, column, week), Palette.Faint, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            DateTime first = First();
            for (int i = 0; i < 42; i++)
            {
                DateTime day = first.AddDays(i);
                var box = new Rectangle((i % 7) * column, header + week + (i / 7) * cell, column, cell);
                int size = Math.Min(column, cell) - 4;
                var dot = new Rectangle(box.X + (box.Width - size) / 2, box.Y + (box.Height - size) / 2, size, size);
                bool selected = Selected.HasValue && Selected.Value == day, inMonth = day.Month == Month.Month;
                if (selected) using (var fill = new SolidBrush(Palette.Accent)) g.FillEllipse(fill, dot);
                else if (day == cursor && Focused) using (var fill = new SolidBrush(Palette.Soft)) g.FillEllipse(fill, dot);
                if (day == Today && !selected) using (var pen = new Pen(Palette.Accent, 1.4F)) g.DrawEllipse(pen, dot);
                Color color = selected ? Palette.Back : !inMonth ? Palette.Faint : day < Today ? Palette.Sub : Palette.Ink;
                TextRenderer.DrawText(g, day.Day.ToString(CultureInfo.InvariantCulture), Font, box, color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }

    // The shared deadline picker. Hosted in a drop-down next to whatever opened it.
    public sealed class DuePickerPanel : UserControl
    {
        private readonly DuePickerOptions options;
        private readonly MonthGrid grid = new MonthGrid();
        private readonly List<ChipButton> dateChips = new List<ChipButton>(), timeChips = new List<ChipButton>(), leadChips = new List<ChipButton>();
        private readonly CompositionTextBox timeBox = new CompositionTextBox { Cue = "时:分" }, leadBox = new CompositionTextBox { Cue = "分钟" };
        private bool arranged, arrangedTimed;
        private readonly Label timeLabel = new Label(), leadLabel = new Label(), leadNote = new Label(), summary = new Label(), hint = new Label();
        private readonly Button done = new Button(), clear = new Button();
        private TimeSpan? time;
        private int lead;
        private bool updating;
        public bool Completed { get; private set; }
        public bool ClearRequested { get; private set; }
        public DueChoice Result { get; private set; }
        public event EventHandler Finished;
        public DateTime? SelectedDate { get { return grid.Selected; } }
        public TimeSpan? SelectedTime { get { return time; } }
        public int SelectedLead { get { return lead; } }
        public string SummaryText { get { return summary.Text; } }
        public IEnumerable<string> DateChipTexts { get { return dateChips.Select(c => c.Text); } }
        public IEnumerable<string> TimeChipTexts { get { return timeChips.Select(c => c.Text); } }

        internal static GraphicsPath Round(Rectangle rect, int radius)
        {
            int d = Math.Max(2, Math.Min(radius * 2, Math.Min(rect.Width, rect.Height)));
            var path = new GraphicsPath();
            path.AddArc(rect.Left, rect.Top, d, d, 180, 90); path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90); path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90); path.CloseFigure();
            return path;
        }

        public DuePickerPanel(DuePickerOptions pickerOptions)
        {
            options = pickerOptions;
            DuePalette p = options.Palette;
            Font = options.Font; BackColor = p.Back; ForeColor = p.Ink; Name = "due-picker"; AccessibleName = "设置截止时间";
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            lead = options.DefaultLead;
            DateTime today = DateTime.Today;

            AddDateChip("今天", today);
            AddDateChip("明天", today.AddDays(1));
            int toFriday = ((int)DayOfWeek.Friday - (int)today.DayOfWeek + 7) % 7;
            if (toFriday > 1) AddDateChip("本周五", today.AddDays(toFriday)); else AddDateChip("下周五", today.AddDays(toFriday + 7));
            int toMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
            AddDateChip("下周一", today.AddDays(toMonday == 0 ? 7 : toMonday));
            DateTime last;
            if (DateTime.TryParseExact(options.LastDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out last) && last >= today && dateChips.All(c => (DateTime)c.Value != last))
                AddDateChip("上次 " + last.Month + "/" + last.Day, last);

            grid.Palette = p; grid.WeekStartDay = options.WeekStartDay; grid.Font = Font; grid.Name = "due-month";
            grid.SelectionChanged += delegate { Sync(); };
            Controls.Add(grid);

            Caption(timeLabel, "时间");
            foreach (string preset in new[] { "12:00", "18:00", "23:59" }) AddTimeChip(preset, preset);
            TimeSpan lastTime;
            if (TryTime(options.LastTime, out lastTime) && timeChips.All(c => (TimeSpan)c.Value != lastTime)) AddTimeChip("上次 " + options.LastTime, options.LastTime);
            StyleBox(timeBox, "due-time", "自定义截止时间，例如 18:30");
            timeBox.TextChanged += delegate { if (updating) return; TimeSpan typed; time = TryTime(timeBox.Text, out typed) ? (TimeSpan?)typed : null; Sync(); };

            Caption(leadLabel, "提醒");
            foreach (int minutes in new[] { 0, 30, 60, 1440 }) AddLeadChip(minutes);
            if (leadChips.All(c => (int)c.Value != lead)) AddLeadChip(lead);
            StyleBox(leadBox, "due-lead", "自定义提前分钟数");
            leadBox.TextChanged += delegate { if (updating) return; int typed; if (Int32.TryParse(leadBox.Text.Trim(), out typed) && typed >= 0 && typed <= 525600) { lead = typed; Sync(); } };
            leadNote.AutoSize = false; leadNote.ForeColor = p.Sub; leadNote.BackColor = p.Back; leadNote.TextAlign = ContentAlignment.MiddleLeft; leadNote.UseMnemonic = false;
            Controls.Add(leadNote);

            summary.AutoSize = false; summary.Name = "due-summary"; summary.ForeColor = p.Ink; summary.BackColor = p.Back; summary.TextAlign = ContentAlignment.MiddleLeft; summary.UseMnemonic = false; summary.AutoEllipsis = true;
            hint.AutoSize = false; hint.Text = "Enter 完成 · Esc 取消"; hint.ForeColor = p.Faint; hint.BackColor = p.Back; hint.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(summary); Controls.Add(hint);

            Flat(done, "完成", "due-done"); done.BackColor = p.Accent; done.ForeColor = p.Back; done.FlatAppearance.MouseOverBackColor = p.Accent;
            done.Click += delegate { Complete(); };
            Flat(clear, "清除截止时间", "due-clear"); clear.BackColor = p.Back; clear.ForeColor = p.Warm; clear.FlatAppearance.MouseOverBackColor = p.Soft; clear.Visible = options.AllowClear;
            clear.Click += delegate { ClearRequested = true; grid.Selected = null; time = null; Complete(); };

            if (options.Initial != null && options.Initial.HasDue)
            {
                grid.Selected = options.Initial.Date.Date; grid.Month = new DateTime(options.Initial.Date.Year, options.Initial.Date.Month, 1);
                time = options.Initial.DateOnly ? (TimeSpan?)null : options.Initial.Time; lead = options.Initial.LeadMinutes;
                if (!options.Initial.DateOnly && leadChips.All(c => (int)c.Value != lead)) AddLeadChip(lead);
            }
            Arrange(); Sync();
        }

        private float Factor() { using (Graphics g = CreateGraphics()) return g.DpiX / 96F * Font.SizeInPoints / 9F; }
        private void Caption(Label label, string text)
        {
            label.Text = text; label.AutoSize = false; label.ForeColor = options.Palette.Sub; label.BackColor = options.Palette.Back; label.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(label);
        }
        private void StyleBox(CompositionTextBox box, string name, string accessible)
        {
            box.Name = name; box.AccessibleName = accessible; box.BorderStyle = BorderStyle.FixedSingle; box.BackColor = options.Palette.Back; box.ForeColor = options.Palette.Ink;
            box.TextAlign = HorizontalAlignment.Center; box.MaxLength = 6; box.Font = Font;
            Controls.Add(box);
        }
        private void Flat(Button button, string text, string name)
        {
            button.Text = text; button.Name = name; button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderSize = 0; button.Cursor = Cursors.Hand; button.Font = Font; button.UseMnemonic = false;
            Controls.Add(button);
        }
        private ChipButton Chip(string text, object value, List<ChipButton> list, EventHandler click)
        {
            var chip = new ChipButton(text) { Palette = options.Palette, Value = value, Font = Font };
            chip.Click += click; list.Add(chip); Controls.Add(chip);
            return chip;
        }
        private void AddDateChip(string text, DateTime date)
        {
            var chip = Chip(text, date, dateChips, null); chip.Name = "due-date-" + dateChips.Count;
            chip.Click += delegate { grid.Select(grid.Selected == date ? (DateTime?)null : date); };
        }
        private void AddTimeChip(string text, string value)
        {
            TimeSpan parsed; TryTime(value, out parsed);
            var chip = Chip(text, parsed, timeChips, null); chip.Name = "due-time-" + value.Replace(":", "");
            chip.Click += delegate { SelectTime(time == parsed ? (TimeSpan?)null : parsed); };
        }
        private void AddLeadChip(int minutes)
        {
            string text = minutes == 0 ? "到期时" : minutes % 1440 == 0 ? minutes / 1440 + " 天前" : minutes % 60 == 0 ? minutes / 60 + " 小时前" : minutes + " 分钟前";
            var chip = Chip(text, minutes, leadChips, null); chip.Name = "due-lead-" + minutes;
            chip.Click += delegate { lead = minutes; updating = true; leadBox.Text = ""; updating = false; Sync(); };
        }
        public static bool TryTime(string text, out TimeSpan value)
        {
            value = TimeSpan.Zero; text = (text ?? "").Trim().Replace('：', ':');
            int hour, minute = 0; string[] parts = text.Split(':');
            if (parts.Length > 2 || !Int32.TryParse(parts[0], out hour) || (parts.Length == 2 && (parts[1].Length != 2 || !Int32.TryParse(parts[1], out minute)))) return false;
            if (hour < 0 || hour > 23 || minute < 0 || minute > 59) return false;
            value = new TimeSpan(hour, minute, 0); return true;
        }
        public void SelectDate(DateTime? date) { grid.Select(date); }
        public void SelectTime(TimeSpan? value)
        {
            time = value; updating = true; timeBox.Text = ""; updating = false; Sync();
        }
        public void SelectLead(int minutes) { lead = minutes; Sync(); }

        private int FlowRow(IEnumerable<Control> controls, int x, int y, int right, int height, int gap)
        {
            int left = x;
            foreach (Control c in controls)
            {
                if (x > left && x + c.Width > right) { x = left; y += height + gap; }
                c.SetBounds(x, y, c.Width, height); x += c.Width + gap;
            }
            return y + height;
        }
        private void Arrange()
        {
            float s = Factor(); Func<float, int> px = v => (int)Math.Round(v * s);
            int pad = px(14), gap = px(6), chipHeight = px(26), column = px(38), width = column * 7 + pad * 2, right = width - pad;
            foreach (ChipButton chip in dateChips.Concat(timeChips).Concat(leadChips)) chip.Width = TextRenderer.MeasureText(chip.Text, Font).Width + px(9);
            int y = FlowRow(dateChips.Cast<Control>(), pad, pad, right, chipHeight, gap) + px(8);
            grid.Measure(column, px(28)); grid.Location = new Point(pad, y); y = grid.Bottom + px(10);
            int label = TextRenderer.MeasureText("提醒", Font).Width + px(6);
            timeBox.Width = px(46); leadBox.Width = px(46);
            timeLabel.SetBounds(pad, y, label, chipHeight);
            y = FlowRow(timeChips.Cast<Control>().Concat(new Control[] { timeBox }), pad + label, y, right, chipHeight, gap) + px(8);
            leadLabel.SetBounds(pad, y, label, chipHeight);
            leadNote.SetBounds(pad + label, y, right - pad - label, chipHeight);
            // With only a date chosen the row is a one-line note; with a time it holds the lead chips.
            bool timed = time.HasValue;
            foreach (ChipButton chip in leadChips) chip.Visible = timed;
            leadBox.Visible = timed; leadNote.Visible = !timed;
            y = (timed ? FlowRow(leadChips.Cast<Control>().Concat(new Control[] { leadBox }), pad + label, y, right, chipHeight, gap) : y + chipHeight) + px(10);
            arranged = true; arrangedTimed = timed;
            summary.SetBounds(pad, y, right - pad, chipHeight); y = summary.Bottom + px(4);
            int doneWidth = TextRenderer.MeasureText(done.Text, Font).Width + px(28), clearWidth = TextRenderer.MeasureText(clear.Text, Font).Width + px(14);
            clear.SetBounds(pad - px(6), y, clearWidth, px(30)); done.SetBounds(right - doneWidth, y, doneWidth, px(30)); y += px(30) + px(6);
            hint.SetBounds(pad, y, right - pad, px(18)); y = hint.Bottom + pad - px(4);
            if (hint.Tag == null) { hint.Font = new Font(Font.FontFamily, Math.Max(7F, Font.SizeInPoints - 1F)); hint.Tag = hint.Font; }
            Size = new Size(width, y);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(options.Palette.Rule)) e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        private void Sync()
        {
            bool timed = time.HasValue;
            foreach (ChipButton chip in dateChips) { chip.Selected = grid.Selected.HasValue && (DateTime)chip.Value == grid.Selected.Value; chip.Invalidate(); }
            foreach (ChipButton chip in timeChips) { chip.Selected = timed && (TimeSpan)chip.Value == time.Value && timeBox.Text.Trim().Length == 0; chip.Invalidate(); }
            if (arranged && arrangedTimed != timed) Arrange();
            foreach (ChipButton chip in leadChips) { chip.Selected = (int)chip.Value == lead && leadBox.Text.Trim().Length == 0; chip.Invalidate(); }
            leadNote.Text = "只选日期：当天 " + options.DateOnlyReminderTime + " 提醒一次";
            DueChoice choice = Build();
            if (!choice.HasDue) { summary.Text = options.AllowClear ? "未选择日期" : "不设置截止时间"; summary.ForeColor = options.Palette.Sub; return; }
            DateTime moment = choice.DateOnly ? choice.Date.AddDays(1) : choice.Date.Add(choice.Time);
            bool passed = moment <= DateTime.Now;
            string text = choice.Date.Month + "/" + choice.Date.Day + " " + Lang.Weekday(choice.Date.DayOfWeek) + (choice.DateOnly ? "" : " " + choice.Time.ToString(@"hh\:mm"));
            summary.Text = passed ? text + " · 这个时间已经过了" : text;
            summary.ForeColor = passed ? options.Palette.Warm : options.Palette.Ink;
        }
        private DueChoice Build()
        {
            var choice = new DueChoice { LeadMinutes = lead };
            if (!grid.Selected.HasValue && !time.HasValue) return choice;
            choice.HasDue = true; choice.Date = (grid.Selected ?? DateTime.Today).Date;
            choice.DateOnly = !time.HasValue; choice.Time = time ?? TimeSpan.Zero;
            return choice;
        }
        public void Complete()
        {
            if (!ClearRequested && timeBox.Text.Trim().Length > 0 && !time.HasValue) { summary.Text = "时间格式应为 18:30"; summary.ForeColor = options.Palette.Warm; timeBox.Focus(); return; }
            DueChoice choice = Build();
            if (choice.HasDue && !choice.DateOnly && TimeZoneInfo.Local.IsInvalidTime(DateTime.SpecifyKind(choice.Date.Add(choice.Time), DateTimeKind.Unspecified)))
            { summary.Text = "这个时间处于夏令时跳转区间"; summary.ForeColor = options.Palette.Warm; return; }
            Result = choice; Completed = true;
            if (Finished != null) Finished(this, EventArgs.Empty);
        }
        public void FocusGrid() { grid.Focus(); }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter) { Complete(); return true; }
            if (keyData == Keys.Escape) { Cancel(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
        public event EventHandler CancelRequested;
        public void Cancel() { if (CancelRequested != null) CancelRequested(this, EventArgs.Empty); }
    }

    // Opens the picker beside an anchor. Anything other than 完成 / Enter counts as a cancel.
    public sealed class DuePickerPopup : IDisposable
    {
        private readonly ToolStripDropDown drop = new ToolStripDropDown();
        public DuePickerPanel Panel { get; private set; }
        public bool IsOpen { get; private set; }
        public DuePickerPopup(DuePickerOptions options, Action<DueChoice, bool> finished, Action cancelled)
        {
            Panel = new DuePickerPanel(options);
            var host = new ToolStripControlHost(Panel) { Margin = Padding.Empty, Padding = Padding.Empty, AutoSize = false, Size = Panel.Size };
            drop.Padding = Padding.Empty; drop.Margin = Padding.Empty; drop.AutoSize = true; drop.BackColor = options.Palette.Back; drop.DropShadowEnabled = true;
            drop.Items.Add(host);
            Panel.Finished += delegate { drop.Close(ToolStripDropDownCloseReason.ItemClicked); };
            Panel.CancelRequested += delegate { drop.Close(ToolStripDropDownCloseReason.Keyboard); };
            // The panel grows by a row when a time is chosen and the lead chips appear.
            Panel.SizeChanged += delegate { host.Size = Panel.Size; };
            drop.Closed += delegate
            {
                IsOpen = false;
                if (Panel.Completed) finished(Panel.Result, Panel.ClearRequested); else if (cancelled != null) cancelled();
            };
        }
        public void Show(Control anchor)
        {
            Rectangle area = Screen.FromControl(anchor).WorkingArea;
            Point below = anchor.PointToScreen(new Point(0, anchor.Height + 2));
            int x = Math.Max(area.Left + 4, Math.Min(below.X, area.Right - Panel.Width - 4));
            int y = below.Y + Panel.Height <= area.Bottom - 4 ? below.Y : Math.Max(area.Top + 4, anchor.PointToScreen(Point.Empty).Y - Panel.Height - 2);
            IsOpen = true;
            drop.Show(new Point(x, y));
            Panel.FocusGrid();
        }
        public void Close() { if (IsOpen) drop.Close(ToolStripDropDownCloseReason.CloseCalled); }
        public void Dispose() { drop.Dispose(); }
    }
}
