using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DeskStudy
{
    public static class Ui
    {
        public static readonly Color Text = Color.FromArgb(37, 48, 64);
        public static readonly Color Muted = Color.FromArgb(110, 120, 133);
        public static readonly Color Background = Color.FromArgb(247, 248, 251);
        public static readonly Color Border = Color.FromArgb(225, 229, 236);
        public static readonly Color Accent = Color.FromArgb(78, 103, 204);
        public static Button Button(string text, EventHandler action)
        {
            Button b = new Button { Text = text, AutoSize = true, Height = 32, MinimumSize = new Size(40, 30), FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = Text, Cursor = Cursors.Hand, Margin = new Padding(3), Padding = new Padding(6, 1, 6, 1) };
            b.FlatAppearance.BorderColor = Border;
            if (action != null) b.Click += action;
            return b;
        }
        public static Label Label(string text, float size, Color color)
        {
            return new Label { Text = text, AutoSize = true, ForeColor = color, Font = new Font("Microsoft YaHei UI", size), BackColor = Color.Transparent };
        }
    }

    public partial class WidgetForm : Form
    {
        protected AppController App;
        protected Panel Body;
        public readonly string WidgetKey;
        private Label heading;
        private CheckBox pin;
        private FlowLayoutPanel headerActions;
        private Button settingsButton, hideButton;
        private Panel header, stripe;
        private Font notebookHeadingFont;
        private string headingTitle = "", headingSuffix = "";
        private bool referenceHeader, referenceRule;
        private Color referenceDot, referenceRuleColor, referenceInk, referenceAccent;
        private bool ready;
        private bool applyingAppearance;
        private readonly Dictionary<Control, AppearanceBaseline> appearanceBaselines = new Dictionary<Control, AppearanceBaseline>();
        public bool PositionLocked { get { WindowState w; return App.Data.Windows.TryGetValue(WidgetKey, out w) && w.PositionLocked; } }
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);

        public WidgetForm(AppController app, string key, string title, Color accent, Size defaultSize)
        {
            SuspendLayout();
            App = app; WidgetKey = key;
            desktopMode = app.Data.Settings.WidgetMode == "Desktop";
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Microsoft YaHei UI", 9F);
            Text = title + " · " + Lang.T("桌面课笺"); BackColor = Ui.Background;
            Icon = App.AppIcon; StartPosition = FormStartPosition.Manual;
            Size = defaultSize; MinimumSize = new Size(350, 430);
            FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = !desktopMode; MinimizeBox = !desktopMode; ShowInTaskbar = !desktopMode;
            header = new EdgePanel { Name = "widget-header", Dock = DockStyle.Top, Height = 48, BackColor = Color.White, Padding = new Padding(12, 6, 8, 6) };
            stripe = new EdgePanel { Name = "widget-accent", Dock = DockStyle.Top, Height = 3, BackColor = accent };
            headingTitle = title;
            heading = Ui.Label(title, 12F, accent); heading.Name = "widget-heading"; heading.Dock = DockStyle.Fill; heading.TextAlign = ContentAlignment.MiddleLeft; heading.AutoSize = false;
            heading.Paint += delegate(object sender, PaintEventArgs e)
            {
                if (!referenceHeader) return;
                float d = e.Graphics.DpiX / 96F; int size = (int)Math.Round(7 * d);
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(referenceDot)) e.Graphics.FillEllipse(brush, 0, (heading.Height - size) / 2, size, size);
            };
            header.Paint += delegate(object sender, PaintEventArgs e)
            {
                if (!referenceHeader || !referenceRule) return;
                using (var pen = new Pen(referenceRuleColor)) e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };
            var actions = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 174, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            headerActions = actions;
            pin = new CheckBox { Text = Lang.T("置顶"), AutoSize = true, Margin = new Padding(4, 7, 7, 2) };
            pin.Name = "widget-pin";
            pin.CheckedChanged += delegate { TopMost = pin.Checked; Remember(); if (referenceHeader) pin.ForeColor = pin.Checked ? referenceAccent : referenceInk; };
            var menu = Ui.Button("⚙", delegate { App.OpenSettings(); }); menu.Name = "open-settings"; menu.AccessibleName = Lang.T("打开设置中心"); menu.AutoSize = false; menu.Width = 40;
            var hide = Ui.Button(Lang.T("隐藏"), delegate { Hide(); }); hide.Name = "hide-widget"; hide.AutoSize = false; hide.Width = 54;
            settingsButton = menu; hideButton = hide;
            InitializeCollapse();
            actions.Controls.Add(pin); actions.Controls.Add(menu); actions.Controls.Add(collapseButton); actions.Controls.Add(hide);
            header.Controls.Add(heading); header.Controls.Add(actions);
            InitializeFrame();
            Body = new EdgePanel { Dock = DockStyle.Fill, Padding = new Padding(12, 6, 12, 10), BackColor = Ui.Background };
            Controls.Add(Body); Controls.Add(header); Controls.Add(stripe);
            MouseEventHandler drag = delegate(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                // Double-clicking the header folds or unfolds the widget.
                if (e.Clicks == 2) { ToggleCollapse(); return; }
                if (!PositionLocked) { ReleaseCapture(); SendMessage(Handle, 0xA1, new IntPtr(2), IntPtr.Zero); }
            };
            header.MouseDown += drag; heading.MouseDown += drag;
            RestoreWindow();
            Move += delegate { Remember(); }; ResizeEnd += delegate { Remember(); };
            VisibleChanged += delegate { Remember(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (!App.Exiting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } else Remember(); };
        }
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            RestoreWindow();
            ready = true;
            ApplyAppearance();
        }
        protected override void WndProc(ref Message m)
        {
            if (App != null && PositionLocked)
            {
                int command = m.WParam.ToInt32() & 0xFFF0;
                if (m.Msg == 0x112 && (command == 0xF010 || command == 0xF000 || command == 0xF030)) return;
                if ((m.Msg == 0xA1 || m.Msg == 0xA3) && m.WParam.ToInt32() == 2) return;
            }
            if (App != null && FrameWndProc(ref m)) return;
            if (App != null) PlacementWndProc(ref m);
            base.WndProc(ref m);
            if (App != null && PositionLocked && m.Msg == 0x84 && m.Result.ToInt32() >= 10 && m.Result.ToInt32() <= 17) m.Result = new IntPtr(1);
            if (App != null) { AfterFrameWndProc(ref m); AfterPlacementWndProc(ref m); }
        }
        public void ApplyAppearance()
        {
            if (applyingAppearance || IsDisposed || App.Data.Settings == null) return;
            if (!CanApplyAppearance()) return;
            applyingAppearance = true;
            try
            {
                var appearance = SettingsLogic.EffectiveAppearance(App.Data, WidgetKey);
                AppearancePainter.Apply(this, appearance, appearanceBaselines);
                Opacity = appearance.Opacity;
                if (headerActions != null)
                {
                    hideButton.Width = Math.Max(hideButton.Width, TextRenderer.MeasureText(Lang.T("隐藏"), hideButton.Font).Width + hideButton.Padding.Horizontal + 10);
                    int height = Math.Max(hideButton.Height, hideButton.Font.Height + hideButton.Padding.Vertical + 8);
                    hideButton.Height = height; settingsButton.Height = height; collapseButton.Height = height;
                    UpdateHeaderActionsWidth();
                }
                OnAppearanceChanged(appearance);
                StyleCollapseButton();
                if (collapsed) FitCollapsed();
            }
            finally { applyingAppearance = false; }
        }
        protected virtual void OnAppearanceChanged(AppearanceOptions appearance) { }
        protected virtual bool CanApplyAppearance() { return true; }
        private void UpdateHeaderActionsWidth()
        {
            int width = (pin.AutoSize ? pin.PreferredSize.Width : pin.Width) + pin.Margin.Horizontal + settingsButton.Width + settingsButton.Margin.Horizontal + collapseButton.Width + collapseButton.Margin.Horizontal + 4;
            if (hideButton.Visible || !referenceHeader) width += hideButton.Width + hideButton.Margin.Horizontal;
            headerActions.Width = width;
        }
        // Reference-style header (清爽卡片 / 手账纸页): colored dot, book label, a 置顶 toggle and a small ⚙.
        // The window's ✕ still hides the widget, so the 隐藏 button and the accent stripe are not shown.
        protected void SetReferenceHeader(bool on, string suffix, Color back, Color ink, Color muted, Color dot, Color accent, Color soft, Color rule, bool bottomRule)
        {
            float dpi; using (var g = CreateGraphics()) dpi = g.DpiY / 96F;
            referenceHeader = on; referenceRule = bottomRule; referenceDot = dot; referenceRuleColor = rule; referenceInk = ink; referenceAccent = accent;
            headingSuffix = on ? suffix : "";
            heading.Text = headingTitle + headingSuffix;
            stripe.Visible = !on; hideButton.Visible = !on;
            pin.Appearance = on ? Appearance.Button : Appearance.Normal;
            pin.FlatStyle = on ? FlatStyle.Flat : FlatStyle.Standard;
            pin.TextAlign = ContentAlignment.MiddleCenter;
            pin.FlatAppearance.BorderSize = 0;
            if (on)
            {
                header.BackColor = back; heading.BackColor = back; headerActions.BackColor = back;
                heading.ForeColor = muted;
                heading.Padding = new Padding((int)Math.Round(15 * dpi), 0, 0, 0);
                bool slim = SlimReferenceHeader;
                header.Padding = slim ? new Padding((int)Math.Round(18 * dpi), (int)Math.Round(6 * dpi), (int)Math.Round(10 * dpi), (int)Math.Round(4 * dpi))
                    : new Padding((int)Math.Round(18 * dpi), (int)Math.Round(6 * dpi), (int)Math.Round(10 * dpi), (int)Math.Round(4 * dpi));
                if (slim)
                {
                    // Slim header (calendar, 1.5.1): smaller 置顶 / ⚙ / ✕ so more of the timetable shows below.
                    float size = (float)Math.Round(8F * SettingsLogic.EffectiveAppearance(App.Data, WidgetKey).FontSize / 9F, 2);
                    if (slimFont == null || Math.Abs(slimFont.SizeInPoints - size) > .01F) { Font old = slimFont; slimFont = new Font("Microsoft YaHei UI", size); if (old != null) old.Dispose(); }
                    pin.Font = slimFont; settingsButton.Font = slimFont; settingsButton.Padding = Padding.Empty;
                    settingsButton.MinimumSize = Size.Empty;
                    settingsButton.Height = Math.Max((int)Math.Round(22 * dpi), slimFont.Height + (int)Math.Round(6 * dpi));
                }
                pin.BackColor = back; pin.ForeColor = pin.Checked ? accent : ink;
                pin.FlatAppearance.CheckedBackColor = soft; pin.FlatAppearance.MouseOverBackColor = soft; pin.FlatAppearance.MouseDownBackColor = soft;
                pin.Padding = Padding.Empty;
                pin.Margin = new Padding(0, 0, (int)Math.Round(2 * dpi), 0);
                settingsButton.BackColor = back; settingsButton.ForeColor = muted;
                settingsButton.FlatAppearance.BorderSize = 0; settingsButton.FlatAppearance.MouseOverBackColor = soft; settingsButton.FlatAppearance.BorderColor = back;
                // Wide enough for the ⚙ glyph with the button's own padding (the notebooks get this from their minimum size).
                settingsButton.Width = slim ? (int)Math.Round(26 * dpi) : Math.Max(settingsButton.MinimumSize.Width, (int)Math.Round(40 * dpi));
                settingsButton.Margin = Padding.Empty;
                pin.AutoSize = false;
                pin.Size = new Size(TextRenderer.MeasureText(Lang.T("置顶"), pin.Font).Width + (int)Math.Round((slim ? 12 : 16) * dpi), settingsButton.Height);
                header.Height = slim ? settingsButton.Height + header.Padding.Vertical : (int)Math.Max(42 * dpi, settingsButton.Height + 12 * dpi);
            }
            else
            {
                heading.Padding = Padding.Empty; header.Padding = new Padding(12, 6, 8, 6);
                pin.Padding = Padding.Empty; pin.Margin = new Padding(4, 7, 7, 2); pin.AutoSize = true;
                settingsButton.Width = (int)Math.Round(40 * dpi); settingsButton.Margin = hideButton.Margin; settingsButton.Height = hideButton.Height;
                settingsButton.FlatAppearance.MouseOverBackColor = Color.Empty;
            }
            // Without a title bar there is no system ✕, so the reference header carries its own.
            StyleCloseButton(on, back, muted, soft, dpi);
            UpdateHeaderActionsWidth();
            header.Invalidate(); heading.Invalidate();
        }
        private Font slimFont;
        // The calendar uses a lower reference header than the notebooks.
        protected virtual bool SlimReferenceHeader { get { return false; } }
        // Header buttons at their designed size for this screen: 32 px tall with 3 px margins, scaled once.
        protected void NormalizeHeaderButtons()
        {
            float dpi; using (var g = CreateGraphics()) dpi = g.DpiY / 96F;
            int height = Math.Max((int)Math.Round(32 * dpi), hideButton.Font.Height + hideButton.Padding.Vertical + 8);
            var margin = new Padding((int)Math.Round(3 * dpi));
            // Their own minimum size was scaled with the form (twice on the calendar) and would keep them oversized.
            hideButton.MinimumSize = settingsButton.MinimumSize = Size.Empty;
            hideButton.Height = settingsButton.Height = height;
            hideButton.Margin = margin; settingsButton.Margin = margin;
        }
        protected void SetCompactNotebookHeader(bool compact) { SetCompactNotebookHeader(compact, 9.5F); }
        protected void SetCompactNotebookHeader(bool compact, float compactSize)
        {
            float dpi; using (var g = CreateGraphics()) dpi = g.DpiY / 96F;
            heading.AutoEllipsis = true;
            heading.Parent.Height = (int)Math.Max((compact ? 36 : 48) * dpi, hideButton.Height + 12 * dpi);
            float size = (compact ? compactSize : 12F) * SettingsLogic.EffectiveAppearance(App.Data, WidgetKey).FontSize / 9F;
            if (notebookHeadingFont == null || Math.Abs(notebookHeadingFont.SizeInPoints - size) > .01F)
            {
                Font previous = notebookHeadingFont; notebookHeadingFont = new Font("Microsoft YaHei UI", size);
                heading.Font = notebookHeadingFont; if (previous != null) previous.Dispose();
            }
            else heading.Font = notebookHeadingFont;
            settingsButton.FlatAppearance.BorderSize = hideButton.FlatAppearance.BorderSize = compact ? 0 : 1;
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                foreach (var baseline in appearanceBaselines.Values) if (baseline.AppliedFont != null) baseline.AppliedFont.Dispose();
                appearanceBaselines.Clear();
                if (notebookHeadingFont != null) { notebookHeadingFont.Dispose(); notebookHeadingFont = null; }
                if (hoverTimer != null) { hoverTimer.Dispose(); hoverTimer = null; }
                if (closeFont != null) { closeFont.Dispose(); closeFont = null; }
                if (slideTimer != null) { slideTimer.Dispose(); slideTimer = null; }
                if (slimFont != null) { slimFont.Dispose(); slimFont = null; }
            }
        }
        protected void SetTitle(string title) { Text = title + " · " + Lang.T("桌面课笺"); headingTitle = title; heading.Text = title + headingSuffix; }
        public void RestoreWindow()
        {
            bool old = ready; ready = false;
            // Placing a widget from saved or typed coordinates always shows it unfolded.
            UnfoldInPlace();
            WindowState ws;
            if (App.Data.Windows.TryGetValue(WidgetKey, out ws))
            {
                var desired = new Rectangle(ws.X, ws.Y, Math.Max(MinimumSize.Width, ws.Width), Math.Max(MinimumSize.Height, ws.Height));
                // Same rule as after a drag: a widget the user left partly off screen stays there, one that is lost comes back.
                desired = PlacementLogic.Contain(desired, Screen.FromRectangle(desired).WorkingArea);
                Bounds = desired; TopMost = ws.TopMost; pin.Checked = ws.TopMost;
            }
            else
            {
                var work = Screen.PrimaryScreen.WorkingArea;
                int offset = WidgetKey == "calendar" ? 0 : WidgetKey == "todo" ? 1 : 2;
                Location = new Point(work.Left + 24 + offset * 65, work.Top + 24 + offset * 62);
            }
            ready = old;
        }
        public void Remember()
        {
            if (!ready || IsDisposed || App.Exiting) return;
            Rectangle b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            WindowState old;
            bool existed = App.Data.Windows.TryGetValue(WidgetKey, out old);
            // A folded widget is saved where it was before folding, so the next start shows it unfolded there.
            if (collapsed) b = foldedFrom;
            var next = new WindowState { X = b.X, Y = b.Y, Width = b.Width, Height = b.Height, TopMost = TopMost, Visible = Visible, PositionLocked = existed && old.PositionLocked };
            if (existed && old.X == next.X && old.Y == next.Y && old.Width == next.Width && old.Height == next.Height && old.TopMost == next.TopMost && old.Visible == next.Visible) return;
            App.Data.Windows[WidgetKey] = next;
            App.QueueSave(); App.NotifyWindowStateChanged();
        }
        public void Reveal() { Show(); WindowState = FormWindowState.Normal; Activate(); Remember(); }
    }

    public sealed partial class AppController : ApplicationContext
    {
        public AppStore Store { get; private set; }
        public AppData Data { get { return Store.Data; } }
        public bool Exiting { get; private set; }
        public Icon AppIcon { get; private set; }
        public event Action DataChanged;
        public event Action SaveStateChanged;
        public string SaveStatus { get { return Lang.T(saveErrorShown ? "保存失败" : dirty ? "保存中…" : "已保存"); } }
        public bool SaveFailed { get { return saveErrorShown; } }
        public event Action<List<ReminderRecord>> RemindersDelivered;
        public List<WidgetForm> Widgets = new List<WidgetForm>();
        private NotifyIcon tray;
        private System.Windows.Forms.Timer saveTimer, reminderTimer, reopenTimer;
        private ReminderCenter center;
        private bool dirty, saveErrorShown;
        private EventWaitHandle showSignal;
        private bool notifications;
        private readonly List<ReminderRecord> pendingDelivery = new List<ReminderRecord>();
        private PowerModeChangedEventHandler powerHandler;
        private SessionEndingEventHandler sessionHandler;
        public AppController(string dataDirectory, EventWaitHandle signal, bool enableNotifications) : this(dataDirectory, signal, enableNotifications, false) { }
        public AppController(string dataDirectory, EventWaitHandle signal, bool enableNotifications, bool quietStart)
        {
            Store = new AppStore(dataDirectory); showSignal = signal; notifications = enableNotifications;
            AppIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            saveTimer = new System.Windows.Forms.Timer { Interval = Data.Settings.AutoSaveDelayMs };
            saveTimer.Tick += delegate { saveTimer.Stop(); Flush(); };
            tray = new NotifyIcon { Icon = AppIcon, Text = Lang.T("桌面课笺 · 课表 / Todo / DDL"), Visible = enableNotifications };
            tray.ContextMenuStrip = CreateMenu(); tray.DoubleClick += delegate { ShowAll(); };
            // Widgets have no taskbar button in desktop mode, so one click on the tray icon brings them forward.
            tray.MouseClick += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) RaiseWidgets(); };
            tray.BalloonTipClicked += delegate { OpenReminders(); };
            if (Store.IsNew)
            {
                // First start: the Windows display language, and notebook names to match.
                Data.Settings.Language = Lang.SystemDefault();
                if (Data.Settings.Language == Lang.English)
                    foreach (var book in Data.Books) book.Name = book.Id == "ddl" ? "Deadlines" : "To-do";
            }
            Lang.Use(Data.Settings.Language);
            bool framesConverted = NormalizeWindowFrames();            Widgets.Add(new CalendarForm(this)); Widgets.Add(new NotebookForm(this, "todo")); Widgets.Add(new NotebookForm(this, "ddl"));
            // Apply physical saved bounds after each derived form has completed DPI scaling,
            // including forms that stay hidden and therefore do not raise Load yet.
            foreach (var w in Widgets) w.RestoreWindow();
            // Capture intended visibility before Show() emits persistence callbacks.
            var visible = Widgets.ToDictionary(w => w.WidgetKey, w => !Data.Windows.ContainsKey(w.WidgetKey) || Data.Windows[w.WidgetKey].Visible);
            foreach (var w in Widgets) if (visible[w.WidgetKey]) w.Show();
            foreach (var w in Widgets) w.ApplyAppearance();
            // Widgets saved partly off screen or on top of each other (older versions, a monitor since removed) are tidied once.
            // First start: the calendar opens tall enough to show 07:00 to 12:00, within the screen.
            if (Store.IsNew) foreach (var w in Widgets.OfType<CalendarForm>()) w.FitDefaultHeight();
            ArrangeWidgets(); WatchDisplays();
            FinishStartup(quietStart);
            if (framesConverted) QueueSave();
            powerHandler = delegate(object sender, PowerModeChangedEventArgs e)
            {
                if (e.Mode == PowerModes.Resume && !Exiting && Widgets.Count > 0 && Widgets[0].IsHandleCreated)
                    Widgets[0].BeginInvoke(new Action(delegate { if (!Exiting) { Data.LastCheckUtc = ""; CheckReminders(DateTime.UtcNow); } }));
            };
            sessionHandler = delegate
            {
                if (!Exiting && Widgets.Count > 0 && Widgets[0].IsHandleCreated)
                {
                    Action finalSave = delegate { if (!Exiting) { try { Store.Save(); } catch { } } };
                    if (Widgets[0].InvokeRequired) Widgets[0].Invoke(finalSave); else finalSave();
                }
            };
            SystemEvents.PowerModeChanged += powerHandler;
            SystemEvents.SessionEnding += sessionHandler;
            reminderTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            reminderTimer.Tick += delegate { CheckReminders(DateTime.UtcNow); }; reminderTimer.Start();
            reopenTimer = new System.Windows.Forms.Timer { Interval = 700 };
            reopenTimer.Tick += delegate { if (showSignal != null && showSignal.WaitOne(0)) ShowAll(); }; reopenTimer.Start();
            var startup = new System.Windows.Forms.Timer { Interval = 1000 };
            startup.Tick += delegate { startup.Stop(); startup.Dispose(); if (!Exiting) { CheckReminders(DateTime.UtcNow); if (!String.IsNullOrEmpty(Store.LoadWarning)) MessageBox.Show(Store.LoadWarning, Lang.T("数据恢复"), MessageBoxButtons.OK, MessageBoxIcon.Warning); } }; startup.Start();
        }
        public void Save() { QueueSave(); PublishChanges(); }
        public void QueueSave() { if (Exiting) return; bool changed = !dirty; dirty = true; saveTimer.Stop(); saveTimer.Start(); if (changed && SaveStateChanged != null) SaveStateChanged(); }
        public bool Flush()
        {
            if (!dirty) return true;
            try { Store.Save(); dirty = false; saveErrorShown = false; tray.Text = Lang.T("桌面课笺 · 已自动保存"); if (SaveStateChanged != null) SaveStateChanged(); return true; }
            catch (Exception ex) { tray.Text = Lang.T("桌面课笺 · 保存失败，请导出备份"); if (!saveErrorShown) { saveErrorShown = true; if (SaveStateChanged != null) SaveStateChanged(); MessageBox.Show(Lang.T("本次保存失败，内容仍在内存中。请检查磁盘或从菜单导出备份。\n\n") + ex.Message, Lang.T("无法保存"), MessageBoxButtons.OK, MessageBoxIcon.Error); } saveTimer.Start(); return false; }
        }
        public void CheckReminders(DateTime utcNow)
        {
            if (Exiting) return;
            pendingDelivery.AddRange(ReminderEngine.Scan(Data, utcNow));
            // Persist both delivery keys and last scan, including while every window is hidden.
            dirty = true; if (!Flush()) return;
            var liveTasks = Data.Books.SelectMany(b => b.Pages).SelectMany(p => p.Tasks).Where(t => !t.Completed).Select(t => t.Id).ToList();
            var batch = pendingDelivery.Where(r => liveTasks.Contains(r.TaskId)).ToList();
            if (SettingsLogic.IsQuietHours(Data.Settings, utcNow.ToLocalTime())) return;
            pendingDelivery.Clear();
            if (batch.Count == 0) return;
            if (RemindersDelivered != null) RemindersDelivered(batch);
            if (notifications)
            {
                bool catchUp = batch.Any(r => r.CatchUp);
                string title = Lang.T(catchUp ? "恢复提醒 · {0} 项" : "课笺提醒 · {0} 项", batch.Count);
                string message = String.Join("\n", batch.Take(3).Select(r => r.Title + "  " + TimeUtil.DueDisplay(r.DueLocal, r.DueDateOnly)));
                if (batch.Count > 3) message += "\n" + Lang.T("另有 {0} 项，点击查看全部", batch.Count - 3);
                if (!NativeNotification.Show(tray, AppIcon, title, message, Data.Settings.Reminders.SoundEnabled))
                { tray.Text = Lang.T("桌面课笺 · 系统通知未发送，请查看提醒中心"); OpenReminders(); }
                if (catchUp) OpenReminders();
            }
            if (center != null && !center.IsDisposed) center.RefreshData();
        }
        public ContextMenuStrip CreateMenu()
        {
            var menu = new ContextMenuStrip { Font = new Font("Microsoft YaHei UI", 9F) };
            menu.Items.Add(Lang.T("设置中心"), null, delegate { OpenSettings(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Lang.T("显示全部组件"), null, delegate { ShowAll(); });
            menu.Items.Add(Lang.T("隐藏全部组件"), null, delegate { HideAll(); });
            menu.Items.Add(new ToolStripSeparator());
            foreach (string id in new[] { "calendar", "todo", "ddl" })
            {
                string key = id;
                string name = key == "calendar" ? Lang.T("日历") : (Data.Books.FirstOrDefault(b => b.Id == key) == null ? key : Data.Books.First(b => b.Id == key).Name);
                var componentItem = menu.Items.Add(Lang.T("显示 / 隐藏 {0}", name), null, delegate { var w = Widgets.FirstOrDefault(f => f.WidgetKey == key); if (w == null) return; SetWidgetVisible(key, !w.Visible); });
                componentItem.Tag = key;
            }
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Lang.T("提醒中心"), null, delegate { OpenReminders(); });
            menu.Items.Add(Lang.T("发送测试通知"), null, delegate { try { SendTestNotification(); } catch (Exception ex) { MessageBox.Show(ex.Message, Lang.T("通知测试"), MessageBoxButtons.OK, MessageBoxIcon.Warning); } });
            menu.Items.Add(Lang.T("导出数据…"), null, delegate { ExportData(); });
            menu.Items.Add(Lang.T("导入数据…"), null, delegate { ImportData(); });
            menu.Items.Add(Lang.T("使用说明"), null, delegate { ShowHelp(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Lang.T("保存并退出"), null, delegate { Shutdown(); });
            menu.Opening += delegate { foreach (ToolStripItem item in menu.Items) { string key = item.Tag as string; if (key == "todo" || key == "ddl") item.Text = Lang.T("显示 / 隐藏 {0}", Data.Books.First(b => b.Id == key).Name); } };
            return menu;
        }
        public void ShowAll() { foreach (var w in Widgets) w.Reveal(); }
        public void OpenReminders() { if (center == null || center.IsDisposed) center = new ReminderCenter(this); center.RefreshData(); center.Show(); center.Activate(); }
        public void ExportData()
        {
            using (var dlg = new SaveFileDialog { Filter = Lang.T("课笺数据 (*.json)|*.json"), FileName = Lang.T("课笺备份-") + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".json", Title = Lang.T("导出全部课表、便签和窗口设置") })
                if (dlg.ShowDialog() == DialogResult.OK) try { Store.Export(dlg.FileName); SettingsChanged(); MessageBox.Show(Lang.T("备份已导出。"), Lang.T("导出完成")); } catch (Exception ex) { MessageBox.Show(ex.Message, Lang.T("导出失败")); }
        }
        public void ImportData()
        {
            using (var dlg = new OpenFileDialog { Filter = Lang.T("课笺数据 (*.json)|*.json"), Title = Lang.T("导入备份") })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                if (MessageBox.Show(Lang.T("导入会替换当前课表、两本便签和窗口设置。当前数据将先保存为本地备份。是否继续？"), Lang.T("导入备份"), MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
                try
                {
                    RestoreBackup(dlg.FileName); MessageBox.Show(Lang.T("备份已恢复。"), Lang.T("导入完成"));
                }
                catch (Exception ex) { MessageBox.Show(ex.Message + Lang.T("\n\n导入前备份保留在本地数据目录。"), Lang.T("导入或界面恢复失败"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }
        private void ShowHelp()
        {
            MessageBox.Show(Lang.T("桌面课笺 1.5.1\n\n桌面组件模式下，组件没有标题栏，也不出现在任务栏：拖动顶栏移动，拖动边缘调整大小；松手后，靠近屏幕边缘或其他组件的会自动贴齐，稍微超出屏幕的会滑回，压住其他组件的会让开；点击组件时它浮到前面，切到别的程序后自动回到其他窗口下面。单击托盘图标或按快捷键（默认 Ctrl+Alt+Shift+D）可把组件浮到前面。可在设置中心的「显示与布局」切回标准窗口。\n\n外观页可切换五种便签布局。点击页标题可编辑，在任务末尾连续录入；点任务下方的状态文字可设置或修改截止时间。\n\n从托盘菜单或任一组件的齿轮按钮打开设置中心。关闭设置中心后，组件与提醒继续运行。\n\n显示与布局：管理置顶、位置锁定、保存布局；窗口移出屏幕后，可使用「找回当前屏幕」。外观支持全局设置和组件单独覆盖，修改立即预览并自动保存。\n\n日历：支持周/月视图和循环课表。临时停课或调课可选择「仅这一次」；设置中心可统一管理课程系列与学期。\n\n便签：文字和任务自动保存，设置中心可管理页面名称、顺序、归档和当前页。隐藏、翻页或归档不会取消未完成任务的提醒。\n\n提醒每 5 秒检查所有页面；免打扰结束、退出后重新运行或休眠恢复后汇总补发。完全退出后不能实时通知，请保留托盘运行。\n\n数据与应用：导入、导出、备份恢复和开机启动。恢复前会先保留当前数据。重置布局或外观不会删除内容。\n\n数据目录：\n") + Store.DirectoryPath, Lang.T("使用说明"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        public void Shutdown()
        {
            foreach (var w in Widgets) w.Remember();
            if (!Flush()) return;
            Exiting = true; reminderTimer.Stop(); saveTimer.Stop(); reopenTimer.Stop();
            SystemEvents.PowerModeChanged -= powerHandler; SystemEvents.SessionEnding -= sessionHandler; UnwatchDisplays();
            tray.Visible = false;
            ReleaseShowHotkey();
            foreach (var w in Widgets) w.Dispose();
            if (center != null) center.Dispose();
            if (settingsCenter != null) settingsCenter.Dispose();
            NativeNotification.Dispose();
            tray.Dispose(); ExitThread();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { if (!Exiting) Shutdown(); saveTimer.Dispose(); reminderTimer.Dispose(); reopenTimer.Dispose(); AppIcon.Dispose(); }
            base.Dispose(disposing);
        }
    }

    public class ReminderCenter : Form
    {
        private AppController app;
        private ListView list;
        public ReminderCenter(AppController controller)
        {
            SuspendLayout(); AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
            app = controller; Text = Lang.T("提醒中心 · 桌面课笺"); Size = new Size(780, 450); MinimumSize = new Size(600, 320);
            Font = new Font("Microsoft YaHei UI", 9F); BackColor = Ui.Background; StartPosition = FormStartPosition.CenterScreen; Icon = app.AppIcon;
            var info = Ui.Label(Lang.T("所有页面的提醒记录 · 错过的提醒会在恢复运行后汇总"), 10F, Ui.Muted); info.Dock = DockStyle.Top; info.Height = 52; info.AutoSize = false; info.Padding = new Padding(14);
            list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = false, BorderStyle = BorderStyle.None };
            list.Columns.Add(Lang.T("任务"), 210); list.Columns.Add(Lang.T("来源"), 170); list.Columns.Add(Lang.T("截止时间"), 145); list.Columns.Add(Lang.T("提醒"), 90); list.Columns.Add(Lang.T("记录时间"), 145);
            Controls.Add(list); Controls.Add(info);
            ResumeLayout(true);
        }
        public void RefreshData()
        {
            list.BeginUpdate(); list.Items.Clear();
            foreach (var r in app.Data.ReminderHistory.AsEnumerable().Reverse())
            {
                DateTime fired; string stamp = DateTime.TryParse(r.FiredUtc, out fired) ? fired.ToLocalTime().ToString("MM-dd HH:mm:ss") : r.FiredUtc;
                var row = new ListViewItem(r.Title); row.SubItems.Add(r.BookName + " / " + r.PageTitle); row.SubItems.Add(TimeUtil.DueDisplay(r.DueLocal, r.DueDateOnly));
                row.SubItems.Add((r.CatchUp ? Lang.T("补发 · ") : "") + (r.DueDateOnly ? Lang.T("当天") : r.Kind == "advance" ? Lang.T("提前") : Lang.T("截止"))); row.SubItems.Add(stamp); list.Items.Add(row);
            }
            if (list.Items.Count == 0) list.Items.Add(Lang.T("暂无提醒。可从组件菜单发送测试通知。"));
            list.EndUpdate();
        }
    }

    internal static class Program
    {
        [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
        [STAThread] private static void Main(string[] args)
        {
            SetProcessDPIAware(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeskStudy");
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "--data-dir") directory = Path.GetFullPath(args[++i]);
            // After "Restart now": wait until the previous instance has saved and gone before taking its place.
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "--restart-after")
                {
                    int previous;
                    if (Int32.TryParse(args[i + 1], out previous))
                        try { using (var old = System.Diagnostics.Process.GetProcessById(previous)) old.WaitForExit(20000); } catch (ArgumentException) { }
                }
            string hash; using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(directory.ToLowerInvariant()))).Replace("-", "").Substring(0, 20);
            bool created;
            using (var signal = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\DeskStudyShow" + hash))
            using (var mutex = new Mutex(true, "Local\\DeskStudy" + hash, out created))
            {
                if (!created) { signal.Set(); return; }
                try { using (var app = new AppController(directory, signal, true, args.Contains("--startup"))) Application.Run(app); }
                catch (Exception ex) { MessageBox.Show(Lang.T("应用无法继续运行。已保存的数据仍保留在本地。\n\n") + ex.Message + Lang.T("\n\n数据目录：") + directory, Lang.T("桌面课笺"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
                finally { mutex.ReleaseMutex(); }
            }
        }
    }
}

