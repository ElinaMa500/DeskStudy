using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DeskStudy
{
    // Keep the native edit handle alive while rearranging a notebook. IME composition
    // is a barrier: no layout, font, visibility or parent changes until it completes.
    public sealed class CompositionTextBox : TextBox
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr param, string text);
        private string cue = "";
        public string Cue { get { return cue; } set { cue = value; if (IsHandleCreated) SendMessage(Handle, 0x1501, IntPtr.Zero, cue); } }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); SendMessage(Handle, 0x1501, IntPtr.Zero, cue); }
        public bool IsComposing { get; private set; }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x10D) IsComposing = true;
            base.WndProc(ref message);
            if (message.Msg == 0x10E) IsComposing = false;
        }
    }

    public sealed partial class NotebookForm
    {
        private Panel _modern, _modernFooter, _quickEntry;
        private Label _originalFooter, _saveLabel;
        private Button _directory, _details, _quickAdd;
        private CompositionTextBox _quickText;
        private ListBox _paperTabs;
        private Timer _layoutRetry;
        private string _appliedLayout = "Original", _quickPageId;
        private bool _changingLayout, _navigationSync, _originalNotesOpen = true, _modernNotesOpen;
        private readonly Dictionary<string, string> _quickDrafts = new Dictionary<string, string>();
        private readonly Dictionary<string, int> _quickSelections = new Dictionary<string, int>();
        private ContextMenuStrip _taskMenu;
        private Label _quickPlus;
        private readonly Dictionary<string, Font> _referenceFonts = new Dictionary<string, Font>();
        private Padding _bodyPadding;
        private bool _bodyPaddingCaptured;
        private Size _originalMinimum;
        public string AppliedNotebookLayout { get { return _appliedLayout; } }
        private bool IsModern { get { return _appliedLayout != "Original"; } }
        // 清爽卡片 (Clean) and 手账纸页 (Journal) follow desktop-notebook-preview.html.
        private bool IsReference { get { return _appliedLayout == "Clean" || _appliedLayout == "Journal"; } }
        private bool HasRail { get { return _appliedLayout == "Paper" || _appliedLayout == "Journal"; } }

        private struct ReferencePalette { public Color Back, Ink, Sub, Faint, Rule, Soft, Accent, Warm, Dot; }
        private static Color Hex(string value) { return ColorTranslator.FromHtml(value); }
        private static Color Blend(Color from, Color to, double amount)
        {
            return Color.FromArgb((int)Math.Round(from.R + (to.R - from.R) * amount), (int)Math.Round(from.G + (to.G - from.G) * amount), (int)Math.Round(from.B + (to.B - from.B) * amount));
        }
        private ReferencePalette Palette()
        {
            Color back = AppearancePainter.Background(SettingsLogic.EffectiveAppearance(App.Data, _bookId));
            bool dark = AppearancePainter.Dark(back), journal = _appliedLayout == "Journal";
            var p = new ReferencePalette { Back = back };
            p.Ink = dark ? Hex("#E3E9E5") : Hex("#283732");
            p.Sub = dark ? Hex("#AFB9B2") : Hex("#68736E");
            p.Accent = dark ? Hex("#A5C5AC") : Hex("#527562");
            p.Warm = dark ? Hex("#F1B894") : Hex("#925331");
            p.Dot = _bookId == "ddl" ? p.Warm : p.Accent;
            if (!dark && back.ToArgb() == Hex(SettingsLogic.LayoutBackground(_appliedLayout)).ToArgb())
            {
                p.Rule = journal ? Hex("#E9E4D6") : Hex("#E8ECE8");
                p.Soft = journal ? Hex("#EFECDF") : Hex("#F3F6F2");
            }
            else { p.Rule = Blend(back, p.Ink, dark ? .16 : .09); p.Soft = Blend(back, p.Ink, dark ? .08 : .045); }
            p.Faint = Blend(p.Sub, back, .35);
            return p;
        }
        private Font ReferenceFont(float size, FontStyle style)
        {
            float scaled = (float)Math.Round(size * SettingsLogic.EffectiveAppearance(App.Data, _bookId).FontSize / 9F, 2);
            string key = scaled.ToString(CultureInfo.InvariantCulture) + "|" + style;
            Font font;
            if (!_referenceFonts.TryGetValue(key, out font)) { font = new Font("Microsoft YaHei UI", scaled, style); _referenceFonts[key] = font; }
            return font;
        }
        private bool IsComposing
        {
            get { return (_pageTitle as CompositionTextBox).IsComposing || (_notes as CompositionTextBox).IsComposing || (_quickText != null && _quickText.IsComposing) || (_inlineEditor != null && _inlineEditor.IsComposing); }
        }
        protected override bool CanApplyAppearance()
        {
            if (_contentReady && IsComposing) { _layoutRetry.Start(); return false; }
            return true;
        }
        private void InitializeNotebookLayouts()
        {
            _modern = new Panel { Name = "modern-notebook", Dock = DockStyle.Fill, Visible = false };
            _modern.Paint += delegate(object sender, PaintEventArgs e)
            {
                if (_appliedLayout == "Paper")
                {
                    int x = _paperTabs.Right + Px(10);
                    using (var pen = new Pen(Color.FromArgb(90, _accent))) e.Graphics.DrawLine(pen, x, Px(8), x, _modern.Height - Px(8));
                    return;
                }
                if (!IsReference) return;
                var p = Palette(); int footerTop = _modernFooter.Top;
                using (var pen = new Pen(p.Rule))
                {
                    if (_appliedLayout == "Journal")
                    {
                        int rail = RailWidth();
                        using (var soft = new SolidBrush(p.Soft)) e.Graphics.FillRectangle(soft, 0, 0, rail, footerTop);
                        e.Graphics.DrawLine(pen, rail, 0, rail, footerTop);
                    }
                    e.Graphics.DrawLine(pen, 0, footerTop - 1, _modern.Width, footerTop - 1);
                }
            };
            Body.Controls.Add(_modern);
            _modernFooter = new Panel { Name = "notebook-pagination" };
            _directory = SmallButton("1 / 1  ▾", delegate { OpenPageDirectory(); });
            _directory.Name = "page-directory"; _directory.AccessibleName = "页面目录";
            _details = SmallButton("⋯", delegate { OpenPageDetails(); });
            _details.Name = "page-details"; _details.AccessibleName = "页面详情和便签操作";
            _directory.Dock = _details.Dock = DockStyle.None;
            _saveLabel = Ui.Label("", 8F, Ui.Muted); _saveLabel.AutoSize = false; _saveLabel.TextAlign = ContentAlignment.MiddleRight;
            _saveLabel.Name = "save-status";
            _notesToggle.Name = "notes-toggle";
            _paperTabs = new ListBox { Name = "paper-page-tabs", BorderStyle = BorderStyle.None, IntegralHeight = false, DrawMode = DrawMode.OwnerDrawFixed };
            _paperTabs.AccessibleName = "纸页目录";
            // Font and item height are set per layout below; changing them recreates the ListBox handle.
            _paperTabs.Tag = "appearance-custom-font";
            _paperTabs.DrawItem += DrawPaperTab;
            _paperTabs.SelectedIndexChanged += delegate
            {
                if (_navigationSync || _paperTabs.SelectedItem == null) return;
                var book = FindBook(); var page = (PageChoice)_paperTabs.SelectedItem;
                // A recreated handle replays the displayed selection; only a different page is a user choice.
                if (book == null || page.Id == _displayedPageId) return;
                if (book.CurrentPageId != page.Id) { book.CurrentPageId = page.Id; Persist(); RefreshFromData(); }
            };
            _quickEntry = new Panel { Name = "quick-task-row", Margin = Padding.Empty };
            _quickText = new CompositionTextBox { Name = "quick-task", BorderStyle = BorderStyle.None, MaxLength = 2000, Font = new Font("Microsoft YaHei UI", 9.5F), AccessibleName = "添加任务内容，按回车连续录入" };
            _quickAdd = SmallButton("＋ 添加", delegate { SubmitQuickTask(); }); _quickAdd.Name = "quick-add";
            _quickAdd.Dock = DockStyle.None;
            _quickText.Cue = "记下一件事，回车添加";
            _quickText.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter && !_quickText.IsComposing) { e.SuppressKeyPress = true; SubmitQuickTask(); } };
            _quickPlus = new Label { Name = "quick-task-plus", Text = "+", AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Visible = false, UseMnemonic = false };
            _quickEntry.Controls.Add(_quickText); _quickEntry.Controls.Add(_quickAdd); _quickEntry.Controls.Add(_quickPlus);
            _quickEntry.Paint += delegate(object sender, PaintEventArgs e)
            {
                if (IsReference) return;
                using (var pen = new Pen(Color.FromArgb(90, _accent))) { pen.DashStyle = DashStyle.Dot; e.Graphics.DrawLine(pen, Px(4), _quickEntry.Height - 2, _quickEntry.Width - Px(4), _quickEntry.Height - 2); }
            };
            InitializeDue();
            _quickText.Enter += delegate { _quickEntry.Invalidate(); }; _quickText.Leave += delegate { _quickEntry.Invalidate(); };
            _quickText.TextChanged += delegate { _quickEntry.Invalidate(); };
            _modern.Controls.Add(_details); _modern.Controls.Add(_modernFooter); _modern.Controls.Add(_paperTabs); _modern.Controls.Add(_saveLabel);
            _modernFooter.Controls.Add(_directory);
            _modern.SizeChanged += delegate { if (IsModern) ArrangeModernLayout(); };
            _layout.ClientSizeChanged += delegate { if (!IsModern) ArrangeOriginalNotes(); };
            _layoutRetry = new Timer { Interval = 120 };
            _layoutRetry.Tick += delegate { if (!IsComposing) { _layoutRetry.Stop(); ApplyAppearance(); RefreshFromData(); } };
            App.SaveStateChanged += UpdateSaveStatus;
        }
        private int Px(float value) { using (var g = CreateGraphics()) return (int)Math.Round(value * g.DpiX / 96F); }
        private void ApplyNotebookLayout()
        {
            if (!_contentReady || _changingLayout || IsComposing) return;
            string requested = App.Data.Settings.NotebookLayout;
            if (_appliedLayout == requested) return;
            _changingLayout = true;
            Control focused = ContainsFocus ? FindFocused(this) : null;
            int titleSelection = _pageTitle.SelectionStart, notesSelection = _notes.SelectionStart, quickSelection = _quickText.SelectionStart;
            int titleLength = _pageTitle.SelectionLength, notesLength = _notes.SelectionLength, quickLength = _quickText.SelectionLength;
            SuspendLayout(); Body.SuspendLayout(); _layout.SuspendLayout(); _modern.SuspendLayout();
            try
            {
                if (IsModern) _modernNotesOpen = _notesExpanded; else _originalNotesOpen = _notesExpanded;
                _appliedLayout = requested;
                _notesExpanded = IsModern ? _modernNotesOpen : _originalNotesOpen;
                if (IsModern)
                {
                    foreach (Control c in new Control[] { _pageTitle, _notesToggle, _notes, _taskCount, _tasks }) { c.Dock = DockStyle.None; _modern.Controls.Add(c); }
                    foreach (Control c in new Control[] { _previous, _next, _newPageButton }) { c.Dock = DockStyle.None; _modernFooter.Controls.Add(c); }
                    _layout.Visible = false; _modern.Visible = true; _modern.BringToFront();
                    _pageTitle.BorderStyle = BorderStyle.None;
                    _tasks.Controls.Add(_quickEntry);
                    _paperTabs.Visible = HasRail;
                }
                else
                {
                    _layout.Controls.Add(_pageTitle, 0, 1); _layout.Controls.Add(_notesToggle, 0, 3); _layout.Controls.Add(_notes, 0, 4); _layout.Controls.Add(_tasks, 0, 6);
                    _taskHeader.Controls.Add(_taskCount, 0, 0);
                    _navigation.Controls.Add(_previous, 0, 0); _navigation.Controls.Add(_next, 2, 0); _navigation.Controls.Add(_newPageButton, 3, 0);
                    foreach (Control c in new Control[] { _pageTitle, _notesToggle, _notes, _taskCount, _tasks, _previous, _next, _newPageButton }) c.Dock = DockStyle.Fill;
                    _pageTitle.BorderStyle = BorderStyle.FixedSingle;
                    _modern.Controls.Add(_quickEntry);
                    _modern.Visible = false; _layout.Visible = true; _layout.BringToFront();
                }
                _notes.Visible = _notesExpanded;
                foreach (var row in _rows) { row.Actions.Visible = !IsModern; row.More.Visible = IsModern; }
                UpdateNotesCaption(); RefreshModernNavigation(false);
            }
            finally
            {
                _modern.ResumeLayout(true); _layout.ResumeLayout(true); Body.ResumeLayout(true); ResumeLayout(true);
                _changingLayout = false;
            }
            if (focused != null && focused.Visible) focused.Focus();
            _pageTitle.Select(Math.Min(titleSelection, _pageTitle.TextLength), titleLength);
            _notes.Select(Math.Min(notesSelection, _notes.TextLength), notesLength);
            _quickText.Select(Math.Min(quickSelection, _quickText.TextLength), quickLength);
        }
        private static Control FindFocused(Control parent) { foreach (Control c in parent.Controls) { if (c.Focused) return c; if (c.ContainsFocus) return FindFocused(c); } return null; }
        private void StyleNotebookLayout(AppearanceOptions appearance)
        {
            SetCompactNotebookHeader(IsModern);
            // 清爽卡片 / 手账纸页 sit closer to the bottom edge, so the notebook can be shorter (1.5.1).
            if (_contentReady && !_bodyPaddingCaptured) { _bodyPadding = Body.Padding; _bodyPaddingCaptured = true; }
            if (_bodyPaddingCaptured) // Narrower side margins too, so two notebooks side by side can match the calendar's width.
            Body.Padding = IsReference ? new Padding(Px(4), _bodyPadding.Top, Px(4), Px(3)) : _bodyPadding;
            if (_contentReady && _originalMinimum.IsEmpty) _originalMinimum = ExpandedMinimumSize;
            if (!_originalMinimum.IsEmpty)
            {
                // The slimmer footer and side margins let these two layouts go shorter and narrower.
                Size minimum = IsReference ? new Size(_originalMinimum.Width - Px(24), _originalMinimum.Height - Px(30)) : _originalMinimum;
                SetExpandedMinimumSize(minimum);
            }
            Color bg = AppearancePainter.Background(appearance), fg = AppearancePainter.Foreground(appearance), surface = AppearancePainter.Surface(appearance);
            _modern.BackColor = bg; _tasks.BackColor = bg;
            _pageTitle.BackColor = IsModern ? bg : surface;
            _quickText.BackColor = surface; _quickEntry.BackColor = surface;
            _paperTabs.BackColor = bg; _paperTabs.ForeColor = fg;
            if (!IsReference) SetPaperTabsMetrics(ReferenceFont(9F, FontStyle.Regular), Px(12));
            _pageTitle.ReadOnly = false;
            foreach (var row in _rows)
            {
                var card = (TaskCardPanel)row.Card; card.LayoutKind = _appliedLayout; card.SurfaceColor = surface;
                var check = row.Toggle as TaskCheckBox; if (check != null && check.Reference) { check.Reference = false; check.Invalidate(); }
                row.More.Visible = IsModern; row.Actions.Visible = !IsModern;
                if (!IsModern) { row.Status.Visible = true; row.Card.Margin = new Padding(0, 0, 0, 8); }
                row.More.FlatAppearance.BorderSize = 0;
                row.More.BackColor = _appliedLayout == "Card" ? surface : bg;
                if (IsModern) { row.Card.BackColor = bg; row.Title.BackColor = Color.Transparent; row.Status.BackColor = Color.Transparent; row.Toggle.BackColor = _appliedLayout == "Card" ? surface : bg; }
                card.Invalidate();
            }
            foreach (Button b in new[] { _previous, _next, _newPageButton, _directory, _details, _quickAdd }) { b.FlatAppearance.BorderSize = IsModern ? 0 : 1; b.BackColor = IsModern ? bg : surface; b.Padding = Padding.Empty; }
            _newPageButton.Text = "＋ 新页";
            _quickAdd.Text = "＋ 添加"; _quickText.Cue = "记下一件事，回车添加"; _quickPlus.Visible = false; _quickEntry.Margin = Padding.Empty;
            if (IsReference) StyleReferenceLayout();
            else SetReferenceHeader(false, "", bg, fg, Ui.Muted, _accent, _accent, surface, Ui.Border, false);
            StyleQuickDue();
            foreach (var row in _rows)
            {
                if (row.AddDue == null) continue;
                row.AddDue.BackColor = row.More.BackColor; row.AddDue.ForeColor = IsReference ? Palette().Faint : Ui.Muted;
                row.AddDue.Font = ReferenceFont(8.5F, FontStyle.Regular);
                if (!OffersHoverDue(row)) row.AddDue.Visible = false;
            }
            if (!IsModern)
            {
                var page = FindPage(_displayedPageId);
                if (page != null) _taskCount.Text = "任务  " + page.Tasks.Count(t => t.Completed) + " / " + page.Tasks.Count + " 已完成";
                foreach (Control control in _tasks.Controls) if (control is Label) { control.Text = "这一页还没有任务\r\n\r\n记下一件小事，完成后也会留在这里。"; control.Height = 110; }
                ArrangeOriginalNotes();
            }
            UpdateNotesCaption();
            if (IsModern) ArrangeModernLayout();
            else _notesToggle.Text = _notesExpanded ? "文字记录  ▾" : "文字记录  ▸  （点击展开）";
        }
        private void StyleReferenceLayout()
        {
            var p = Palette(); bool journal = _appliedLayout == "Journal";
            SetCompactNotebookHeader(true, 8.5F);
            SetReferenceHeader(true, _bookId == "ddl" ? " · 截止本" : " · 待办本", p.Back, p.Ink, journal ? p.Ink : p.Sub, p.Dot, p.Accent, p.Soft, p.Rule, journal);
            Font small = ReferenceFont(8.5F, FontStyle.Regular);
            _pageTitle.Font = ReferenceFont(journal ? 14F : 15F, FontStyle.Regular);
            _pageTitle.ForeColor = p.Ink; _pageTitle.BackColor = p.Back;
            _taskCount.Font = small; _taskCount.ForeColor = p.Sub; _taskCount.TextAlign = ContentAlignment.MiddleLeft;
            _saveLabel.Font = small;
            _notesToggle.Font = small; _notesToggle.ForeColor = p.Sub; _notesToggle.BackColor = p.Back; _notesToggle.FlatAppearance.BorderSize = 0;
            _notesToggle.FlatAppearance.MouseOverBackColor = p.Soft; _notesToggle.TextAlign = ContentAlignment.MiddleLeft;
            _notes.BackColor = p.Back; _notes.ForeColor = p.Ink;
            foreach (Button b in new[] { _previous, _next, _directory, _newPageButton, _details, _quickAdd })
            {
                b.BackColor = p.Back; b.ForeColor = p.Sub; b.FlatAppearance.BorderSize = 0; b.FlatAppearance.MouseOverBackColor = p.Soft; b.FlatAppearance.BorderColor = p.Back;
            }
            foreach (Button b in new[] { _directory, _newPageButton }) b.Font = small;
            _previous.Font = _next.Font = ReferenceFont(12F, FontStyle.Regular);
            _details.ForeColor = p.Faint; _details.Font = ReferenceFont(10F, FontStyle.Regular);
            _newPageButton.Text = "+ 新一页";
            _quickEntry.BackColor = p.Back; _quickText.BackColor = p.Back; _quickText.ForeColor = p.Ink;
            _quickText.Cue = _bookId == "ddl" ? "添加截止事项…" : "添加一项任务…";
            _quickAdd.Text = "↵"; _quickAdd.Font = ReferenceFont(11F, FontStyle.Regular);
            _quickPlus.Visible = true; _quickPlus.ForeColor = p.Sub; _quickPlus.BackColor = p.Back; _quickPlus.Font = ReferenceFont(11F, FontStyle.Regular);
            _paperTabs.BackColor = p.Soft; _paperTabs.ForeColor = p.Sub;
            SetPaperTabsMetrics(ReferenceFont(8F, FontStyle.Regular), Px(16));
            foreach (var row in _rows)
            {
                var card = (TaskCardPanel)row.Card; card.BorderColor = p.Rule; card.BackColor = p.Back;
                row.Title.ForeColor = row.Task.Completed ? p.Sub : p.Ink;
                row.More.ForeColor = p.Faint; row.More.BackColor = p.Back; row.More.FlatAppearance.MouseOverBackColor = p.Soft;
                var check = row.Toggle as TaskCheckBox;
                if (check != null) { check.Reference = true; check.Accent = p.Accent; check.Box = Blend(p.Sub, p.Back, .25); check.BackColor = p.Back; check.Invalidate(); }
            }
            foreach (Control c in _tasks.Controls) if (c is Label) { c.ForeColor = p.Sub; c.Font = ReferenceFont(9F, FontStyle.Regular); }
            _modern.Invalidate();
        }
        private void SetPaperTabsMetrics(Font font, int padding)
        {
            int height = Math.Max(Px(36), font.Height + padding);
            if (_paperTabs.Font == font && _paperTabs.ItemHeight == height) return;
            _navigationSync = true;
            try { _paperTabs.Font = font; _paperTabs.ItemHeight = height; }
            finally { _navigationSync = false; }
            RefreshModernNavigation(false);
        }
        private int RailWidth()
        {
            if (_appliedLayout != "Journal") return 0;
            int listHeight = Math.Max(1, _modernFooter.Top - Px(18));
            bool overflow = _paperTabs.Items.Count * _paperTabs.ItemHeight > listHeight;
            return Px(35) + (overflow ? SystemInformation.VerticalScrollBarWidth : 0);
        }
        private void ArrangeReferenceLayout()
        {
            bool journal = _appliedLayout == "Journal";
            int clientWidth = _modern.ClientSize.Width, clientHeight = _modern.ClientSize.Height;
            // Slim footer (1.5.1).
            int footer = Math.Max(Px(32), _newPageButton.Font.Height + Px(10));
            int footerY = Math.Max(Px(200), clientHeight - footer);
            _modernFooter.SetBounds(0, footerY, clientWidth, footer);
            int rail = RailWidth(), side = journal ? Px(16) : Px(20);
            int left = rail + side, width = Math.Max(Px(160), clientWidth - left - side);
            if (journal) _paperTabs.SetBounds(Px(4), Px(16), rail - Px(8), Math.Max(Px(40), footerY - Px(20)));
            int top = journal ? Px(14) : Px(2), detailWidth = Px(28);
            int titleHeight = _pageTitle.PreferredHeight + Px(2);
            _pageTitle.SetBounds(left, top, width - detailWidth - Px(4), titleHeight);
            _details.SetBounds(left + width - detailWidth, top, detailWidth, titleHeight);
            int small = _taskCount.Font.Height + Px(4);
            bool failed = App.SaveStatus.IndexOf("失败", StringComparison.Ordinal) >= 0;
            int saveWidth = failed ? Math.Min(width / 2, TextRenderer.MeasureText(App.SaveStatus, _saveLabel.Font).Width + Px(8)) : 0;
            _taskCount.SetBounds(left, _pageTitle.Bottom, Math.Max(Px(60), width - saveWidth), small);
            _saveLabel.SetBounds(left + width - saveWidth, _pageTitle.Bottom, Math.Max(1, saveWidth), small);
            int y = _taskCount.Bottom + Px(journal ? 9 : 14);
            int toggle = _notesToggle.Font.Height + Px(10);
            int notesHeight = _notesExpanded ? Math.Max(Px(60), Math.Min(Px(90), clientHeight / 5)) : 0;
            int notesBlock = toggle + (_notesExpanded ? notesHeight + Px(4) : 0) + Px(8);
            int maxTasks = Math.Max(Px(70), footerY - Px(6) - notesBlock - y);
            // Rows are laid out at ClientSize - scrollbar, so widen the list to keep rows aligned with the title.
            _tasks.SetBounds(left, y, width + SystemInformation.VerticalScrollBarWidth + Px(2), maxTasks);
            LayoutTaskCards();
            int content = 0;
            foreach (Control c in _tasks.Controls) content += c.Height + c.Margin.Vertical;
            int tasksHeight = Math.Min(maxTasks, content + Px(4));
            _tasks.Height = Math.Max(Px(40), tasksHeight);
            int notesY = _tasks.Bottom + Px(journal ? 6 : 10);
            _notesToggle.SetBounds(left - Px(2), notesY, Math.Min(width, TextRenderer.MeasureText(_notesToggle.Text, _notesToggle.Font).Width + Px(30)), toggle);
            _notes.Visible = _notesExpanded;
            _notes.SetBounds(left, _notesToggle.Bottom + Px(4), width, Math.Max(1, notesHeight));
            int arrow = Px(24), pad = Px(13);
            int directoryWidth = TextRenderer.MeasureText(_directory.Text, _directory.Font).Width + Px(14);
            int newWidth = TextRenderer.MeasureText(_newPageButton.Text, _newPageButton.Font).Width + Px(16);
            int buttonHeight = footer - Px(6);
            // The ‹ › glyphs sit low in their line; raise the two buttons so the arrows line up with the page number.
            int lift = Px(6);
            _previous.SetBounds(pad, Px(3) - lift, arrow, buttonHeight);
            _directory.SetBounds(_previous.Right, Px(3), Math.Max(Px(60), Math.Min(directoryWidth, clientWidth - pad * 2 - arrow * 2 - newWidth)), buttonHeight);
            _next.SetBounds(_directory.Right, Px(3) - lift, arrow, buttonHeight);
            _newPageButton.SetBounds(clientWidth - pad - newWidth, Px(3), newWidth, buttonHeight);
            UpdateSaveStatus(); _modern.Invalidate();
        }
        private void ArrangeModernLayout()
        {
            if (!IsModern || _changingLayout || _modern == null || !_contentReady) return;
            if (IsReference) { ArrangeReferenceLayout(); return; }
            int pad = Px(8);
            int tabWidth = Math.Max(Px(32), TextRenderer.MeasureText(Math.Max(99, _paperTabs.Items.Count).ToString(), _paperTabs.Font).Width + SystemInformation.VerticalScrollBarWidth + Px(4));
            int rail = _appliedLayout == "Paper" ? tabWidth + Px(7) : 0;
            int left = rail + pad, width = Math.Max(Px(160), _modern.ClientSize.Width - left - pad);
            int titleHeight = Math.Max(Px(36), _pageTitle.PreferredHeight + Px(10));
            int small = Math.Max(Px(28), _notesToggle.Font.Height + Px(8));
            int footer = Math.Max(Px(38), _newPageButton.Font.Height + Px(14));
            _paperTabs.SetBounds(0, Px(10), tabWidth, Math.Max(30, _modern.ClientSize.Height - Px(20)));
            _pageTitle.SetBounds(left, Px(8), width - Px(35), titleHeight);
            _details.SetBounds(left + width - Px(30), Px(4), Px(30), titleHeight);
            _taskCount.SetBounds(left, titleHeight + Px(8), width, small);
            int y = _taskCount.Bottom + Px(3);
            _notesToggle.SetBounds(left, y, width, small); y += small;
            _notes.Visible = _notesExpanded;
            int notesHeight = _notesExpanded ? Math.Max(Px(46), Math.Min(Px(82), _modern.Height / 5)) : 0;
            _notes.SetBounds(left, y, width, Math.Max(1, notesHeight)); y += notesHeight + Px(6);
            int footerY = Math.Max(y + Px(60), _modern.ClientSize.Height - footer);
            _modernFooter.SetBounds(left, footerY, width, footer);
            _tasks.SetBounds(left, y, width, Math.Max(Px(60), footerY - y - Px(3)));
            int arrow = Px(29), newWidth = (int)PreferredButtonWidth(_newPageButton, 60, DpiScale());
            _previous.SetBounds(0, 0, arrow, footer); _next.SetBounds(Math.Max(arrow + Px(65), width - newWidth - arrow), 0, arrow, footer);
            _directory.SetBounds(arrow, 0, Math.Max(Px(60), _next.Left - arrow), footer);
            _newPageButton.SetBounds(width - newWidth, 0, newWidth, footer);
            _saveLabel.SetBounds(left + width - Px(90), _taskCount.Top, Px(90), small);
            _taskCount.Width = Math.Max(Px(65), width - Px(90));
            UpdateSaveStatus(); LayoutTaskCards(); _modern.Invalidate();
        }
        private void ArrangeOriginalNotes()
        {
            if (!_contentReady || IsModern) return;
            float fixedHeight = _layout.Padding.Vertical;
            for (int i = 0; i < _layout.RowCount; i++) if (i != 4 && i != 6) fixedHeight += _layout.RowStyles[i].Height;
            float available = _layout.ClientSize.Height - fixedHeight - Px(72);
            float wanted = _notesExpanded ? Math.Min(Px(82), Math.Max(_notes.Font.Height + Px(5), available)) : 0;
            if (Math.Abs(_layout.RowStyles[4].Height - wanted) > 1) _layout.RowStyles[4].Height = wanted;
        }
        private float DpiScale() { using (var g = CreateGraphics()) return g.DpiX / 96F; }
        private void UpdateSaveStatus()
        {
            if (_saveLabel == null || IsDisposed) return;
            _saveLabel.Text = App.SaveStatus;
            bool dark = AppearancePainter.Dark(_modern.BackColor);
            bool failed = App.SaveStatus.IndexOf("失败", StringComparison.Ordinal) >= 0;
            _saveLabel.ForeColor = failed ? (dark ? Color.Salmon : Color.Firebrick) : (dark ? Color.Silver : Ui.Muted);
            // Reference layouts stay quiet while saving works and only surface failures.
            bool show = !IsReference || failed;
            if (_saveLabel.Visible != show) { _saveLabel.Visible = show; if (IsReference && !_changingLayout && _contentReady) ArrangeModernLayout(); }
        }
        private void UpdateNotesCaption()
        {
            if (IsReference) _notesToggle.Text = (_notesExpanded ? "▾  " : "▸  ") + "页内备注" + (_notes.Text.Length > 0 ? " · 已记录" : "");
            else if (IsModern) _notesToggle.Text = "页内备注" + (_notes.Text.Length > 0 ? " · 有内容" : "") + (_notesExpanded ? "  ▾" : "  ▸");
        }
        private string ReferenceSummary(NotePage page)
        {
            DateTime created;
            string date = DateTime.TryParse(page.CreatedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out created)
                ? String.Format("{0} 月 {1} 日", created.ToLocalTime().Month, created.ToLocalTime().Day) : "";
            int done = page.Tasks.Count(t => t.Completed);
            string count = _bookId == "ddl" ? (page.Tasks.Count - done) + " 项待完成" : done + "/" + page.Tasks.Count + " 完成";
            return date.Length == 0 ? count : date + " · " + count;
        }
        private void RefreshModernNavigation(bool pageChanged)
        {
            if (_quickText == null) return;
            if (pageChanged || _quickPageId == null)
            {
                if (_quickPageId != null) { _quickDrafts[_quickPageId] = _quickText.Text; _quickSelections[_quickPageId] = _quickText.SelectionStart; }
                _quickPageId = _displayedPageId;
                // A deadline chosen for the next task belongs to the page it was chosen on.
                if (pageChanged && _pendingDue != null) { _pendingDue = null; _quickDue.Text = "+ 截止时间"; }
                string draft; int selection;
                _quickText.Text = _quickDrafts.TryGetValue(_quickPageId, out draft) ? draft : "";
                _quickText.SelectionStart = _quickSelections.TryGetValue(_quickPageId, out selection) ? Math.Min(selection, _quickText.TextLength) : _quickText.TextLength;
            }
            var book = FindBook(); if (book == null) return;
            var visible = VisiblePages(book);
            var current = FindPage(book.CurrentPageId);
            if (IsReference && current != null) _taskCount.Text = ReferenceSummary(current);
            else if (IsModern && current != null) _taskCount.Text = current.Tasks.Count(t => t.Completed) + " / " + current.Tasks.Count + " 已完成";
            int position = visible.FindIndex(p => p.Id == book.CurrentPageId) + 1;
            _directory.Text = IsReference ? "第 " + position + " / " + visible.Count + " 页  ▾" : position + " / " + visible.Count + "  ▾";
            _navigationSync = true;
            try
            {
                if (_paperTabs.Items.Count != visible.Count || !visible.Select(p => p.Id).SequenceEqual(_paperTabs.Items.Cast<PageChoice>().Select(p => p.Id)))
                { _paperTabs.Items.Clear(); for (int i = 0; i < visible.Count; i++) _paperTabs.Items.Add(new PageChoice(visible[i].Id, (i + 1).ToString("D2"))); }
                _paperTabs.SelectedIndex = visible.FindIndex(p => p.Id == book.CurrentPageId);
            }
            finally { _navigationSync = false; }
            UpdateNotesCaption(); UpdateSaveStatus();
        }
        private void OpenPageDirectory()
        {
            using (var dialog = new Form { Text = "页面目录", Size = new Size(360, 430), StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false, Font = Font })
            {
                var list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, Font = Font, ItemHeight = Font.Height + 10 };
                var pages = VisiblePages(FindBook());
                foreach (var page in pages) list.Items.Add(new PageChoice(page.Id, page.Title));
                list.SelectedIndex = pages.FindIndex(p => p.Id == _displayedPageId);
                var open = Ui.Button("打开页面", delegate { dialog.DialogResult = DialogResult.OK; }); open.Dock = DockStyle.Bottom;
                list.DoubleClick += delegate { dialog.DialogResult = DialogResult.OK; };
                dialog.Controls.Add(list); dialog.Controls.Add(open); dialog.AcceptButton = open;
                if (dialog.ShowDialog(this) == DialogResult.OK && list.SelectedItem != null) { FindBook().CurrentPageId = ((PageChoice)list.SelectedItem).Id; Persist(); RefreshFromData(); }
            }
        }
        private void OpenPageDetails()
        {
            var page = FindPage(_displayedPageId); if (page == null) return;
            var menu = new ContextMenuStrip();
                menu.Items.Add(_pageMeta.Text).Enabled = false;
                menu.Items.Add("点击页标题可直接重命名").Enabled = false;
                menu.Items.Add("重命名便签本…", null, delegate { RenameNotebook(); });
                menu.Items.Add("页面目录…", null, delegate { OpenPageDirectory(); });
                menu.Items.Add("设置中心…", null, delegate { App.OpenSettings(); });
                menu.Closed += delegate { menu.Dispose(); };
                menu.Show(_details, new Point(0, _details.Height));
        }
        private void OpenTaskActions(TaskRow row)
        {
            if (_taskMenu != null) _taskMenu.Dispose();
            _taskMenu = new ContextMenuStrip { Font = Font };
            string page = _displayedPageId, task = row.Task.Id;
            _taskMenu.Items.Add("编辑任务 / 截止时间…", null, delegate { EditTask(task); });
            var up = _taskMenu.Items.Add("上移任务", null, delegate { MoveTask(page, task, -1); }); up.Enabled = row.Actions.Controls[1].Enabled;
            var down = _taskMenu.Items.Add("下移任务", null, delegate { MoveTask(page, task, 1); }); down.Enabled = row.Actions.Controls[2].Enabled;
            _taskMenu.Items.Add("删除任务…", null, delegate { DeleteTask(page, task); });
            _taskMenu.Show(row.More, new Point(0, row.More.Height));
        }
        private void DrawPaperTab(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            if (_appliedLayout == "Journal")
            {
                var p = Palette();
                using (var soft = new SolidBrush(p.Soft)) e.Graphics.FillRectangle(soft, e.Bounds);
                var chip = new Rectangle(e.Bounds.X + Math.Max(0, (e.Bounds.Width - Px(27)) / 2), e.Bounds.Y + Px(3), Math.Min(Px(27), e.Bounds.Width), e.Bounds.Height - Px(6));
                if (selected)
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var path = RoundPath(chip, Px(5))) using (var back = new SolidBrush(p.Back)) e.Graphics.FillPath(back, path);
                }
                TextRenderer.DrawText(e.Graphics, _paperTabs.Items[e.Index].ToString(), _paperTabs.Font, chip, selected ? p.Ink : p.Sub, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }
            Color bg = selected ? _accent : _paperTabs.BackColor;
            using (var brush = new SolidBrush(bg)) e.Graphics.FillRectangle(brush, e.Bounds);
            TextRenderer.DrawText(e.Graphics, _paperTabs.Items[e.Index].ToString(), _paperTabs.Font, e.Bounds, selected ? Color.White : _paperTabs.ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        private void LayoutModernTasks()
        {
            if (_layingOut) return; _layingOut = true;
            try
            {
                int width = Math.Max(80, _tasks.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - Px(2));
                if (IsReference) { LayoutReferenceTasks(width); return; }
                foreach (var row in _rows)
                {
                    int pad = Px(12), inset = Px(36), moreWidth = Math.Max(Px(28), row.More.Font.Height + 6);
                    row.Card.Width = width; row.Card.Margin = new Padding(0, 0, 0, Px(_appliedLayout == "Card" ? 9 : 2));
                    int textWidth = Math.Max(30, width - inset - moreWidth - pad);
                    var measured = TextRenderer.MeasureText(row.Title.Text, row.Title.Font, new Size(textWidth, Int32.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                    row.Toggle.SetBounds(pad, pad, Px(18), Math.Max(Px(21), row.Title.Font.Height));
                    row.Title.SetBounds(inset, pad, textWidth, Math.Max(row.Title.Font.Height, measured.Height + 2));
                    row.More.SetBounds(width - moreWidth - Px(5), pad - Px(3), moreWidth, Math.Max(Px(28), row.More.Font.Height + 6));
                    row.Status.Visible = _bookId == "ddl" || !String.IsNullOrEmpty(row.Task.DueLocal) || row.Task.Completed;
                    int statusWidth = Math.Max(30, width - inset - pad);
                    int statusHeight = TextRenderer.MeasureText(row.Status.Text, row.Status.Font, new Size(statusWidth, Int32.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 2;
                    row.Status.SetBounds(inset, row.Title.Bottom + Px(5), statusWidth, statusHeight);
                    row.Card.Height = (row.Status.Visible ? row.Status.Bottom : row.Title.Bottom) + pad + Px(4);
                    row.Actions.Visible = false; row.More.Visible = true;
                    PlaceAddDue(row, row.More.Left - Px(4));
                }
                foreach (Control c in _tasks.Controls) if (c is Label) { c.Width = width; c.Height = Px(52); c.Text = "从下面记下一件要做的事。"; }
                _quickEntry.Width = width; _quickEntry.Height = Math.Max(Px(42), _quickText.PreferredHeight + Px(18));
                int addWidth = Math.Max(Px(65), TextRenderer.MeasureText(_quickAdd.Text, _quickAdd.Font).Width + Px(8));
                int dueWidth = PlaceQuickDue(width - addWidth - Px(6), Px(2), _quickEntry.Height - Px(4), width - addWidth - Px(22));
                _quickText.SetBounds(Px(10), Px(10), Math.Max(30, width - addWidth - Px(22) - dueWidth), _quickText.PreferredHeight);
                _quickAdd.SetBounds(width - addWidth - Px(3), Px(2), addWidth, _quickEntry.Height - Px(4));
            }
            finally { _layingOut = false; }
        }
        private void LayoutReferenceTasks(int width)
        {
            int vpad = Px(_appliedLayout == "Journal" ? 13 : 12), box = Px(17), gap = Px(10), moreWidth = Px(24);
            int textLeft = box + gap + Px(1);
            foreach (var row in _rows)
            {
                row.Card.Margin = Padding.Empty; row.Card.Width = width;
                int line = row.Title.Font.Height;
                int textWidth = Math.Max(30, width - textLeft - moreWidth - Px(6));
                var measured = TextRenderer.MeasureText(row.Title.Text.Length == 0 ? " " : row.Title.Text, row.Title.Font, new Size(textWidth, Int32.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                row.Toggle.SetBounds(0, vpad + Math.Max(0, (line - box) / 2), box + Px(2), box + Px(2));
                row.Title.SetBounds(textLeft, vpad, textWidth, Math.Max(line, measured.Height + 2));
                row.More.SetBounds(width - moreWidth, vpad - Px(3), moreWidth, line + Px(6));
                row.Status.Visible = !String.IsNullOrEmpty(row.Task.DueLocal) || (_bookId == "ddl" && !row.Task.Completed);
                int statusWidth = Math.Max(30, width - textLeft - Px(4));
                int statusHeight = TextRenderer.MeasureText(row.Status.Text.Length == 0 ? " " : row.Status.Text, row.Status.Font, new Size(statusWidth, Int32.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 2;
                row.Status.SetBounds(textLeft, row.Title.Bottom + Px(2), statusWidth, statusHeight);
                row.Card.Height = (row.Status.Visible ? row.Status.Bottom : row.Title.Bottom) + vpad;
                row.Actions.Visible = false; row.More.Visible = true;
                PlaceAddDue(row, row.More.Left - Px(4));
            }
            foreach (Control c in _tasks.Controls) if (c is Label) { c.Width = width; c.Height = Px(56); c.Padding = new Padding(0, Px(18), 0, 0); c.Text = "写下这一页的第一件事"; }
            _quickEntry.Margin = new Padding(0, Px(6), 0, 0);
            _quickEntry.Width = width; _quickEntry.Height = Math.Max(Px(36), _quickText.PreferredHeight + Px(14));
            int plus = Px(18), add = Px(30);
            _quickPlus.SetBounds(0, 0, plus, _quickEntry.Height);
            int due = PlaceQuickDue(width - add - Px(2), Px(3), _quickEntry.Height - Px(6), width - plus - Px(8) - add - Px(4));
            _quickText.SetBounds(plus + Px(8), (_quickEntry.Height - _quickText.PreferredHeight) / 2, Math.Max(30, width - plus - Px(8) - add - Px(4) - due), _quickText.PreferredHeight);
            _quickAdd.SetBounds(width - add, Px(3), add, _quickEntry.Height - Px(6));
        }
        private void PlaceAddDue(TaskRow row, int right)
        {
            if (row.AddDue == null) return;
            int width = TextRenderer.MeasureText(row.AddDue.Text, row.AddDue.Font).Width + Px(6);
            row.AddDue.SetBounds(Math.Max(row.Title.Left, right - width), row.Title.Top, width, row.Title.Font.Height + 2);
            if (!OffersHoverDue(row)) row.AddDue.Visible = false;
        }
        // e.g. 明天 23:59 · 9/28、剩余 3 天 · 9/30 18:00; DDL appends the reminder lead time.
        private string ReferenceDue(TaskItem task, out bool urgent)
        {
            urgent = false;
            DateTime due;
            if (!DateTime.TryParseExact(task.DueLocal, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out due))
                return _bookId == "ddl" && !task.Completed ? "未设置截止时间" : "";
            DateTime now = DateTime.Now;
            if (!String.IsNullOrEmpty(task.TimeZoneId))
            {
                try { now = TimeZoneInfo.ConvertTime(DateTimeOffset.Now, TimeZoneInfo.FindSystemTimeZoneById(task.TimeZoneId)).DateTime; }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { }
            }
            var inv = CultureInfo.InvariantCulture;
            string time = due.ToString("HH:mm", inv), day = due.ToString("M/d", inv), full = due.ToString("M/d HH:mm", inv);
            int days = (due.Date - now.Date).Days;
            string text;
            urgent = !task.Completed && (due - now).TotalHours <= 24;
            if (task.DueDateOnly)
            {
                // Only a day was chosen: show the day, never the stored 23:59.
                string weekday = new[] { "周日", "周一", "周二", "周三", "周四", "周五", "周六" }[(int)due.DayOfWeek];
                if (task.Completed) text = day;
                else if (due < now) text = "已逾期 · " + day;
                else if (days == 0) text = "今天截止 · " + day;
                else if (days == 1) text = "明天截止 · " + day;
                else if (days <= 7) text = "剩余 " + days + " 天 · " + day + " " + weekday;
                else text = day + " " + weekday;
                return _bookId == "ddl" ? text + " · " + DayReminderText : text;
            }
            if (task.Completed) text = full;
            else if (due < now) text = "已逾期 · " + full;
            else if (days == 0) text = "今天 " + time + " · " + day;
            else if (days == 1) text = "明天 " + time + " · " + day;
            else if (days <= 7) text = "剩余 " + days + " 天 · " + full;
            else text = full;
            if (_bookId == "ddl") text += task.ReminderMinutes <= 0 ? " · 到期提醒" : " · 提前 " + task.ReminderMinutes + " 分钟";
            return text;
        }
        private void RefreshModernStatus()
        {
            if (IsReference)
            {
                var p = Palette();
                foreach (var row in _rows)
                {
                    bool urgent;
                    row.Status.Text = ReferenceDue(row.Task, out urgent);
                    row.Status.ForeColor = urgent ? p.Warm : p.Sub;
                    row.Status.Font = ReferenceFont(8.5F, urgent ? FontStyle.Bold : FontStyle.Regular);
                }
                return;
            }
            foreach (var row in _rows)
            {
                DateTime due;
                if (!DateTime.TryParseExact(row.Task.DueLocal, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out due)) { row.Status.Text = row.Task.Completed ? "已完成" : "未设置截止时间"; continue; }
                DateTime now = TimeZoneInfo.ConvertTime(DateTimeOffset.Now, TimeZoneInfo.FindSystemTimeZoneById(row.Task.TimeZoneId)).DateTime;
                int days = (due.Date - now.Date).Days;
                string label = row.Task.Completed ? "已完成" : due < now ? "已逾期" : days == 0 ? "今天截止" : days == 1 ? "明天截止" : "剩余 " + days + " 天";
                row.Status.Text = label + "  ·  " + due.ToString(row.Task.DueDateOnly ? "yyyy/MM/dd" : "yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture);
                if (_bookId == "ddl") row.Status.Text += "\r\n" + (row.Task.DueDateOnly ? DayReminderText : row.Task.ReminderMinutes == 0 ? "到期提醒" : "提前 " + row.Task.ReminderMinutes + " 分钟提醒");
            }
        }
        private void DisposeNotebookLayouts()
        {
            App.SaveStateChanged -= UpdateSaveStatus;
            if (_layoutRetry != null) _layoutRetry.Dispose();
            if (_taskMenu != null) _taskMenu.Dispose();
            if (_quickEntry != null && _quickEntry.Parent == null) _quickEntry.Dispose();
            if (_duePopup != null) { _duePopup.Dispose(); _duePopup = null; }
            foreach (var font in _referenceFonts.Values) font.Dispose();
            _referenceFonts.Clear();
        }
        private static GraphicsPath RoundPath(Rectangle rect, int radius)
        {
            int diameter = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
            var path = new GraphicsPath();
            path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90); path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90); path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90); path.CloseFigure(); return path;
        }
    }
}
