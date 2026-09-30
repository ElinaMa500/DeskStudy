using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace DeskStudy
{
    public sealed class CalendarForm : WidgetForm
    {
        private DateTime focusDate = DateTime.Today;
        private bool monthView;
        private string lastDefaultView;
        private Label period;
        private Label hint;
        private Button weekButton;
        private Button monthButton;
        private CalendarSurface surface;
        private Panel dayHeader;
        private TableLayoutPanel contentLayout;
        private bool contentReady;
        private readonly Timer clockTimer = new Timer();
        private DateTime lastToday = DateTime.Today;
        private List<Occurrence> occurrences = new List<Occurrence>();
        private readonly Color ink = Color.FromArgb(38, 49, 65);
        private readonly Color muted = Color.FromArgb(119, 128, 141);

        public CalendarForm(AppController app) : base(app, "calendar", "日历 · 课表", Color.FromArgb(76, 118, 108), new Size(900, 710))
        {
            MinimumSize = new Size(680, 520);
            Body.BackColor = Color.FromArgb(250, 250, 247);
            BuildContent();
            App.DataChanged += RefreshData;
            FormClosed += delegate { App.DataChanged -= RefreshData; };
            clockTimer.Interval = 30000;
            clockTimer.Tick += delegate {
                if (lastToday != DateTime.Today)
                {
                    if (focusDate == lastToday) focusDate = DateTime.Today;
                    lastToday = DateTime.Today;
                    RefreshData();
                }
                else surface.Invalidate();
            };
            clockTimer.Start();
            RefreshData();
            Shown += delegate { surface.ScrollToMorning(); };
            ResumeLayout(true);
            contentReady = true;
            ApplyAppearance();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                clockTimer.Dispose();
                App.DataChanged -= RefreshData;
            }
            base.Dispose(disposing);
        }

        private Button MakeButton(string text, int width, EventHandler action)
        {
            Button button = new Button();
            button.Text = text; button.Width = width; button.Height = 32;
            button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderColor = Color.FromArgb(217, 224, 220);
            button.BackColor = Color.White; button.ForeColor = ink;
            button.Font = new Font("Microsoft YaHei UI", 9F);
            button.Margin = new Padding(3); button.Cursor = Cursors.Hand;
            button.Click += action; return button;
        }

        private void BuildContent()
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            contentLayout = layout;
            layout.Dock = DockStyle.Fill; layout.RowCount = 4; layout.ColumnCount = 1;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Padding = new Padding(12, 4, 12, 10);
            Body.Controls.Add(layout);

            TableLayoutPanel toolbar = new TableLayoutPanel();
            toolbar.Dock = DockStyle.Fill; toolbar.ColumnCount = 3; toolbar.RowCount = 1;
            toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 146));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 265));
            FlowLayoutPanel navigation = new FlowLayoutPanel(); navigation.Dock = DockStyle.Fill; navigation.WrapContents = false;
            navigation.Padding = new Padding(0, 5, 0, 0);
            navigation.Controls.Add(MakeButton("‹", 32, delegate { MovePeriod(-1); }));
            navigation.Controls.Add(MakeButton("今天", 59, delegate { focusDate = DateTime.Today; RefreshData(); }));
            navigation.Controls.Add(MakeButton("›", 32, delegate { MovePeriod(1); }));
            toolbar.Controls.Add(navigation, 0, 0);
            period = new Label(); period.Dock = DockStyle.Fill; period.TextAlign = ContentAlignment.MiddleLeft;
            period.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold); period.ForeColor = ink;
            toolbar.Controls.Add(period, 1, 0);
            FlowLayoutPanel actions = new FlowLayoutPanel(); actions.Dock = DockStyle.Fill; actions.WrapContents = false;
            actions.Padding = new Padding(0, 5, 0, 0);
            weekButton = MakeButton("周", 42, delegate { monthView = false; RefreshData(); surface.ScrollToMorning(); });
            monthButton = MakeButton("月", 42, delegate { monthView = true; RefreshData(); });
            actions.Controls.Add(weekButton); actions.Controls.Add(monthButton);
            Button add = MakeButton("＋ 添加日程", 145, delegate { AddEvent(focusDate, 9); });
            add.BackColor = Color.FromArgb(73, 111, 101); add.ForeColor = Color.White;
            actions.Controls.Add(add); toolbar.Controls.Add(actions, 2, 0);
            layout.Controls.Add(toolbar, 0, 0);

            hint = new Label(); hint.Dock = DockStyle.Fill;
            hint.Text = "每一天，留一点从容   ·   点击课程编辑；双击空白处添加";
            hint.Font = new Font("Microsoft YaHei UI", 9F); hint.ForeColor = muted;
            hint.TextAlign = ContentAlignment.MiddleLeft; hint.Padding = new Padding(6, 0, 0, 0);
            layout.Controls.Add(hint, 0, 1);
            dayHeader = new Panel(); dayHeader.Dock = DockStyle.Fill; dayHeader.Paint += PaintHeader;
            layout.Controls.Add(dayHeader, 0, 2);
            surface = new CalendarSurface(); surface.Dock = DockStyle.Fill;
            surface.OpenOccurrence += EditEvent;
            surface.CreateEvent += AddEvent;
            surface.SelectDate += delegate(DateTime date) { focusDate = date; };
            surface.SizeChanged += delegate { dayHeader.Invalidate(); };
            layout.Controls.Add(surface, 0, 3);
        }

        private DateTime WeekStart(DateTime date)
        {
            int start = App.Data.Settings.Calendar.WeekStartDay;
            return date.Date.AddDays(-(((int)date.DayOfWeek - start + 7) % 7));
        }

        private void MovePeriod(int amount)
        {
            focusDate = monthView ? focusDate.AddMonths(amount) : focusDate.AddDays(amount * 7);
            RefreshData();
        }

        public void RefreshData()
        {
            if (IsDisposed || surface == null) return;
            string defaultView = App.Data.Settings.Calendar.DefaultView;
            if (lastDefaultView != defaultView) { monthView = defaultView == "Month"; lastDefaultView = defaultView; }
            DateTime start = monthView ? WeekStart(new DateTime(focusDate.Year, focusDate.Month, 1)) : WeekStart(focusDate);
            DateTime end = start.AddDays(monthView ? 41 : 6);
            occurrences = CalendarEngine.GetOccurrences(App.Data, start, end).ToList();
            period.Text = monthView ? focusDate.ToString("yyyy 年 M 月") : start.ToString("M.d") + " – " + end.ToString("M.d") + "  ·  " + focusDate.Year;
            int teachingWeek = SettingsLogic.TeachingWeek(App.Data.Settings, focusDate);
            hint.Text = (teachingWeek > 0 ? "教学第 " + teachingWeek + " 周   ·   " : "") + "点击课程编辑；双击空白处添加";
            surface.SetData(start, focusDate, monthView, occurrences);
            ApplyAppearance();
            dayHeader.Invalidate();
        }

        protected override void OnAppearanceChanged(AppearanceOptions appearance)
        {
            if (surface == null) return;
            surface.SetAppearance(appearance);
            Color selected = AppearancePainter.Dark(AppearancePainter.Background(appearance)) ? Color.FromArgb(64, 90, 86) : Color.FromArgb(219, 233, 227);
            weekButton.BackColor = monthView ? AppearancePainter.Surface(appearance) : selected;
            monthButton.BackColor = monthView ? selected : AppearancePainter.Surface(appearance);
            if (contentReady)
            {
                float dpi;
                using (Graphics graphics = CreateGraphics()) dpi = graphics.DpiY / 96F;
                using (Font headerFont = new Font("Microsoft YaHei UI", appearance.FontSize, FontStyle.Bold))
                    contentLayout.RowStyles[2].Height = Math.Max(45 * dpi, TextRenderer.MeasureText(monthView ? "周一" : "周一\n9/28", headerFont).Height + 10 * dpi);
            }
            dayHeader.Invalidate();
        }

        private void PaintHeader(object sender, PaintEventArgs e)
        {
            AppearanceOptions appearance = SettingsLogic.EffectiveAppearance(App.Data, "calendar");
            Color foreground = AppearancePainter.Foreground(appearance);
            bool dark = AppearancePainter.Dark(AppearancePainter.Background(appearance));
            e.Graphics.Clear(AppearancePainter.Background(appearance));
            string[] weekdays = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
            int left = monthView ? 0 : surface.TimeGutter;
            int available = surface == null ? dayHeader.Width : surface.GridWidth;
            float width = (available - left) / 7F;
            DateTime start = WeekStart(focusDate);
            for (int i = 0; i < 7; i++)
            {
                DateTime date = start.AddDays(i);
                Rectangle rect = new Rectangle(left + (int)(i * width), 1, (int)width, Math.Max(1, dayHeader.ClientSize.Height - 2));
                bool today = !monthView && date.Date == DateTime.Today;
                if (today) using (Brush b = new SolidBrush(dark ? Color.FromArgb(58, 83, 77) : Color.FromArgb(226, 237, 231))) e.Graphics.FillRectangle(b, rect);
                string text = weekdays[(int)date.DayOfWeek] + (monthView ? "" : "\n" + date.ToString("M/d"));
                using (Font font = new Font("Microsoft YaHei UI", appearance.FontSize, today ? FontStyle.Bold : FontStyle.Regular))
                    TextRenderer.DrawText(e.Graphics, text, font, rect, foreground, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        private void AddEvent(DateTime date, int hour)
        {
            CalendarEvent item = new CalendarEvent();
            item.Id = Guid.NewGuid().ToString("N"); item.Title = ""; item.Date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            item.StartTime = Math.Max(0, Math.Min(22, hour)).ToString("00") + ":00";
            item.EndTime = (Math.Max(0, Math.Min(22, hour)) + 1).ToString("00") + ":00";
            item.Location = ""; item.Notes = ""; item.Color = "#6C9385";
            item.TimeZoneId = TimeZoneInfo.Local.Id; item.RepeatEndDate = date.AddMonths(4).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            DateTime termEnd;
            if (DateTime.TryParseExact(App.Data.Settings.Calendar.SemesterEnd, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out termEnd) && termEnd >= date)
                item.RepeatEndDate = termEnd.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            item.RecurrenceAnchorDate = App.Data.Settings.Calendar.TeachingWeekOne;
            item.WeekDays = new List<int> { (int)date.DayOfWeek };
            item.ExcludedDates = new List<string>(); item.Overrides = new List<EventOverride>();
            using (CalendarEditor editor = new CalendarEditor(item, true, false))
            {
                if (editor.ShowDialog(this) != DialogResult.OK) return;
                App.Data.Events.Add(editor.Result); App.Save();
            }
        }

        private void EditEvent(Occurrence occurrence)
        {
            bool wholeSeries = occurrence.Series.RepeatWeeks == 0;
            if (!wholeSeries)
            {
                using (OccurrenceScopeDialog scope = new OccurrenceScopeDialog())
                {
                    if (scope.ShowDialog(this) != DialogResult.OK) return;
                    wholeSeries = scope.WholeSeries;
                }
            }
            CalendarEvent source = wholeSeries ? occurrence.Series : OccurrenceAsEvent(occurrence);
            using (CalendarEditor editor = new CalendarEditor(source, wholeSeries, true))
            {
                if (editor.ShowDialog(this) != DialogResult.OK) return;
                if (editor.DeleteRequested)
                {
                    if (wholeSeries) App.Data.Events.Remove(occurrence.Series);
                    else CalendarEngine.CancelOccurrence(occurrence.Series, occurrence.KeyDate);
                }
                else if (wholeSeries)
                {
                    int index = App.Data.Events.IndexOf(occurrence.Series);
                    if (index >= 0) App.Data.Events[index] = editor.Result;
                }
                else
                {
                    CalendarEvent changed = editor.Result;
                    CalendarEngine.UpdateOccurrence(occurrence.Series, new EventOverride {
                        OriginalDate = occurrence.KeyDate, Date = changed.Date, StartTime = changed.StartTime,
                        EndTime = changed.EndTime, Title = changed.Title, Location = changed.Location,
                        Notes = changed.Notes, Color = changed.Color
                    });
                }
                App.Save();
            }
        }

        private static CalendarEvent OccurrenceAsEvent(Occurrence item)
        {
            return new CalendarEvent {
                Id = item.Series.Id, Title = item.Title, Date = item.Date, StartTime = item.StartTime,
                EndTime = item.EndTime, Location = item.Location, Notes = item.Notes, Color = item.Color,
                TimeZoneId = item.Series.TimeZoneId, RepeatWeeks = 0, RepeatEndDate = item.Date, RecurrenceAnchorDate = item.Series.RecurrenceAnchorDate,
                WeekDays = new List<int>(), ExcludedDates = new List<string>(), Overrides = new List<EventOverride>()
            };
        }
    }

    internal sealed class CalendarSurface : ScrollableControl
    {
        public event Action<Occurrence> OpenOccurrence;
        public event Action<DateTime, int> CreateEvent;
        public event Action<DateTime> SelectDate;
        private DateTime startDate;
        private DateTime focusDate;
        private bool monthView;
        private List<Occurrence> items = new List<Occurrence>();
        private readonly List<CalendarHit> hits = new List<CalendarHit>();
        private readonly ToolTip tooltip = new ToolTip();
        private string tip = "";
        private float dpiScale = 1F;
        private float fontScale = 1F;
        private Color canvas = Color.White;
        private Color foreground = Color.FromArgb(46, 66, 58);
        private Color secondary = Color.FromArgb(119, 128, 141);
        private Color ruleColor = Color.FromArgb(234, 237, 233);
        private Color halfRuleColor = Color.FromArgb(246, 247, 244);
        private Color todayColor = Color.FromArgb(249, 251, 247);
        private Color outsideColor = Color.FromArgb(247, 248, 245);
        private int S(int value) { return (int)Math.Round(value * dpiScale, MidpointRounding.AwayFromZero); }
        private int T(int value) { return (int)Math.Round(value * dpiScale * fontScale, MidpointRounding.AwayFromZero); }
        private int HourHeight { get { return T(62); } }
        public int TimeGutter { get { return T(49); } }
        private int MonthHeight { get { return Math.Max(T(600), ClientSize.Height); } }
        private const TextFormatFlags ScrolledText = TextFormatFlags.PreserveGraphicsTranslateTransform | TextFormatFlags.PreserveGraphicsClipping;
        // ClientSize already excludes the native scrollbar; subtracting it again
        // leaves an unused column and makes dates disagree with their hit targets.
        public int GridWidth { get { return ClientSize.Width; } }

        public CalendarSurface()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AutoScroll = true; BackColor = Color.White;
            MouseClick += HandleClick; MouseDoubleClick += HandleDoubleClick; MouseMove += HandleMove;
            Scroll += delegate { Invalidate(); };
            tooltip.AutoPopDelay = 12000; tooltip.InitialDelay = 300;
        }

        public void SetAppearance(AppearanceOptions appearance)
        {
            float oldFontScale = fontScale;
            fontScale = appearance.FontSize / 9F;
            canvas = AppearancePainter.Surface(appearance);
            foreground = AppearancePainter.Foreground(appearance);
            bool dark = AppearancePainter.Dark(canvas);
            secondary = Blend(canvas, foreground, .62F);
            ruleColor = Blend(canvas, foreground, dark ? .18F : .11F);
            halfRuleColor = Blend(canvas, foreground, dark ? .08F : .04F);
            todayColor = Blend(canvas, Color.FromArgb(100, 155, 128), dark ? .22F : .1F);
            outsideColor = Blend(canvas, foreground, .04F);
            BackColor = canvas;
            if (Math.Abs(oldFontScale - fontScale) > .01F) UpdateScrollExtent();
            Invalidate();
        }

        private static Color Blend(Color background, Color color, float amount)
        {
            return Color.FromArgb((int)(background.R * (1F - amount) + color.R * amount),
                (int)(background.G * (1F - amount) + color.G * amount), (int)(background.B * (1F - amount) + color.B * amount));
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            using (Graphics graphics = CreateGraphics()) dpiScale = graphics.DpiX / 96F;
            UpdateScrollExtent();
        }

        private void UpdateScrollExtent()
        {
            AutoScrollMinSize = new Size(0, monthView ? T(600) : 24 * HourHeight + S(15));
        }

        public void SetData(DateTime start, DateTime focus, bool month, List<Occurrence> values)
        {
            bool changedMode = monthView != month;
            startDate = start; focusDate = focus; monthView = month; items = values;
            UpdateScrollExtent();
            if (changedMode) AutoScrollPosition = Point.Empty;
            Invalidate();
        }

        public void ScrollToMorning()
        {
            if (!monthView) AutoScrollPosition = new Point(0, HourHeight * 7);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); hits.Clear();
            e.Graphics.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);
            if (monthView) DrawMonth(e.Graphics); else DrawWeek(e.Graphics);
        }

        private static int Minutes(string time)
        {
            TimeSpan value;
            return TimeSpan.TryParse(time, CultureInfo.InvariantCulture, out value) ? (int)value.TotalMinutes : 0;
        }

        private static Color EventColor(string color)
        {
            try { return ColorTranslator.FromHtml(color); } catch { return Color.FromArgb(108, 147, 133); }
        }

        private Color Tint(Color color)
        {
            return Blend(canvas, color, AppearancePainter.Dark(canvas) ? .3F : .16F);
        }

        private void DrawWeek(Graphics graphics)
        {
            int width = Math.Max(S(100), GridWidth); float col = (width - TimeGutter) / 7F;
            using (Pen rule = new Pen(ruleColor))
            using (Pen halfRule = new Pen(halfRuleColor))
            using (Font timeFont = new Font("Microsoft YaHei UI", 8F * fontScale))
            {
                for (int day = 0; day < 7; day++)
                {
                    DateTime date = startDate.AddDays(day);
                    if (date.Date == DateTime.Today)
                        using (Brush today = new SolidBrush(todayColor)) graphics.FillRectangle(today, TimeGutter + day * col, 0, col, 24 * HourHeight);
                    graphics.DrawLine(rule, TimeGutter + day * col, 0, TimeGutter + day * col, 24 * HourHeight);
                }
                for (int hour = 0; hour <= 24; hour++)
                {
                    int y = hour * HourHeight;
                    graphics.DrawLine(rule, TimeGutter, y, width, y);
                    if (hour < 24) graphics.DrawLine(halfRule, TimeGutter, y + HourHeight / 2, width, y + HourHeight / 2);
                    if (hour < 24) TextRenderer.DrawText(graphics, hour.ToString("00") + ":00", timeFont, new Rectangle(0, y + S(2), T(45), T(20)), secondary, TextFormatFlags.Right | ScrolledText);
                }
            }
            for (int day = 0; day < 7; day++)
            {
                string date = startDate.AddDays(day).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                List<Occurrence> dayItems = items.Where(o => o.Date == date).OrderBy(o => o.StartTime).ThenBy(o => o.EndTime).ToList();
                int cursor = 0;
                while (cursor < dayItems.Count)
                {
                    List<Occurrence> cluster = new List<Occurrence>();
                    int clusterEnd = Minutes(dayItems[cursor].EndTime);
                    cluster.Add(dayItems[cursor++]);
                    while (cursor < dayItems.Count && Minutes(dayItems[cursor].StartTime) < clusterEnd)
                    {
                        clusterEnd = Math.Max(clusterEnd, Minutes(dayItems[cursor].EndTime)); cluster.Add(dayItems[cursor++]);
                    }
                    List<int> laneEnds = new List<int>(); List<int> lanes = new List<int>();
                    foreach (Occurrence item in cluster)
                    {
                        int lane = laneEnds.FindIndex(end => end <= Minutes(item.StartTime));
                        if (lane < 0) { lane = laneEnds.Count; laneEnds.Add(0); }
                        laneEnds[lane] = Minutes(item.EndTime); lanes.Add(lane);
                    }
                    float laneWidth = (col - S(6)) / laneEnds.Count;
                    for (int i = 0; i < cluster.Count; i++)
                    {
                        Occurrence item = cluster[i];
                        int top = Minutes(item.StartTime) * HourHeight / 60;
                        int height = Math.Max(S(18), (Minutes(item.EndTime) - Minutes(item.StartTime)) * HourHeight / 60 - S(3));
                        Rectangle rect = new Rectangle(TimeGutter + S(3) + (int)(day * col + lanes[i] * laneWidth), top + S(2), Math.Max(S(8), (int)laneWidth - S(2)), height);
                        DrawEvent(graphics, item, rect, false); hits.Add(new CalendarHit(rect, item));
                    }
                }
            }
            if (DateTime.Today >= startDate && DateTime.Today < startDate.AddDays(7))
            {
                int day = (DateTime.Today - startDate).Days; int y = (int)(DateTime.Now.TimeOfDay.TotalMinutes * HourHeight / 60);
                using (Pen now = new Pen(Color.FromArgb(193, 103, 94), S(2))) graphics.DrawLine(now, TimeGutter + day * col, y, TimeGutter + (day + 1) * col, y);
            }
            if (items.Count == 0)
                using (Font font = new Font("Microsoft YaHei UI", 10F * fontScale))
                    TextRenderer.DrawText(graphics, "本周还没有日程 · 双击时间格开始安排", font, new Rectangle(TimeGutter + S(8), 9 * HourHeight + S(15), Math.Max(S(100), width - TimeGutter - S(16)), T(28)), secondary, TextFormatFlags.HorizontalCenter | ScrolledText);
        }

        private void DrawMonth(Graphics graphics)
        {
            float col = Math.Max(S(100), GridWidth) / 7F;
            int height = MonthHeight; float row = height / 6F;
            using (Pen rule = new Pen(ruleColor))
            using (Font dateFont = new Font("Microsoft YaHei UI", 10F * fontScale, FontStyle.Bold))
            using (Font smallFont = new Font("Microsoft YaHei UI", 8F * fontScale))
            {
                for (int index = 0; index < 42; index++)
                {
                    DateTime date = startDate.AddDays(index); int x = (int)(index % 7 * col); int y = (int)(index / 7 * row);
                    Rectangle cell = new Rectangle(x, y, (int)col, (int)row);
                    Color bg = date.Month == focusDate.Month ? canvas : outsideColor;
                    if (date == DateTime.Today) bg = todayColor;
                    using (Brush fill = new SolidBrush(bg)) graphics.FillRectangle(fill, cell);
                    graphics.DrawRectangle(rule, cell);
                    TextRenderer.DrawText(graphics, date.Day.ToString() + (date == DateTime.Today ? " 今天" : ""), dateFont, new Rectangle(x + S(6), y + S(5), (int)col - S(10), T(24)), date.Month == focusDate.Month ? foreground : secondary, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | ScrolledText);
                    List<Occurrence> dayItems = items.Where(o => o.Date == date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).OrderBy(o => o.StartTime).ToList();
                    int capacity = Math.Max(1, ((int)row - T(37)) / T(25));
                    int shown = dayItems.Count > capacity ? Math.Max(0, capacity - 1) : dayItems.Count;
                    for (int j = 0; j < shown; j++)
                    {
                        Rectangle rect = new Rectangle(x + S(3), y + T(32) + j * T(25), (int)col - S(6), T(22));
                        DrawEvent(graphics, dayItems[j], rect, true); hits.Add(new CalendarHit(rect, dayItems[j]));
                    }
                    if (shown < dayItems.Count)
                    {
                        Rectangle rect = new Rectangle(x + S(4), y + T(32) + shown * T(25), (int)col - S(8), T(24));
                        TextRenderer.DrawText(graphics, "+ " + (dayItems.Count - shown) + " 项 · 查看", smallFont, rect, foreground, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | ScrolledText);
                        hits.Add(new CalendarHit(rect, date));
                    }
                }
            }
        }

        private void DrawEvent(Graphics graphics, Occurrence item, Rectangle rect, bool compact)
        {
            Color color = EventColor(item.Color);
            using (Brush fill = new SolidBrush(Tint(color))) graphics.FillRectangle(fill, rect);
            using (Brush stripe = new SolidBrush(color)) graphics.FillRectangle(stripe, rect.X, rect.Y, Math.Min(S(3), rect.Width), rect.Height);
            Rectangle inner = new Rectangle(rect.X + S(5), rect.Y + S(3), Math.Max(1, rect.Width - S(9)), Math.Max(1, rect.Height - S(6)));
            using (Font titleFont = new Font("Microsoft YaHei UI", (compact ? 8F : 9F) * fontScale, FontStyle.Bold))
            using (Font detailFont = new Font("Microsoft YaHei UI", 8F * fontScale))
            {
                string title = compact ? item.StartTime + " " + item.Title : item.Title;
                TextRenderer.DrawText(graphics, title, titleFont, inner, foreground, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | ScrolledText);
                if (!compact && rect.Height >= T(41))
                    TextRenderer.DrawText(graphics, item.StartTime + "–" + item.EndTime, detailFont, new Rectangle(inner.X, inner.Y + T(20), inner.Width, T(18)), foreground, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | ScrolledText);
                if (!compact && rect.Height >= T(64) && !String.IsNullOrWhiteSpace(item.Location))
                    TextRenderer.DrawText(graphics, item.Location, detailFont, new Rectangle(inner.X, inner.Y + T(40), inner.Width, T(19)), foreground, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | ScrolledText);
            }
        }

        private Point ContentPoint(Point location) { return new Point(location.X - AutoScrollPosition.X, location.Y - AutoScrollPosition.Y); }

        private DateTime DateAt(Point point)
        {
            if (monthView)
            {
                int column = Math.Min(6, Math.Max(0, (int)(point.X / (Math.Max(S(100), GridWidth) / 7F))));
                int row = Math.Min(5, Math.Max(0, (int)(point.Y / (MonthHeight / 6F))));
                return startDate.AddDays(row * 7 + column);
            }
            int day = Math.Min(6, Math.Max(0, (int)((point.X - TimeGutter) / ((Math.Max(S(100), GridWidth) - TimeGutter) / 7F))));
            return startDate.AddDays(day);
        }

        private void HandleClick(object sender, MouseEventArgs e)
        {
            Point point = ContentPoint(e.Location); CalendarHit hit = hits.LastOrDefault(h => h.Bounds.Contains(point));
            if (hit != null)
            {
                if (hit.Item != null && OpenOccurrence != null) OpenOccurrence(hit.Item);
                else if (hit.Item == null) ShowDay(hit.Date);
                return;
            }
            if (SelectDate != null) SelectDate(DateAt(point));
        }

        private void HandleDoubleClick(object sender, MouseEventArgs e)
        {
            Point point = ContentPoint(e.Location);
            if (hits.Any(h => h.Bounds.Contains(point))) return;
            if (CreateEvent != null) CreateEvent(DateAt(point), monthView ? 9 : Math.Max(0, Math.Min(22, point.Y / HourHeight)));
        }

        private void HandleMove(object sender, MouseEventArgs e)
        {
            Point point = ContentPoint(e.Location); CalendarHit hit = hits.LastOrDefault(h => h.Bounds.Contains(point));
            Cursor = hit == null ? Cursors.Default : Cursors.Hand;
            string next = hit == null || hit.Item == null ? "" : hit.Item.Title + "\n" + hit.Item.Date + "  " + hit.Item.StartTime + "–" + hit.Item.EndTime + (String.IsNullOrWhiteSpace(hit.Item.Location) ? "" : "\n" + hit.Item.Location) + (String.IsNullOrWhiteSpace(hit.Item.Notes) ? "" : "\n" + hit.Item.Notes);
            if (next != tip) { tip = next; tooltip.SetToolTip(this, tip); }
        }

        private void ShowDay(DateTime date)
        {
            using (Form dialog = new Form())
            {
                dialog.SuspendLayout(); dialog.AutoScaleDimensions = new SizeF(96F, 96F); dialog.AutoScaleMode = AutoScaleMode.Dpi;
                dialog.Text = date.ToString("M 月 d 日") + " · 全部日程"; dialog.Size = new Size(410, 420);
                dialog.StartPosition = FormStartPosition.CenterParent; dialog.MinimizeBox = false; dialog.MaximizeBox = false;
                dialog.Font = new Font("Microsoft YaHei UI", 10F); dialog.BackColor = Color.FromArgb(250, 250, 247);
                FlowLayoutPanel list = new FlowLayoutPanel(); list.Dock = DockStyle.Fill; list.FlowDirection = FlowDirection.TopDown; list.WrapContents = false; list.AutoScroll = true; list.Padding = new Padding(12);
                dialog.Controls.Add(list);
                foreach (Occurrence item in items.Where(o => o.Date == date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).OrderBy(o => o.StartTime))
                {
                    Occurrence captured = item; Button button = new Button(); button.Text = item.StartTime + "–" + item.EndTime + "  " + item.Title + "\n" + item.Location;
                    button.Size = new Size(345, 65); button.TextAlign = ContentAlignment.MiddleLeft;
                    button.FlatStyle = FlatStyle.Flat; button.BackColor = Tint(EventColor(item.Color)); button.FlatAppearance.BorderSize = 0;
                    button.Click += delegate { dialog.Close(); if (OpenOccurrence != null) OpenOccurrence(captured); };
                    list.Controls.Add(button);
                }
                dialog.ResumeLayout(true);
                dialog.ShowDialog(FindForm());
            }
        }

        protected override void Dispose(bool disposing) { if (disposing) tooltip.Dispose(); base.Dispose(disposing); }
        private sealed class CalendarHit
        {
            public Rectangle Bounds; public Occurrence Item; public DateTime Date;
            public CalendarHit(Rectangle bounds, Occurrence item) { Bounds = bounds; Item = item; }
            public CalendarHit(Rectangle bounds, DateTime date) { Bounds = bounds; Date = date; }
        }
    }

    internal sealed class OccurrenceScopeDialog : Form
    {
        public bool WholeSeries;
        public OccurrenceScopeDialog()
        {
            SuspendLayout(); AutoScaleDimensions = new SizeF(96F, 96F); AutoScaleMode = AutoScaleMode.Dpi;
            Text = "循环课程"; Size = new Size(470, 208); FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent; MaximizeBox = false; MinimizeBox = false;
            Font = new Font("Microsoft YaHei UI", 10F); BackColor = Color.FromArgb(250, 250, 247);
            Label heading = new Label { Text = "要修改或删除哪一部分？", AutoSize = true, Location = new Point(22, 22), Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold) };
            Label detail = new Label { Text = "“仅这一次”保留其他日期的课程安排。", AutoSize = true, Location = new Point(22, 60), ForeColor = Color.FromArgb(113, 126, 117) };
            Controls.Add(heading); Controls.Add(detail);
            Button single = new Button { Text = "仅这一次", Location = new Point(22, 111), Size = new Size(125, 34) };
            Button all = new Button { Text = "整个系列", Location = new Point(159, 111), Size = new Size(125, 34) };
            Button cancel = new Button { Text = "取消", Location = new Point(296, 111), Size = new Size(125, 34), DialogResult = DialogResult.Cancel };
            single.Click += delegate { WholeSeries = false; DialogResult = DialogResult.OK; };
            all.Click += delegate { WholeSeries = true; DialogResult = DialogResult.OK; };
            Controls.Add(single); Controls.Add(all); Controls.Add(cancel); CancelButton = cancel;
            ResumeLayout(true);
        }
    }

    internal sealed class CalendarEditor : Form
    {
        public CalendarEvent Result { get; private set; }
        public bool DeleteRequested { get; private set; }
        private readonly CalendarEvent source;
        private readonly bool seriesMode;
        private TextBox titleBox, locationBox, notesBox;
        private DateTimePicker datePicker, startPicker, endPicker, untilPicker, anchorPicker;
        private ComboBox repeatBox, colorBox;
        private CheckedListBox weekdays;
        private Label error;
        private readonly string[] palette = { "#6C9385", "#7196B1", "#A68EB5", "#C49472", "#BB818B", "#939C63" };

        public CalendarEditor(CalendarEvent item, bool allowSeries, bool existing)
        {
            SuspendLayout(); AutoScaleDimensions = new SizeF(96F, 96F); AutoScaleMode = AutoScaleMode.Dpi;
            source = item; seriesMode = allowSeries;
            Text = existing ? (allowSeries ? "编辑日程" : "编辑 · 仅这一次") : "添加日程";
            ClientSize = new Size(564, 624); MinimumSize = new Size(580, 650);
            StartPosition = FormStartPosition.CenterParent; MaximizeBox = false; MinimizeBox = false;
            Font = new Font("Microsoft YaHei UI", 9.5F); BackColor = Color.FromArgb(250, 250, 247);
            BuildEditor(existing); FillEditor();
            ResumeLayout(true);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // The editor body scrolls, while the Save/Cancel footer stays reachable
            // on smaller displays after Windows has applied its DPI scale.
            Rectangle work = Screen.FromControl(Owner ?? this).WorkingArea;
            MinimumSize = new Size(Math.Min(MinimumSize.Width, work.Width), Math.Min(MinimumSize.Height, work.Height));
            Size = new Size(Math.Min(Width, work.Width), Math.Min(Height, work.Height));
            Location = new Point(Math.Max(work.Left, Math.Min(Left, work.Right - Width)), Math.Max(work.Top, Math.Min(Top, work.Bottom - Height)));
            using (Graphics graphics = CreateGraphics()) weekdays.ColumnWidth = (int)Math.Ceiling(54 * graphics.DpiX / 96F);
        }

        private void BuildEditor(bool existing)
        {
            TableLayoutPanel outer = new TableLayoutPanel(); outer.Dock = DockStyle.Fill; outer.RowCount = 2; outer.ColumnCount = 1;
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            Controls.Add(outer);
            Panel scroll = new Panel(); scroll.Dock = DockStyle.Fill; scroll.AutoScroll = true; scroll.Padding = new Padding(20, 16, 20, 8);
            outer.Controls.Add(scroll, 0, 0);
            TableLayoutPanel table = new TableLayoutPanel(); table.Dock = DockStyle.Top; table.AutoSize = true; table.ColumnCount = 2;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 93)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            scroll.Controls.Add(table);
            titleBox = new TextBox(); titleBox.MaxLength = 180; AddRow(table, "名称", titleBox, 42);
            datePicker = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy 年 MM 月 dd 日" };
            AddRow(table, "日期 / 起日", datePicker, 42);
            FlowLayoutPanel times = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            startPicker = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 115 };
            endPicker = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 115 };
            times.Controls.Add(startPicker); times.Controls.Add(new Label { Text = "至", Width = 28, Height = 26, TextAlign = ContentAlignment.MiddleCenter }); times.Controls.Add(endPicker);
            AddRow(table, "上课时间", times, 42);
            times.Margin = Padding.Empty;
            locationBox = new TextBox(); locationBox.MaxLength = 300; AddRow(table, "地点", locationBox, 42);
            notesBox = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, MaxLength = 10000 }; AddRow(table, "备注", notesBox, 78);
            colorBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            colorBox.Items.AddRange(new object[] { "鼠尾草绿", "雾蓝", "浅紫", "杏茶", "玫瑰", "橄榄" }); AddRow(table, "颜色", colorBox, 42);
            repeatBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Enabled = seriesMode };
            repeatBox.Items.AddRange(new object[] { "不重复", "每周", "每两周" }); AddRow(table, "重复", repeatBox, 42);
            weekdays = new CheckedListBox { CheckOnClick = true, MultiColumn = true, ColumnWidth = 54, Height = 47, BorderStyle = BorderStyle.None, BackColor = BackColor, IntegralHeight = false };
            weekdays.Items.AddRange(new object[] { "周一", "周二", "周三", "周四", "周五", "周六", "周日" }); AddRow(table, "每逢", weekdays, 54);
            untilPicker = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy 年 MM 月 dd 日" }; AddRow(table, "循环截止", untilPicker, 42);
            anchorPicker = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy 年 MM 月 dd 日", ShowCheckBox = true };
            AddRow(table, "双周基准日", anchorPicker, 42);
            ToolTip anchorHint = new ToolTip();
            anchorHint.SetToolTip(anchorPicker, "勾选：以此日期所在周作为每两周循环的第 1 周。取消勾选：按课程起日所在周循环。已有课程不会跟随全局教学周设置改动。\n要从教学第 2 周上课，请将此日期设为第 2 周内任一天。");
            Disposed += delegate { anchorHint.Dispose(); };
            Label timezone = new Label { AutoSize = false, Text = "按本地上课时间循环（含夏令时）。时区：\n" + TimeZoneLabel(source.TimeZoneId), ForeColor = Color.FromArgb(113, 124, 117) };
            AddRow(table, "时区", timezone, 68);
            repeatBox.SelectedIndexChanged += delegate { bool enabled = seriesMode && repeatBox.SelectedIndex > 0; weekdays.Enabled = enabled; untilPicker.Enabled = enabled; anchorPicker.Enabled = seriesMode && repeatBox.SelectedIndex == 2; };

            TableLayoutPanel footer = new TableLayoutPanel(); footer.Dock = DockStyle.Fill; footer.RowCount = 2; footer.ColumnCount = 1; footer.Padding = new Padding(20, 0, 20, 12);
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); outer.Controls.Add(footer, 0, 1);
            error = new Label { Dock = DockStyle.Fill, ForeColor = Color.FromArgb(171, 69, 67), TextAlign = ContentAlignment.MiddleLeft }; footer.Controls.Add(error, 0, 0);
            FlowLayoutPanel buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            Button save = new Button { Text = "保存日程", Width = 109, Height = 34, BackColor = Color.FromArgb(72, 112, 98), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            Button cancel = new Button { Text = "取消", Width = 83, Height = 34, DialogResult = DialogResult.Cancel };
            buttons.Controls.Add(save); buttons.Controls.Add(cancel); save.Click += SaveEditor; CancelButton = cancel;
            if (existing)
            {
                Button delete = new Button { Text = seriesMode ? "删除日程" : "取消这次课程", Width = 125, Height = 34, ForeColor = Color.FromArgb(170, 76, 71) };
                delete.Click += delegate {
                    string text = seriesMode && source.RepeatWeeks > 0 ? "确定删除整个循环系列及其例外安排？" : "确定删除这次日程？";
                    if (MessageBox.Show(this, text, "删除确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    { DeleteRequested = true; DialogResult = DialogResult.OK; }
                };
                buttons.Controls.Add(delete);
            }
            footer.Controls.Add(buttons, 0, 1);
        }

        private static string TimeZoneLabel(string id)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(String.IsNullOrWhiteSpace(id) ? TimeZoneInfo.Local.Id : id).DisplayName; }
            catch { return id; }
        }

        private static void AddRow(TableLayoutPanel table, string label, Control control, int height)
        {
            int row = table.RowCount++; table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            Label caption = new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopLeft, Padding = new Padding(0, 7, 0, 0), ForeColor = Color.FromArgb(100, 111, 104) };
            control.Dock = DockStyle.Fill; control.Margin = new Padding(0, 4, 0, 9); table.Controls.Add(caption, 0, row); table.Controls.Add(control, 1, row);
        }

        private void FillEditor()
        {
            titleBox.Text = source.Title; locationBox.Text = source.Location; notesBox.Text = source.Notes;
            DateTime date; if (!DateTime.TryParseExact(source.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) date = DateTime.Today;
            datePicker.Value = date;
            DateTime until; if (!DateTime.TryParseExact(source.RepeatEndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out until)) until = date.AddMonths(4);
            untilPicker.Value = until;
            DateTime anchor;
            bool hasAnchor = DateTime.TryParseExact(source.RecurrenceAnchorDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out anchor);
            anchorPicker.Value = hasAnchor ? anchor : date;
            anchorPicker.Checked = hasAnchor;
            TimeSpan start; if (!TimeSpan.TryParse(source.StartTime, out start)) start = TimeSpan.FromHours(9);
            TimeSpan end; if (!TimeSpan.TryParse(source.EndTime, out end)) end = TimeSpan.FromHours(10);
            startPicker.Value = date.Date.Add(start); endPicker.Value = date.Date.Add(end);
            colorBox.SelectedIndex = Math.Max(0, Array.IndexOf(palette, source.Color));
            repeatBox.SelectedIndex = Math.Max(0, Math.Min(2, source.RepeatWeeks));
            for (int i = 0; i < 7; i++) weekdays.SetItemChecked(i, source.WeekDays != null && source.WeekDays.Contains((i + 1) % 7));
            if (weekdays.CheckedItems.Count == 0) weekdays.SetItemChecked(((int)date.DayOfWeek + 6) % 7, true);
        }

        private void SaveEditor(object sender, EventArgs args)
        {
            if (String.IsNullOrWhiteSpace(titleBox.Text)) { error.Text = "请填写日程名称。"; titleBox.Focus(); return; }
            TimeSpan start = new TimeSpan(startPicker.Value.Hour, startPicker.Value.Minute, 0);
            TimeSpan end = new TimeSpan(endPicker.Value.Hour, endPicker.Value.Minute, 0);
            if (end <= start) { error.Text = "结束时间须晚于开始时间；跨天日程请分成两条。"; return; }
            int repeat = seriesMode ? repeatBox.SelectedIndex : 0;
            if (repeat > 0 && untilPicker.Value.Date < datePicker.Value.Date) { error.Text = "循环截止日期不能早于开始日期。"; return; }
            if (repeat > 0 && (untilPicker.Value.Date - datePicker.Value.Date).TotalDays > 36600) { error.Text = "循环跨度不能超过 100 年。"; return; }
            if (repeat > 0 && weekdays.CheckedItems.Count == 0) { error.Text = "请至少选择一个重复星期。"; return; }
            List<int> days = new List<int>(); foreach (int index in weekdays.CheckedIndices) days.Add((index + 1) % 7);
            Result = new CalendarEvent {
                Id = source.Id, Title = titleBox.Text.Trim(), Date = datePicker.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                StartTime = startPicker.Value.ToString("HH:mm", CultureInfo.InvariantCulture), EndTime = endPicker.Value.ToString("HH:mm", CultureInfo.InvariantCulture),
                Location = locationBox.Text.Trim(), Notes = notesBox.Text, Color = palette[Math.Max(0, colorBox.SelectedIndex)],
                TimeZoneId = String.IsNullOrWhiteSpace(source.TimeZoneId) ? TimeZoneInfo.Local.Id : source.TimeZoneId,
                RepeatWeeks = repeat, RepeatEndDate = untilPicker.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), WeekDays = days,
                RecurrenceAnchorDate = repeat > 0 && anchorPicker.Checked ? anchorPicker.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "",
                ExcludedDates = repeat == 0 || source.ExcludedDates == null ? new List<string>() : new List<string>(source.ExcludedDates),
                Overrides = repeat == 0 || source.Overrides == null ? new List<EventOverride>() : new List<EventOverride>(source.Overrides)
            };
            DialogResult = DialogResult.OK;
        }
    }
}
