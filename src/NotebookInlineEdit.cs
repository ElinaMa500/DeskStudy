using System;
using System.Drawing;
using System.Windows.Forms;

namespace DeskStudy
{
    // Double-click a task's text to edit it in place (1.5.1). Enter saves, Shift+Enter starts a new line,
    // Esc cancels, and clicking elsewhere or switching to another program saves.
    public sealed partial class NotebookForm
    {
        private CompositionTextBox _inlineEditor;
        private TaskRow _inlineRow;
        private string _inlineOriginal;
        private bool _inlineClosing, _inlineRefreshPending;
        public bool InlineEditing { get { return _inlineRow != null; } }

        private void WireInlineEdit(TaskRow row)
        {
            row.Title.MouseDoubleClick += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) BeginInlineEdit(row, e.Location); };
            EventHandler follow = delegate { if (_inlineRow == row && _inlineEditor != null && !row.Title.IsDisposed) _inlineEditor.Bounds = row.Title.Bounds; };
            row.Title.LocationChanged += follow; row.Title.SizeChanged += follow;
        }

        private void BeginInlineEdit(TaskRow row, Point at)
        {
            if (_inlineRow != null || row.Card.IsDisposed || row.Title.IsDisposed) return;
            _inlineRow = row;
            _inlineOriginal = row.Task.Text ?? "";
            var box = new CompositionTextBox
            {
                Name = "task-inline-editor", Multiline = true, WordWrap = true, AcceptsReturn = true, ScrollBars = ScrollBars.None,
                BorderStyle = BorderStyle.None, MaxLength = 2000, Tag = "appearance-custom-font", AccessibleName = "编辑任务内容，回车保存，Shift+回车换行，Esc 取消"
            };
            box.Font = _taskFont;
            box.ForeColor = AppearancePainter.Foreground(SettingsLogic.EffectiveAppearance(App.Data, _bookId));
            // A faint tint marks the text as being edited.
            Color back = row.Card.BackColor, ink = box.ForeColor;
            box.BackColor = Color.FromArgb((back.R * 94 + ink.R * 6) / 100, (back.G * 94 + ink.G * 6) / 100, (back.B * 94 + ink.B * 6) / 100);
            box.Text = _inlineOriginal.Replace("\r\n", "\n").Replace("\n", "\r\n");
            row.Card.Controls.Add(box);
            box.Bounds = row.Title.Bounds;
            box.BringToFront();
            row.Title.Visible = false;
            _inlineEditor = box;
            box.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (box.IsComposing) return;
                if (e.KeyCode == Keys.Enter && !e.Shift) { e.SuppressKeyPress = true; EndInlineEdit(true); }
                else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; EndInlineEdit(false); }
            };
            box.TextChanged += delegate { FitInlineEditor(); };
            box.Leave += delegate { EndInlineEdit(true); };
            Deactivate += InlineEditorDeactivated;
            box.Focus();
            box.Select(CaretAt(box, at), 0);
        }

        // The caret goes where the text was double-clicked.
        private static int CaretAt(TextBox box, Point at)
        {
            if (box.TextLength == 0) return 0;
            int index = box.GetCharIndexFromPosition(at);
            Point start = box.GetPositionFromCharIndex(index);
            string character = box.Text.Substring(index, 1);
            int width = character == "\r" || character == "\n" ? 0 : TextRenderer.MeasureText(character, box.Font, Size.Empty, TextFormatFlags.NoPadding).Width;
            bool lastLine = box.GetLineFromCharIndex(index) == box.GetLineFromCharIndex(box.TextLength - 1);
            if (at.X > start.X + width / 2 && width > 0) index++;
            if (index == box.TextLength - 1 && lastLine && at.X > start.X + width) index = box.TextLength;
            return Math.Max(0, Math.Min(box.TextLength, index));
        }

        private void InlineEditorDeactivated(object sender, EventArgs e) { EndInlineEdit(true); }

        // The row grows with the text: the hidden label carries the text so the usual layout measures it.
        private void FitInlineEditor()
        {
            if (_inlineRow == null || _inlineEditor == null || _inlineRow.Title.IsDisposed) return;
            _inlineRow.Title.Text = _inlineEditor.Text.Length == 0 ? " " : _inlineEditor.Text;
            // The whole layout, so the task list grows with the row instead of showing a scroll bar.
            if (IsModern) ArrangeModernLayout(); else LayoutTaskCards();
            _inlineEditor.Bounds = _inlineRow.Title.Bounds;
        }

        private void EndInlineEdit(bool save)
        {
            if (_inlineRow == null || _inlineClosing) return;
            _inlineClosing = true;
            TaskRow row = _inlineRow; CompositionTextBox box = _inlineEditor;
            string edited = box == null ? _inlineOriginal : box.Text.Replace("\r\n", "\n").Trim();
            string taskId = row.Task.Id, original = _inlineOriginal;
            _inlineRow = null; _inlineEditor = null;
            Deactivate -= InlineEditorDeactivated;
            try
            {
                if (box != null && !box.IsDisposed) { if (!row.Card.IsDisposed) row.Card.Controls.Remove(box); box.Dispose(); }
                if (!row.Title.IsDisposed) { row.Title.Text = original; row.Title.Visible = true; }
            }
            finally { _inlineClosing = false; }
            // An emptied task keeps its old text; deleting stays in the ⋯ menu.
            bool changed = save && edited.Length > 0 && edited != original.Replace("\r\n", "\n");
            if (changed)
            {
                NotePage page = FindPage(_displayedPageId);
                TaskItem task = page == null ? null : page.Tasks.Find(delegate(TaskItem item) { return item.Id == taskId; });
                if (task != null) { task.Text = edited; Persist(); }
            }
            bool refresh = changed || _inlineRefreshPending;
            _inlineRefreshPending = false;
            if (refresh) RefreshFromData();
            if (IsModern) ArrangeModernLayout(); else LayoutTaskCards();
        }

        // Refreshes wait while a task is being edited, unless that task has gone (deleted elsewhere).
        private bool DeferRefreshForInlineEdit()
        {
            if (_inlineRow == null) return false;
            NotePage page = FindPage(_displayedPageId);
            string id = _inlineRow.Task.Id;
            if (page != null && page.Tasks.Exists(delegate(TaskItem item) { return item.Id == id; })) { _inlineRefreshPending = true; return true; }
            EndInlineEdit(false);
            return false;
        }
    }
}
