using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

namespace DeskStudy
{
    public sealed partial class NotebookForm : WidgetForm
    {
        private readonly string _bookId;
        private readonly Color _accent;
        private readonly TableLayoutPanel _layout;
        private readonly ComboBox _pages;
        private readonly TextBox _pageTitle;
        private readonly TextBox _notes;
        private readonly Label _pageMeta;
        private readonly Label _taskCount;
        private readonly Button _previous;
        private readonly Button _next;
        private readonly Button _notesToggle;
        private readonly FlowLayoutPanel _tasks;
        private TableLayoutPanel _navigation;
        private TableLayoutPanel _metadata;
        private TableLayoutPanel _taskHeader;
        private Button _newPageButton;
        private Button _renameButton;
        private Button _addTaskButton;
        private readonly Timer _statusTimer;
        private Font _taskFont;
        private Font _doneFont;
        private readonly List<TaskRow> _rows = new List<TaskRow>();
        private string _displayedPageId;
        private string _taskSignature;
        private string _pageSignature;
        private bool _rendering;
        private bool _saving;
        private bool _layingOut;
        private bool _notesExpanded = true;
        private bool _contentReady;

        public NotebookForm(AppController app, string bookId)
            : base(app, bookId, bookId == "ddl" ? "DDL" : "Todo",
                   bookId == "ddl" ? Color.FromArgb(184, 113, 75) : Color.FromArgb(76, 126, 111),
                   new Size(400, 560))
        {
            _bookId = bookId;
            _accent = bookId == "ddl" ? Color.FromArgb(184, 113, 75) : Color.FromArgb(76, 126, 111);
            MinimumSize = new Size(350, 430);
            _taskFont = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular);
            _doneFont = new Font(_taskFont, FontStyle.Strikeout);
            Body.BackColor = Ui.Background;
            _layout = new TableLayoutPanel();
            _layout.Dock = DockStyle.Fill;
            _layout.Padding = new Padding(12, 5, 12, 8);
            _layout.ColumnCount = 1;
            _layout.RowCount = 8;
            _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (int height in new int[] { 34, 34, 26, 23, 82, 39 })
                _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));
            Body.Controls.Add(_layout);

            TableLayoutPanel navigation = new TableLayoutPanel();
            _navigation = navigation;
            navigation.Dock = DockStyle.Fill;
            navigation.Margin = Padding.Empty;
            navigation.ColumnCount = 4;
            navigation.RowCount = 1;
            navigation.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
            navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
            navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            _previous = SmallButton("‹", delegate { TurnPage(-1); });
            _next = SmallButton("›", delegate { TurnPage(1); });
            _previous.Name = "previous-page";
            _next.Name = "next-page";
            _pages = new ComboBox();
            _pages.DropDownStyle = ComboBoxStyle.DropDownList;
            _pages.Dock = DockStyle.Fill;
            _pages.Margin = new Padding(4, 3, 4, 0);
            _pages.Font = new Font("Microsoft YaHei UI", 9f);
            _pages.AccessibleName = "页面列表";
            _pages.SelectedIndexChanged += delegate
            {
                if (_rendering || _pages.SelectedItem == null) return;
                PageChoice choice = (PageChoice)_pages.SelectedItem;
                Notebook book = FindBook();
                if (book == null || book.CurrentPageId == choice.Id) return;
                book.CurrentPageId = choice.Id;
                Persist();
                RefreshFromData();
            };
            navigation.Controls.Add(_previous, 0, 0);
            navigation.Controls.Add(_pages, 1, 0);
            navigation.Controls.Add(_next, 2, 0);
            var newPageButton = SmallButton("＋ 新页", delegate { AddPage(); });
            _newPageButton = newPageButton;
            newPageButton.Name = "new-page";
            navigation.Controls.Add(newPageButton, 3, 0);
            _layout.Controls.Add(navigation, 0, 0);

            _pageTitle = new CompositionTextBox();
            _pageTitle.Name = "page-title";
            _pageTitle.Dock = DockStyle.Fill;
            _pageTitle.Margin = new Padding(0, 4, 0, 0);
            _pageTitle.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
            _pageTitle.ForeColor = Ui.Text;
            _pageTitle.BorderStyle = BorderStyle.FixedSingle;
            _pageTitle.MaxLength = 140;
            _pageTitle.AccessibleName = "页面标题";
            _pageTitle.TextChanged += delegate
            {
                if (_rendering) return;
                NotePage page = FindPage(_displayedPageId);
                if (page == null || page.Title == _pageTitle.Text) return;
                page.Title = _pageTitle.Text;
                Persist();
                RefreshPageChoices();
            };
            _layout.Controls.Add(_pageTitle, 0, 1);

            TableLayoutPanel metadata = new TableLayoutPanel();
            _metadata = metadata;
            metadata.Dock = DockStyle.Fill;
            metadata.Margin = Padding.Empty;
            metadata.ColumnCount = 2;
            metadata.RowCount = 1;
            metadata.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            metadata.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            metadata.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
            _pageMeta = Ui.Label("", 8.5f, Ui.Muted);
            _pageMeta.Dock = DockStyle.Fill;
            _pageMeta.TextAlign = ContentAlignment.MiddleLeft;
            _pageMeta.Margin = Padding.Empty;
            metadata.Controls.Add(_pageMeta, 0, 0);
            Button rename = SmallButton("重命名便签", delegate { RenameNotebook(); });
            _renameButton = rename;
            rename.Font = new Font("Microsoft YaHei UI", 8f);
            metadata.Controls.Add(rename, 1, 0);
            _layout.Controls.Add(metadata, 0, 2);

            _notesToggle = Ui.Button("文字记录  ▾", delegate { ToggleNotes(); });
            _notesToggle.AutoSize = false;
            _notesToggle.MinimumSize = Size.Empty;
            _notesToggle.Dock = DockStyle.Fill;
            _notesToggle.Margin = Padding.Empty;
            _notesToggle.TextAlign = ContentAlignment.MiddleLeft;
            _notesToggle.FlatStyle = FlatStyle.Flat;
            _notesToggle.FlatAppearance.BorderSize = 0;
            _notesToggle.BackColor = Ui.Background;
            _notesToggle.ForeColor = Ui.Muted;
            _notesToggle.Font = new Font("Microsoft YaHei UI", 8.5f);
            _layout.Controls.Add(_notesToggle, 0, 3);
            _notes = new CompositionTextBox();
            _notes.Name = "page-text";
            _notes.Dock = DockStyle.Fill;
            _notes.Margin = new Padding(0, 0, 0, 3);
            _notes.Multiline = true;
            _notes.AcceptsReturn = true;
            _notes.ScrollBars = ScrollBars.Vertical;
            _notes.WordWrap = true;
            _notes.Font = new Font("Microsoft YaHei UI", 9.5f);
            _notes.BorderStyle = BorderStyle.FixedSingle;
            _notes.BackColor = Color.White;
            _notes.ForeColor = Ui.Text;
            _notes.AccessibleName = "本页文字记录";
            _notes.TextChanged += delegate
            {
                if (_rendering) return;
                NotePage page = FindPage(_displayedPageId);
                if (page == null || page.Text == _notes.Text) return;
                page.Text = _notes.Text;
                Persist();
            };
            _layout.Controls.Add(_notes, 0, 4);

            TableLayoutPanel taskHeader = new TableLayoutPanel();
            _taskHeader = taskHeader;
            taskHeader.Dock = DockStyle.Fill;
            taskHeader.Margin = new Padding(0, 7, 0, 3);
            taskHeader.ColumnCount = 2;
            taskHeader.RowCount = 1;
            taskHeader.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            taskHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            taskHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            _taskCount = Ui.Label("任务", 9.5f, Ui.Text);
            _taskCount.Dock = DockStyle.Fill;
            _taskCount.TextAlign = ContentAlignment.MiddleLeft;
            _taskCount.Margin = Padding.Empty;
            taskHeader.Controls.Add(_taskCount, 0, 0);
            _addTaskButton = SmallButton("＋ 添加任务", delegate { EditTask(null); });
            taskHeader.Controls.Add(_addTaskButton, 1, 0);
            _layout.Controls.Add(taskHeader, 0, 5);

            _tasks = new FlowLayoutPanel();
            _tasks.Name = "task-list";
            _tasks.Dock = DockStyle.Fill;
            _tasks.Margin = Padding.Empty;
            _tasks.FlowDirection = FlowDirection.TopDown;
            _tasks.WrapContents = false;
            _tasks.AutoScroll = true;
            _tasks.BackColor = Ui.Background;
            _tasks.ClientSizeChanged += delegate { LayoutTaskCards(); };
            _layout.Controls.Add(_tasks, 0, 6);
            Label footer = Ui.Label("隐藏、翻页、归档后仍会提醒", 8f, Ui.Muted);
            footer.AutoEllipsis = true;
            footer.Dock = DockStyle.Fill;
            footer.TextAlign = ContentAlignment.BottomLeft;
            footer.Margin = Padding.Empty;
            _layout.Controls.Add(footer, 0, 7);
            _originalFooter = footer;
            InitializeNotebookLayouts();

            EnsureNotebook();
            RefreshFromData();
            App.DataChanged += OnDataChanged;
            _statusTimer = new Timer();
            _statusTimer.Interval = 15000;
            _statusTimer.Tick += delegate { RefreshStatuses(); };
            _statusTimer.Start();
            ResumeLayout(true);
            _contentReady = true;
            ApplyNotebookLayout();
            ApplyAppearance();
        }

        private Button SmallButton(string text, EventHandler action)
        {
            Button button = Ui.Button(text, action);
            button.AutoSize = false;
            button.MinimumSize = Size.Empty;
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(1, 1, 1, 1);
            button.Padding = Padding.Empty;
            button.Font = new Font("Microsoft YaHei UI", 8.5f);
            return button;
        }

        protected override void OnAppearanceChanged(AppearanceOptions appearance)
        {
            if (!_contentReady || _tasks == null || _layout == null) return;
            ApplyNotebookLayout();
            float desired = 9.5F * appearance.FontSize / 9F;
            Font oldTask = null, oldDone = null;
            if (_taskFont == null || Math.Abs(_taskFont.SizeInPoints - desired) > .05F)
            {
                oldTask = _taskFont; oldDone = _doneFont;
                _taskFont = new Font("Microsoft YaHei UI", desired, FontStyle.Regular);
                _doneFont = new Font(_taskFont, FontStyle.Strikeout);
            }
            Color background = AppearancePainter.Background(appearance);
            Color foreground = AppearancePainter.Foreground(appearance);
            bool dark = AppearancePainter.Dark(background);
            Color muted = dark ? Color.FromArgb(181, 191, 200) : Ui.Muted;
            foreach (TaskRow row in _rows)
            {
                row.Card.BackColor = row.Task.Completed ? background : AppearancePainter.Surface(appearance);
                row.Title.Font = row.Task.Completed ? _doneFont : _taskFont;
                row.Title.ForeColor = row.Task.Completed ? muted : foreground;
                TaskCardPanel card = row.Card as TaskCardPanel;
                if (card != null) card.BorderColor = dark ? Color.FromArgb(76, 87, 101) : Ui.Border;
            }
            if (oldTask != null) oldTask.Dispose();
            if (oldDone != null) oldDone.Dispose();
            float dpi;
            using (Graphics graphics = CreateGraphics()) dpi = graphics.DpiY / 96F;
            _layout.RowStyles[0].Height = Math.Max(34 * dpi, Math.Max(_pages.PreferredHeight + 7 * dpi, _newPageButton.Font.Height + 12 * dpi));
            _layout.RowStyles[1].Height = Math.Max(34 * dpi, _pageTitle.PreferredHeight + 6 * dpi);
            _layout.RowStyles[2].Height = Math.Max(26 * dpi, _renameButton.Font.Height + 10 * dpi);
            _layout.RowStyles[3].Height = Math.Max(23 * dpi, _notesToggle.Font.Height + 6 * dpi);
            _layout.RowStyles[4].Height = _notesExpanded ? 82 * dpi : 0;
            _layout.RowStyles[5].Height = Math.Max(39 * dpi, _addTaskButton.Font.Height + 22 * dpi);
            _layout.RowStyles[7].Height = Math.Max(23 * dpi, Font.Height + 4 * dpi);
            _navigation.ColumnStyles[3].Width = PreferredButtonWidth(_newPageButton, 72, dpi);
            _metadata.ColumnStyles[1].Width = PreferredButtonWidth(_renameButton, 88, dpi);
            _taskHeader.ColumnStyles[1].Width = PreferredButtonWidth(_addTaskButton, 92, dpi);
            _pageMeta.AutoEllipsis = true;
            _taskCount.AutoEllipsis = true;
            RefreshStatuses();
            StyleNotebookLayout(appearance);
            LayoutTaskCards();
        }

        private static float PreferredButtonWidth(Button button, int minimum, float dpi)
        {
            return Math.Max(minimum * dpi, TextRenderer.MeasureText(button.Text, button.Font, Size.Empty,
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width + 16 * dpi);
        }

        private Notebook FindBook()
        {
            if (App.Data.Books == null) return null;
            return App.Data.Books.Find(delegate(Notebook book) { return book.Id == _bookId; });
        }

        private NotePage FindPage(string pageId)
        {
            Notebook book = FindBook();
            if (book == null || book.Pages == null) return null;
            return book.Pages.Find(delegate(NotePage page) { return page.Id == pageId; });
        }

        private void EnsureNotebook()
        {
            bool changed = false;
            if (App.Data.Books == null) App.Data.Books = new List<Notebook>();
            Notebook book = FindBook();
            if (book == null)
            {
                book = new Notebook();
                book.Id = _bookId;
                book.Name = _bookId == "ddl" ? "DDL" : "Todo";
                App.Data.Books.Add(book);
                changed = true;
            }
            if (book.Pages == null) book.Pages = new List<NotePage>();
            if (!book.Pages.Exists(delegate(NotePage p) { return !p.Archived; }))
            {
                NotePage page = NewPage();
                book.Pages.Add(page);
                book.CurrentPageId = page.Id;
                changed = true;
            }
            NotePage currentPage = FindPage(book.CurrentPageId);
            if (currentPage == null || currentPage.Archived)
            {
                book.CurrentPageId = book.Pages.Find(delegate(NotePage p) { return !p.Archived; }).Id;
                changed = true;
            }
            foreach (NotePage page in book.Pages)
                if (page.Tasks == null) { page.Tasks = new List<TaskItem>(); changed = true; }
            if (changed) Persist();
        }

        private static NotePage NewPage()
        {
            NotePage page = new NotePage();
            page.Id = Guid.NewGuid().ToString("N");
            page.Title = "未命名页";
            page.CreatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            page.Text = "";
            page.Tasks = new List<TaskItem>();
            return page;
        }

        private void Persist()
        {
            _saving = true;
            try { App.Save(); }
            finally { _saving = false; }
        }

        private void OnDataChanged()
        {
            if (_saving || _rendering || IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action(OnDataChanged)); return; }
            if (IsComposing) { _layoutRetry.Start(); return; }
            EnsureNotebook();
            RefreshFromData();
        }

        public void RefreshData() { OnDataChanged(); }

        private List<NotePage> VisiblePages(Notebook book)
        {
            return book.Pages.FindAll(delegate(NotePage page) { return !page.Archived; });
        }

        private void RefreshFromData()
        {
            Notebook book = FindBook();
            if (book == null) return;
            NotePage page = FindPage(book.CurrentPageId);
            if (page == null) return;
            bool pageChanged = _displayedPageId != page.Id;
            _rendering = true;
            try
            {
                SetTitle(String.IsNullOrWhiteSpace(book.Name) ? (_bookId == "ddl" ? "DDL" : "Todo") : book.Name);
                _displayedPageId = page.Id;
                RefreshPageChoices();
                if ((pageChanged || (!_pageTitle.Focused && !IsComposing)) && _pageTitle.Text != (page.Title ?? "")) _pageTitle.Text = page.Title ?? "";
                if ((pageChanged || (!_notes.Focused && !IsComposing)) && _notes.Text != (page.Text ?? "")) _notes.Text = page.Text ?? "";
                DateTime created;
                string date = DateTime.TryParse(page.CreatedUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out created) ? created.ToLocalTime().ToString("yyyy.MM.dd") : "—";
                _pageMeta.Text = "创建于 " + date;
                List<NotePage> visible = VisiblePages(book);
                _previous.Enabled = visible.IndexOf(page) > 0;
                _next.Enabled = visible.IndexOf(page) < visible.Count - 1;
                string signature = TaskSignature(page);
                if (pageChanged || _taskSignature != signature)
                {
                    _taskSignature = signature;
                    RebuildTasks(page, pageChanged);
                }
                RefreshStatuses();
                RefreshModernNavigation(pageChanged);
            }
            finally { _rendering = false; }
        }

        private void RefreshPageChoices()
        {
            Notebook book = FindBook();
            if (book == null) return;
            StringBuilder key = new StringBuilder(book.CurrentPageId);
            List<NotePage> visible = VisiblePages(book);
            foreach (NotePage page in visible) key.Append('\n').Append(page.Id).Append('\t').Append(page.Title);
            string signature = key.ToString();
            if (_pageSignature == signature) return;
            bool rendering = _rendering;
            _rendering = true;
            try
            {
                _pages.BeginUpdate();
                _pages.Items.Clear();
                for (int i = 0; i < visible.Count; i++)
                {
                    NotePage page = visible[i];
                    string title = String.IsNullOrWhiteSpace(page.Title) ? "未命名页" : page.Title;
                    _pages.Items.Add(new PageChoice(page.Id, (i + 1).ToString("D2") + "  " + title));
                    if (page.Id == book.CurrentPageId) _pages.SelectedIndex = i;
                }
                _pageSignature = signature;
            }
            finally { _pages.EndUpdate(); _rendering = rendering; }
        }

        private static string TaskSignature(NotePage page)
        {
            StringBuilder key = new StringBuilder(page.Id);
            foreach (TaskItem task in page.Tasks)
                key.Append('\n').Append(task.Id).Append('\t').Append(task.Text).Append('\t').Append(task.Completed)
                    .Append('\t').Append(task.DueLocal).Append('\t').Append(task.TimeZoneId).Append('\t').Append(task.ReminderMinutes);
            return key.ToString();
        }

        private void TurnPage(int step)
        {
            Notebook book = FindBook();
            if (book == null) return;
            List<NotePage> visible = VisiblePages(book);
            int index = visible.FindIndex(delegate(NotePage page) { return page.Id == book.CurrentPageId; });
            int target = index + step;
            if (target < 0 || target >= visible.Count) return;
            book.CurrentPageId = visible[target].Id;
            Persist();
            RefreshFromData();
        }

        private void AddPage()
        {
            Notebook book = FindBook();
            if (book == null) return;
            NotePage page = NewPage();
            book.Pages.Add(page);
            book.CurrentPageId = page.Id;
            Persist();
            RefreshFromData();
            _pageTitle.Focus();
            _pageTitle.SelectAll();
        }

        private void RenameNotebook()
        {
            Notebook book = FindBook();
            if (book == null) return;
            using (RenameBookDialog dialog = new RenameBookDialog(book.Name))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                book = FindBook();
                if (book == null) return;
                book.Name = dialog.BookName;
                Persist();
                RefreshFromData();
            }
        }

        private void ToggleNotes()
        {
            _notesExpanded = !_notesExpanded;
            _notes.Visible = _notesExpanded;
            using (var graphics = CreateGraphics()) _layout.RowStyles[4].Height = _notesExpanded ? (float)Math.Round(82 * graphics.DpiY / 96F) : 0;
            _notesToggle.Text = _notesExpanded ? "文字记录  ▾" : "文字记录  ▸  （点击展开）";
            if (IsModern) { UpdateNotesCaption(); ArrangeModernLayout(); }
            else ArrangeOriginalNotes();
        }

        private void RebuildTasks(NotePage page, bool resetScroll)
        {
            int scroll = resetScroll ? 0 : -_tasks.AutoScrollPosition.Y;
            _tasks.SuspendLayout();
            try
            {
                if (_quickEntry != null && _quickEntry.Parent == _tasks) _tasks.Controls.Remove(_quickEntry);
                _rows.Clear();
                while (_tasks.Controls.Count > 0)
                {
                    Control old = _tasks.Controls[0];
                    _tasks.Controls.RemoveAt(0);
                    old.Dispose();
                }
                int completed = 0;
                for (int i = 0; i < page.Tasks.Count; i++)
                {
                    TaskItem task = page.Tasks[i];
                    if (task.Completed) completed++;
                    TaskRow row = MakeTaskRow(page.Id, task, i, page.Tasks.Count);
                    _rows.Add(row);
                    _tasks.Controls.Add(row.Card);
                }
                _taskCount.Text = "任务  " + completed + " / " + page.Tasks.Count + " 已完成";
                if (page.Tasks.Count == 0)
                {
                    Label empty = Ui.Label(IsModern ? "从下面记下一件要做的事。" : "这一页还没有任务\r\n\r\n记下一件小事，完成后也会留在这里。", 9f, Ui.Muted);
                    empty.AutoSize = false;
                    empty.Padding = new Padding(12, 17, 8, 8);
                    empty.Height = 110;
                    empty.Margin = Padding.Empty;
                    _tasks.Controls.Add(empty);
                }
                if (IsModern && _quickEntry != null) _tasks.Controls.Add(_quickEntry);
            }
            finally { _tasks.ResumeLayout(); }
            if (_contentReady) ApplyAppearance();
            LayoutTaskCards();
            _tasks.AutoScrollPosition = new Point(0, scroll);
        }

        private TaskRow MakeTaskRow(string pageId, TaskItem task, int index, int total)
        {
            TaskRow row = new TaskRow();
            row.Task = task;
            row.Card = new TaskCardPanel();
            row.Card.BackColor = task.Completed ? Color.FromArgb(246, 248, 247) : Color.White;
            row.Card.Margin = new Padding(0, 0, 0, 8);
            row.Toggle = new TaskCheckBox();
            row.Toggle.Name = "task-checkbox-" + task.Id;
            row.Toggle.Location = new Point(10, 11);
            row.Toggle.Size = new Size(20, 22);
            row.Toggle.Checked = task.Completed;
            row.Toggle.AccessibleName = "完成任务：" + task.Text;
            row.Toggle.CheckedChanged += delegate
            {
                if (_rendering) return;
                NotePage currentPage = FindPage(pageId);
                TaskItem current = currentPage == null ? null : currentPage.Tasks.Find(delegate(TaskItem item) { return item.Id == task.Id; });
                if (current == null) return;
                current.Completed = row.Toggle.Checked;
                if (!current.Completed) ReminderEngine.Reset(current);
                Persist();
                RefreshFromData();
            };
            row.Title = new Label();
            row.Title.Tag = "appearance-custom-font";
            row.Title.Text = task.Text;
            row.Title.Font = task.Completed ? _doneFont : _taskFont;
            row.Title.ForeColor = task.Completed ? Ui.Muted : Ui.Text;
            row.Title.Location = new Point(35, 10);
            row.Title.UseMnemonic = false;
            row.Status = Ui.Label("", 8f, Ui.Muted);
            row.Status.AutoSize = false;
            row.Actions = new FlowLayoutPanel();
            row.Actions.FlowDirection = FlowDirection.LeftToRight;
            row.Actions.WrapContents = false;
            row.Actions.Height = 27;
            row.Actions.Margin = Padding.Empty;
            Button edit = ActionButton("编辑", 45, delegate { EditTask(task.Id); });
            Button up = ActionButton("↑", 29, delegate { MoveTask(pageId, task.Id, -1); });
            Button down = ActionButton("↓", 29, delegate { MoveTask(pageId, task.Id, 1); });
            Button delete = ActionButton("删除", 45, delegate { DeleteTask(pageId, task.Id); });
            up.Enabled = index > 0;
            down.Enabled = index < total - 1;
            up.AccessibleName = "上移任务";
            down.AccessibleName = "下移任务";
            delete.ForeColor = Color.FromArgb(155, 95, 82);
            row.Actions.Controls.Add(edit);
            row.Actions.Controls.Add(up);
            row.Actions.Controls.Add(down);
            row.Actions.Controls.Add(delete);
            row.More = ActionButton("⋯", 26, delegate { OpenTaskActions(row); });
            row.More.AccessibleName = "任务操作";
            row.Card.Controls.Add(row.Toggle);
            row.Card.Controls.Add(row.Title);
            row.Card.Controls.Add(row.Status);
            row.Card.Controls.Add(row.Actions);
            row.Card.Controls.Add(row.More);
            return row;
        }

        private Button ActionButton(string text, int width, EventHandler action)
        {
            Button button = Ui.Button(text, action);
            button.AutoSize = false;
            button.MinimumSize = Size.Empty;
            button.Size = new Size(width, 25);
            button.Margin = new Padding(0, 0, 5, 0);
            button.Padding = Padding.Empty;
            button.Font = new Font("Microsoft YaHei UI", 8f);
            button.BackColor = Color.FromArgb(246, 248, 247);
            return button;
        }

        private void LayoutTaskCards()
        {
            if (_layingOut || _tasks == null || _tasks.IsDisposed) return;
            if (IsModern) { LayoutModernTasks(); return; }
            _layingOut = true;
            try
            {
                float scale;
                using (var graphics = CreateGraphics()) scale = graphics.DpiX / 96F;
                int inset = (int)(35 * scale), pad = (int)(10 * scale);
                int width = Math.Max((int)(240 * scale), _tasks.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 3);
                foreach (TaskRow row in _rows)
                {
                    row.Card.Width = width;
                    int textWidth = width - inset - pad;
                    Size measured = TextRenderer.MeasureText(row.Title.Text.Length == 0 ? " " : row.Title.Text,
                        row.Title.Font, new Size(textWidth, Int32.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                    row.Toggle.SetBounds(pad, pad, (int)(20 * scale), (int)(22 * scale));
                    row.Title.Location = new Point(inset, pad);
                    row.Title.Size = new Size(textWidth, Math.Max((int)(23 * scale), measured.Height + 2));
                    row.Status.SetBounds(inset, row.Title.Bottom + (int)(4 * scale), textWidth, row.Status.Font.Height * 2 + (int)(5 * scale));
                    float fontScale = Math.Max(1F, SettingsLogic.EffectiveAppearance(App.Data, _bookId).FontSize / 9F);
                    int actionHeight = (int)(29 * scale * fontScale);
                    int actionRows = 1, usedWidth = 0;
                    foreach (Control action in row.Actions.Controls)
                    {
                        action.Size = new Size((int)((action.Text == "↑" || action.Text == "↓" ? 29 : 45) * scale * fontScale), (int)(27 * scale * fontScale));
                        action.Margin = new Padding(0, 0, (int)(5 * scale), 0);
                        int needed = action.Width + action.Margin.Horizontal;
                        if (usedWidth > 0 && usedWidth + needed > textWidth) { actionRows++; usedWidth = 0; }
                        usedWidth += needed;
                    }
                    row.Actions.WrapContents = true;
                    row.Actions.SetBounds(inset, row.Status.Bottom + (int)(2 * scale), textWidth, actionHeight * actionRows);
                    row.Card.Height = row.Actions.Bottom + (int)(9 * scale);
                }
                if (_rows.Count == 0 && _tasks.Controls.Count > 0) _tasks.Controls[0].Width = width;
            }
            finally { _layingOut = false; }
        }

        private void RefreshStatuses()
        {
            bool dark = AppearancePainter.Dark(AppearancePainter.Background(SettingsLogic.EffectiveAppearance(App.Data, _bookId)));
            foreach (TaskRow row in _rows)
            {
                string text;
                Color color;
                GetTaskStatus(row.Task, out text, out color);
                if (dark)
                    color = Color.FromArgb(Math.Min(255, color.R + 65), Math.Min(255, color.G + 65), Math.Min(255, color.B + 65));
                row.Status.Text = text;
                row.Status.ForeColor = color;
            }
            if (IsModern) RefreshModernStatus();
        }

        private static void GetTaskStatus(TaskItem task, out string text, out Color color)
        {
            color = Ui.Muted;
            DateTime due;
            bool hasDue = DateTime.TryParseExact(task.DueLocal, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out due);
            if (task.Completed)
            {
                text = "已完成" + (hasDue ? "  ·  截止 " + due.ToString("MM/dd HH:mm") : "");
                color = Color.FromArgb(89, 132, 116);
                return;
            }
            if (!hasDue) { text = "未设置截止时间"; return; }
            DateTime now = DateTime.Now;
            if (!String.IsNullOrEmpty(task.TimeZoneId))
            {
                try { now = TimeZoneInfo.ConvertTime(DateTimeOffset.Now, TimeZoneInfo.FindSystemTimeZoneById(task.TimeZoneId)).DateTime; }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { }
            }
            TimeSpan remaining = due - now;
            string status;
            if (remaining.TotalSeconds < 0)
            {
                status = "已逾期";
                color = Color.FromArgb(175, 78, 65);
            }
            else if (remaining.TotalHours <= 24)
            {
                status = remaining.TotalMinutes < 60 ? "即将截止" : "24 小时内截止";
                color = Color.FromArgb(176, 114, 41);
            }
            else { status = "待完成"; }
            string reminder = task.ReminderMinutes <= 0 ? "到期提醒" : "提前 " + task.ReminderMinutes + " 分钟提醒";
            text = status + "  ·  " + due.ToString("yyyy/MM/dd HH:mm") + "\r\n" + reminder;
        }

        private void EditTask(string taskId)
        {
            string pageId = _displayedPageId;
            NotePage page = FindPage(pageId);
            if (page == null) return;
            TaskItem original = taskId == null ? null : page.Tasks.Find(delegate(TaskItem task) { return task.Id == taskId; });
            if (taskId != null && original == null) return;
            using (TaskEditorDialog dialog = new TaskEditorDialog(original, _accent, App.Data.Settings.Reminders.DefaultLeadMinutes))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                page = FindPage(pageId);
                if (page == null) return;
                TaskItem task = taskId == null ? new TaskItem() : page.Tasks.Find(delegate(TaskItem item) { return item.Id == taskId; });
                if (task == null) return;
                bool scheduleChanged = task.DueLocal != dialog.DueLocal || task.ReminderMinutes != dialog.ReminderMinutes || task.TimeZoneId != dialog.TimeZoneId;
                if (taskId == null) task.Id = Guid.NewGuid().ToString("N");
                task.Text = dialog.TaskText;
                task.DueLocal = dialog.DueLocal;
                task.ReminderMinutes = dialog.ReminderMinutes;
                task.TimeZoneId = dialog.TimeZoneId;
                if (scheduleChanged || taskId == null) ReminderEngine.Reset(task);
                if (taskId == null) page.Tasks.Add(task);
                Persist();
                RefreshFromData();
            }
        }

        private void MoveTask(string pageId, string taskId, int direction)
        {
            NotePage page = FindPage(pageId);
            if (page == null) return;
            int index = page.Tasks.FindIndex(delegate(TaskItem item) { return item.Id == taskId; });
            int target = index + direction;
            if (index < 0 || target < 0 || target >= page.Tasks.Count) return;
            TaskItem movedTask = page.Tasks[index];
            page.Tasks.RemoveAt(index);
            page.Tasks.Insert(target, movedTask);
            Persist();
            RefreshFromData();
        }

        private void DeleteTask(string pageId, string taskId)
        {
            NotePage page = FindPage(pageId);
            if (page == null) return;
            TaskItem task = page.Tasks.Find(delegate(TaskItem item) { return item.Id == taskId; });
            if (task == null) return;
            if (MessageBox.Show(this, "删除这项任务？此操作不会影响其他任务。", "删除任务",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            page = FindPage(pageId);
            if (page == null) return;
            page.Tasks.RemoveAll(delegate(TaskItem item) { return item.Id == taskId; });
            Persist();
            RefreshFromData();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                App.DataChanged -= OnDataChanged;
                if (_statusTimer != null) { _statusTimer.Stop(); _statusTimer.Dispose(); }
                DisposeNotebookLayouts();
            }
            base.Dispose(disposing);
            if (disposing)
            {
                if (_taskFont != null) _taskFont.Dispose();
                if (_doneFont != null) _doneFont.Dispose();
            }
        }

        private sealed class PageChoice
        {
            public readonly string Id;
            private readonly string _text;
            public PageChoice(string id, string text) { Id = id; _text = text; }
            public override string ToString() { return _text; }
        }

        private sealed class TaskRow
        {
            public TaskItem Task;
            public Panel Card;
            public CheckBox Toggle;
            public Label Title;
            public Label Status;
            public FlowLayoutPanel Actions;
            public Button More;
        }

        private sealed class TaskCardPanel : Panel
        {
            public Color BorderColor = Ui.Border;
            public string LayoutKind = "Original";
            public Color SurfaceColor = Color.White;
            public TaskCardPanel() { DoubleBuffered = true; }
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                if (LayoutKind == "Card")
                {
                    e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    Rectangle rect = new Rectangle(1, 1, Math.Max(1, Width - 5), Math.Max(1, Height - 5));
                    using (var shadow = RoundPath(new Rectangle(rect.X + 1, rect.Y + 2, rect.Width, rect.Height), 9))
                    using (var brush = new SolidBrush(Color.FromArgb(22, 30, 45, 35))) e.Graphics.FillPath(brush, shadow);
                    using (var path = RoundPath(rect, 9))
                    using (var brush = new SolidBrush(SurfaceColor))
                    using (var pen = new Pen(BorderColor)) { e.Graphics.FillPath(brush, path); e.Graphics.DrawPath(pen, path); }
                    return;
                }
                if (LayoutKind == "Paper") { using (var pen = new Pen(BorderColor)) e.Graphics.DrawLine(pen, 7, Height - 1, Width - 7, Height - 1); return; }
                if (LayoutKind == "Clean" || LayoutKind == "Journal") { using (var pen = new Pen(BorderColor)) e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1); return; }
                using (Pen pen = new Pen(BorderColor)) e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }
    }

    // A normal CheckBox; the reference layouts draw it as a rounded box filled with their accent color.
    public sealed class TaskCheckBox : CheckBox
    {
        public bool Reference;
        public Color Accent = Color.FromArgb(82, 117, 98), Box = Color.FromArgb(150, 160, 155);
        public TaskCheckBox() { SetStyle(ControlStyles.OptimizedDoubleBuffer, true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (!Reference) { base.OnPaint(e); return; }
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            float d = g.DpiX / 96F; float size = 16 * d, x = 1, y = (Height - size) / 2F;
            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                float r = 3.5F * d;
                path.AddArc(x, y, r * 2, r * 2, 180, 90); path.AddArc(x + size - r * 2, y, r * 2, r * 2, 270, 90);
                path.AddArc(x + size - r * 2, y + size - r * 2, r * 2, r * 2, 0, 90); path.AddArc(x, y + size - r * 2, r * 2, r * 2, 90, 90); path.CloseFigure();
                if (Checked)
                {
                    using (var fill = new SolidBrush(Accent)) g.FillPath(fill, path);
                    using (var tick = new Pen(Color.White, 1.8F * d) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round, LineJoin = System.Drawing.Drawing2D.LineJoin.Round })
                        g.DrawLines(tick, new[] { new PointF(x + size * .24F, y + size * .52F), new PointF(x + size * .43F, y + size * .70F), new PointF(x + size * .77F, y + size * .32F) });
                }
                else using (var pen = new Pen(Box, 1.3F * d)) g.DrawPath(pen, path);
            }
            if (Focused && ShowFocusCues) using (var focus = new Pen(Accent, 1F) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot }) g.DrawRectangle(focus, 0, 0, Width - 1, Height - 1);
        }
    }

    internal sealed class RenameBookDialog : Form
    {
        private readonly TextBox _name;
        public string BookName { get { return _name.Text.Trim(); } }

        public RenameBookDialog(string currentName)
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96, 96);
            Text = "重命名便签";
            Font = new Font("Microsoft YaHei UI", 9f);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(344, 146);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Ui.Background;
            Label label = Ui.Label("便签名称", 9f, Ui.Text);
            label.SetBounds(20, 15, 302, 22);
            _name = new TextBox();
            _name.SetBounds(20, 42, 302, 28);
            _name.MaxLength = 40;
            _name.Text = currentName ?? "";
            Button cancel = Ui.Button("取消", delegate { DialogResult = DialogResult.Cancel; });
            cancel.SetBounds(166, 93, 74, 32);
            Button save = Ui.Button("保存", delegate
            {
                if (BookName.Length == 0)
                {
                    MessageBox.Show(this, "请输入便签名称。", "还差一个名称", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    _name.Focus();
                    return;
                }
                DialogResult = DialogResult.OK;
            });
            save.SetBounds(248, 93, 74, 32);
            Controls.Add(label);
            Controls.Add(_name);
            Controls.Add(cancel);
            Controls.Add(save);
            AcceptButton = save;
            CancelButton = cancel;
            Shown += delegate { _name.Focus(); _name.SelectAll(); };
            ResumeLayout(true);
        }
    }

    internal sealed class TaskEditorDialog : Form
    {
        private readonly TextBox _text;
        private readonly CheckBox _hasDue;
        private readonly DateTimePicker _date;
        private readonly DateTimePicker _time;
        private readonly NumericUpDown _advance;
        private readonly string _originalTimeZone;
        private readonly string _originalDue;
        public string TaskText { get { return _text.Text.Trim(); } }
        public string DueLocal { get { return _hasDue.Checked ? SelectedDue.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) : ""; } }
        public string TimeZoneId { get { return _hasDue.Checked && DueLocal == _originalDue ? _originalTimeZone : System.TimeZoneInfo.Local.Id; } }
        public int ReminderMinutes { get { return (int)_advance.Value; } }
        private DateTime SelectedDue { get { return _date.Value.Date.Add(_time.Value.TimeOfDay).AddTicks(-(_time.Value.Ticks % TimeSpan.TicksPerMinute)); } }

        public TaskEditorDialog(TaskItem task, Color accent) : this(task, accent, 30) { }

        public TaskEditorDialog(TaskItem task, Color accent, int defaultLeadMinutes)
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96, 96);
            Text = task == null ? "添加任务" : "编辑任务";
            Font = new Font("Microsoft YaHei UI", 9f);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(412, 386);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Ui.Background;
            _originalTimeZone = task == null || String.IsNullOrEmpty(task.TimeZoneId) ? System.TimeZoneInfo.Local.Id : task.TimeZoneId;
            _originalDue = task == null ? "" : task.DueLocal;

            Label name = Ui.Label("要完成什么？", 10f, Ui.Text);
            name.SetBounds(22, 18, 366, 25);
            _text = new TextBox();
            _text.SetBounds(22, 49, 366, 80);
            _text.Multiline = true;
            _text.ScrollBars = ScrollBars.Vertical;
            _text.MaxLength = 2000;
            _text.Text = task == null ? "" : task.Text;
            _text.AccessibleName = "任务内容";
            _hasDue = new CheckBox();
            _hasDue.Text = "设置截止时间与提醒";
            _hasDue.SetBounds(22, 146, 366, 25);

            _date = new DateTimePicker();
            _date.Format = DateTimePickerFormat.Custom;
            _date.CustomFormat = "yyyy-MM-dd";
            _date.SetBounds(22, 182, 207, 28);
            _time = new DateTimePicker();
            _time.Format = DateTimePickerFormat.Custom;
            _time.CustomFormat = "HH:mm";
            _time.ShowUpDown = true;
            _time.SetBounds(241, 182, 147, 28);
            DateTime due = DateTime.Now.AddHours(1);
            bool hasDue = task != null && DateTime.TryParseExact(task.DueLocal, "yyyy-MM-dd'T'HH:mm:ss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out due);
            if (!hasDue) due = DateTime.Now.AddHours(1);
            if (due < _date.MinDate) due = _date.MinDate;
            if (due > _date.MaxDate) due = _date.MaxDate;
            _date.Value = due;
            _time.Value = due;
            _hasDue.Checked = hasDue;

            Label advance = Ui.Label("提前提醒", 9f, Ui.Text);
            advance.SetBounds(22, 225, 90, 27);
            _advance = new NumericUpDown();
            _advance.SetBounds(113, 224, 116, 28);
            _advance.Minimum = 0;
            _advance.Maximum = 525600;
            _advance.ThousandsSeparator = true;
            _advance.Value = Math.Max(0, Math.Min(525600, task == null ? defaultLeadMinutes : task.ReminderMinutes));
            Label minutes = Ui.Label("分钟（0 = 到期提醒）", 8.5f, Ui.Muted);
            minutes.SetBounds(240, 225, 151, 27);
            Label explanation = Ui.Label("翻到其他页面仍会提醒；完成后停止提醒。\r\n时间使用当前本地时区，提醒在应用运行时生效。", 8.5f, Ui.Muted);
            explanation.SetBounds(22, 269, 366, 43);
            _hasDue.CheckedChanged += delegate { UpdateEnabled(); };

            Button cancel = Ui.Button("取消", delegate { DialogResult = DialogResult.Cancel; });
            cancel.SetBounds(221, 331, 78, 34);
            Button save = Ui.Button("保存任务", delegate
            {
                if (TaskText.Length == 0)
                {
                    MessageBox.Show(this, "请输入任务内容。", "还差一点内容", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    _text.Focus();
                    return;
                }
                if (_hasDue.Checked && System.TimeZoneInfo.Local.IsInvalidTime(DateTime.SpecifyKind(SelectedDue, DateTimeKind.Unspecified)))
                {
                    MessageBox.Show(this, "此时间处于夏令时跳转区间，请选择一个有效的本地时间。", "时间不可用", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                DialogResult = DialogResult.OK;
            });
            save.BackColor = accent;
            save.ForeColor = Color.White;
            save.SetBounds(309, 331, 79, 34);
            Controls.Add(name);
            Controls.Add(_text);
            Controls.Add(_hasDue);
            Controls.Add(_date);
            Controls.Add(_time);
            Controls.Add(advance);
            Controls.Add(_advance);
            Controls.Add(minutes);
            Controls.Add(explanation);
            Controls.Add(cancel);
            Controls.Add(save);
            CancelButton = cancel;
            UpdateEnabled();
            Shown += delegate { _text.Focus(); _text.SelectionStart = _text.TextLength; };
            ResumeLayout(true);
        }

        private void UpdateEnabled()
        {
            _date.Enabled = _hasDue.Checked;
            _time.Enabled = _hasDue.Checked;
            _advance.Enabled = _hasDue.Checked;
        }
    }
}
