using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using DeskStudy;

// Screenshots for the landing page: three typical users and the five notebook layouts, from sample data only.
// Run once with a Chinese and once with an English first start: docs/images/site/zh and .../en.
public static class SiteShots
{
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hwnd, IntPtr dc, uint flags);
    [DllImport("user32.dll")] static extern int GetWindowRgn(IntPtr hwnd, IntPtr region);
    [DllImport("gdi32.dll")] static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr handle);
    static void Pump(int ms) { var clock = Stopwatch.StartNew(); while (clock.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(10); } }
    static IEnumerable<Control> All(Control root) { foreach (Control c in root.Controls) { yield return c; foreach (Control d in All(c)) yield return d; } }
    static string folder; static bool en;
    static string L(string zh, string english) { return en ? english : zh; }

    static Bitmap Picture(Control w)
    {
        var bmp = new Bitmap(w.Width, w.Height);
        using (var g = Graphics.FromImage(bmp)) { IntPtr dc = g.GetHdc(); PrintWindow(w.Handle, dc, 2); g.ReleaseHdc(dc); }
        return bmp;
    }
    static void Desk(string name, params Control[] widgets)
    {
        Rectangle all = widgets.Select(w => w.Bounds).Aggregate(Rectangle.Union);
        using (var canvas = new Bitmap(all.Width + 32, all.Height + 32))
        {
            using (var g = Graphics.FromImage(canvas))
            {
                g.Clear(Color.FromArgb(214, 222, 218));
                foreach (var w in widgets) using (var b = Picture(w))
                {
                    int x = w.Left - all.Left + 16, y = w.Top - all.Top + 16;
                    // Rounded widgets are clipped by a window region; PrintWindow leaves the cut-off corners black, so paste
                    // the picture through the same region.
                    IntPtr rgn = CreateRectRgn(0, 0, 0, 0);
                    if (GetWindowRgn(w.Handle, rgn) > 1)
                        using (var clip = Region.FromHrgn(rgn)) { clip.Translate(x, y); g.SetClip(clip, System.Drawing.Drawing2D.CombineMode.Replace); }
                    DeleteObject(rgn);
                    g.DrawImage(b, x, y);
                    g.ResetClip();
                }
            }
            canvas.Save(Path.Combine(folder, name + ".png"));
        }
        Console.WriteLine("SHOT " + name);
    }

    static DateTime monday, soon;
    static string Day(int n) { return monday.AddDays(n).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
    static CalendarEvent Course(string title, int day, string start, string end, string color, string place, int every, params int[] weekdays)
    {
        var e = new CalendarEvent { Title = title, Date = Day(day), StartTime = start, EndTime = end, Color = color, Location = place, RepeatWeeks = every,
            RepeatEndDate = every == 0 ? Day(day) : monday.AddDays(120).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) };
        if (every > 0) e.WeekDays = (weekdays.Length > 0 ? weekdays : new[] { (int)monday.AddDays(day).DayOfWeek }).ToList();
        return e;
    }
    static CalendarEvent Whole(string title, int day, int days, string color)
    {
        return new CalendarEvent { Title = title, Date = Day(day), StartTime = "00:00", EndTime = "23:59", AllDay = true, Days = days, Color = color, RepeatWeeks = 0, RepeatEndDate = Day(day) };
    }
    // days from tomorrow; time "HH:mm", or "" for a date-only deadline; days < 0: no deadline.
    static TaskItem Task(string text, int days, string time, bool done)
    {
        var t = new TaskItem { Text = text, Completed = done };
        if (days >= 0)
        {
            DateTime due = time == "" ? soon.AddDays(days).Date.AddHours(23).AddMinutes(59) : soon.AddDays(days).Date.Add(TimeSpan.Parse(time, CultureInfo.InvariantCulture));
            t.DueDateOnly = time == ""; t.DueLocal = due.ToString(TimeUtil.LocalFormat, CultureInfo.InvariantCulture);
        }
        return t;
    }

    sealed class Persona
    {
        public string Name, TodoTitle, DdlTitle;
        public List<CalendarEvent> Events = new List<CalendarEvent>();
        public List<TaskItem> Todo = new List<TaskItem>(), Ddl = new List<TaskItem>();
    }

    static List<Persona> Personas()
    {
        var research = new Persona { Name = "persona-research", TodoTitle = L("论文修改", "Paper revision"), DdlTitle = L("投稿与申请", "Submissions") };
        research.Events.Add(Course(L("课题组组会", "Group meeting"), 0, "09:30", "11:00", "#939C63", L("实验楼 402", "Lab 402"), 2));
        research.Events.Add(Course(L("文献研讨", "Reading group"), 2, "14:00", "15:30", "#A68EB5", L("图书馆研讨室", "Library room 3"), 1));
        research.Events.Add(Course(L("导师一对一", "Supervisor 1:1"), 3, "10:00", "10:45", "#7196B1", L("导师办公室", "Office 214"), 1));
        research.Events.Add(Course(L("助教：习题课", "TA: problem class"), 1, "08:00", "09:40", "#C49472", L("教学楼 B105", "Room B105"), 1, 2, 4));
        research.Events.Add(Whole(L("学术会议", "Conference"), 4, 2, "#BB818B"));
        research.Todo.AddRange(new[] { Task(L("按审稿意见改第 4 节", "Revise section 4 for reviewers"), -1, "", false), Task(L("重跑消融实验", "Re-run the ablation study"), -1, "", true),
            Task(L("整理会议海报", "Prepare the conference poster"), 3, "18:00", false), Task(L("给合作者发初稿", "Send the draft to co-authors"), -1, "", false) });
        research.Ddl.AddRange(new[] { Task(L("会议论文截稿", "Conference paper deadline"), 2, "23:59", false), Task(L("返修意见回复", "Response to reviewers"), 1, "", false),
            Task(L("开题报告", "Thesis proposal"), 6, "", false), Task(L("奖学金申请材料", "Scholarship application"), 9, "17:00", false) });

        var student = new Persona { Name = "persona-student", TodoTitle = L("本周要做", "This week"), DdlTitle = L("课程作业", "Coursework") };
        student.Events.Add(Course(L("高等数学", "Calculus"), 0, "08:00", "09:40", "#6C9385", L("教学楼 A201", "Room A201"), 1, 1, 3));
        student.Events.Add(Course(L("线性代数", "Linear Algebra"), 1, "10:00", "11:40", "#7196B1", L("教学楼 B105", "Room B105"), 1, 2, 4));
        student.Events.Add(Course(L("大学英语", "Academic English"), 2, "13:30", "15:00", "#C49472", L("外语楼 302", "Languages 302"), 1));
        student.Events.Add(Course(L("物理实验", "Physics lab"), 4, "08:30", "11:00", "#A68EB5", L("理学楼 102", "Science 102"), 2));
        student.Events.Add(Whole(L("校运动会", "Sports day"), 5, 2, "#BB818B"));
        student.Todo.AddRange(new[] { Task(L("复习线性代数第 3 章", "Review Linear Algebra ch. 3"), -1, "", false), Task(L("预约图书馆研讨室", "Book a library study room"), -1, "", true),
            Task(L("小组讨论分工", "Split up the group project"), -1, "", false), Task(L("整理实验数据", "Tidy up the lab data"), 3, "17:00", false) });
        student.Ddl.AddRange(new[] { Task(L("高数习题 3.2", "Calculus problem set 3.2"), 0, "12:00", false), Task(L("物理实验报告", "Physics lab report"), 2, "23:59", false),
            Task(L("英语作文", "English essay"), 3, "", false), Task(L("小组展示 PPT", "Group presentation slides"), 8, "09:00", false) });

        var faculty = new Persona { Name = "persona-faculty", TodoTitle = L("教学", "Teaching"), DdlTitle = L("审稿与申请", "Reviews and grants") };
        faculty.Events.Add(Course(L("线性代数（本科）", "Linear Algebra (UG)"), 0, "08:00", "09:40", "#6C9385", L("教学楼 A201", "Room A201"), 1, 1, 3));
        faculty.Events.Add(Course(L("矩阵分析（研究生）", "Matrix Analysis (PG)"), 1, "14:00", "15:40", "#7196B1", L("研究生楼 305", "PG 305"), 1));
        faculty.Events.Add(Course(L("Office Hour", "Office hours"), 3, "10:00", "11:00", "#C49472", L("办公室 214", "Office 214"), 1));
        faculty.Events.Add(Course(L("课题组组会", "Group meeting"), 4, "09:30", "11:00", "#939C63", L("实验楼 402", "Lab 402"), 2));
        faculty.Events.Add(Whole(L("期中考试周", "Midterm week"), 7, 5, "#BB818B"));
        faculty.Todo.AddRange(new[] { Task(L("准备第 6 周课件", "Slides for next week"), -1, "", false), Task(L("批改作业 3", "Mark assignment 3"), 2, "", false),
            Task(L("更新课程主页", "Update the course page"), -1, "", true), Task(L("出期中试题", "Set the midterm paper"), 4, "12:00", false) });
        faculty.Ddl.AddRange(new[] { Task(L("期刊审稿", "Journal review"), 1, "18:00", false), Task(L("基金申请书", "Grant proposal"), 4, "", false),
            Task(L("成绩录入", "Enter grades"), 7, "10:00", false), Task(L("学生推荐信", "Reference letter"), 2, "", false) });
        return new List<Persona> { research, student, faculty };
    }

    static void Load(AppController app, Persona p)
    {
        app.Data.Events.Clear(); app.Data.Events.AddRange(p.Events);
        var todo = app.Data.Books.First(b => b.Id == "todo"); var ddl = app.Data.Books.First(b => b.Id == "ddl");
        todo.Name = L("Todo", "To-do"); ddl.Name = L("DDL", "Deadlines");
        foreach (var pair in new[] { new { book = todo, title = p.TodoTitle, tasks = p.Todo }, new { book = ddl, title = p.DdlTitle, tasks = p.Ddl } })
        {
            var page = new NotePage { Title = pair.title }; page.Tasks.AddRange(pair.tasks);
            pair.book.Pages = new List<NotePage> { page, new NotePage { Title = L("下一阶段", "Later") } }; pair.book.CurrentPageId = page.Id;
        }
        app.SettingsChanged(); Pump(400);
    }

    public static void Run(AppController app, string path, string language)
    {
        en = language == "en";
        folder = Path.Combine(path, "site", language); Directory.CreateDirectory(folder);
        var calendar = (CalendarForm)app.Widgets.First(w => w.WidgetKey == "calendar");
        var todo = (NotebookForm)app.Widgets.First(w => w.WidgetKey == "todo");
        var ddl = (NotebookForm)app.Widgets.First(w => w.WidgetKey == "ddl");
        soon = DateTime.Today.AddDays(1);
        monday = soon.AddDays(-(((int)soon.DayOfWeek + 6) % 7));
        bool nextWeek = monday > DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));

        app.Data.Settings.NotebookLayout = "Journal"; app.Data.Settings.Calendar.DefaultView = "Week"; app.Data.Settings.Calendar.WorkWeek = false;
        app.Data.Settings.GlobalAppearance.FontSize = 9; app.Data.Settings.GlobalAppearance.Opacity = 1;
        foreach (var w in app.Widgets) app.Data.Windows[w.WidgetKey].PositionLocked = false;
        app.ShowAll(); app.SettingsChanged(); Pump(400);
        var surface = All(calendar).OfType<CalendarSurface>().First();
        if (nextWeek) { All(calendar).OfType<Button>().First(b => b.Name == "calendar-next").PerformClick(); Pump(300); }

        // The three typical users: calendar under the two notebooks, the way the widgets sit on a desktop.
        var personas = Personas();
        foreach (var p in personas)
        {
            Load(app, p);
            // The two notebooks stacked on the left, the calendar beside them at the same height.
            ddl.Bounds = new Rectangle(0, 0, 420, 488); todo.Bounds = new Rectangle(0, 493, 420, 488); calendar.Bounds = new Rectangle(425, 0, 846, 488 * 2 + 5); Pump(400);
            calendar.RefreshData(); surface.AutoScrollPosition = new Point(0, surface.HourPixels * 7 + surface.HourPixels / 2); Pump(300);
            Desk(p.Name, ddl, todo, calendar);
        }

        // The five layouts: the two notebooks side by side, with the student's week.
        Load(app, personas[1]);
        foreach (string layout in new[] { "Clean", "Journal", "Card", "Paper", "Original" })
        {
            app.Data.Settings.NotebookLayout = layout; app.SettingsChanged(); Pump(450);
            ddl.Bounds = new Rectangle(0, 0, 420, 520); todo.Bounds = new Rectangle(425, 0, 421, 520); Pump(350);
            Desk("layout-" + layout.ToLowerInvariant(), ddl, todo);
        }
        Console.WriteLine("SITE SHOTS: " + folder);
    }
}
