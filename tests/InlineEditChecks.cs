using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using DeskStudy;

// Double-click a task's text to edit it in place.
public static class InlineEditChecks
{
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hwnd, IntPtr dc, uint flags);
    static void Capture(Form form, string file)
    {
        using (var bmp = new Bitmap(form.Width, form.Height))
        {
            using (var g = Graphics.FromImage(bmp)) { IntPtr dc = g.GetHdc(); PrintWindow(form.Handle, dc, 2); g.ReleaseHdc(dc); }
            bmp.Save(file);
        }
    }
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static void Pump(int ms) { var clock = Stopwatch.StartNew(); while (clock.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(10); } }
    static IEnumerable<Control> All(Control root) { foreach (Control c in root.Controls) { yield return c; foreach (Control d in All(c)) yield return d; } }
    static TaskItem Task(AppController app, string id) { return app.Data.Books.SelectMany(b => b.Pages).SelectMany(p => p.Tasks).FirstOrDefault(t => t.Id == id); }
    static Label TitleOf(NotebookForm form, string text) { return All(form).OfType<Label>().First(l => l.Text == text && l.Visible); }
    static TextBox Editor(NotebookForm form) { return All(form).OfType<TextBox>().FirstOrDefault(t => t.Name == "task-inline-editor"); }
    static void DoubleClick(Label title, Point at)
    {
        typeof(Control).GetMethod("OnMouseDoubleClick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(title, new object[] { new MouseEventArgs(MouseButtons.Left, 2, at.X, at.Y, 0) });
        Pump(100);
    }
    static void Key(TextBox box, Keys key) { SendMessage(box.Handle, 0x0100, new IntPtr((int)key), IntPtr.Zero); Pump(150); }

    public static void Run(AppController app, string path, bool afterRestart)
    {
        var form = (NotebookForm)app.Widgets.First(w => w.WidgetKey == "todo");
        var book = app.Data.Books.First(b => b.Id == "todo");
        if (afterRestart)
        {
            var saved = book.Pages.SelectMany(p => p.Tasks).FirstOrDefault(t => t.Text.StartsWith("读完论文第三章"));
            Assert(saved != null && saved.Text == "读完论文第三章\n记下问题", "an edited task keeps its new text, line break included, after a restart");
            return;
        }
        app.ShowAll(); Pump(200);
        var page = book.Pages.First(p => p.Id == book.CurrentPageId);
        var task = new TaskItem { Text = "读完论文第二章", DueLocal = DateTime.Today.AddDays(3).ToString("yyyy-MM-dd") + "T18:00:00", TimeZoneId = TimeZoneInfo.Local.Id, ReminderMinutes = 45, AdvanceNotifiedKey = "kept-key" };
        page.Tasks.Add(task); app.Save(); Pump(250);

        foreach (string layout in SettingsLogic.NotebookLayouts)
        {
            app.Data.Settings.NotebookLayout = layout; app.SettingsChanged(); Pump(250);
            var title = TitleOf(form, task.Text);
            DoubleClick(title, new Point(4, title.Height / 2));
            var editor = Editor(form);
            Assert(editor != null && editor.Focused && !title.Visible && editor.Text == task.Text, layout + ": double-clicking the task text opens an editor in its place");
            Key(editor, Keys.Escape);
            Assert(Editor(form) == null && TitleOf(form, task.Text).Visible, layout + ": Esc closes it and keeps the text");
        }

        app.Data.Settings.NotebookLayout = "Clean"; app.SettingsChanged(); Pump(250);
        // The caret lands where the text was double-clicked.
        var label = TitleOf(form, task.Text);
        int twoChars = TextRenderer.MeasureText("读完", label.Font, Size.Empty, TextFormatFlags.NoPadding).Width;
        DoubleClick(label, new Point(twoChars + 1, label.Height / 2));
        var box = Editor(form);
        Assert(box.SelectionStart == 2 && box.SelectionLength == 0, "the caret is placed where the text was double-clicked (" + box.SelectionStart + ")");

        // A refresh while editing does not throw the editor away.
        app.Save(); form.RefreshData(); Pump(150);
        Assert(Editor(form) == box && !box.IsDisposed, "a refresh while editing waits until the edit is finished");

        // Enter saves; due time and reminder state are untouched.
        box.Text = "读完论文第三章"; Pump(80);
        Key(box, Keys.Enter);
        Assert(Editor(form) == null && Task(app, task.Id).Text == "读完论文第三章" && TitleOf(form, "读完论文第三章").Visible, "Enter saves the new text");
        Assert(Task(app, task.Id).DueLocal == task.DueLocal && Task(app, task.Id).ReminderMinutes == 45 && Task(app, task.Id).AdvanceNotifiedKey == "kept-key", "editing the text leaves the due time and reminder state alone");

        // Emptied: the old text comes back.
        DoubleClick(TitleOf(form, "读完论文第三章"), new Point(4, 8));
        box = Editor(form); box.Text = "   "; Pump(80); Key(box, Keys.Enter);
        Assert(Task(app, task.Id).Text == "读完论文第三章", "an emptied task keeps its old text");

        // A line break (Shift+Enter in use) is kept, and the row grows.
        int oneLine = TitleOf(form, "读完论文第三章").Height;
        DoubleClick(TitleOf(form, "读完论文第三章"), new Point(4, 8));
        box = Editor(form); box.Text = "读完论文第三章\r\n记下问题"; Pump(120);
        Assert(box.Height > oneLine, "the editor grows when the text gets a second line");
        box.Select(box.TextLength, 0); Pump(80);
        Capture(form, System.IO.Path.Combine(path, "inline-clean.png"));
        // Clicking elsewhere saves.
        All(form).OfType<TextBox>().First(t => t.Name == "quick-task").Focus(); Pump(150);
        Assert(Editor(form) == null && Task(app, task.Id).Text == "读完论文第三章\n记下问题", "clicking elsewhere saves, keeping the line break");
        Assert(TitleOf(form, "读完论文第三章\n记下问题").Height > oneLine, "the task shows on two lines");

        // A completed task can be edited too.
        var done = new TaskItem { Text = "已完成的任务", Completed = true, TimeZoneId = TimeZoneInfo.Local.Id };
        page.Tasks.Add(done); app.Save(); Pump(200);
        DoubleClick(TitleOf(form, "已完成的任务"), new Point(4, 8));
        box = Editor(form); box.Text = "已完成的任务（改）"; Pump(60); Key(box, Keys.Enter);
        Assert(Task(app, done.Id).Text == "已完成的任务（改）" && Task(app, done.Id).Completed, "a completed task can be edited and stays completed");

        // The task is deleted elsewhere while being edited: the editor just closes.
        DoubleClick(TitleOf(form, "已完成的任务（改）"), new Point(4, 8));
        Assert(Editor(form) != null, "editing again");
        page.Tasks.RemoveAll(t => t.Id == done.Id); app.Save(); form.RefreshData(); Pump(200);
        Assert(Editor(form) == null && Task(app, done.Id) == null, "a task deleted elsewhere while being edited closes the editor without an error");

        app.Save(); Assert(app.Flush(), "inline edit checks saved");
    }
}
