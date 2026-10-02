using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DeskStudy
{
    // One modeless settings window; every control reads the same model as the desktop widgets.
    public sealed class SettingsForm : Form
    {
        private readonly AppController app;
        private readonly ListBox navigation;
        private readonly Panel host;
        private readonly Label status;
        private readonly List<Panel> pages = new List<Panel>();
        private bool layingOutPages;
        private readonly Dictionary<string, CheckBox> visibleChecks = new Dictionary<string, CheckBox>();
        private readonly Dictionary<string, Label> overviewDetails = new Dictionary<string, Label>();
        private readonly Dictionary<string, Label> componentNames = new Dictionary<string, Label>();
        private readonly Dictionary<string, LayoutEditors> layoutEditors = new Dictionary<string, LayoutEditors>();
        private bool syncing;
        private bool calendarDatesPending;
        private bool cleanupComplete;
        private ComboBox uiLanguage;
        private Label languageNote;
        private Button restartButton;
        private ComboBox appearanceTarget, theme, defaultView, weekStart, bookChoice, desktopPage, saveDelay, widgetMode, widgetCorners;
        private TextBox hotkeyBox;
        private Label hotkeyStatus;
        private NumericUpDown opacity, fontSize, leadMinutes;
        private CheckBox followingGlobal, quietEnabled, soundEnabled, startup;
        private Button backgroundColor;
        private readonly List<NotebookLayoutPreview> notebookLayouts = new List<NotebookLayoutPreview>();
        private DateTimePicker semesterStart, semesterEnd, teachingWeekOne, quietStart, quietEnd, dateOnlyReminder;
        private TextBox bookName, pageName;
        private ListView courses, pageList, pendingTasks, backups;
        private Label appearanceHint, layoutSaved, pageDetail, lastBackup, storagePath;
        private readonly Timer refreshTimer;
        private string courseSignature = "", pageSignature = "", pendingSignature = "", backupSignature = "", bookSignature = "";

        private sealed class Choice
        {
            public readonly string Id;
            public readonly string Text;
            public Choice(string id, string text) { Id = id; Text = text; }
            public override string ToString() { return Text; }
        }
        private sealed class LayoutEditors
        {
            public NumericUpDown X, Y, Width, Height;
            public CheckBox Pin, Locked;
        }

        public SettingsForm(AppController controller)
        {
            app = controller;
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Microsoft YaHei UI", 9F);
            BackColor = Ui.Background; ForeColor = Ui.Text;
            Text = Lang.T("设置中心 · 桌面课笺"); Name = "settings-center";
            Icon = app.AppIcon; StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(800, 560); Size = new Size(1060, 760);
            var header = new Panel { Dock = DockStyle.Top, Height = 83, Padding = new Padding(25, 16, 20, 12), BackColor = Color.White };
            var heading = Ui.Label(Lang.T("设置中心"), 19F, Ui.Text); heading.Location = new Point(24, 13);
            var subtitle = Ui.Label(Lang.T("课表、便签与桌面，都在这里。更改会自动保存。"), 9F, Ui.Muted); subtitle.Location = new Point(27, 49);
            header.Controls.Add(heading); header.Controls.Add(subtitle);
            status = Ui.Label(Lang.T("关闭此窗口后，桌面组件与提醒继续运行。"), 8.5F, Ui.Muted);
            status.Name = "settings-status"; status.AutoSize = false; status.Dock = DockStyle.Bottom; status.Height = 35; status.Padding = new Padding(24, 8, 16, 5);
            var main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(16, 16, 16, 0) };
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 177)); main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            navigation = new ListBox { Name = "settings-nav", Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = Ui.Background, ForeColor = Ui.Text, IntegralHeight = false, ItemHeight = 47, DrawMode = DrawMode.OwnerDrawFixed, Margin = new Padding(0, 0, 15, 0) };
            navigation.AccessibleName = Lang.T("设置页面");
            navigation.Items.AddRange(new object[] { Lang.T("组件总览"), Lang.T("显示与布局"), Lang.T("外观"), Lang.T("日历与课表"), Lang.T("便签与页面"), Lang.T("提醒"), Lang.T("数据与应用") });
            navigation.DrawItem += DrawNavigation;
            navigation.SelectedIndexChanged += delegate { ShowPage(navigation.SelectedIndex); };
            host = new Panel { Name = "settings-pages", Dock = DockStyle.Fill, Margin = Padding.Empty };
            main.Controls.Add(navigation, 0, 0); main.Controls.Add(host, 1, 0);
            Controls.Add(main); Controls.Add(status); Controls.Add(header);
            BuildOverview(); BuildLayout(); BuildAppearance(); BuildCalendar(); BuildNotebooks(); BuildReminders(); BuildData();
            app.DataChanged += RefreshData; app.StateChanged += RefreshData;
            refreshTimer = new Timer { Interval = 30000 }; refreshTimer.Tick += delegate { if (Visible) RefreshData(); }; refreshTimer.Start();
            navigation.SelectedIndex = 0; RefreshData(); ResumeLayout(true);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Rectangle area = Screen.FromControl(this).WorkingArea;
            MinimumSize = new Size(Math.Min(MinimumSize.Width, area.Width), Math.Min(MinimumSize.Height, area.Height));
            Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
            Location = new Point(Math.Max(area.Left, Math.Min(Left, area.Right - Width)), Math.Max(area.Top, Math.Min(Top, area.Bottom - Height)));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !cleanupComplete)
            {
                cleanupComplete = true;
                if (app != null) { app.DataChanged -= RefreshData; app.StateChanged -= RefreshData; }
                if (refreshTimer != null) { refreshTimer.Stop(); refreshTimer.Dispose(); }
            }
            base.Dispose(disposing);
        }

        private void DrawNavigation(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using (var brush = new SolidBrush(selected ? Color.FromArgb(230, 235, 250) : Ui.Background)) e.Graphics.FillRectangle(brush, e.Bounds);
            var rect = new Rectangle(e.Bounds.X + 15, e.Bounds.Y, e.Bounds.Width - 18, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, navigation.Items[e.Index].ToString(), Font, rect, selected ? Ui.Accent : Ui.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            if (selected) using (var brush = new SolidBrush(Ui.Accent)) e.Graphics.FillRectangle(brush, e.Bounds.X, e.Bounds.Y + 12, 3, e.Bounds.Height - 24);
        }

        private void ShowPage(int index)
        {
            if (index < 0 || index >= pages.Count) return;
            foreach (var page in pages) page.Visible = false;
            pages[index].Visible = true; pages[index].BringToFront(); RefreshData();
        }

        private Panel NewPage(string title, string description)
        {
            var page = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(2, 0, 10, 16), Visible = false, Name = "settings-page-" + pages.Count };
            var titleLabel = Ui.Label(title, 16F, Ui.Text); titleLabel.Margin = new Padding(0, 0, 0, 7);
            var info = Ui.Label(description, 9F, Ui.Muted); info.Margin = new Padding(0, 0, 0, 12);
            page.Controls.Add(titleLabel); page.Controls.Add(info);
            page.SizeChanged += delegate { ResizePage(page); };
            pages.Add(page); host.Controls.Add(page); return page;
        }

        private void ResizePage(Panel page)
        {
            if (layingOutPages) return;
            layingOutPages = true;
            try
            {
                int width = Math.Max(350, page.ClientSize.Width - page.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 4);
                int y = page.Padding.Top;
                foreach (Control c in page.Controls)
                {
                    c.MinimumSize = Size.Empty; c.MaximumSize = new Size(width, 0); c.Width = width;
                    c.PerformLayout();
                    TableLayoutPanel card = c as TableLayoutPanel;
                    int height = card == null ? c.GetPreferredSize(new Size(width, 0)).Height : ArrangeCard(card);
                    y += c.Margin.Top;
                    c.SetBounds(page.Padding.Left + page.AutoScrollPosition.X, y + page.AutoScrollPosition.Y, width, height);
                    y += height + c.Margin.Bottom;
                }
                page.AutoScrollMinSize = new Size(0, y + page.Padding.Bottom);
            }
            finally { layingOutPages = false; }
        }

        private static int ArrangeCard(TableLayoutPanel table)
        {
            table.AutoSize = false; table.Dock = DockStyle.None; table.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            table.PerformLayout();
            var heights = new int[table.RowCount];
            foreach (Control child in table.Controls)
            {
                TableLayoutPanel nested = child as TableLayoutPanel;
                FlowLayoutPanel flow = child as FlowLayoutPanel;
                if (nested != null) child.Height = ArrangeCard(nested);
                else if (flow != null)
                {
                    flow.AutoSize = false; flow.Dock = DockStyle.None; flow.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; flow.PerformLayout();
                    int bottom = flow.Padding.Top;
                    foreach (Control item in flow.Controls) bottom = Math.Max(bottom, item.Bottom + item.Margin.Bottom);
                    flow.Height = bottom + flow.Padding.Bottom;
                }
                int row = table.GetRow(child);
                if (row >= 0) heights[row] = Math.Max(heights[row], child.Height + child.Margin.Vertical);
            }
            for (int i = 0; i < heights.Length; i++) { table.RowStyles[i].SizeType = SizeType.Absolute; table.RowStyles[i].Height = heights[i]; }
            int height = table.Padding.Vertical + heights.Sum();
            table.Height = height; table.PerformLayout(); return height;
        }

        private TableLayoutPanel Card(Panel page, string title)
        {
            var card = new TableLayoutPanel { ColumnCount = 1, RowCount = 0, AutoSize = false, BackColor = Color.White, Padding = new Padding(16, 12, 16, 13), Margin = new Padding(0, 0, 0, 12), Width = 650 };
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var label = Ui.Label(title, 11F, Ui.Text); label.Margin = new Padding(0, 0, 0, 11);
            Add(card, label); page.Controls.Add(card); return card;
        }

        private static void Add(TableLayoutPanel card, Control child)
        {
            int row = card.RowCount++; card.RowStyles.Add(new RowStyle(SizeType.AutoSize)); child.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top; card.Controls.Add(child, 0, row);
        }

        private FlowLayoutPanel Buttons(params Control[] controls)
        {
            var row = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Margin = new Padding(0, 4, 0, 0), Dock = DockStyle.Fill };
            row.Controls.AddRange(controls); return row;
        }

        private Button ActionButton(string name, string text, Action action)
        {
            var button = Ui.Button(text, delegate { if (!syncing) Run(action); }); button.Name = name; button.AccessibleName = text; return button;
        }

        private static CheckBox Check(string name, string text)
        {
            return new CheckBox { Name = name, Text = text, AutoSize = true, Margin = new Padding(2, 7, 13, 6) };
        }

        private static ComboBox Combo(string name, params Choice[] choices)
        {
            var combo = new ComboBox { Name = name, DropDownStyle = ComboBoxStyle.DropDownList, Width = 220, Margin = new Padding(0, 4, 0, 5) };
            combo.Items.AddRange(choices); return combo;
        }

        private static NumericUpDown Number(string name, decimal minimum, decimal maximum, decimal increment)
        {
            return new NumericUpDown { AutoScaleMode = AutoScaleMode.None, Name = name, Minimum = minimum, Maximum = maximum, Increment = increment, Width = 110, Margin = new Padding(0, 4, 0, 5) };
        }

        private static void SetNumber(NumericUpDown control, decimal value) { control.Value = Math.Max(control.Minimum, Math.Min(control.Maximum, value)); }

        private static string SelectedId(ComboBox combo) { var item = combo.SelectedItem as Choice; return item == null ? "" : item.Id; }
        private static void SelectId(ComboBox combo, string id)
        {
            for (int i = 0; i < combo.Items.Count; i++) if (((Choice)combo.Items[i]).Id == id) { if (combo.SelectedIndex != i) combo.SelectedIndex = i; return; }
            combo.SelectedIndex = combo.Items.Count == 0 ? -1 : 0;
        }

        private void Field(TableLayoutPanel card, string label, Control control)
        {
            var row = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Dock = DockStyle.Fill, Height = 1 };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var caption = Ui.Label(label, 9F, Ui.Text); caption.Margin = new Padding(0, 8, 6, 5);
            row.Controls.Add(caption, 0, 0); control.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            if (!(control is FlowLayoutPanel)) control.MinimumSize = new Size(control.Width, 0);
            row.Controls.Add(control, 1, 0); Add(card, row);
        }

        private static ListView List(string name, int height, params string[] columns)
        {
            var list = new ListView { Name = name, Height = height, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false, ShowItemToolTips = true, BorderStyle = BorderStyle.FixedSingle, HeaderStyle = ColumnHeaderStyle.Nonclickable, Margin = new Padding(0, 5, 0, 5) };
            foreach (string column in columns) list.Columns.Add(column, columns.Length == 2 ? 240 : 145);
            list.SizeChanged += delegate { if (list.Columns.Count > 0) { int available = Math.Max(180, list.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4); for (int i = 0; i < list.Columns.Count; i++) list.Columns[i].Width = i == 0 ? available * 2 / (list.Columns.Count + 1) : available / (list.Columns.Count + 1); } };
            return list;
        }

        private static string SelectedTag(ListView list) { return list.SelectedItems.Count == 0 ? "" : Convert.ToString(list.SelectedItems[0].Tag, CultureInfo.InvariantCulture); }
        private static void SelectTag(ListView list, string id)
        {
            foreach (ListViewItem item in list.Items) if ((string)item.Tag == id) { item.Selected = true; return; }
            if (list.Items.Count > 0) list.Items[0].Selected = true;
        }

        private void Run(Action action)
        {
            try { action(); status.Text = Lang.T("更改已应用并自动保存。关闭设置中心后，组件与提醒继续运行。"); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Lang.T("无法完成操作"), MessageBoxButtons.OK, MessageBoxIcon.Warning); RefreshData(); }
        }

        private string ComponentName(string key) { var book = app.Data.Books.FirstOrDefault(b => b.Id == key); return key == "calendar" ? Lang.T("日历 · 课表") : book == null ? key : book.Name; }

        private void BuildOverview()
        {
            var page = NewPage(Lang.T("组件总览"), Lang.T("三个独立窗口，随时显示、隐藏或定位。隐藏窗口后，所有页面中的任务仍会提醒。"));
            var all = Card(page, Lang.T("桌面工作区"));
            Add(all, Buttons(ActionButton("show-all", Lang.T("显示全部"), app.ShowAll), ActionButton("hide-all", Lang.T("隐藏全部"), app.HideAll), ActionButton("overview-rescue-windows", Lang.T("找回当前屏幕"), app.RescueWindows)));
            foreach (string keyValue in new[] { "calendar", "todo", "ddl" })
            {
                string key = keyValue; var card = Card(page, key == "calendar" ? Lang.T("日历 · 课表") : key == "todo" ? "Todo" : "DDL");
                componentNames[key] = (Label)card.Controls[0];
                var detail = Ui.Label("", 9F, Ui.Muted); detail.Margin = new Padding(0, 0, 0, 7); overviewDetails[key] = detail; Add(card, detail);
                var toggle = Check("overview-" + key + "-toggle", Lang.T("在桌面显示")); visibleChecks[key] = toggle;
                toggle.CheckedChanged += delegate { if (!syncing) Run(delegate { app.SetWidgetVisible(key, toggle.Checked); }); };
                Add(card, Buttons(toggle, ActionButton("locate-" + key, Lang.T("快速定位"), delegate { app.LocateWidget(key); })));
            }
        }

        private void BuildLayout()
        {
            var page = NewPage(Lang.T("显示与布局"), Lang.T("位置与尺寸使用屏幕像素。锁定位置后，组件不再接受拖动和调整大小；仍可在这里修改。"));
            var mode = Card(page, Lang.T("窗口模式"));
            widgetMode = Combo("widget-mode", new Choice("Desktop", Lang.T("桌面组件模式")), new Choice("Standard", Lang.T("标准窗口"))); Field(mode, Lang.T("组件窗口"), widgetMode);
            widgetMode.SelectedIndexChanged += delegate { if (!syncing) Run(delegate { app.SetWidgetMode(SelectedId(widgetMode)); }); };
            widgetCorners = Combo("widget-corners", new Choice("Round", Lang.T("圆角")), new Choice("Square", Lang.T("直角"))); Field(mode, Lang.T("组件四角"), widgetCorners);
            widgetCorners.SelectedIndexChanged += delegate { if (!syncing) Run(delegate { app.SetWidgetCorners(SelectedId(widgetCorners)); }); };
            Add(mode, Ui.Label(Lang.T("桌面组件模式下生效。平时没有阴影；拖动或调整大小时显示系统阴影（Windows 11 上此时为系统圆角）。"), 8.5F, Ui.Muted));
            Add(mode, Ui.Label(Lang.T("桌面组件模式：没有标题栏，不出现在任务栏和 Alt+Tab 里。拖动顶栏移动，拖动边缘调整大小。"), 8.5F, Ui.Muted));
            Add(mode, Ui.Label(Lang.T("点击组件时它浮到前面；切到别的程序后，它自动回到其他窗口下面。勾选「始终置顶」的组件不受影响。"), 8.5F, Ui.Muted));
            hotkeyBox = new TextBox { Name = "show-hotkey", Width = 220, ReadOnly = true, BackColor = Color.White, Margin = new Padding(0, 4, 8, 5), ShortcutsEnabled = false, AccessibleName = Lang.T("显示或收起全部组件的快捷键，点击后按下新的组合键") };
            hotkeyBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                e.SuppressKeyPress = true; e.Handled = true;
                string combination = HotkeySpec.Format((e.Control ? HotkeySpec.Ctrl : 0) | (e.Alt ? HotkeySpec.Alt : 0) | (e.Shift ? HotkeySpec.Shift : 0), (int)e.KeyCode);
                HotkeySpec parsed;
                if (combination != "" && HotkeySpec.TryParse(combination, out parsed)) Run(delegate { app.SetShowHotkey(combination); RefreshData(); });
            };
            Field(mode, Lang.T("显示 / 收起快捷键"), Buttons(hotkeyBox, ActionButton("hotkey-disable", Lang.T("停用"), delegate { app.SetShowHotkey(""); RefreshData(); }), ActionButton("hotkey-default", Lang.T("恢复默认"), delegate { app.SetShowHotkey(HotkeySpec.Default); RefreshData(); })));
            hotkeyStatus = Ui.Label("", 8.5F, Ui.Muted); hotkeyStatus.Name = "hotkey-status"; Add(mode, hotkeyStatus);
            Add(mode, Ui.Label(Lang.T("点一下输入框，再按下新的组合键即可修改。按一次把组件浮到前面，再按一次收回去。"), 8.5F, Ui.Muted));
            Add(mode, Ui.Label(Lang.T("单击托盘图标也能把组件浮到前面。"), 8.5F, Ui.Muted));
            var operations = Card(page, Lang.T("布局管理"));
            Add(operations, Buttons(ActionButton("save-layout", Lang.T("保存当前布局"), app.SaveLayout), ActionButton("restore-saved-layout", Lang.T("恢复已保存布局"), app.RestoreSavedLayout), ActionButton("reset-layout", Lang.T("恢复默认布局"), app.ResetLayout), ActionButton("rescue-windows", Lang.T("将组件找回当前屏幕"), app.RescueWindows)));
            layoutSaved = Ui.Label("", 8.5F, Ui.Muted); layoutSaved.Margin = new Padding(0, 8, 0, 0); Add(operations, layoutSaved);
            foreach (string keyValue in new[] { "calendar", "todo", "ddl" })
            {
                string key = keyValue; var card = Card(page, key == "calendar" ? Lang.T("日历") : key == "todo" ? Lang.T("Todo 便签") : Lang.T("DDL 便签"));
                var editor = new LayoutEditors(); layoutEditors[key] = editor;
                editor.X = Number("layout-" + key + "-x", -100000, 100000, 10); editor.Y = Number("layout-" + key + "-y", -100000, 100000, 10);
                editor.Width = Number("layout-" + key + "-width", key == "calendar" ? 680 : 350, 20000, 10); editor.Height = Number("layout-" + key + "-height", key == "calendar" ? 420 : 430, 20000, 10);
                var position = Buttons(Ui.Label("X", 9F, Ui.Muted), editor.X, Ui.Label("Y", 9F, Ui.Muted), editor.Y);
                var size = Buttons(Ui.Label(Lang.T("宽"), 9F, Ui.Muted), editor.Width, Ui.Label(Lang.T("高"), 9F, Ui.Muted), editor.Height);
                Field(card, Lang.T("窗口位置"), position); Field(card, Lang.T("窗口尺寸"), size);
                editor.Pin = Check("layout-" + key + "-pin", Lang.T("始终置顶")); editor.Locked = Check("layout-" + key + "-locked", Lang.T("锁定位置与尺寸"));
                Add(card, Buttons(editor.Pin, editor.Locked));
                editor.X.ValueChanged += delegate { ChangeWindow(key, delegate(WindowState w) { w.X = (int)editor.X.Value; }); };
                editor.Y.ValueChanged += delegate { ChangeWindow(key, delegate(WindowState w) { w.Y = (int)editor.Y.Value; }); };
                editor.Width.ValueChanged += delegate { ChangeWindow(key, delegate(WindowState w) { w.Width = (int)editor.Width.Value; }); };
                editor.Height.ValueChanged += delegate { ChangeWindow(key, delegate(WindowState w) { w.Height = (int)editor.Height.Value; }); };
                editor.Pin.CheckedChanged += delegate { ChangeWindow(key, delegate(WindowState w) { w.TopMost = editor.Pin.Checked; }); };
                editor.Locked.CheckedChanged += delegate { ChangeWindow(key, delegate(WindowState w) { w.PositionLocked = editor.Locked.Checked; }); };
            }
        }

        private void ChangeWindow(string key, Action<WindowState> change)
        {
            if (syncing) return;
            Run(delegate { WindowState current = app.Data.Windows[key]; var value = new WindowState { X = current.X, Y = current.Y, Width = current.Width, Height = current.Height, Visible = current.Visible, TopMost = current.TopMost, PositionLocked = current.PositionLocked }; change(value); app.UpdateWindow(key, value); });
        }

        private void BuildAppearance()
        {
            var page = NewPage(Lang.T("外观"), Lang.T("颜色、透明度与字号会实时应用到桌面组件。单个组件可以覆盖全局设置，也可以重新跟随全局。"));
            var language = Card(page, Lang.T("界面语言 · Language"));
            uiLanguage = Combo("ui-language", new Choice("zh-CN", Lang.T("中文")), new Choice("en", "English")); Field(language, Lang.T("界面语言"), uiLanguage);
            uiLanguage.SelectedIndexChanged += delegate { if (!syncing) { ChangeSetting(delegate { app.Data.Settings.Language = SelectedId(uiLanguage); }); UpdateLanguageNote(); } };
            languageNote = Ui.Label(Lang.T("切换语言后需要重启程序 · Restart DeskStudy to apply"), 8.5F, Ui.Muted); Add(language, languageNote);
            restartButton = ActionButton("restart-for-language", Lang.T("立即重启 · Restart now"), app.Restart); Add(language, Buttons(restartButton));
            var layouts = Card(page, Lang.T("便签布局 · Todo 与 DDL 同步切换"));
            var choices = Buttons();
            string[] ids = SettingsLogic.NotebookLayouts, titles = { Lang.T("原始外观"), Lang.T("轻量卡片"), Lang.T("纸页本"), Lang.T("清爽卡片"), Lang.T("手账纸页") }, descriptions = { Lang.T("顶部翻页，保留文字区与完整操作按钮。"), Lang.T("任务优先，折叠备注，固定底部翻页。"), Lang.T("暖纸色与页边线，侧边目录可滚动。"), Lang.T("白底大标题，细线任务列表，界面最简洁。"), Lang.T("米色纸页与窄侧页签，更有手账感。") };
            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i]; var choice = new NotebookLayoutPreview(id, titles[i], descriptions[i]);
                choice.Click += delegate { if (!syncing) Run(delegate { app.Data.Settings.NotebookLayout = id; app.SettingsChanged(); }); };
                notebookLayouts.Add(choice); choices.Controls.Add(choice);
            }
            Add(layouts, choices);
            Add(layouts, Ui.Label(Lang.T("预览示意实际结构。切换保留各自当前页、内容与个性设置；输入法组合输入结束后应用。"), 8.5F, Ui.Muted));
            Add(layouts, Ui.Label(Lang.T("布局提供默认风格；明确设置的字号、颜色与透明度优先，仍遵循全局／单组件设置。"), 8.5F, Ui.Muted));
            var card = Card(page, Lang.T("实时预览"));
            appearanceTarget = Combo("appearance-target", new Choice("global", Lang.T("全局 · 所有跟随的组件")), new Choice("calendar", Lang.T("日历")), new Choice("todo", Lang.T("Todo 便签")), new Choice("ddl", Lang.T("DDL 便签")));
            Field(card, Lang.T("调整范围"), appearanceTarget);
            followingGlobal = Check("appearance-follow-global", Lang.T("跟随全局外观")); Field(card, Lang.T("组件设置"), followingGlobal);
            theme = Combo("appearance-theme", new Choice("Light", Lang.T("浅色")), new Choice("Dark", Lang.T("深色"))); Field(card, Lang.T("主题"), theme);
            backgroundColor = ActionButton("appearance-background", Lang.T("选择背景颜色"), ChooseBackground); backgroundColor.Width = 220; backgroundColor.AutoSize = false; Field(card, Lang.T("背景颜色"), backgroundColor);
            opacity = Number("appearance-opacity", 35, 100, 5); Field(card, Lang.T("不透明度（%）"), opacity);
            fontSize = Number("appearance-font", 8, 14, 0.5M); fontSize.DecimalPlaces = 1; Field(card, Lang.T("字号（pt）"), fontSize);
            appearanceHint = Ui.Label("", 9F, Ui.Muted); appearanceHint.Margin = new Padding(0, 12, 0, 0); Add(card, appearanceHint);
            Add(card, Buttons(ActionButton("reset-current-appearance", Lang.T("恢复此范围的默认外观"), delegate { app.ResetAppearance(SelectedId(appearanceTarget) == "global" ? null : SelectedId(appearanceTarget)); }), ActionButton("reset-all-appearance", Lang.T("全部恢复默认外观"), delegate { app.ResetAppearance("all"); })));
            Add(card, Ui.Label(Lang.T("重置外观会保留所有课程、任务与历史页面。"), 8.5F, Ui.Muted));
            appearanceTarget.SelectedIndexChanged += delegate { if (!syncing) RefreshData(); };
            followingGlobal.CheckedChanged += delegate
            {
                if (syncing || SelectedId(appearanceTarget) == "global") return;
                Run(delegate { string key = SelectedId(appearanceTarget); if (followingGlobal.Checked) app.Data.Settings.AppearanceOverrides.Remove(key); else app.Data.Settings.AppearanceOverrides[key] = CopyAppearance(app.Data.Settings.GlobalAppearance); app.SettingsChanged(); });
            };
            theme.SelectedIndexChanged += delegate { ChangeAppearance(delegate(AppearanceOptions value) { value.Theme = SelectedId(theme); value.BackgroundColor = value.Theme == "Dark" ? "#252B36" : "#F7F8FB"; SettingsLogic.MarkAppearanceCustomized(value, "Theme"); SettingsLogic.MarkAppearanceCustomized(value, "BackgroundColor"); }); };
            opacity.ValueChanged += delegate { ChangeAppearance(delegate(AppearanceOptions value) { value.Opacity = (double)opacity.Value / 100.0; SettingsLogic.MarkAppearanceCustomized(value, "Opacity"); }); };
            fontSize.ValueChanged += delegate { ChangeAppearance(delegate(AppearanceOptions value) { value.FontSize = (float)fontSize.Value; SettingsLogic.MarkAppearanceCustomized(value, "FontSize"); }); };
            appearanceTarget.SelectedIndex = 0;
        }

        private static AppearanceOptions CopyAppearance(AppearanceOptions value) { return SettingsLogic.CopyAppearance(value); }
        private void ChangeAppearance(Action<AppearanceOptions> change)
        {
            if (syncing) return;
            Run(delegate { string key = SelectedId(appearanceTarget); AppearanceOptions value; if (key == "global") value = app.Data.Settings.GlobalAppearance; else { if (!app.Data.Settings.AppearanceOverrides.TryGetValue(key, out value)) { value = CopyAppearance(app.Data.Settings.GlobalAppearance); app.Data.Settings.AppearanceOverrides[key] = value; } } change(value); app.SettingsChanged(); });
        }
        private void ChooseBackground()
        {
            using (var dialog = new ColorDialog { FullOpen = true, Color = backgroundColor.BackColor }) if (dialog.ShowDialog(this) == DialogResult.OK) ChangeAppearance(delegate(AppearanceOptions value) { value.BackgroundColor = "#" + dialog.Color.R.ToString("X2") + dialog.Color.G.ToString("X2") + dialog.Color.B.ToString("X2"); SettingsLogic.MarkAppearanceCustomized(value, "BackgroundColor"); });
        }

        private static DateTimePicker DatePicker(string name)
        {
            return new DateTimePicker { Name = name, Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd", ShowCheckBox = false, Width = 220, Margin = new Padding(0, 4, 0, 5) };
        }
        private static string DateValue(DateTimePicker value) { return value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
        private static void SetDate(DateTimePicker picker, string value)
        {
            DateTime parsed; bool present = DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed);
            if (!picker.ContainsFocus && present && parsed >= picker.MinDate && parsed <= picker.MaxDate) picker.Value = parsed;
        }
        private void BuildCalendar()
        {
            var page = NewPage(Lang.T("日历与课表"), Lang.T("设定学期节奏与桌面日历显示。下方统一管理整个课程系列，临时停课或调课可在日历中选择“仅这一次”。"));
            var card = Card(page, Lang.T("日历与学期默认值"));
            defaultView = Combo("calendar-default-view", new Choice("WorkWeek", Lang.T("工作周（周一至周五）")), new Choice("Week", Lang.T("周视图")), new Choice("Month", Lang.T("月视图"))); Field(card, Lang.T("日历视图"), defaultView);
            weekStart = Combo("calendar-week-start", new Choice("1", Lang.T("星期一")), new Choice("0", Lang.T("星期日")), new Choice("2", Lang.T("星期二")), new Choice("3", Lang.T("星期三")), new Choice("4", Lang.T("星期四")), new Choice("5", Lang.T("星期五")), new Choice("6", Lang.T("星期六"))); Field(card, Lang.T("一周起始日"), weekStart);
            semesterStart = DatePicker("semester-start"); semesterEnd = DatePicker("semester-end"); teachingWeekOne = DatePicker("teaching-week-one");
            Field(card, Lang.T("学期开始"), semesterStart); Field(card, Lang.T("学期结束"), semesterEnd); Field(card, Lang.T("教学第 1 周起点"), teachingWeekOne);
            Add(card, Ui.Label(Lang.T("日历会记住你上次在组件上选的视图，下次打开仍是它。"), 8.5F, Ui.Muted));
            Add(card, Ui.Label(Lang.T("学期与教学周用于新增课程的默认循环范围；有效日期会自动保存。"), 8.5F, Ui.Muted));
            defaultView.SelectedIndexChanged += delegate { ChangeSetting(delegate { string view = SelectedId(defaultView); app.Data.Settings.Calendar.DefaultView = view == "Month" ? "Month" : "Week"; app.Data.Settings.Calendar.WorkWeek = view == "WorkWeek"; }); };
            weekStart.SelectedIndexChanged += delegate { ChangeSetting(delegate { app.Data.Settings.Calendar.WeekStartDay = Int32.Parse(SelectedId(weekStart), CultureInfo.InvariantCulture); }); };
            semesterStart.ValueChanged += delegate { SaveCalendarDates(); }; semesterEnd.ValueChanged += delegate { SaveCalendarDates(); }; teachingWeekOne.ValueChanged += delegate { SaveCalendarDates(); };
            var series = Card(page, Lang.T("课程与循环系列")); courses = List("calendar-series", 195, Lang.T("课程名称"), Lang.T("日期 / 周期"), Lang.T("时间")); Add(series, courses);
            Add(series, Buttons(ActionButton("new-calendar-series", Lang.T("添加课程"), delegate { app.EditCalendarSeries(null); }), ActionButton("edit-calendar-series", Lang.T("编辑整个系列"), delegate { string id = SelectedTag(courses); if (id != "") app.EditCalendarSeries(id); }), ActionButton("delete-calendar-series", Lang.T("删除整个系列"), DeleteSeries)));
            courses.DoubleClick += delegate { string id = SelectedTag(courses); if (id != "") Run(delegate { app.EditCalendarSeries(id); }); };
        }
        private void SaveCalendarDates()
        {
            if (syncing) return;
            calendarDatesPending = semesterEnd.Value.Date < semesterStart.Value.Date;
            if (calendarDatesPending) { status.Text = Lang.T("学期结束日期应晚于或等于开始日期；请调整日期后自动保存。"); return; }
            ChangeSetting(delegate { app.Data.Settings.Calendar.SemesterStart = DateValue(semesterStart); app.Data.Settings.Calendar.SemesterEnd = DateValue(semesterEnd); app.Data.Settings.Calendar.TeachingWeekOne = DateValue(teachingWeekOne); });
        }
        // The restart button only shows while the chosen language differs from the one in use.
        private void UpdateLanguageNote()
        {
            if (restartButton == null) return;
            bool pending = app.Data.Settings.Language != Lang.Current;
            restartButton.Visible = pending;
            languageNote.Text = Lang.T(pending ? "重启后切换为所选语言 · The new language applies after a restart" : "切换语言后需要重启程序 · Restart DeskStudy to apply");
        }
        private void ChangeSetting(Action action) { if (!syncing) Run(delegate { action(); app.SettingsChanged(); }); }
        private void DeleteSeries()
        {
            string id = SelectedTag(courses); var value = app.Data.Events.FirstOrDefault(e => e.Id == id); if (value == null) return;
            if (MessageBox.Show(this, Lang.T("删除“{0}”整个系列及所有单次调整？", value.Title), Lang.T("删除整个课程系列"), MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK) { app.Data.Events.Remove(value); app.Save(); }
        }

        private Notebook CurrentBook() { string id = SelectedId(bookChoice); return app.Data.Books.FirstOrDefault(b => b.Id == id); }
        private NotePage SelectedPage() { var book = CurrentBook(); string id = SelectedTag(pageList); return book == null ? null : book.Pages.FirstOrDefault(p => p.Id == id); }
        private void BuildNotebooks()
        {
            var page = NewPage(Lang.T("便签与页面"), Lang.T("归档会将页面收进历史列表，内容与完成状态完整保留。归档页中的未完成任务仍然提醒。"));
            var card = Card(page, Lang.T("便签与桌面页面")); bookChoice = Combo("notebook-choice"); Field(card, Lang.T("选择便签"), bookChoice);
            bookName = new TextBox { Name = "notebook-name", Width = 230, MaxLength = 60, Margin = new Padding(0, 6, 6, 0) };
            Field(card, Lang.T("便签名称"), Buttons(bookName, ActionButton("rename-notebook", Lang.T("保存名称"), delegate { var book = CurrentBook(); if (book != null && !String.IsNullOrWhiteSpace(bookName.Text)) app.RenameBook(book.Id, bookName.Text.Trim()); })));
            desktopPage = Combo("desktop-current-page"); desktopPage.Width = 330; Field(card, Lang.T("桌面当前页"), desktopPage);
            bookChoice.SelectedIndexChanged += delegate { if (!syncing) { pageSignature = ""; RefreshData(); } };
            desktopPage.SelectedIndexChanged += delegate { if (!syncing) { var book = CurrentBook(); string id = SelectedId(desktopPage); if (book != null && id != "") Run(delegate { app.SelectPage(book.Id, id); }); } };
            var pageCard = Card(page, Lang.T("全部页面 · 包含归档")); pageList = List("notebook-pages", 220, Lang.T("页面标题"), Lang.T("状态"), Lang.T("创建日期")); Add(pageCard, pageList);
            pageList.SelectedIndexChanged += delegate { if (!syncing) RefreshPageSelection(); };
            pageName = new TextBox { Name = "page-rename-title", Width = 260, MaxLength = 140, Margin = new Padding(0, 6, 6, 0) };
            Add(pageCard, Buttons(pageName, ActionButton("rename-page", Lang.T("重命名页面"), delegate { var book = CurrentBook(); var selected = SelectedPage(); if (book != null && selected != null && !String.IsNullOrWhiteSpace(pageName.Text)) app.RenamePage(book.Id, selected.Id, pageName.Text.Trim()); })));
            Add(pageCard, Buttons(ActionButton("settings-new-page", Lang.T("新建页面"), delegate { var book = CurrentBook(); if (book != null) { app.AddPage(book.Id); SelectTag(pageList, book.CurrentPageId); RefreshPageSelection(); } }), ActionButton("page-move-up", Lang.T("上移"), delegate { MoveSelectedPage(-1); }), ActionButton("page-move-down", Lang.T("下移"), delegate { MoveSelectedPage(1); }), ActionButton("archive-page", Lang.T("归档 / 取消归档"), delegate { var book = CurrentBook(); var selected = SelectedPage(); if (book != null && selected != null) app.SetPageArchived(book.Id, selected.Id, !selected.Archived); }), ActionButton("show-selected-page", Lang.T("显示在桌面"), delegate { var book = CurrentBook(); var selected = SelectedPage(); if (book != null && selected != null) { app.SelectPage(book.Id, selected.Id); app.LocateWidget(book.Id); } })));
            pageDetail = Ui.Label("", 8.5F, Ui.Muted); pageDetail.Margin = new Padding(0, 9, 0, 0); Add(pageCard, pageDetail);
        }
        private void MoveSelectedPage(int delta) { var book = CurrentBook(); var selected = SelectedPage(); if (book != null && selected != null) app.MovePage(book.Id, selected.Id, delta); }
        private void RefreshPageSelection()
        {
            var selected = SelectedPage(); if (selected == null) { if (!pageName.ContainsFocus) pageName.Text = ""; pageDetail.Text = Lang.T("选择页面以管理名称、顺序与归档状态。"); return; }
            if (!pageName.ContainsFocus) pageName.Text = selected.Title;
            pageDetail.Text = Lang.T("{0} 项任务 · {1} 项已完成", selected.Tasks.Count, selected.Tasks.Count(t => t.Completed)) + " · " + Lang.T(selected.Archived ? "已归档，未完成任务继续提醒" : "未归档");
        }

        private static DateTimePicker TimePicker(string name)
        {
            return new DateTimePicker { Name = name, Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 120, Margin = new Padding(0, 4, 0, 5) };
        }
        private static void SetTime(DateTimePicker picker, string value)
        {
            DateTime parsed; if (!picker.ContainsFocus && DateTime.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)) picker.Value = DateTime.Today.Add(parsed.TimeOfDay);
        }
        private void BuildReminders()
        {
            var page = NewPage(Lang.T("提醒"), Lang.T("提醒会检查两本便签的所有页面，包括隐藏和归档页面。完成任务后停止后续提醒，错过的提醒会在恢复后汇总。"));
            var card = Card(page, Lang.T("提醒方式")); leadMinutes = Number("reminder-default-lead", 0, 525600, 5); Field(card, Lang.T("默认提前（分钟）"), leadMinutes);
            dateOnlyReminder = TimePicker("reminder-dateonly-time"); Field(card, Lang.T("只有日期的任务"), Buttons(dateOnlyReminder, Ui.Label(Lang.T("当天这个时间提醒一次"), 9F, Ui.Muted)));
            dateOnlyReminder.ValueChanged += delegate { ChangeSetting(delegate { app.Data.Settings.Reminders.DateOnlyReminderTime = dateOnlyReminder.Value.ToString("HH:mm", CultureInfo.InvariantCulture); }); };
            quietEnabled = Check("reminder-quiet-enabled", Lang.T("启用免打扰时段")); Field(card, Lang.T("免打扰"), quietEnabled);
            quietStart = TimePicker("reminder-quiet-start"); quietEnd = TimePicker("reminder-quiet-end"); Field(card, Lang.T("开始 / 结束"), Buttons(quietStart, Ui.Label(Lang.T("至"), 9F, Ui.Muted), quietEnd));
            soundEnabled = Check("reminder-sound-enabled", Lang.T("播放提醒声音")); Field(card, Lang.T("声音"), soundEnabled);
            Add(card, Buttons(ActionButton("test-notification", Lang.T("发送测试通知"), app.SendTestNotification)));
            Add(card, Ui.Label(Lang.T("默认提前时间应用于新任务。免打扰结束后汇总待发提醒；测试通知可随时发送。"), 8.5F, Ui.Muted));
            leadMinutes.ValueChanged += delegate { ChangeSetting(delegate { app.Data.Settings.Reminders.DefaultLeadMinutes = (int)leadMinutes.Value; }); };
            quietEnabled.CheckedChanged += delegate { ChangeSetting(delegate { app.Data.Settings.Reminders.QuietHoursEnabled = quietEnabled.Checked; }); };
            quietStart.ValueChanged += delegate { ChangeSetting(delegate { app.Data.Settings.Reminders.QuietStart = quietStart.Value.ToString("HH:mm", CultureInfo.InvariantCulture); }); };
            quietEnd.ValueChanged += delegate { ChangeSetting(delegate { app.Data.Settings.Reminders.QuietEnd = quietEnd.Value.ToString("HH:mm", CultureInfo.InvariantCulture); }); };
            soundEnabled.CheckedChanged += delegate { ChangeSetting(delegate { app.Data.Settings.Reminders.SoundEnabled = soundEnabled.Checked; }); };
            var all = Card(page, Lang.T("所有未完成的截止任务")); pendingTasks = List("pending-reminders", 270, Lang.T("任务"), Lang.T("便签 / 页面"), Lang.T("截止时间"), Lang.T("状态")); Add(all, pendingTasks);
            Add(all, Ui.Label(Lang.T("此列表包含已到期或已提醒但尚未完成的任务。双击可打开所在页面。"), 8.5F, Ui.Muted));
            pendingTasks.DoubleClick += delegate
            {
                string id = SelectedTag(pendingTasks); if (id == "") return;
                foreach (var book in app.Data.Books) foreach (var note in book.Pages) if (note.Tasks.Any(t => t.Id == id)) { string bookId = book.Id, pageId = note.Id; Run(delegate { app.SelectPage(bookId, pageId); app.LocateWidget(bookId); }); return; }
            };
        }

        private void BuildData()
        {
            var page = NewPage(Lang.T("数据与应用"), Lang.T("所有数据仅保存在本机。导入与恢复备份前，会先保留当前内容，便于回退。"));
            var card = Card(page, Lang.T("本地保存"));
            saveDelay = Combo("autosave-delay", new Choice("450", Lang.T("即时 · 编辑后约 0.45 秒")), new Choice("1000", Lang.T("编辑后 1 秒")), new Choice("2500", Lang.T("编辑后 2.5 秒"))); Field(card, Lang.T("自动保存"), saveDelay);
            Add(card, Ui.Label(Lang.T("自动保存始终启用；退出时立即保存。"), 8.5F, Ui.Muted));
            startup = Check("launch-at-startup", Lang.T("登录 Windows 后自动启动")); Add(card, startup);
            storagePath = Ui.Label("", 8.5F, Ui.Muted); storagePath.AutoSize = false; storagePath.Height = 50; storagePath.Margin = new Padding(0, 8, 0, 5); Add(card, storagePath);
            Add(card, Buttons(ActionButton("export-data", Lang.T("导出全部数据…"), app.ExportData), ActionButton("import-data", Lang.T("导入数据…"), app.ImportData)));
            saveDelay.SelectedIndexChanged += delegate { ChangeSetting(delegate { app.Data.Settings.AutoSaveDelayMs = Int32.Parse(SelectedId(saveDelay), CultureInfo.InvariantCulture); }); };
            startup.CheckedChanged += delegate { if (!syncing) Run(delegate { app.SetStartup(startup.Checked); }); };
            var backupCard = Card(page, Lang.T("备份与恢复")); lastBackup = Ui.Label("", 9F, Ui.Muted); Add(backupCard, lastBackup);
            Add(backupCard, Buttons(ActionButton("backup-now", Lang.T("立即备份"), app.BackupNow), ActionButton("refresh-backups", Lang.T("刷新列表"), delegate { backupSignature = ""; RefreshData(); })));
            backups = List("backup-list", 205, Lang.T("备份文件"), Lang.T("保存时间")); Add(backupCard, backups);
            Add(backupCard, Buttons(ActionButton("restore-backup", Lang.T("恢复所选备份…"), delegate { string path = SelectedTag(backups); if (path != "" && MessageBox.Show(this, Lang.T("将恢复所选备份中的课表、便签、设置和布局。恢复前会先保留当前数据。\n\n") + Path.GetFileName(path), Lang.T("恢复备份"), MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK) app.RestoreBackup(path); })));
            Add(backupCard, Ui.Label(Lang.T("恢复前将创建当前数据的安全备份。布局与外观重置不删除课程、任务或历史页面。"), 8.5F, Ui.Muted));
        }

        public void RefreshData()
        {
            if (syncing || IsDisposed || saveDelay == null) return;
            syncing = true;
            try
            {
                foreach (string key in new[] { "calendar", "todo", "ddl" })
                {
                    WindowState w; if (!app.Data.Windows.TryGetValue(key, out w)) continue;
                    visibleChecks[key].Checked = w.Visible; componentNames[key].Text = ComponentName(key);
                    overviewDetails[key].Text = (w.Visible ? Lang.T("已显示") : Lang.T("已隐藏")) + " · " + w.Width + " × " + w.Height + (w.TopMost ? Lang.T(" · 始终置顶") : "") + (w.PositionLocked ? Lang.T(" · 位置已锁定") : "");
                    var editor = layoutEditors[key]; SetNumber(editor.X, w.X); SetNumber(editor.Y, w.Y); SetNumber(editor.Width, w.Width); SetNumber(editor.Height, w.Height); editor.Pin.Checked = w.TopMost; editor.Locked.Checked = w.PositionLocked;
                }
                SelectId(uiLanguage, app.Data.Settings.Language); UpdateLanguageNote();
                SelectId(widgetMode, app.Data.Settings.WidgetMode); SelectId(widgetCorners, app.Data.Settings.WidgetCorners);
                hotkeyBox.Text = app.Data.Settings.ShowHotkey == "" ? Lang.T("已停用") : app.Data.Settings.ShowHotkey;
                hotkeyStatus.Text = app.HotkeyStatus ?? "";
                layoutSaved.Text = String.IsNullOrEmpty(app.Data.Settings.SavedLayoutUtc) ? Lang.T("尚未保存自定义布局。重置与找回不会影响内容。") : Lang.T("已保存布局：{0}。重置与找回不会影响内容。", LocalStamp(app.Data.Settings.SavedLayoutUtc));
                string target = SelectedId(appearanceTarget); if (String.IsNullOrEmpty(target)) target = "global";
                foreach (var choice in notebookLayouts) choice.SelectedLayout = choice.LayoutId == app.Data.Settings.NotebookLayout;
                AppearanceOptions appearance = target == "global" ? app.Data.Settings.GlobalAppearance : SettingsLogic.EffectiveAppearance(app.Data, target);
                bool follows = target != "global" && !app.Data.Settings.AppearanceOverrides.ContainsKey(target);
                followingGlobal.Enabled = target != "global"; followingGlobal.Checked = follows;
                theme.Enabled = !follows; backgroundColor.Enabled = !follows; opacity.Enabled = !follows; fontSize.Enabled = !follows;
                SelectId(theme, appearance.Theme); SetNumber(opacity, (decimal)(appearance.Opacity * 100)); SetNumber(fontSize, (decimal)appearance.FontSize);
                Color color; try { color = ColorTranslator.FromHtml(appearance.BackgroundColor); } catch { color = Ui.Background; }
                backgroundColor.BackColor = color; backgroundColor.ForeColor = color.GetBrightness() < 0.5F ? Color.White : Ui.Text; backgroundColor.Text = appearance.BackgroundColor.ToUpperInvariant() + " · " + Lang.T("选择颜色");
                appearanceHint.Text = target == "global" ? Lang.T("正在调整全局外观，所有跟随全局的组件会同步变化。") : follows ? Lang.T("此组件正在跟随全局。取消勾选后可以单独调整。") : Lang.T("此组件使用独立外观。勾选“跟随全局外观”即可恢复同步。");
                SelectId(defaultView, app.Data.Settings.Calendar.WorkWeek && app.Data.Settings.Calendar.DefaultView == "Week" ? "WorkWeek" : app.Data.Settings.Calendar.DefaultView); SelectId(weekStart, app.Data.Settings.Calendar.WeekStartDay.ToString(CultureInfo.InvariantCulture));
                if (!calendarDatesPending) { SetDate(semesterStart, app.Data.Settings.Calendar.SemesterStart); SetDate(semesterEnd, app.Data.Settings.Calendar.SemesterEnd); SetDate(teachingWeekOne, app.Data.Settings.Calendar.TeachingWeekOne); }
                RefreshCourses(); RefreshBooks();
                SetNumber(leadMinutes, app.Data.Settings.Reminders.DefaultLeadMinutes); quietEnabled.Checked = app.Data.Settings.Reminders.QuietHoursEnabled;
                quietStart.Enabled = quietEnabled.Checked; quietEnd.Enabled = quietEnabled.Checked;
                SetTime(quietStart, app.Data.Settings.Reminders.QuietStart); SetTime(quietEnd, app.Data.Settings.Reminders.QuietEnd); SetTime(dateOnlyReminder, app.Data.Settings.Reminders.DateOnlyReminderTime); soundEnabled.Checked = app.Data.Settings.Reminders.SoundEnabled;
                RefreshPending(); SelectId(saveDelay, app.Data.Settings.AutoSaveDelayMs.ToString(CultureInfo.InvariantCulture)); startup.Checked = app.StartupEnabled;
                storagePath.Text = Lang.T("数据位置：{0}", app.Store.DirectoryPath) + (String.IsNullOrEmpty(app.StartupWarning) ? "" : "\n" + app.StartupWarning);
                lastBackup.Text = String.IsNullOrEmpty(app.Store.LatestBackupUtc) ? Lang.T("最近备份：尚无备份") : Lang.T("最近备份：{0}", LocalStamp(app.Store.LatestBackupUtc));
                RefreshBackups();
                foreach (var page in pages) if (page.Visible) ResizePage(page);
            }
            finally { syncing = false; }
        }

        private void RefreshCourses()
        {
            string signature = String.Join("|", app.Data.Events.Select(e => e.Id + e.Title + e.Date + e.RepeatWeeks + e.StartTime + e.EndTime + e.RepeatEndDate));
            if (signature == courseSignature) return; courseSignature = signature; string selected = SelectedTag(courses);
            courses.BeginUpdate(); courses.Items.Clear();
            foreach (var value in app.Data.Events)
            {
                var item = new ListViewItem(value.Title) { Tag = value.Id };
                item.SubItems.Add(value.RepeatWeeks == 0 ? value.Date : value.RepeatWeeks == 1 ? Lang.T("每周 · 至 {0}", value.RepeatEndDate) : Lang.T("每两周 · 至 {0}", value.RepeatEndDate));
                item.SubItems.Add(value.StartTime + "–" + value.EndTime); courses.Items.Add(item);
            }
            SelectTag(courses, selected); courses.EndUpdate();
        }

        private void RefreshBooks()
        {
            string books = String.Join("|", app.Data.Books.Select(b => b.Id + b.Name));
            if (books != bookSignature)
            {
                bookSignature = books; string id = SelectedId(bookChoice); bookChoice.Items.Clear(); foreach (var book in app.Data.Books) bookChoice.Items.Add(new Choice(book.Id, book.Name)); SelectId(bookChoice, id);
            }
            var current = CurrentBook(); if (current == null) return;
            if (!bookName.ContainsFocus) bookName.Text = current.Name;
            string signature = current.Id + "|" + String.Join("|", current.Pages.Select(p => p.Id + p.Title + p.Archived + p.CreatedUtc + p.Tasks.Count + p.Tasks.Count(t => t.Completed)));
            if (signature != pageSignature)
            {
                pageSignature = signature; string selected = SelectedTag(pageList); pageList.BeginUpdate(); pageList.Items.Clear(); desktopPage.Items.Clear();
                foreach (var note in current.Pages)
                {
                    var item = new ListViewItem(note.Title) { Tag = note.Id }; item.SubItems.Add(note.Archived ? Lang.T("已归档") : Lang.T("使用中")); item.SubItems.Add(LocalStamp(note.CreatedUtc)); pageList.Items.Add(item);
                    if (!note.Archived) desktopPage.Items.Add(new Choice(note.Id, note.Title));
                }
                SelectTag(pageList, selected); pageList.EndUpdate();
            }
            SelectId(desktopPage, current.CurrentPageId); RefreshPageSelection();
        }

        private void RefreshPending()
        {
            var rows = new List<string[]>(); DateTime now = DateTime.UtcNow;
            foreach (var book in app.Data.Books) foreach (var note in book.Pages) foreach (var task in note.Tasks)
            {
                if (task.Completed || String.IsNullOrEmpty(task.DueLocal)) continue;
                string state = Lang.T("待提醒");
                try { DateTime due = TimeUtil.LocalToUtc(TimeUtil.ParseLocal(task.DueLocal), task.TimeZoneId); state = due < now ? Lang.T("已逾期") : (due - now).TotalHours <= 24 ? Lang.T("临近截止") : Lang.T("待提醒"); if (due >= now && !String.IsNullOrEmpty(task.DueNotifiedKey)) state = Lang.T("已发送提醒"); }
                catch { state = Lang.T("检查截止时间"); }
                rows.Add(new[] { task.Id, task.Text, book.Name + " / " + note.Title + (note.Archived ? Lang.T("（归档）") : ""), TimeUtil.DueDisplay(task.DueLocal, task.DueDateOnly), state });
            }
            rows = rows.OrderBy(r => r[3], StringComparer.Ordinal).ToList();
            string signature = String.Join("|", rows.Select(r => String.Join("~", r))); if (signature == pendingSignature) return; pendingSignature = signature;
            string selected = SelectedTag(pendingTasks); pendingTasks.BeginUpdate(); pendingTasks.Items.Clear();
            foreach (var row in rows) { var item = new ListViewItem(row[1]) { Tag = row[0] }; item.SubItems.AddRange(new[] { row[2], row[3], row[4] }); if (row[4] == Lang.T("已逾期")) item.ForeColor = Color.FromArgb(177, 77, 66); pendingTasks.Items.Add(item); }
            SelectTag(pendingTasks, selected); pendingTasks.EndUpdate();
        }

        private void RefreshBackups()
        {
            var files = new List<FileInfo>();
            try
            {
                files = new DirectoryInfo(app.Store.DirectoryPath).GetFiles("*.json").Where(f => f.Name.StartsWith("backup-", StringComparison.OrdinalIgnoreCase) || f.Name.StartsWith("before-import-", StringComparison.OrdinalIgnoreCase) || f.Name.StartsWith("migration-v1-", StringComparison.OrdinalIgnoreCase) || f.Name.Equals("data.previous.json", StringComparison.OrdinalIgnoreCase)).OrderByDescending(f => f.LastWriteTimeUtc).ToList();
            }
            catch (IOException) { return; }
            catch (UnauthorizedAccessException) { return; }
            string signature = String.Join("|", files.Select(f => f.FullName + f.LastWriteTimeUtc.Ticks)); if (signature == backupSignature) return; backupSignature = signature;
            string selected = SelectedTag(backups); backups.BeginUpdate(); backups.Items.Clear();
            foreach (var file in files) { var item = new ListViewItem(file.Name) { Tag = file.FullName, ToolTipText = file.FullName }; item.SubItems.Add(file.LastWriteTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)); backups.Items.Add(item); }
            SelectTag(backups, selected); backups.EndUpdate();
        }

        private static string LocalStamp(string value)
        {
            DateTime stamp; return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out stamp) ? stamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : value;
        }
    }
}
