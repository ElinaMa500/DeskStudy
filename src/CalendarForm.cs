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
        private Button weekButton, workWeekButton;
        private Button monthButton;
        private CalendarSurface surface;
        private Panel dayHeader;
        private TableLayoutPanel contentLayout;
        private bool contentReady;
        private readonly Timer clockTimer = new Timer();
        private DateTime lastToday = DateTime.Today;
        private List<Occurrence> occurrences = new List<Occurrence>();
        // DDL deadlines in view; in a week view every day with deadlines shows a ⚑ badge beside its date.
        private List<DeadlineMark> deadlines = new List<DeadlineMark>();
        // Outlook all-day events in view; a week view lists them in a row under the day header.
        private List<Occurrence> allDay = new List<Occurrence>();
        private readonly List<KeyValuePair<Rectangle, Occurrence>> allDayHits = new List<KeyValuePair<Rectangle, Occurrence>>();
        internal static DateTime FirstDay(Occurrence o) { return TimeUtil.ParseDate(o.Date); }
        internal static DateTime AfterLastDay(Occurrence o) { return TimeUtil.ParseDate(o.Date).AddDays(Math.Max(1, o.Days)); }
        private readonly List<KeyValuePair<Rectangle, List<DeadlineMark>>> badgeHits = new List<KeyValuePair<Rectangle, List<DeadlineMark>>>();
        private readonly ClickOrDouble<List<DeadlineMark>> badgeClicks = new ClickOrDouble<List<DeadlineMark>>();
        private readonly ToolTip badgeTip = new ToolTip { AutoPopDelay = 12000, InitialDelay = 300 };
        private string badgeTipText = "";
        private List<DeadlineMark> DayDeadlines { get { return monthView ? new List<DeadlineMark>() : deadlines; } }
        private readonly Color ink = Color.FromArgb(38, 49, 65);
        private readonly Color muted = Color.FromArgb(119, 128, 141);
        // Reference styles follow the notebook layout: 清爽 for 轻量卡片 / 清爽卡片, 纸页 for 纸页本 / 手账纸页.
        private TableLayoutPanel toolbar;
        private Panel refBar;
        private Button refPrev, refNext, refToday, refWorkWeek, refWeek, refMonth, refAdd;
        // 工作周: the week view limited to Monday–Friday.
        private bool workWeek;
        private int ViewDays { get { return !monthView && workWeek ? 5 : 7; } }
        private DateTime ViewStart() { return workWeek ? focusDate.Date.AddDays(-(((int)focusDate.DayOfWeek + 6) % 7)) : WeekStart(focusDate); }
        private Label refRange, refMeta;
        private string calendarStyle = "Original";
        private Padding originalBodyPadding, originalLayoutPadding;
        private float originalToolbarRow, originalHintRow;
        private bool originalsCaptured;
        private CalendarPalette palette;
        private readonly Dictionary<string, Font> refFonts = new Dictionary<string, Font>();
        public string AppliedCalendarStyle { get { return calendarStyle; } }

        public CalendarForm(AppController app) : base(app, "calendar", Lang.T("日历 · 课表"), Color.FromArgb(76, 118, 108), new Size(900, 710))
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
                clockTimer.Dispose(); badgeClicks.Dispose(); badgeTip.Dispose();
                App.DataChanged -= RefreshData;
                foreach (Font font in refFonts.Values) font.Dispose();
                refFonts.Clear();
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

            toolbar = new TableLayoutPanel();
            toolbar.Dock = DockStyle.Fill; toolbar.ColumnCount = 3; toolbar.RowCount = 1;
            toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 146));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Lang.IsEnglish ? 340 : 312));
            FlowLayoutPanel navigation = new FlowLayoutPanel(); navigation.Dock = DockStyle.Fill; navigation.WrapContents = false;
            navigation.Padding = new Padding(0, 5, 0, 0);
            navigation.Controls.Add(MakeButton("‹", 32, delegate { MovePeriod(-1); }));
            navigation.Controls.Add(MakeButton(Lang.T("今天"), 59, delegate { focusDate = DateTime.Today; RefreshData(); }));
            navigation.Controls.Add(MakeButton("›", 32, delegate { MovePeriod(1); }));
            toolbar.Controls.Add(navigation, 0, 0);
            period = new Label(); period.Dock = DockStyle.Fill; period.TextAlign = ContentAlignment.MiddleLeft;
            period.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold); period.ForeColor = ink; period.AutoEllipsis = true;
            period.Resize += delegate { FitPeriod(); };
            toolbar.Controls.Add(period, 1, 0);
            FlowLayoutPanel actions = new FlowLayoutPanel(); actions.Dock = DockStyle.Fill; actions.WrapContents = false;
            actions.Padding = new Padding(0, 5, 0, 0);
            workWeekButton = MakeButton(Lang.T("工作周"), Lang.IsEnglish ? 86 : 72, delegate { SetView(false, true); });
            weekButton = MakeButton(Lang.T("周"), Lang.IsEnglish ? 54 : 42, delegate { SetView(false, false); });
            monthButton = MakeButton(Lang.T("月"), Lang.IsEnglish ? 58 : 42, delegate { SetView(true, false); });
            workWeekButton.Name = "calendar-original-workweek"; weekButton.Name = "calendar-original-week"; monthButton.Name = "calendar-original-month";
            actions.Controls.Add(workWeekButton); actions.Controls.Add(weekButton); actions.Controls.Add(monthButton);
            Button add = MakeButton(Lang.T("＋ 添加日程"), Lang.IsEnglish ? 108 : 125, delegate { AddEvent(focusDate, 9); });
            add.BackColor = Color.FromArgb(73, 111, 101); add.ForeColor = Color.White;
            actions.Controls.Add(add); toolbar.Controls.Add(actions, 2, 0);
            Panel topRow = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            topRow.Controls.Add(toolbar);
            BuildReferenceBar(); topRow.Controls.Add(refBar);
            layout.Controls.Add(topRow, 0, 0);

            hint = new Label(); hint.Dock = DockStyle.Fill;
            hint.Text = Lang.T("每一天，留一点从容   ·   点击课程编辑；双击空白处添加");
            hint.Font = new Font("Microsoft YaHei UI", 9F); hint.ForeColor = muted;
            hint.TextAlign = ContentAlignment.MiddleLeft; hint.Padding = new Padding(6, 0, 0, 0);
            layout.Controls.Add(hint, 0, 1);
            dayHeader = new DoubleBufferedPanel(); dayHeader.Dock = DockStyle.Fill; dayHeader.Margin = Padding.Empty; dayHeader.Paint += PaintHeader;
            dayHeader.Name = "calendar-day-header";
            dayHeader.MouseClick += delegate(object sender, MouseEventArgs e)
            {
                var whole = AllDayHit(e.Location); if (whole != null) { EditEvent(whole); return; }
                var hit = BadgeHit(e.Location); if (hit != null) badgeClicks.Click(hit);
            };
            dayHeader.MouseDoubleClick += delegate(object sender, MouseEventArgs e)
            {
                var hit = BadgeHit(e.Location); if (hit != null) { badgeClicks.DoubleClick(hit); return; }
                // Double-click a date (or the empty all-day row under it): a new all-day event on that day.
                if (!monthView && AllDayHit(e.Location) == null && e.X >= surface.TimeGutter) AddAllDayEvent(HeaderDate(e.X));
            };
            dayHeader.MouseMove += delegate(object sender, MouseEventArgs e)
            {
                var hit = BadgeHit(e.Location); var whole = AllDayHit(e.Location);
                dayHeader.Cursor = hit == null && whole == null ? Cursors.Default : Cursors.Hand;
                string text = whole != null ? CalendarSurface.OccurrenceTip(whole) : hit == null ? "" : hit.Count == 1 ? CalendarSurface.DeadlineTip(hit[0]) : CalendarSurface.DeadlineList(hit);
                if (text != badgeTipText) { badgeTipText = text; badgeTip.SetToolTip(dayHeader, text); }
            };
            badgeClicks.Single += OpenBadge;
            badgeClicks.Double += delegate(List<DeadlineMark> hit) { if (hit.Count == 1) App.EditDeadline(this, hit[0].PageId, hit[0].TaskId); else OpenBadge(hit); };
            layout.Controls.Add(dayHeader, 0, 2);
            surface = new CalendarSurface(); surface.Dock = DockStyle.Fill;
            surface.OpenOccurrence += EditEvent;
            surface.OpenDeadline += delegate(DeadlineMark mark) { App.ShowDeadline(this, mark.PageId, mark.TaskId); };
            surface.EditDeadline += delegate(DeadlineMark mark) { App.EditDeadline(this, mark.PageId, mark.TaskId); };
            surface.ChooseDeadline += delegate(List<DeadlineMark> group) { OpenBadge(group); };
            surface.CreateEvent += AddEvent;
            surface.SelectDate += delegate(DateTime date) { focusDate = date; };
            surface.SizeChanged += delegate { dayHeader.Invalidate(); };
            layout.Controls.Add(surface, 0, 3);
        }

        private string periodFull = "", periodShort = "";
        // Drops the year when the range would otherwise wrap onto a second line.
        private void FitPeriod()
        {
            if (period == null) return;
            string text = TextRenderer.MeasureText(periodFull, period.Font).Width <= period.ClientSize.Width ? periodFull : periodShort;
            if (period.Text != text) period.Text = text;
        }

        private DateTime WeekStart(DateTime date)
        {
            int start = App.Data.Settings.Calendar.WeekStartDay;
            return date.Date.AddDays(-(((int)date.DayOfWeek - start + 7) % 7));
        }

        // Switches between 工作周, 周 and 月 and remembers the choice for the next start.
        private void SetView(bool month, bool work)
        {
            monthView = month; workWeek = !month && work;
            CalendarOptions options = App.Data.Settings.Calendar;
            options.DefaultView = month ? "Month" : "Week"; options.WorkWeek = workWeek;
            lastDefaultView = options.DefaultView + (options.WorkWeek ? "|work" : "");
            App.SettingsChanged();
            RefreshData();
            if (!month) surface.ScrollToMorning();
        }

        // Default height: enough to show 07:00 to 12:00 below the toolbar, but never taller than the screen allows.
        public void FitDefaultHeight()
        {
            if (!Visible || surface == null || WindowState != FormWindowState.Normal) return;
            int desired = Height - surface.ClientSize.Height + 5 * surface.HourPixels + Px(24);
            Rectangle work = Screen.FromControl(this).WorkingArea;
            int top = Top, height = Math.Max(MinimumSize.Height, desired);
            if (top + height > work.Bottom) height = Math.Max(MinimumSize.Height, work.Bottom - top);
            if (top + height > work.Bottom) top = Math.Max(work.Top, work.Bottom - height);
            Bounds = new Rectangle(Left, top, Width, Math.Min(height, work.Height));
            surface.ScrollToMorning();
            Remember();
        }

        private void MovePeriod(int amount)
        {
            focusDate = monthView ? focusDate.AddMonths(amount) : focusDate.AddDays(amount * 7);
            RefreshData();
        }

        public void RefreshData()
        {
            if (IsDisposed || surface == null) return;
            using (new RedrawPause(Body)) RefreshDataNow();
        }
        private void RefreshDataNow()
        {
            string defaultView = App.Data.Settings.Calendar.DefaultView + (App.Data.Settings.Calendar.WorkWeek ? "|work" : "");
            if (lastDefaultView != defaultView) { monthView = App.Data.Settings.Calendar.DefaultView == "Month"; workWeek = !monthView && App.Data.Settings.Calendar.WorkWeek; lastDefaultView = defaultView; }
            DateTime start = monthView ? WeekStart(new DateTime(focusDate.Year, focusDate.Month, 1)) : ViewStart();
            DateTime end = start.AddDays(monthView ? 41 : ViewDays - 1);
            occurrences = CalendarEngine.GetOccurrences(App.Data, start, end).ToList();
            deadlines = App.Data.Settings.Calendar.ShowDeadlines ? DeadlineLogic.Marks(App.Data, start, end) : new List<DeadlineMark>();
            bool outlook = App.Data.Settings.Calendar.Outlook.Show;
            if (outlook) occurrences.AddRange(OutlookLogic.Timed(App.Outlook, App.Data.Settings.Calendar.Outlook, start, end));
            // All-day events (own and Outlook's) go in the row under the dates, not in the hour grid.
            allDay = occurrences.Where(o => o.AllDay).ToList();
            if (outlook) allDay.AddRange(OutlookLogic.AllDay(App.Outlook, start, end).Select(e => new Occurrence {
                External = e, Date = e.StartLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), KeyDate = e.StartLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                StartTime = "", EndTime = "", Title = e.Title, Location = e.Location, Notes = e.Notes, AllDay = true, Days = Math.Max(1, (e.EndLocal.Date - e.StartLocal.Date).Days),
                Color = OutlookLogic.ColorFor(App.Data.Settings.Calendar.Outlook, e) }));
            allDay = allDay.OrderBy(o => o.Date, StringComparer.Ordinal).ThenByDescending(o => o.Days).ThenBy(o => o.Title, StringComparer.Ordinal).ToList();
            occurrences = occurrences.Where(o => !o.AllDay).ToList();
            surface.SetAllDay(allDay);
            surface.SetDeadlines(deadlines);
            periodFull = monthView ? Lang.MonthTitle(focusDate) : start.ToString("M.d") + " – " + end.ToString("M.d") + "  ·  " + focusDate.Year;
            periodShort = monthView ? periodFull : start.ToString("M.d") + " – " + end.ToString("M.d");
            FitPeriod();
            int teachingWeek = SettingsLogic.TeachingWeek(App.Data.Settings, focusDate);
            hint.Text = (teachingWeek > 0 ? Lang.T("教学第 {0} 周", teachingWeek) + "   ·   " : "") + Lang.T("点击课程编辑；双击空白处添加");
            refRange.Text = monthView ? Lang.MonthTitle(focusDate) : start.ToString("M.d") + " – " + end.ToString("M.d");
            refMeta.Text = monthView ? (teachingWeek > 0 ? Lang.T("教学第 {0} 周", teachingWeek) : "") : focusDate.Year + (teachingWeek > 0 ? " · " + Lang.T("教学第 {0} 周", teachingWeek) : "");
            ArrangeReferenceBar();
            surface.SetData(start, focusDate, monthView, occurrences, ViewDays);
            // The view buttons and the day row's height depend on the view; restyle only when that changed.
            string chrome = monthView + "|" + workWeek + "|" + Math.Min(AllDayRows, AllDayLayout().Count);
            if (chrome != appliedChrome) { appliedChrome = chrome; ApplyAppearance(); }
            dayHeader.Invalidate();
        }
        private string appliedChrome;

        private static string StyleFor(string notebookLayout)
        {
            return notebookLayout == "Card" || notebookLayout == "Clean" ? "Clean" : notebookLayout == "Paper" || notebookLayout == "Journal" ? "Journal" : "Original";
        }
        private bool IsReference { get { return calendarStyle != "Original"; } }
        // Unfolded: the low, small-type header. Folded: the same bar as the notebooks, so folded bars match.
        protected override bool SlimReferenceHeader { get { return !Collapsed; } }
        private int Px(float value) { using (Graphics g = CreateGraphics()) return (int)Math.Round(value * g.DpiY / 96F); }

        protected override void OnAppearanceChanged(AppearanceOptions appearance)
        {
            if (surface == null) return;
            calendarStyle = StyleFor(App.Data.Settings.NotebookLayout);
            surface.SetAppearance(appearance);
            // Sizes are measured in screen pixels, so styling waits until the form has been scaled for this display.
            if (!contentReady) return;
            NormalizeHeaderButtons();
            if (!originalsCaptured)
            {
                originalBodyPadding = Body.Padding; originalLayoutPadding = contentLayout.Padding;
                originalToolbarRow = contentLayout.RowStyles[0].Height; originalHintRow = contentLayout.RowStyles[1].Height;
                originalsCaptured = true;
            }
            if (IsReference) StyleReference(appearance);
            else StyleOriginal(appearance);
            dayHeader.Invalidate();
        }

        private void StyleOriginal(AppearanceOptions appearance)
        {
            Color bg = AppearancePainter.Background(appearance), surfaceColor = AppearancePainter.Surface(appearance);
            SetCompactNotebookHeader(false);
            SetReferenceHeader(false, "", bg, AppearancePainter.Foreground(appearance), Ui.Muted, Color.FromArgb(76, 118, 108), Color.FromArgb(76, 118, 108), surfaceColor, Ui.Border, false);
            surface.SetReference(false, palette);
            toolbar.Visible = true; refBar.Visible = false; hint.Visible = true;
            Color selected = AppearancePainter.Dark(bg) ? Color.FromArgb(64, 90, 86) : Color.FromArgb(219, 233, 227);
            weekButton.BackColor = !monthView && !workWeek ? selected : surfaceColor;
            workWeekButton.BackColor = !monthView && workWeek ? selected : surfaceColor;
            monthButton.BackColor = monthView ? selected : surfaceColor;
            SetExpandedMinimumSize(new Size(ExpandedMinimumSize.Width, Px(520)));
            if (!contentReady) return;
            if (originalsCaptured)
            {
                Body.Padding = originalBodyPadding; contentLayout.Padding = originalLayoutPadding;
                contentLayout.RowStyles[0].Height = originalToolbarRow; contentLayout.RowStyles[1].Height = originalHintRow;
            }
            using (Font headerFont = new Font("Microsoft YaHei UI", appearance.FontSize, FontStyle.Bold))
                contentLayout.RowStyles[2].Height = Math.Max(Px(45), TextRenderer.MeasureText(monthView ? Lang.T("周一") : Lang.T("周一\n9/28"), headerFont).Height + Px(10)) + AllDayHeight;
        }

        // 清爽 / 纸页: the same header as the reference notebooks, one navigation row, a one-line day header.
        private void StyleReference(AppearanceOptions appearance)
        {
            bool journal = calendarStyle == "Journal";
            palette = CalendarPalette.For(AppearancePainter.Background(appearance), App.Data.Settings.NotebookLayout);
            CalendarPalette p = palette;
            SetCompactNotebookHeader(true, Collapsed ? 8.5F : 8F);
            SetReferenceHeader(true, "", p.Back, p.Ink, journal ? p.Ink : p.Sub, p.Accent, p.Accent, p.Soft, p.Rule, journal);
            surface.SetReference(true, p);
            toolbar.Visible = false; refBar.Visible = true; hint.Visible = false;
            refBar.BackColor = p.Back; refBar.Parent.BackColor = p.Back; dayHeader.BackColor = p.Back; contentLayout.BackColor = p.Back; Body.BackColor = p.Back;
            refRange.Font = RefFont(11.5F, FontStyle.Regular); refRange.ForeColor = p.Ink; refRange.BackColor = p.Back;
            refMeta.Font = RefFont(8.5F, FontStyle.Regular); refMeta.ForeColor = p.Sub; refMeta.BackColor = p.Back;
            foreach (Button b in new[] { refPrev, refNext, refToday, refWorkWeek, refWeek, refMonth, refAdd })
            {
                b.BackColor = p.Back; b.ForeColor = p.Sub; b.Font = RefFont(9F, FontStyle.Regular);
                ((CalendarBarButton)b).HoverColor = p.Soft;
            }
            refPrev.Font = refNext.Font = RefFont(12F, FontStyle.Regular);
            refAdd.Font = RefFont(11F, FontStyle.Regular); refAdd.ForeColor = p.Accent;
            // The ‹ › glyphs sit low in their line box.
            ((CalendarBarButton)refPrev).TextOffset = ((CalendarBarButton)refNext).TextOffset = -Px(2);
            Button selected = monthView ? refMonth : workWeek ? refWorkWeek : refWeek;
            selected.BackColor = p.Soft; selected.ForeColor = p.Ink; selected.Font = RefFont(9F, FontStyle.Bold);
            SetExpandedMinimumSize(new Size(ExpandedMinimumSize.Width, Px(420)));
            if (!contentReady) return;
            Body.Padding = new Padding(0, 0, 0, Px(2));
            contentLayout.Padding = new Padding(Px(16), Px(2), Px(14), Px(8));
            contentLayout.RowStyles[0].Height = Math.Max(Px(34), refRange.Font.Height + Px(12));
            contentLayout.RowStyles[1].Height = 0;
            contentLayout.RowStyles[2].Height = Math.Max(Px(32), RefFont(9F, FontStyle.Bold).Height + Px(14)) + AllDayHeight;
            ArrangeReferenceBar();
        }

        private Font RefFont(float size, FontStyle style)
        {
            float scaled = (float)Math.Round(size * SettingsLogic.EffectiveAppearance(App.Data, "calendar").FontSize / 9F, 2);
            string key = scaled.ToString(CultureInfo.InvariantCulture) + "|" + style;
            Font font;
            if (!refFonts.TryGetValue(key, out font)) { font = new Font("Microsoft YaHei UI", scaled, style); refFonts[key] = font; }
            return font;
        }

        private Button RefButton(string text, string name, string accessible, EventHandler action)
        {
            Button b = new CalendarBarButton { Text = text, Name = name, AccessibleName = accessible, AutoSize = false, Margin = Padding.Empty, Cursor = Cursors.Hand, Tag = "appearance-custom-font", UseMnemonic = false, TabStop = false };
            b.Click += action; return b;
        }

        private void BuildReferenceBar()
        {
            refBar = new Panel { Name = "calendar-reference-bar", Dock = DockStyle.Fill, Visible = false };
            refPrev = RefButton("‹", "calendar-previous", Lang.T("上一周或上一月"), delegate { MovePeriod(-1); });
            refNext = RefButton("›", "calendar-next", Lang.T("下一周或下一月"), delegate { MovePeriod(1); });
            refToday = RefButton(Lang.T("今天"), "calendar-today", Lang.T("回到今天"), delegate { focusDate = DateTime.Today; RefreshData(); });
            refWorkWeek = RefButton(Lang.T("工作周"), "calendar-workweek", Lang.T("工作周视图：只看周一至周五"), delegate { SetView(false, true); });
            refWeek = RefButton(Lang.T("周"), "calendar-week", Lang.T("周视图"), delegate { SetView(false, false); });
            refMonth = RefButton(Lang.T("月"), "calendar-month", Lang.T("月视图"), delegate { SetView(true, false); });
            refAdd = RefButton("＋", "calendar-add", Lang.T("添加日程"), delegate { AddEvent(focusDate, 9); });
            refRange = new Label { Name = "calendar-range", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Tag = "appearance-custom-font", UseMnemonic = false };
            refMeta = new Label { Name = "calendar-meta", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Tag = "appearance-custom-font", UseMnemonic = false, AutoEllipsis = true };
            refBar.Controls.AddRange(new Control[] { refPrev, refRange, refNext, refMeta, refToday, refWorkWeek, refWeek, refMonth, refAdd });
            refBar.Resize += delegate { ArrangeReferenceBar(); };
            var tips = new ToolTip(); tips.SetToolTip(refWorkWeek, Lang.T("只看周一至周五")); Disposed += delegate { tips.Dispose(); };
        }

        private void ArrangeReferenceBar()
        {
            if (refBar == null || !refBar.Visible || refRange.Font == null) return;
            int w = refBar.ClientSize.Width, h = refBar.ClientSize.Height;
            int bh = Math.Min(h - Px(4), Math.Max(Px(24), refToday.Font.Height + Px(8))), y = (h - bh) / 2, arrow = Px(22);
            refPrev.SetBounds(-Px(6), y, arrow, bh);
            int rangeWidth = TextRenderer.MeasureText(refRange.Text, refRange.Font).Width + Px(2);
            refRange.SetBounds(refPrev.Right, 0, rangeWidth, h);
            refNext.SetBounds(refRange.Right, y, arrow, bh);
            int add = Px(26); refAdd.SetBounds(w - add, y, add, bh);
            int segment = Math.Max(Px(28), TextRenderer.MeasureText(Lang.T("月"), refMonth.Font).Width + Px(14));
            refMonth.SetBounds(refAdd.Left - Px(8) - segment, y, segment, bh);
            refWeek.SetBounds(refMonth.Left - segment, y, segment, bh);
            int workSegment = TextRenderer.MeasureText(Lang.T("工作周"), refWorkWeek.Font).Width + Px(14);
            refWorkWeek.SetBounds(refWeek.Left - workSegment, y, workSegment, bh);
            int today = TextRenderer.MeasureText(Lang.T("今天"), refToday.Font).Width + Px(14);
            refToday.SetBounds(refWorkWeek.Left - Px(8) - today, y, today, bh);
            int metaLeft = refNext.Right + Px(8);
            refMeta.SetBounds(metaLeft, 0, Math.Max(0, refToday.Left - Px(8) - metaLeft), h);
        }

        private void PaintHeader(object sender, PaintEventArgs e)
        {
            badgeHits.Clear(); allDayHits.Clear();
            if (IsReference) { PaintReferenceHeader(e.Graphics); return; }
            AppearanceOptions appearance = SettingsLogic.EffectiveAppearance(App.Data, "calendar");
            Color foreground = AppearancePainter.Foreground(appearance);
            bool dark = AppearancePainter.Dark(AppearancePainter.Background(appearance));
            e.Graphics.Clear(AppearancePainter.Background(appearance));
            int left = monthView ? 0 : surface.TimeGutter;
            int available = surface == null ? dayHeader.Width : surface.GridWidth;
            int count = monthView ? 7 : ViewDays;
            float width = (available - left) / (float)count;
            DateTime start = monthView ? WeekStart(focusDate) : ViewStart();
            for (int i = 0; i < count; i++)
            {
                DateTime date = start.AddDays(i);
                Rectangle rect = new Rectangle(left + (int)(i * width), 1, (int)width, Math.Max(1, dayHeader.ClientSize.Height - AllDayHeight - 2));
                bool today = !monthView && date.Date == DateTime.Today;
                if (today) using (Brush b = new SolidBrush(dark ? Color.FromArgb(58, 83, 77) : Color.FromArgb(226, 237, 231))) e.Graphics.FillRectangle(b, rect);
                string text = Lang.Weekday(date.DayOfWeek) + (monthView ? "" : "\n" + date.ToString("M/d"));
                using (Font font = new Font("Microsoft YaHei UI", appearance.FontSize, today ? FontStyle.Bold : FontStyle.Regular))
                {
                    TextRenderer.DrawText(e.Graphics, text, font, rect, foreground, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    Size label = TextRenderer.MeasureText(e.Graphics, text, font);
                    DrawDayBadge(e.Graphics, date, rect.Left + (rect.Width + label.Width) / 2 + Px(2), rect.Top, rect.Height, AppearancePainter.Background(appearance));
                }
            }
            PaintAllDay(e.Graphics, AppearancePainter.Background(appearance), foreground, Blend(AppearancePainter.Background(appearance), foreground, .55F), false);
        }

        // One line per day, "一 28"; today sits in a soft accent pill.
        private void PaintReferenceHeader(Graphics graphics)
        {
            CalendarPalette p = palette;
            graphics.Clear(p.Back);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int left = monthView ? 0 : surface.TimeGutter;
            int count = monthView ? 7 : ViewDays;
            float width = (surface.GridWidth - left) / (float)count;
            int height = dayHeader.ClientSize.Height - AllDayHeight;
            DateTime start = monthView ? WeekStart(focusDate) : ViewStart();
            Font regular = RefFont(9F, FontStyle.Regular), bold = RefFont(9F, FontStyle.Bold);
            const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            for (int i = 0; i < 7; i++)
            {
                DateTime date = start.AddDays(i);
                bool today = !monthView && date.Date == DateTime.Today;
                string day = Lang.WeekdayShort(date.DayOfWeek), number = monthView ? "" : " " + date.Day;
                Font font = today ? bold : regular;
                Size daySize = TextRenderer.MeasureText(graphics, day, font, Size.Empty, flags), numberSize = TextRenderer.MeasureText(graphics, number, font, Size.Empty, flags);
                int total = daySize.Width + numberSize.Width;
                // Keep the pill a few pixels clear of the rule underneath.
                int pillHeight = daySize.Height + Px(4), pillTop = Math.Max(0, (height - 1 - Px(5) - pillHeight) / 2 + Px(1));
                int x = left + (int)(i * width + (width - total) / 2), y = pillTop + (pillHeight - daySize.Height) / 2;
                if (today)
                {
                    Rectangle pill = new Rectangle(x - Px(9), pillTop, total + Px(18), pillHeight);
                    using (var path = CalendarSurface.Rounded(pill, pill.Height / 2))
                    using (Brush fill = new SolidBrush(p.Today)) graphics.FillPath(fill, path);
                }
                TextRenderer.DrawText(graphics, day, font, new Point(x, y), today ? p.Accent : p.Sub, flags);
                if (number != "") TextRenderer.DrawText(graphics, number, font, new Point(x + daySize.Width, y), today ? p.Accent : p.Ink, flags);
                if (!monthView) DrawDayBadge(graphics, date, x + total + (today ? Px(11) : Px(4)), pillTop, pillHeight, p.Back);
            }
            PaintAllDay(graphics, p.Back, p.Ink, p.Sub, true);
            using (Pen rule = new Pen(p.Rule)) graphics.DrawLine(rule, 0, dayHeader.ClientSize.Height - 1, dayHeader.Width, dayHeader.ClientSize.Height - 1);
        }

        // All-day events: one bar per event across the days it covers, at most three rows.
        private const int AllDayRows = 3;
        private List<List<Occurrence>> AllDayLayout()
        {
            var rows = new List<List<Occurrence>>();
            if (monthView) return rows;
            DateTime first = ViewStart(), last = first.AddDays(ViewDays);
            foreach (Occurrence e in allDay.Where(x => FirstDay(x) < last && AfterLastDay(x) > first))
            {
                var row = rows.FirstOrDefault(r => r.All(o => AfterLastDay(o) <= FirstDay(e) || FirstDay(o) >= AfterLastDay(e)));
                if (row == null) { row = new List<Occurrence>(); rows.Add(row); }
                row.Add(e);
            }
            return rows;
        }
        // The date under a point of the day row (week views).
        private DateTime HeaderDate(int x)
        {
            float width = (surface.GridWidth - surface.TimeGutter) / (float)ViewDays;
            int index = Math.Max(0, Math.Min(ViewDays - 1, (int)((x - surface.TimeGutter) / width)));
            return ViewStart().AddDays(index);
        }
        private int AllDayRowHeight { get { using (Font font = AllDayFont()) return Math.Max(Px(20), font.Height + Px(7)); } }
        private int AllDayHeight { get { int rows = Math.Min(AllDayRows, AllDayLayout().Count); return rows == 0 ? 0 : rows * AllDayRowHeight + Px(6); } }
        private Font AllDayFont() { return new Font("Microsoft YaHei UI", 8F * SettingsLogic.EffectiveAppearance(App.Data, "calendar").FontSize / 9F); }

        private void PaintAllDay(Graphics graphics, Color back, Color ink, Color sub, bool rounded)
        {
            var rows = AllDayLayout(); if (rows.Count == 0) return;
            int rowHeight = AllDayRowHeight, top = dayHeader.ClientSize.Height - AllDayHeight + Px(2), left = surface.TimeGutter;
            float width = (surface.GridWidth - left) / (float)ViewDays;
            DateTime first = ViewStart();
            int hidden = rows.Skip(AllDayRows).Sum(r => r.Count);
            using (Font font = AllDayFont())
            {
                TextRenderer.DrawText(graphics, Lang.T("全天") + (hidden > 0 ? " +" + hidden : ""), font, new Rectangle(0, top, left - Px(6), rowHeight), sub, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                for (int r = 0; r < Math.Min(AllDayRows, rows.Count); r++)
                    foreach (Occurrence e in rows[r])
                    {
                        Color blue; try { blue = ColorTranslator.FromHtml(e.Color); } catch { blue = ColorTranslator.FromHtml(OutlookLogic.Color); }
                        int a = Math.Max(0, (FirstDay(e) - first).Days), b = Math.Min(ViewDays, (AfterLastDay(e) - first).Days);
                        Rectangle bar = new Rectangle(left + (int)(a * width) + Px(3), top + r * rowHeight + Px(1), Math.Max(Px(10), (int)((b - a) * width) - Px(6)), rowHeight - Px(3));
                        var smoothing = graphics.SmoothingMode; graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        using (var path = CalendarSurface.Rounded(bar, rounded ? Px(4) : Px(2)))
                        using (Brush fill = new SolidBrush(Blend(back, blue, AppearancePainter.Dark(back) ? .3F : .14F))) graphics.FillPath(fill, path);
                        using (Brush stripe = new SolidBrush(blue)) graphics.FillRectangle(stripe, bar.X, bar.Y + Px(2), Px(3), bar.Height - Px(4));
                        graphics.SmoothingMode = smoothing;
                        TextRenderer.DrawText(graphics, e.Title, font, new Rectangle(bar.X + Px(7), bar.Y, bar.Width - Px(9), bar.Height), ink, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                        allDayHits.Add(new KeyValuePair<Rectangle, Occurrence>(bar, e));
                    }
            }
        }
        private Occurrence AllDayHit(Point point)
        {
            foreach (var hit in allDayHits) if (hit.Key.Contains(point)) return hit.Value;
            return null;
        }

        // ⚑ beside the date, "⚑2" for several; hover shows them, a click opens one (or a menu to pick from).
        private void DrawDayBadge(Graphics graphics, DateTime date, int x, int top, int height, Color back)
        {
            if (monthView) return;
            string key = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            List<DeadlineMark> day = DayDeadlines.Where(m => m.Date == key).ToList();
            if (day.Count == 0) return;
            using (Font font = BadgeFont())
            {
                const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;
                string text = "⚑" + (day.Count > 1 ? day.Count.ToString(CultureInfo.InvariantCulture) : "");
                Size size = TextRenderer.MeasureText(graphics, text, font, Size.Empty, flags);
                Rectangle badge = new Rectangle(x, top + (height - size.Height - Px(4)) / 2, size.Width + Px(8), size.Height + Px(4));
                Color line = CalendarSurface.DeadlineColor(back);
                var smoothing = graphics.SmoothingMode; graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var path = CalendarSurface.Rounded(badge, badge.Height / 2))
                using (Brush fill = new SolidBrush(Blend(back, line, .14F))) graphics.FillPath(fill, path);
                graphics.SmoothingMode = smoothing;
                TextRenderer.DrawText(graphics, text, font, badge, line, flags | TextFormatFlags.HorizontalCenter);
                Rectangle target = badge; target.Inflate(Px(3), Px(3));
                badgeHits.Add(new KeyValuePair<Rectangle, List<DeadlineMark>>(target, day));
            }
        }

        private Font BadgeFont() { return new Font("Microsoft YaHei UI", 8F * SettingsLogic.EffectiveAppearance(App.Data, "calendar").FontSize / 9F, FontStyle.Bold); }

        private List<DeadlineMark> BadgeHit(Point point)
        {
            foreach (var hit in badgeHits) if (hit.Key.Contains(point)) return hit.Value;
            return null;
        }

        // One deadline: open it in the DDL notebook. Several: pick one from a short menu.
        private void OpenBadge(List<DeadlineMark> hit)
        {
            if (hit.Count == 1) { App.ShowDeadline(this, hit[0].PageId, hit[0].TaskId); return; }
            var menu = new ContextMenuStrip { Font = new Font("Microsoft YaHei UI", 9F), ShowImageMargin = false };
            foreach (DeadlineMark mark in hit)
            {
                DeadlineMark captured = mark;
                var item = menu.Items.Add("⚑ " + CalendarSurface.DeadlineCaption(mark, true) + (mark.Archived ? Lang.T("（已归档）") : ""), null, delegate { App.ShowDeadline(this, captured.PageId, captured.TaskId); });
                item.ToolTipText = CalendarSurface.DeadlineTip(mark);
            }
            menu.Closed += delegate { BeginInvoke(new Action(menu.Dispose)); };
            menu.Show(dayHeader, dayHeader.PointToClient(Cursor.Position));
        }

        private static Color Blend(Color background, Color color, float amount)
        {
            return Color.FromArgb((int)(background.R * (1F - amount) + color.R * amount), (int)(background.G * (1F - amount) + color.G * amount), (int)(background.B * (1F - amount) + color.B * amount));
        }

        private void AddAllDayEvent(DateTime date) { AddEvent(date, 0, true); }
        private void AddEvent(DateTime date, int hour) { AddEvent(date, hour, false); }
        private void AddEvent(DateTime date, int hour, bool allDayEvent)
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
            if (allDayEvent) { item.AllDay = true; item.Days = 1; item.StartTime = "00:00"; item.EndTime = "23:59"; item.WeekDays = new List<int> { (int)date.DayOfWeek }; }
            using (CalendarEditor editor = new CalendarEditor(item, true, false))
            {
                if (editor.ShowDialog(this) != DialogResult.OK) return;
                App.Data.Events.Add(editor.Result); App.Save();
            }
        }

        // Details of an Outlook event (read-only), and the color for every event of that name.
        private void ShowOutlookEvent(OutlookEvent e)
        {
            using (var dialog = new Form { Text = Lang.T("Outlook 日程（只读）"), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false, AutoScaleMode = AutoScaleMode.Dpi, AutoScaleDimensions = new SizeF(96F, 96F), Font = new Font("Microsoft YaHei UI", 9.5F), BackColor = Color.FromArgb(250, 250, 247), ClientSize = new Size(400, 230) })
            {
                Color current = ColorTranslator.FromHtml(OutlookLogic.ColorFor(App.Data.Settings.Calendar.Outlook, e));
                var text = new Label { Text = CalendarSurface.OutlookTip(e), Location = new Point(18, 16), Size = new Size(364, 150), UseMnemonic = false, Name = "outlook-details" };
                var swatch = new Panel { BackColor = current, Location = new Point(18, 182), Size = new Size(18, 18) };
                var color = new Button { Text = Lang.T("更改这个日程的颜色…"), Name = "outlook-title-color", Location = new Point(44, 176), Size = new Size(200, 32), FlatStyle = FlatStyle.Flat };
                var close = new Button { Text = Lang.T("关闭"), Location = new Point(292, 176), Size = new Size(90, 32), DialogResult = DialogResult.Cancel };
                color.Click += delegate
                {
                    using (var picker = new ColorDialog { Color = current, FullOpen = true })
                    {
                        if (picker.ShowDialog(dialog) != DialogResult.OK) return;
                        App.SetOutlookTitleColor(e.Title, "#" + picker.Color.R.ToString("X2") + picker.Color.G.ToString("X2") + picker.Color.B.ToString("X2"));
                        current = picker.Color; swatch.BackColor = current;
                    }
                };
                dialog.Controls.AddRange(new Control[] { text, swatch, color, close }); dialog.CancelButton = close;
                dialog.ShowDialog(this);
            }
        }

        private void EditEvent(Occurrence occurrence)
        {
            if (occurrence.External != null) { ShowOutlookEvent(occurrence.External); return; }
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
                WeekDays = new List<int>(), ExcludedDates = new List<string>(), Overrides = new List<EventOverride>(), AllDay = item.Series.AllDay, Days = item.Series.Days
            };
        }
    }

    internal sealed class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel() { DoubleBuffered = true; ResizeRedraw = true; }
    }

    // Tells a single click from the first half of a double click: the single action waits out the double-click time.
    internal sealed class ClickOrDouble<T> : IDisposable where T : class
    {
        public event Action<T> Single;
        public event Action<T> Double;
        private readonly Timer timer = new Timer();
        private T pending;
        public ClickOrDouble()
        {
            timer.Interval = Math.Max(100, SystemInformation.DoubleClickTime);
            timer.Tick += delegate { timer.Stop(); T value = pending; pending = null; if (value != null && Single != null) Single(value); };
        }
        public void Click(T value) { pending = value; timer.Stop(); timer.Start(); }
        public void DoubleClick(T value) { timer.Stop(); pending = null; if (Double != null) Double(value); }
        public void Dispose() { timer.Dispose(); }
    }

    // Text button for the calendar's navigation row: drawn by hand so the text sits exactly in the middle.
    internal sealed class CalendarBarButton : Button
    {
        public Color HoverColor = Color.Empty;
        public int TextOffset;
        private bool hot;
        public CalendarBarButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
        }
        protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hot = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Color behind = Parent != null ? Parent.BackColor : BackColor;
            e.Graphics.Clear(behind);
            Color fill = hot && !HoverColor.IsEmpty ? HoverColor : BackColor;
            if (fill.ToArgb() != behind.ToArgb())
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var path = CalendarSurface.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), Math.Max(2, Height / 5)))
                using (Brush brush = new SolidBrush(fill)) e.Graphics.FillPath(brush, path);
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(0, TextOffset, Width, Height), ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }
    }

    // Colors for the 清爽 and 纸页 calendar, matching the reference notebooks.
    internal struct CalendarPalette
    {
        public Color Back, Ink, Sub, Faint, Rule, HalfRule, Soft, Accent, Today, Outside;
        private static Color Hex(string value) { return ColorTranslator.FromHtml(value); }
        private static Color Mix(Color from, Color to, double amount)
        {
            return Color.FromArgb((int)Math.Round(from.R + (to.R - from.R) * amount), (int)Math.Round(from.G + (to.G - from.G) * amount), (int)Math.Round(from.B + (to.B - from.B) * amount));
        }
        public static CalendarPalette For(Color back, string notebookLayout)
        {
            bool dark = AppearancePainter.Dark(back), paper = notebookLayout == "Paper" || notebookLayout == "Journal";
            var p = new CalendarPalette { Back = back };
            p.Ink = dark ? Hex("#E3E9E5") : Hex("#283732");
            p.Sub = dark ? Hex("#AFB9B2") : Hex("#68736E");
            p.Accent = dark ? Hex("#A5C5AC") : Hex("#527562");
            if (!dark && back.ToArgb() == Hex(SettingsLogic.LayoutBackground(notebookLayout)).ToArgb())
            {
                p.Rule = paper ? Hex("#E9E4D6") : Hex("#E8ECE8");
                p.Soft = paper ? Hex("#EFECDF") : Hex("#F3F6F2");
            }
            else { p.Rule = Mix(back, p.Ink, dark ? .16 : .09); p.Soft = Mix(back, p.Ink, dark ? .08 : .045); }
            p.HalfRule = Mix(back, p.Rule, .45);
            p.Faint = Mix(p.Sub, back, .35);
            p.Today = Mix(back, p.Accent, dark ? .22 : .12);
            p.Outside = Mix(back, p.Ink, dark ? .05 : .025);
            return p;
        }
    }

    internal sealed class CalendarSurface : ScrollableControl
    {
        private bool reference;
        private Color accent = Color.FromArgb(82, 117, 98);
        // 清爽 / 纸页: the notebook palette, softer rules, rounded course blocks.
        public void SetReference(bool on, CalendarPalette p)
        {
            reference = on;
            if (on)
            {
                canvas = p.Back; foreground = p.Ink; secondary = p.Sub; ruleColor = p.Rule; halfRuleColor = p.HalfRule;
                todayColor = Blend(p.Back, p.Accent, AppearancePainter.Dark(p.Back) ? .1F : .045F); outsideColor = p.Outside; accent = p.Accent;
                BackColor = canvas;
            }
            Invalidate();
        }
        internal static System.Drawing.Drawing2D.GraphicsPath Rounded(Rectangle r, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int d = Math.Max(1, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
            path.AddArc(r.X, r.Y, d, d, 180, 90); path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure(); return path;
        }
        public event Action<Occurrence> OpenOccurrence;
        public event Action<DeadlineMark> OpenDeadline;
        public event Action<DeadlineMark> EditDeadline;
        public event Action<List<DeadlineMark>> ChooseDeadline;
        private List<DeadlineMark> marks = new List<DeadlineMark>();
        private readonly ClickOrDouble<DeadlineMark> markClicks = new ClickOrDouble<DeadlineMark>();
        private List<Occurrence> allDayEvents = new List<Occurrence>();
        public void SetAllDay(List<Occurrence> values) { allDayEvents = values ?? new List<Occurrence>(); Invalidate(); }

        // Hover text for any occurrence: Outlook's own, an all-day one, or a timed one.
        internal static string OccurrenceTip(Occurrence o)
        {
            if (o.External != null) return OutlookTip(o.External);
            DateTime first = TimeUtil.ParseDate(o.Date), last = first.AddDays(Math.Max(1, o.Days) - 1);
            string when = o.AllDay ? Lang.MonthDay(first) + (last > first ? " – " + Lang.MonthDay(last) : "") + " · " + Lang.T("全天") : o.Date + "  " + o.StartTime + "–" + o.EndTime;
            return o.Title + "\n" + when + (String.IsNullOrWhiteSpace(o.Location) ? "" : "\n" + o.Location) + (String.IsNullOrWhiteSpace(o.Notes) ? "" : "\n" + o.Notes);
        }

        // Hover and details text for an Outlook event.
        internal static string OutlookTip(OutlookEvent e)
        {
            string when = e.AllDay
                ? Lang.MonthDay(e.StartLocal) + (e.EndLocal.Date.AddDays(-1) > e.StartLocal.Date ? " – " + Lang.MonthDay(e.EndLocal.Date.AddDays(-1)) : "") + " · " + Lang.T("全天")
                : Lang.MonthDay(e.StartLocal) + " " + Lang.Weekday(e.StartLocal.DayOfWeek) + " " + e.StartLocal.ToString("HH:mm", CultureInfo.InvariantCulture) + "–" + e.EndLocal.ToString("HH:mm", CultureInfo.InvariantCulture);
            return e.Title + "\n" + when + (String.IsNullOrWhiteSpace(e.Location) ? "" : "\n" + e.Location) + (String.IsNullOrWhiteSpace(e.Notes) ? "" : "\n" + (e.Notes.Length > 300 ? e.Notes.Substring(0, 300) + "…" : e.Notes))
                + "\n" + Lang.T("来自 Outlook · 只读，请在 Outlook 中修改");
        }

        public void SetDeadlines(List<DeadlineMark> values) { marks = values ?? new List<DeadlineMark>(); Invalidate(); }
        // DDL marks use the DDL notebook's accent.
        internal static Color DeadlineColor(Color canvas) { return AppearancePainter.Dark(canvas) ? Color.FromArgb(226, 164, 126) : Color.FromArgb(184, 113, 75); }
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
        public int HourPixels { get { return HourHeight; } }
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
            markClicks.Single += delegate(DeadlineMark mark) { if (OpenDeadline != null) OpenDeadline(mark); };
            markClicks.Double += delegate(DeadlineMark mark) { if (EditDeadline != null) EditDeadline(mark); };
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

        private int days = 7;
        public void SetData(DateTime start, DateTime focus, bool month, List<Occurrence> values) { SetData(start, focus, month, values, 7); }
        public void SetData(DateTime start, DateTime focus, bool month, List<Occurrence> values, int dayCount)
        {
            days = Math.Max(1, Math.Min(7, dayCount));
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
            int width = Math.Max(S(100), GridWidth); float col = (width - TimeGutter) / (float)days;
            using (Pen rule = new Pen(ruleColor))
            using (Pen halfRule = new Pen(halfRuleColor))
            using (Font timeFont = new Font("Microsoft YaHei UI", (reference ? 7.5F : 8F) * fontScale))
            {
                for (int day = 0; day < days; day++)
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
            for (int day = 0; day < days; day++)
            {
                string date = startDate.AddDays(day).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                List<Slot> dayItems = items.Where(o => o.Date == date).Select(o => new Slot { Start = Minutes(o.StartTime), End = Minutes(o.EndTime), Item = o })
                    .OrderBy(s => s.Start).ThenBy(s => s.End).ToList();
                int cursor = 0;
                while (cursor < dayItems.Count)
                {
                    List<Slot> cluster = new List<Slot>();
                    int clusterEnd = dayItems[cursor].End;
                    cluster.Add(dayItems[cursor++]);
                    while (cursor < dayItems.Count && dayItems[cursor].Start < clusterEnd)
                    {
                        clusterEnd = Math.Max(clusterEnd, dayItems[cursor].End); cluster.Add(dayItems[cursor++]);
                    }
                    List<int> laneEnds = new List<int>(); List<int> lanes = new List<int>();
                    foreach (Slot item in cluster)
                    {
                        int lane = laneEnds.FindIndex(end => end <= item.Start);
                        if (lane < 0) { lane = laneEnds.Count; laneEnds.Add(0); }
                        laneEnds[lane] = item.End; lanes.Add(lane);
                    }
                    float laneWidth = (col - S(6)) / laneEnds.Count;
                    for (int i = 0; i < cluster.Count; i++)
                    {
                        Slot item = cluster[i];
                        int left = TimeGutter + S(3) + (int)(day * col + lanes[i] * laneWidth), laneW = Math.Max(S(8), (int)laneWidth - S(2));
                        int topY = item.Start * HourHeight / 60;
                        int height = Math.Max(S(18), (item.End - item.Start) * HourHeight / 60 - S(3));
                        Rectangle rect = new Rectangle(left, topY + S(2), laneW, height);
                        DrawEvent(graphics, item.Item, rect, false); hits.Add(new CalendarHit(rect, item.Item));
                    }
                }
            }
            DrawWeekDeadlines(graphics, col);
            if (DateTime.Today >= startDate && DateTime.Today < startDate.AddDays(days))
            {
                int day = (DateTime.Today - startDate).Days; int y = (int)(DateTime.Now.TimeOfDay.TotalMinutes * HourHeight / 60);
                using (Pen now = new Pen(Color.FromArgb(193, 103, 94), S(2))) graphics.DrawLine(now, TimeGutter + day * col, y, TimeGutter + (day + 1) * col, y);
            }
            if (items.Count == 0 && marks.Count == 0 && allDayEvents.Count == 0)
                using (Font font = new Font("Microsoft YaHei UI", 10F * fontScale))
                    TextRenderer.DrawText(graphics, Lang.T("本周还没有日程 · 双击时间格开始安排"), font, new Rectangle(TimeGutter + S(8), 9 * HourHeight + S(15), Math.Max(S(100), width - TimeGutter - S(16)), T(28)), secondary, TextFormatFlags.HorizontalCenter | ScrolledText);
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
                    if (reference && date == DateTime.Today)
                    {
                        // Today: the date number in an accent circle.
                        int size = T(24); Rectangle dot = new Rectangle(x + S(4), y + S(4), size, size);
                        var smoothing = graphics.SmoothingMode; graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        using (Brush b = new SolidBrush(accent)) graphics.FillEllipse(b, dot);
                        graphics.SmoothingMode = smoothing;
                        TextRenderer.DrawText(graphics, date.Day.ToString(), dateFont, dot, canvas, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | ScrolledText);
                    }
                    else
                        TextRenderer.DrawText(graphics, date.Day.ToString() + (!reference && date == DateTime.Today ? Lang.T(" 今天") : ""), dateFont, new Rectangle(x + S(6), y + S(5), (int)col - S(10), T(24)), date.Month == focusDate.Month ? foreground : secondary, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | ScrolledText);
                    List<Slot> dayItems = DaySlots(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    int capacity = Math.Max(1, ((int)row - T(37)) / T(25));
                    int shown = dayItems.Count > capacity ? Math.Max(0, capacity - 1) : dayItems.Count;
                    for (int j = 0; j < shown; j++)
                    {
                        Rectangle rect = new Rectangle(x + S(3), y + T(32) + j * T(25), (int)col - S(6), T(22));
                        if (dayItems[j].Mark != null) { DrawDeadline(graphics, dayItems[j].Mark, rect, true); hits.Add(new CalendarHit(rect, dayItems[j].Mark)); }
                        else { DrawEvent(graphics, dayItems[j].Item, rect, true); hits.Add(new CalendarHit(rect, dayItems[j].Item)); }
                    }
                    if (shown < dayItems.Count)
                    {
                        Rectangle rect = new Rectangle(x + S(4), y + T(32) + shown * T(25), (int)col - S(8), T(24));
                        TextRenderer.DrawText(graphics, Lang.T("+ {0} 项 · 查看", dayItems.Count - shown), smallFont, rect, foreground, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | ScrolledText);
                        hits.Add(new CalendarHit(rect, date));
                    }
                }
            }
        }

        private sealed class Slot { public int Start, End; public Occurrence Item; public DeadlineMark Mark; }

        // Courses and deadlines of one day in time order; a date-only deadline counts as the end of the day.
        private List<Slot> DaySlots(string date)
        {
            DateTime day = TimeUtil.ParseDate(date);
            return allDayEvents.Where(e => CalendarForm.FirstDay(e) <= day && CalendarForm.AfterLastDay(e) > day)
                .Select(e => new Slot { Start = -1, End = -1, Item = e })
                .Concat(items.Where(o => o.Date == date).Select(o => new Slot { Start = Minutes(o.StartTime), End = Minutes(o.EndTime), Item = o }))
                .Concat(marks.Where(m => m.Date == date).Select(m => new Slot { Start = m.Minutes, End = m.Minutes, Mark = m }))
                .OrderBy(s => s.Start).ThenBy(s => s.Mark == null ? 0 : 1).ToList();
        }

        // Timed deadlines lie on top of the courses as one-line tags whose bottom edge is the due time.
        // Courses keep their full width; only tags that would touch each other share the column.
        private void DrawWeekDeadlines(Graphics graphics, float col)
        {
            int tagHeight;
            using (Font font = new Font("Microsoft YaHei UI", 8.5F * fontScale, FontStyle.Bold)) tagHeight = Math.Max(S(20), font.Height + S(8));
            for (int day = 0; day < days; day++)
            {
                string date = startDate.AddDays(day).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var dayMarks = marks.Where(m => m.Date == date && !m.DateOnly).OrderBy(m => m.Minutes).ToList();
                int cursor = 0;
                while (cursor < dayMarks.Count)
                {
                    var group = new List<DeadlineMark> { dayMarks[cursor++] };
                    while (cursor < dayMarks.Count && dayMarks[cursor].Minutes * HourHeight / 60 - tagHeight < group[group.Count - 1].Minutes * HourHeight / 60) group.Add(dayMarks[cursor++]);
                    // Tags that would touch become one "⚑ 3 项截止" tag at the earliest due time; a click lists them.
                    int bottom = Math.Max(tagHeight, group[0].Minutes * HourHeight / 60);
                    Rectangle tag = new Rectangle(TimeGutter + S(3) + (int)(day * col), bottom - tagHeight, Math.Max(S(8), (int)col - S(8)), tagHeight);
                    if (group.Count == 1) { DrawDeadline(graphics, group[0], tag, false); hits.Add(new CalendarHit(tag, group[0])); continue; }
                    DrawDeadline(graphics, new DeadlineMark { Text = Lang.T("{0} 项截止", group.Count), Date = date, Time = group[0].Time }, tag, false);
                    hits.Add(new CalendarHit(tag, group));
                }
            }
        }

        // Hover text for several deadlines at once.
        internal static string DeadlineList(List<DeadlineMark> group)
        {
            return String.Join("\n", group.Select(m => "⚑ " + DeadlineCaption(m, true) + (m.Archived ? Lang.T("（已归档）") : ""))) + "\n" + Lang.T("单击选择要查看的任务");
        }

        internal static string DeadlineCaption(DeadlineMark mark, bool withTime) { return (withTime && !mark.DateOnly ? mark.Time + " " : "") + mark.Text; }

        // A deadline tag: outlined in the DDL color with ⚑ in front; in the week view a firmer bottom edge marks the due time.
        internal void DrawDeadline(Graphics graphics, DeadlineMark mark, Rectangle rect, bool compact)
        {
            PaintDeadline(graphics, mark, rect, compact, canvas, foreground, reference, dpiScale, fontScale, ScrolledText);
        }
        internal static void PaintDeadline(Graphics graphics, DeadlineMark mark, Rectangle rect, bool compact, Color canvas, Color foreground, bool rounded, float dpi, float fontScale, TextFormatFlags extra)
        {
            Func<int, int> s = v => (int)Math.Round(v * dpi, MidpointRounding.AwayFromZero);
            Color line = DeadlineColor(canvas), fill = Blend(canvas, line, AppearancePainter.Dark(canvas) ? .2F : .09F);
            var smoothing = graphics.SmoothingMode; graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Rectangle box = new Rectangle(rect.X, rect.Y, Math.Max(1, rect.Width - 1), Math.Max(1, rect.Height - 1));
            using (var path = Rounded(box, rounded ? s(4) : s(2)))
            using (Brush brush = new SolidBrush(fill))
            using (Pen pen = new Pen(Blend(canvas, line, .7F)))
            {
                graphics.FillPath(brush, path); graphics.DrawPath(pen, path);
                if (!compact)
                {
                    var clip = graphics.Clip; graphics.SetClip(path, System.Drawing.Drawing2D.CombineMode.Intersect);
                    using (Brush edge = new SolidBrush(line)) graphics.FillRectangle(edge, rect.X, rect.Bottom - s(2), rect.Width, s(2));
                    graphics.Clip = clip;
                }
            }
            graphics.SmoothingMode = smoothing;
            using (Font font = new Font("Microsoft YaHei UI", (compact ? 8F : 8.5F) * fontScale, FontStyle.Bold))
            {
                TextFormatFlags flags = TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | extra;
                Rectangle inner = new Rectangle(rect.X + s(5), rect.Y, Math.Max(1, rect.Width - s(8)), rect.Height - (compact ? 0 : s(2)));
                int flag = TextRenderer.MeasureText(graphics, "⚑", font, Size.Empty, flags).Width + s(3);
                TextRenderer.DrawText(graphics, "⚑", font, new Rectangle(inner.X, inner.Y, flag, inner.Height), line, flags);
                TextRenderer.DrawText(graphics, DeadlineCaption(mark, compact), font, new Rectangle(inner.X + flag, inner.Y, Math.Max(1, inner.Width - flag), inner.Height), foreground, flags | TextFormatFlags.EndEllipsis);
            }
        }

        // Hover text for a deadline: what, when, and which page it is on.
        internal static string DeadlineTip(DeadlineMark mark)
        {
            DateTime date = TimeUtil.ParseDate(mark.Date);
            string when = Lang.MonthDay(date) + " " + Lang.Weekday(date.DayOfWeek) + (mark.DateOnly ? "" : " " + mark.Time);
            string page = String.IsNullOrWhiteSpace(mark.PageTitle) ? Lang.T("未命名页") : mark.PageTitle;
            return mark.Text + "\n" + Lang.T("截止：{0}", when) + "\n" + Lang.T("页面：{0}", page) + (mark.Archived ? Lang.T("（已归档）") : "")
                + "\n" + (mark.Archived ? Lang.T("单击或双击：编辑任务") : Lang.T("单击：在 DDL 便签中查看 · 双击：编辑"));
        }

        private void DrawEvent(Graphics graphics, Occurrence item, Rectangle rect, bool compact)
        {
            Color color = EventColor(item.Color);
            if (reference)
            {
                // Rounded block with the color bar along its left edge.
                var smoothing = graphics.SmoothingMode; graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var path = Rounded(rect, S(5)))
                {
                    using (Brush fill = new SolidBrush(Tint(color))) graphics.FillPath(fill, path);
                    var clip = graphics.Clip; graphics.SetClip(path, System.Drawing.Drawing2D.CombineMode.Intersect);
                    using (Brush stripe = new SolidBrush(color)) graphics.FillRectangle(stripe, rect.X, rect.Y, Math.Min(S(3), rect.Width), rect.Height);
                    graphics.Clip = clip;
                }
                graphics.SmoothingMode = smoothing;
            }
            else
            {
                using (Brush fill = new SolidBrush(Tint(color))) graphics.FillRectangle(fill, rect);
                using (Brush stripe = new SolidBrush(color)) graphics.FillRectangle(stripe, rect.X, rect.Y, Math.Min(S(3), rect.Width), rect.Height);
            }
            Rectangle inner = new Rectangle(rect.X + S(5), rect.Y + S(3), Math.Max(1, rect.Width - S(9)), Math.Max(1, rect.Height - S(6)));
            using (Font titleFont = new Font("Microsoft YaHei UI", (compact ? 8F : 9F) * fontScale, FontStyle.Bold))
            using (Font detailFont = new Font("Microsoft YaHei UI", 8F * fontScale))
            {
                string title = compact && !item.AllDay && item.StartTime != "" ? item.StartTime + " " + item.Title : item.Title;
                if (compact)
                {
                    TextRenderer.DrawText(graphics, title, titleFont, inner, foreground, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | ScrolledText);
                    return;
                }
                // Week view: the whole title, wrapped over as many lines as it needs (long words break too),
                // then the time and the place underneath while there is room. Nothing is cut to "…";
                // text stops at the last whole line that fits the block. The time is shown only on one line.
                const TextFormatFlags wrap = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | ScrolledText;
                const TextFormatFlags line = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | ScrolledText;
                int y = inner.Y;
                string time = item.StartTime + "–" + item.EndTime;
                foreach (var part in new[] { new KeyValuePair<string, Font>(item.Title, titleFont), new KeyValuePair<string, Font>(time, detailFont), new KeyValuePair<string, Font>(item.Location ?? "", detailFont) })
                {
                    if (String.IsNullOrWhiteSpace(part.Key)) continue;
                    int lineHeight = part.Value.Height, room = (inner.Bottom - y) / lineHeight * lineHeight;
                    if (room <= 0) break;
                    if (part.Key == time)
                    {
                        if (TextRenderer.MeasureText(graphics, time, part.Value, Size.Empty, line).Width > inner.Width) continue;
                        TextRenderer.DrawText(graphics, time, part.Value, new Rectangle(inner.X, y, inner.Width, lineHeight), foreground, line);
                        y += lineHeight + S(2); continue;
                    }
                    int height = Math.Min(room, TextRenderer.MeasureText(graphics, part.Key, part.Value, new Size(inner.Width, Int32.MaxValue), wrap).Height);
                    TextRenderer.DrawText(graphics, part.Key, part.Value, new Rectangle(inner.X, y, inner.Width, height), foreground, wrap);
                    y += height + S(2);
                }
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
            int day = Math.Min(days - 1, Math.Max(0, (int)((point.X - TimeGutter) / ((Math.Max(S(100), GridWidth) - TimeGutter) / (float)days))));
            return startDate.AddDays(day);
        }

        private void HandleClick(object sender, MouseEventArgs e)
        {
            Point point = ContentPoint(e.Location); CalendarHit hit = hits.LastOrDefault(h => h.Bounds.Contains(point));
            if (hit != null)
            {
                if (hit.Group != null) { if (ChooseDeadline != null) ChooseDeadline(hit.Group); }
                else if (hit.Mark != null) markClicks.Click(hit.Mark);
                else if (hit.Item != null && OpenOccurrence != null) OpenOccurrence(hit.Item);
                else if (hit.Item == null) ShowDay(hit.Date);
                return;
            }
            if (SelectDate != null) SelectDate(DateAt(point));
        }

        private void HandleDoubleClick(object sender, MouseEventArgs e)
        {
            Point point = ContentPoint(e.Location); CalendarHit hit = hits.LastOrDefault(h => h.Bounds.Contains(point));
            if (hit != null) { if (hit.Mark != null) markClicks.DoubleClick(hit.Mark); return; }
            if (CreateEvent != null) CreateEvent(DateAt(point), monthView ? 9 : Math.Max(0, Math.Min(22, point.Y / HourHeight)));
        }

        private void HandleMove(object sender, MouseEventArgs e)
        {
            Point point = ContentPoint(e.Location); CalendarHit hit = hits.LastOrDefault(h => h.Bounds.Contains(point));
            Cursor = hit == null ? Cursors.Default : Cursors.Hand;
            string next = hit != null && hit.Group != null ? DeadlineList(hit.Group) : hit != null && hit.Mark != null ? DeadlineTip(hit.Mark) : hit == null || hit.Item == null ? "" : OccurrenceTip(hit.Item);
            if (next != tip) { tip = next; tooltip.SetToolTip(this, tip); }
        }

        private void ShowDay(DateTime date)
        {
            using (Form dialog = new Form())
            {
                dialog.SuspendLayout(); dialog.AutoScaleDimensions = new SizeF(96F, 96F); dialog.AutoScaleMode = AutoScaleMode.Dpi;
                dialog.Text = Lang.MonthDay(date) + " · " + Lang.T("全部日程"); dialog.Size = new Size(410, 420);
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
                foreach (DeadlineMark mark in marks.Where(m => m.Date == date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)))
                {
                    DeadlineMark captured = mark; Button button = new Button(); button.Text = "⚑ " + (mark.DateOnly ? Lang.T("当天截止") : Lang.T("{0} 截止", mark.Time)) + "\n" + mark.Text;
                    button.Size = new Size(345, 65); button.TextAlign = ContentAlignment.MiddleLeft; button.UseMnemonic = false;
                    button.FlatStyle = FlatStyle.Flat; button.BackColor = Blend(Color.White, DeadlineColor(Color.White), .12F); button.FlatAppearance.BorderSize = 0;
                    button.Click += delegate { dialog.Close(); if (OpenDeadline != null) OpenDeadline(captured); };
                    list.Controls.Add(button);
                }
                dialog.ResumeLayout(true);
                dialog.ShowDialog(FindForm());
            }
        }

        protected override void Dispose(bool disposing) { if (disposing) { tooltip.Dispose(); markClicks.Dispose(); } base.Dispose(disposing); }
        private sealed class CalendarHit
        {
            public Rectangle Bounds; public Occurrence Item; public DateTime Date; public DeadlineMark Mark;
            public List<DeadlineMark> Group;
            public CalendarHit(Rectangle bounds, DeadlineMark mark) { Bounds = bounds; Mark = mark; }
            public CalendarHit(Rectangle bounds, List<DeadlineMark> group) { Bounds = bounds; Group = group; }
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
            Text = Lang.T("循环课程"); Size = new Size(470, 208); FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent; MaximizeBox = false; MinimizeBox = false;
            Font = new Font("Microsoft YaHei UI", 10F); BackColor = Color.FromArgb(250, 250, 247);
            Label heading = new Label { Text = Lang.T("要修改或删除哪一部分？"), AutoSize = true, Location = new Point(22, 22), Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold) };
            Label detail = new Label { Text = Lang.T("“仅这一次”保留其他日期的课程安排。"), AutoSize = true, Location = new Point(22, 60), ForeColor = Color.FromArgb(113, 126, 117) };
            Controls.Add(heading); Controls.Add(detail);
            Button single = new Button { Text = Lang.T("仅这一次"), Location = new Point(22, 111), Size = new Size(125, 34) };
            Button all = new Button { Text = Lang.T("整个系列"), Location = new Point(159, 111), Size = new Size(125, 34) };
            Button cancel = new Button { Text = Lang.T("取消"), Location = new Point(296, 111), Size = new Size(125, 34), DialogResult = DialogResult.Cancel };
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
        private CheckBox allDayBox; private NumericUpDown daysBox; private Label daysUnit, toLabel; private Panel timeRow;
        private Label error;
        private readonly string[] palette = { "#6C9385", "#7196B1", "#A68EB5", "#C49472", "#BB818B", "#939C63" };
        private string keptColor;

        public CalendarEditor(CalendarEvent item, bool allowSeries, bool existing)
        {
            SuspendLayout(); AutoScaleDimensions = new SizeF(96F, 96F); AutoScaleMode = AutoScaleMode.Dpi;
            source = item; seriesMode = allowSeries;
            Text = existing ? (allowSeries ? Lang.T("编辑日程") : Lang.T("编辑 · 仅这一次")) : Lang.T("添加日程");
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
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Lang.IsEnglish ? 122 : 93)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            scroll.Controls.Add(table);
            titleBox = new TextBox(); titleBox.MaxLength = 180; AddRow(table, Lang.T("名称"), titleBox, 42);
            datePicker = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = Lang.T("yyyy 年 MM 月 dd 日") };
            AddRow(table, Lang.T("日期 / 起日"), datePicker, 42);
            // 全天: the times give way to a number of days. The row places its controls itself (left to right,
            // vertically centred) so switching between the two always lays them out again.
            timeRow = new Panel { Dock = DockStyle.Fill };
            Panel times = timeRow;
            allDayBox = new CheckBox { Name = "event-all-day", Text = Lang.T("全天"), AutoSize = true, Margin = new Padding(0, 4, 12, 0) };
            startPicker = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 100 };
            endPicker = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 100 };
            toLabel = new Label { Text = Lang.T("至"), Width = 28, Height = 26, TextAlign = ContentAlignment.MiddleCenter };
            daysBox = new NumericUpDown { Name = "event-days", Minimum = 1, Maximum = 366, Value = 1, Width = 70 };
            daysUnit = new Label { Text = Lang.T("天"), AutoSize = true, Margin = new Padding(4, 6, 0, 0) };
            times.Controls.Add(allDayBox); times.Controls.Add(startPicker); times.Controls.Add(toLabel); times.Controls.Add(endPicker); times.Controls.Add(daysBox); times.Controls.Add(daysUnit);
            allDayBox.CheckedChanged += delegate { ShowAllDay(); };
            timeRow.Resize += delegate { ArrangeTimeRow(); };
            AddRow(table, Lang.T("时间"), times, 42);
            times.Margin = Padding.Empty;
            locationBox = new TextBox(); locationBox.MaxLength = 300; AddRow(table, Lang.T("地点"), locationBox, 42);
            notesBox = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, MaxLength = 10000 }; AddRow(table, Lang.T("备注"), notesBox, 78);
            colorBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            colorBox.Items.AddRange(new object[] { Lang.T("鼠尾草绿"), Lang.T("雾蓝"), Lang.T("浅紫"), Lang.T("杏茶"), Lang.T("玫瑰"), Lang.T("橄榄") }); AddRow(table, Lang.T("颜色"), colorBox, 42);
            repeatBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Enabled = seriesMode };
            repeatBox.Items.AddRange(new object[] { Lang.T("不重复"), Lang.T("每周"), Lang.T("每两周") }); AddRow(table, Lang.T("重复"), repeatBox, 42);
            weekdays = new CheckedListBox { CheckOnClick = true, MultiColumn = true, ColumnWidth = 54, Height = 47, BorderStyle = BorderStyle.None, BackColor = BackColor, IntegralHeight = false };
            weekdays.Items.AddRange(new object[] { Lang.T("周一"), Lang.T("周二"), Lang.T("周三"), Lang.T("周四"), Lang.T("周五"), Lang.T("周六"), Lang.T("周日") }); AddRow(table, Lang.T("每逢"), weekdays, 54);
            untilPicker = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = Lang.T("yyyy 年 MM 月 dd 日") }; AddRow(table, Lang.T("循环截止"), untilPicker, 42);
            anchorPicker = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = Lang.T("yyyy 年 MM 月 dd 日"), ShowCheckBox = true };
            AddRow(table, Lang.T("双周基准日"), anchorPicker, 42);
            ToolTip anchorHint = new ToolTip();
            anchorHint.SetToolTip(anchorPicker, Lang.T("勾选：以此日期所在周作为每两周循环的第 1 周。取消勾选：按课程起日所在周循环。已有课程不会跟随全局教学周设置改动。\n要从教学第 2 周上课，请将此日期设为第 2 周内任一天。"));
            Disposed += delegate { anchorHint.Dispose(); };
            Label timezone = new Label { AutoSize = false, Text = Lang.T("按本地上课时间循环（含夏令时）。时区：\n") + TimeZoneLabel(source.TimeZoneId), ForeColor = Color.FromArgb(113, 124, 117) };
            AddRow(table, Lang.T("时区"), timezone, 68);
            repeatBox.SelectedIndexChanged += delegate { bool enabled = seriesMode && repeatBox.SelectedIndex > 0; weekdays.Enabled = enabled; untilPicker.Enabled = enabled; anchorPicker.Enabled = seriesMode && repeatBox.SelectedIndex == 2; };

            TableLayoutPanel footer = new TableLayoutPanel(); footer.Dock = DockStyle.Fill; footer.RowCount = 2; footer.ColumnCount = 1; footer.Padding = new Padding(20, 0, 20, 12);
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); outer.Controls.Add(footer, 0, 1);
            error = new Label { Dock = DockStyle.Fill, ForeColor = Color.FromArgb(171, 69, 67), TextAlign = ContentAlignment.MiddleLeft }; footer.Controls.Add(error, 0, 0);
            FlowLayoutPanel buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            Button save = new Button { Text = Lang.T("保存日程"), Width = 109, Height = 34, BackColor = Color.FromArgb(72, 112, 98), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            Button cancel = new Button { Text = Lang.T("取消"), Width = 83, Height = 34, DialogResult = DialogResult.Cancel };
            buttons.Controls.Add(save); buttons.Controls.Add(cancel); save.Click += SaveEditor; CancelButton = cancel;
            if (existing)
            {
                Button delete = new Button { Text = seriesMode ? Lang.T("删除日程") : Lang.T("取消这次课程"), Width = 125, Height = 34, ForeColor = Color.FromArgb(170, 76, 71) };
                delete.Click += delegate {
                    string text = seriesMode && source.RepeatWeeks > 0 ? Lang.T("确定删除整个循环系列及其例外安排？") : Lang.T("确定删除这次日程？");
                    if (MessageBox.Show(this, text, Lang.T("删除确认"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    { DeleteRequested = true; DialogResult = DialogResult.OK; }
                };
                buttons.Controls.Add(delete);
            }
            footer.Controls.Add(buttons, 0, 1);
        }

        private void ShowAllDay()
        {
            bool whole = allDayBox.Checked;
            startPicker.Visible = toLabel.Visible = endPicker.Visible = !whole;
            daysBox.Visible = daysUnit.Visible = whole;
            // A single changed occurrence keeps the series' length.
            daysBox.Enabled = seriesMode;
            ArrangeTimeRow();
        }
        private void ArrangeTimeRow()
        {
            if (timeRow == null) return;
            float scale = DeviceScale();
            int x = 0, gap = (int)Math.Round(6 * scale), field = startPicker.PreferredSize.Height;
            // Sizes are set here, from the screen scale, so they are never scaled twice.
            var sizes = new Dictionary<Control, Size> {
                { allDayBox, allDayBox.PreferredSize }, { startPicker, new Size((int)(100 * scale), field) }, { endPicker, new Size((int)(100 * scale), field) },
                { toLabel, new Size((int)(26 * scale), field) }, { daysBox, new Size((int)(64 * scale), daysBox.PreferredSize.Height) }, { daysUnit, daysUnit.PreferredSize } };
            // Which controls show follows the checkbox (Visible reads false until the window is on screen).
            foreach (Control c in allDayBox.Checked ? new Control[] { allDayBox, daysBox, daysUnit } : new Control[] { allDayBox, startPicker, toLabel, endPicker })
            {
                Size size = sizes[c];
                c.SetBounds(x, Math.Max(0, (timeRow.ClientSize.Height - size.Height) / 2), size.Width, size.Height);
                x += size.Width + gap;
            }
        }
        private float DeviceScale() { using (var g = CreateGraphics()) return g.DpiX / 96F; }

        private static string TimeZoneLabel(string id)
        {
            try
            {
                var zone = TimeZoneInfo.FindSystemTimeZoneById(String.IsNullOrWhiteSpace(id) ? TimeZoneInfo.Local.Id : id);
                // Windows names zones in its own display language; the English interface shows the zone's English ID instead.
                if (!Lang.IsEnglish) return zone.DisplayName;
                TimeSpan offset = zone.BaseUtcOffset;
                return "(UTC" + (offset < TimeSpan.Zero ? "-" : "+") + offset.ToString(@"hh\:mm") + ") " + zone.Id;
            }
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
            allDayBox.Checked = source.AllDay; daysBox.Value = Math.Max(1, Math.Min(366, source.Days)); ShowAllDay();
            int known = Array.IndexOf(palette, source.Color);
            if (known < 0 && !String.IsNullOrEmpty(source.Color)) { colorBox.Items.Add(Lang.T("原来的颜色")); keptColor = source.Color; known = colorBox.Items.Count - 1; }
            colorBox.SelectedIndex = Math.Max(0, known);
            repeatBox.SelectedIndex = Math.Max(0, Math.Min(2, source.RepeatWeeks));
            for (int i = 0; i < 7; i++) weekdays.SetItemChecked(i, source.WeekDays != null && source.WeekDays.Contains((i + 1) % 7));
            if (weekdays.CheckedItems.Count == 0) weekdays.SetItemChecked(((int)date.DayOfWeek + 6) % 7, true);
        }

        private void SaveEditor(object sender, EventArgs args)
        {
            if (String.IsNullOrWhiteSpace(titleBox.Text)) { error.Text = Lang.T("请填写日程名称。"); titleBox.Focus(); return; }
            TimeSpan start = new TimeSpan(startPicker.Value.Hour, startPicker.Value.Minute, 0);
            TimeSpan end = new TimeSpan(endPicker.Value.Hour, endPicker.Value.Minute, 0);
            bool whole = allDayBox.Checked;
            if (!whole && end <= start) { error.Text = Lang.T("结束时间须晚于开始时间；跨天日程请分成两条，或勾选「全天」。"); return; }
            int repeat = seriesMode ? repeatBox.SelectedIndex : 0;
            if (repeat > 0 && untilPicker.Value.Date < datePicker.Value.Date) { error.Text = Lang.T("循环截止日期不能早于开始日期。"); return; }
            if (repeat > 0 && (untilPicker.Value.Date - datePicker.Value.Date).TotalDays > 36600) { error.Text = Lang.T("循环跨度不能超过 100 年。"); return; }
            if (repeat > 0 && weekdays.CheckedItems.Count == 0) { error.Text = Lang.T("请至少选择一个重复星期。"); return; }
            List<int> days = new List<int>(); foreach (int index in weekdays.CheckedIndices) days.Add((index + 1) % 7);
            Result = new CalendarEvent {
                Id = source.Id, Title = titleBox.Text.Trim(), Date = datePicker.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                StartTime = whole ? "00:00" : startPicker.Value.ToString("HH:mm", CultureInfo.InvariantCulture), EndTime = whole ? "23:59" : endPicker.Value.ToString("HH:mm", CultureInfo.InvariantCulture),
                AllDay = whole, Days = whole ? (int)daysBox.Value : 1,
                Location = locationBox.Text.Trim(), Notes = notesBox.Text, Color = colorBox.SelectedIndex >= palette.Length && keptColor != null ? keptColor : palette[Math.Max(0, Math.Min(palette.Length - 1, colorBox.SelectedIndex))],
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
