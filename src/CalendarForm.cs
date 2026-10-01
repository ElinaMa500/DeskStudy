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
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 312));
            FlowLayoutPanel navigation = new FlowLayoutPanel(); navigation.Dock = DockStyle.Fill; navigation.WrapContents = false;
            navigation.Padding = new Padding(0, 5, 0, 0);
            navigation.Controls.Add(MakeButton("‹", 32, delegate { MovePeriod(-1); }));
            navigation.Controls.Add(MakeButton("今天", 59, delegate { focusDate = DateTime.Today; RefreshData(); }));
            navigation.Controls.Add(MakeButton("›", 32, delegate { MovePeriod(1); }));
            toolbar.Controls.Add(navigation, 0, 0);
            period = new Label(); period.Dock = DockStyle.Fill; period.TextAlign = ContentAlignment.MiddleLeft;
            period.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold); period.ForeColor = ink; period.AutoEllipsis = true;
            period.Resize += delegate { FitPeriod(); };
            toolbar.Controls.Add(period, 1, 0);
            FlowLayoutPanel actions = new FlowLayoutPanel(); actions.Dock = DockStyle.Fill; actions.WrapContents = false;
            actions.Padding = new Padding(0, 5, 0, 0);
            workWeekButton = MakeButton("工作周", 72, delegate { SetView(false, true); });
            weekButton = MakeButton("周", 42, delegate { SetView(false, false); });
            monthButton = MakeButton("月", 42, delegate { SetView(true, false); });
            workWeekButton.Name = "calendar-original-workweek"; weekButton.Name = "calendar-original-week"; monthButton.Name = "calendar-original-month";
            actions.Controls.Add(workWeekButton); actions.Controls.Add(weekButton); actions.Controls.Add(monthButton);
            Button add = MakeButton("＋ 添加日程", 125, delegate { AddEvent(focusDate, 9); });
            add.BackColor = Color.FromArgb(73, 111, 101); add.ForeColor = Color.White;
            actions.Controls.Add(add); toolbar.Controls.Add(actions, 2, 0);
            Panel topRow = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            topRow.Controls.Add(toolbar);
            BuildReferenceBar(); topRow.Controls.Add(refBar);
            layout.Controls.Add(topRow, 0, 0);

            hint = new Label(); hint.Dock = DockStyle.Fill;
            hint.Text = "每一天，留一点从容   ·   点击课程编辑；双击空白处添加";
            hint.Font = new Font("Microsoft YaHei UI", 9F); hint.ForeColor = muted;
            hint.TextAlign = ContentAlignment.MiddleLeft; hint.Padding = new Padding(6, 0, 0, 0);
            layout.Controls.Add(hint, 0, 1);
            dayHeader = new Panel(); dayHeader.Dock = DockStyle.Fill; dayHeader.Margin = Padding.Empty; dayHeader.Paint += PaintHeader;
            layout.Controls.Add(dayHeader, 0, 2);
            surface = new CalendarSurface(); surface.Dock = DockStyle.Fill;
            surface.OpenOccurrence += EditEvent;
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

        private void MovePeriod(int amount)
        {
            focusDate = monthView ? focusDate.AddMonths(amount) : focusDate.AddDays(amount * 7);
            RefreshData();
        }

        public void RefreshData()
        {
            if (IsDisposed || surface == null) return;
            string defaultView = App.Data.Settings.Calendar.DefaultView + (App.Data.Settings.Calendar.WorkWeek ? "|work" : "");
            if (lastDefaultView != defaultView) { monthView = App.Data.Settings.Calendar.DefaultView == "Month"; workWeek = !monthView && App.Data.Settings.Calendar.WorkWeek; lastDefaultView = defaultView; }
            DateTime start = monthView ? WeekStart(new DateTime(focusDate.Year, focusDate.Month, 1)) : ViewStart();
            DateTime end = start.AddDays(monthView ? 41 : ViewDays - 1);
            occurrences = CalendarEngine.GetOccurrences(App.Data, start, end).ToList();
            periodFull = monthView ? focusDate.ToString("yyyy 年 M 月") : start.ToString("M.d") + " – " + end.ToString("M.d") + "  ·  " + focusDate.Year;
            periodShort = monthView ? periodFull : start.ToString("M.d") + " – " + end.ToString("M.d");
            FitPeriod();
            int teachingWeek = SettingsLogic.TeachingWeek(App.Data.Settings, focusDate);
            hint.Text = (teachingWeek > 0 ? "教学第 " + teachingWeek + " 周   ·   " : "") + "点击课程编辑；双击空白处添加";
            refRange.Text = monthView ? focusDate.ToString("yyyy 年 M 月") : start.ToString("M.d") + " – " + end.ToString("M.d");
            refMeta.Text = monthView ? (teachingWeek > 0 ? "教学第 " + teachingWeek + " 周" : "") : focusDate.Year + (teachingWeek > 0 ? " · 教学第 " + teachingWeek + " 周" : "");
            ArrangeReferenceBar();
            surface.SetData(start, focusDate, monthView, occurrences, ViewDays);
            ApplyAppearance();
            dayHeader.Invalidate();
        }

        private static string StyleFor(string notebookLayout)
        {
            return notebookLayout == "Card" || notebookLayout == "Clean" ? "Clean" : notebookLayout == "Paper" || notebookLayout == "Journal" ? "Journal" : "Original";
        }
        private bool IsReference { get { return calendarStyle != "Original"; } }
        protected override bool SlimReferenceHeader { get { return true; } }
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
                contentLayout.RowStyles[2].Height = Math.Max(Px(45), TextRenderer.MeasureText(monthView ? "周一" : "周一\n9/28", headerFont).Height + Px(10));
        }

        // 清爽 / 纸页: the same header as the reference notebooks, one navigation row, a one-line day header.
        private void StyleReference(AppearanceOptions appearance)
        {
            bool journal = calendarStyle == "Journal";
            palette = CalendarPalette.For(AppearancePainter.Background(appearance), App.Data.Settings.NotebookLayout);
            CalendarPalette p = palette;
            SetCompactNotebookHeader(true, 8F);
            SetReferenceHeader(true, "", p.Back, p.Ink, journal ? p.Ink : p.Sub, p.Accent, p.Accent, p.Soft, p.Rule, journal);
            surface.SetReference(true, p);
            toolbar.Visible = false; refBar.Visible = true; hint.Visible = false;
            refBar.BackColor = p.Back; refBar.Parent.BackColor = p.Back; dayHeader.BackColor = p.Back; contentLayout.BackColor = p.Back; Body.BackColor = p.Back;
            refRange.Font = RefFont(10.5F, FontStyle.Regular); refRange.ForeColor = p.Ink; refRange.BackColor = p.Back;
            refMeta.Font = RefFont(8F, FontStyle.Regular); refMeta.ForeColor = p.Sub; refMeta.BackColor = p.Back;
            foreach (Button b in new[] { refPrev, refNext, refToday, refWorkWeek, refWeek, refMonth, refAdd })
            {
                b.BackColor = p.Back; b.ForeColor = p.Sub; b.Font = RefFont(8F, FontStyle.Regular);
                ((CalendarBarButton)b).HoverColor = p.Soft;
            }
            refPrev.Font = refNext.Font = RefFont(11F, FontStyle.Regular);
            refAdd.Font = RefFont(10F, FontStyle.Regular); refAdd.ForeColor = p.Accent;
            // The ‹ › glyphs sit low in their line box.
            ((CalendarBarButton)refPrev).TextOffset = ((CalendarBarButton)refNext).TextOffset = -Px(2);
            Button selected = monthView ? refMonth : workWeek ? refWorkWeek : refWeek;
            selected.BackColor = p.Soft; selected.ForeColor = p.Ink; selected.Font = RefFont(8F, FontStyle.Bold);
            SetExpandedMinimumSize(new Size(ExpandedMinimumSize.Width, Px(420)));
            if (!contentReady) return;
            Body.Padding = new Padding(0, 0, 0, Px(2));
            contentLayout.Padding = new Padding(Px(16), Px(2), Px(14), Px(8));
            contentLayout.RowStyles[0].Height = Math.Max(Px(34), refRange.Font.Height + Px(12));
            contentLayout.RowStyles[1].Height = 0;
            contentLayout.RowStyles[2].Height = Math.Max(Px(32), RefFont(9F, FontStyle.Bold).Height + Px(14));
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
            refPrev = RefButton("‹", "calendar-previous", "上一周或上一月", delegate { MovePeriod(-1); });
            refNext = RefButton("›", "calendar-next", "下一周或下一月", delegate { MovePeriod(1); });
            refToday = RefButton("今天", "calendar-today", "回到今天", delegate { focusDate = DateTime.Today; RefreshData(); });
            refWorkWeek = RefButton("工作周", "calendar-workweek", "工作周视图：只看周一至周五", delegate { SetView(false, true); });
            refWeek = RefButton("周", "calendar-week", "周视图", delegate { SetView(false, false); });
            refMonth = RefButton("月", "calendar-month", "月视图", delegate { SetView(true, false); });
            refAdd = RefButton("＋", "calendar-add", "添加日程", delegate { AddEvent(focusDate, 9); });
            refRange = new Label { Name = "calendar-range", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Tag = "appearance-custom-font", UseMnemonic = false };
            refMeta = new Label { Name = "calendar-meta", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Tag = "appearance-custom-font", UseMnemonic = false, AutoEllipsis = true };
            refBar.Controls.AddRange(new Control[] { refPrev, refRange, refNext, refMeta, refToday, refWorkWeek, refWeek, refMonth, refAdd });
            refBar.Resize += delegate { ArrangeReferenceBar(); };
            var tips = new ToolTip(); tips.SetToolTip(refWorkWeek, "只看周一至周五"); Disposed += delegate { tips.Dispose(); };
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
            int segment = Math.Max(Px(28), TextRenderer.MeasureText("月", refMonth.Font).Width + Px(14));
            refMonth.SetBounds(refAdd.Left - Px(8) - segment, y, segment, bh);
            refWeek.SetBounds(refMonth.Left - segment, y, segment, bh);
            int workSegment = TextRenderer.MeasureText("工作周", refWorkWeek.Font).Width + Px(14);
            refWorkWeek.SetBounds(refWeek.Left - workSegment, y, workSegment, bh);
            int today = TextRenderer.MeasureText("今天", refToday.Font).Width + Px(14);
            refToday.SetBounds(refWorkWeek.Left - Px(8) - today, y, today, bh);
            int metaLeft = refNext.Right + Px(8);
            refMeta.SetBounds(metaLeft, 0, Math.Max(0, refToday.Left - Px(8) - metaLeft), h);
        }

        private void PaintHeader(object sender, PaintEventArgs e)
        {
            if (IsReference) { PaintReferenceHeader(e.Graphics); return; }
            AppearanceOptions appearance = SettingsLogic.EffectiveAppearance(App.Data, "calendar");
            Color foreground = AppearancePainter.Foreground(appearance);
            bool dark = AppearancePainter.Dark(AppearancePainter.Background(appearance));
            e.Graphics.Clear(AppearancePainter.Background(appearance));
            string[] weekdays = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
            int left = monthView ? 0 : surface.TimeGutter;
            int available = surface == null ? dayHeader.Width : surface.GridWidth;
            int count = monthView ? 7 : ViewDays;
            float width = (available - left) / (float)count;
            DateTime start = monthView ? WeekStart(focusDate) : ViewStart();
            for (int i = 0; i < count; i++)
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

        // One line per day, "一 28"; today sits in a soft accent pill.
        private void PaintReferenceHeader(Graphics graphics)
        {
            CalendarPalette p = palette;
            graphics.Clear(p.Back);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            string[] weekdays = { "日", "一", "二", "三", "四", "五", "六" };
            int left = monthView ? 0 : surface.TimeGutter;
            int count = monthView ? 7 : ViewDays;
            float width = (surface.GridWidth - left) / (float)count;
            int height = dayHeader.ClientSize.Height;
            DateTime start = monthView ? WeekStart(focusDate) : ViewStart();
            Font regular = RefFont(9F, FontStyle.Regular), bold = RefFont(9F, FontStyle.Bold);
            const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            for (int i = 0; i < 7; i++)
            {
                DateTime date = start.AddDays(i);
                bool today = !monthView && date.Date == DateTime.Today;
                string day = weekdays[(int)date.DayOfWeek], number = monthView ? "" : " " + date.Day;
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
            }
            using (Pen rule = new Pen(p.Rule)) graphics.DrawLine(rule, 0, height - 1, dayHeader.Width, height - 1);
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
            if (DateTime.Today >= startDate && DateTime.Today < startDate.AddDays(days))
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
                        TextRenderer.DrawText(graphics, date.Day.ToString() + (!reference && date == DateTime.Today ? " 今天" : ""), dateFont, new Rectangle(x + S(6), y + S(5), (int)col - S(10), T(24)), date.Month == focusDate.Month ? foreground : secondary, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | ScrolledText);
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
            int day = Math.Min(days - 1, Math.Max(0, (int)((point.X - TimeGutter) / ((Math.Max(S(100), GridWidth) - TimeGutter) / (float)days))));
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
