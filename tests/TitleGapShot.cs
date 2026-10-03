using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using DeskStudy;

// Measures the blank space above and below a notebook's page title, from the widgets' own pictures.
public static class TitleGapShot
{
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hwnd, IntPtr dc, uint flags);
    static void Pump(int ms) { var clock = Stopwatch.StartNew(); while (clock.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(10); } }
    static IEnumerable<Control> All(Control root) { foreach (Control c in root.Controls) { yield return c; foreach (Control d in All(c)) yield return d; } }

    static Bitmap Picture(Form w)
    {
        var bmp = new Bitmap(w.Width, w.Height);
        using (var g = Graphics.FromImage(bmp)) { IntPtr dc = g.GetHdc(); PrintWindow(w.Handle, dc, 2); g.ReleaseHdc(dc); }
        return bmp;
    }
    static Rectangle InWindow(Form w, Control c) { Point p = w.PointToClient(c.PointToScreen(Point.Empty)); Point o = w.PointToClient(w.Location); return new Rectangle(p.X - o.X, p.Y - o.Y, c.Width, c.Height); }
    static bool Ink(Bitmap b, int y, int x0, int x1, Color back, int threshold)
    {
        for (int x = Math.Max(0, x0); x < Math.Min(b.Width, x1); x++)
        {
            Color c = b.GetPixel(x, y);
            if (Math.Abs(c.R - back.R) + Math.Abs(c.G - back.G) + Math.Abs(c.B - back.B) > threshold) return true;
        }
        return false;
    }

    public static void Run(AppController app, string path, string tag)
    {
        string shots = Path.Combine(path, "title"); Directory.CreateDirectory(shots);
        var todo = (NotebookForm)app.Widgets.First(w => w.WidgetKey == "todo"); var ddl = (NotebookForm)app.Widgets.First(w => w.WidgetKey == "ddl");
        var t = app.Data.Books.First(b => b.Id == "todo"); var d = app.Data.Books.First(b => b.Id == "ddl");
        t.Pages[0].Title = "开学week2 TODO"; t.Pages[0].Tasks.Clear();
        foreach (var s in new[] { "交选课确认单", "买教材", "办校园卡" }) t.Pages[0].Tasks.Add(new TaskItem { Text = s });
        d.Pages[0].Title = "课程作业"; d.Pages[0].Tasks.Clear();
        d.Pages[0].Tasks.Add(new TaskItem { Text = "物理实验报告", DueLocal = DateTime.Today.AddDays(2).ToString("yyyy-MM-dd") + "T11:30:00" });
        d.Pages[0].Tasks.Add(new TaskItem { Text = "英语作文", DueLocal = DateTime.Today.AddDays(4).ToString("yyyy-MM-dd") + "T23:59:00", DueDateOnly = true });
        app.Data.Settings.GlobalAppearance.FontSize = 9; app.Data.Settings.GlobalAppearance.Opacity = 1;
        foreach (var w in app.Widgets) app.Data.Windows[w.WidgetKey].PositionLocked = false;
        app.ShowAll(); app.Widgets.First(w => w.WidgetKey == "calendar").Hide();
        foreach (string layout in new[] { "Journal", "Clean" })
        {
            app.Data.Settings.NotebookLayout = layout; app.SettingsChanged(); app.Save(); Pump(300);
            ddl.Bounds = new Rectangle(1074, 0, 420, 488); todo.Bounds = new Rectangle(1499, 0, 421, 488); Pump(400);
            foreach (var w in new[] { ddl, todo })
            {
                using (var bmp = Picture(w))
                {
                    var title = All(w).First(c => c is TextBox && c.Visible && c.Font.Size >= 12);
                    var modern = title.Parent;
                    var tasks = All(w).OfType<ScrollableControl>().Where(c => c.Visible && c.Parent == modern && c.Top > title.Bottom).OrderBy(c => c.Top).First();
                    Color back = modern.BackColor;
                    Rectangle top = InWindow(w, modern), tr = InWindow(w, title), lr = InWindow(w, tasks);
                    int x0 = tr.Left + 2, x1 = tr.Left + 150;
                    int inkTop = Enumerable.Range(top.Top + 2, 80).First(y => Ink(bmp, y, x0, x1, back, 160));
                    int y2 = inkTop; while (Ink(bmp, y2, x0, x1, back, 160)) y2++;               // title glyphs end
                    int dateTop = Enumerable.Range(y2, 60).First(y => Ink(bmp, y, x0, x1, back, 90));
                    int dateBottom = dateTop; while (Ink(bmp, dateBottom, x0, x1, back, 90)) dateBottom++;
                    int taskTop = Enumerable.Range(dateBottom, 80).First(y => Ink(bmp, y, lr.Left + 2, lr.Left + 250, back, 90));
                    Console.WriteLine(String.Format("{0} {1} {2}: header line→title glyph top {3} px; title glyph bottom→date line {4} px; date line→first task {5} px; title box {6} px tall at y={7}",
                        tag, layout, w.WidgetKey, inkTop - top.Top, dateTop - y2, taskTop - dateBottom, tr.Height, tr.Top - top.Top));
                    var gear = All(w).OfType<Button>().First(c => c.Name == "open-settings");
                    Console.WriteLine(String.Format("{0} {1} {2}: top bar {3} px tall (window top → rule), gear button {4} px", tag, layout, w.WidgetKey, top.Top, gear.Height));
                }
            }
            using (var canvas = new Bitmap(ddl.Width + todo.Width + 29, 260))
            {
                using (var g = Graphics.FromImage(canvas))
                {
                    g.Clear(Color.FromArgb(214, 222, 218));
                    using (var a = Picture(ddl)) g.DrawImage(a, new Rectangle(12, 12, a.Width, 236), new Rectangle(0, 0, a.Width, 236), GraphicsUnit.Pixel);
                    using (var b = Picture(todo)) g.DrawImage(b, new Rectangle(17 + ddl.Width, 12, b.Width, 236), new Rectangle(0, 0, b.Width, 236), GraphicsUnit.Pixel);
                }
                canvas.Save(Path.Combine(shots, tag + "-" + layout + ".png"));
            }
        }
        Console.WriteLine("TITLE SHOTS: " + shots);
    }
}
