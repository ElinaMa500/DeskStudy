using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using DeskStudy;

// Desktop and Start-menu shortcuts: offered once on the first start, switchable in settings, following a moved folder.
// Everything happens in the test's own folders; the real desktop and Start menu are never touched.
public static class ShortcutChecks
{
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hwnd, IntPtr dc, uint flags);
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static void Pump(int ms) { var clock = Stopwatch.StartNew(); while (clock.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(10); } }
    static string desk, start;

    // Called before the program starts, so the prompt one second after start-up uses the test folders.
    public static void Prepare(string path)
    {
        desk = Path.Combine(path, "desk"); start = Path.Combine(path, "start");
        Directory.CreateDirectory(desk); Directory.CreateDirectory(start);
        AppController.TestDesktop = desk; AppController.TestStartMenu = start; AppController.TestProgram = Program(path, "v1");
    }
    static string Program(string path, string folder)
    {
        string exe = Path.Combine(path, folder, "DeskStudy.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(exe)); if (!File.Exists(exe)) File.WriteAllText(exe, "");
        return exe;
    }
    // Answers a prompt that opens while pumping; returns how many prompts appeared.
    static int Answer(int ms, bool startMenu, string shot) { return Answer(ms, startMenu, shot, null); }
    // The timer starts first, so a prompt that should not appear is answered (and counted) instead of blocking the test.
    static int Answer(int ms, bool startMenu, string shot, Action during)
    {
        int seen = 0;
        var timer = new System.Windows.Forms.Timer { Interval = 50 };
        timer.Tick += delegate
        {
            var prompt = Application.OpenForms.OfType<ShortcutPrompt>().FirstOrDefault(); if (prompt == null || !prompt.Visible) return;
            seen++;
            if (seen == 1)
            {
                Assert(prompt.Desktop.Checked && !prompt.StartMenu.Checked && prompt.TopMost && !prompt.ShowInTaskbar, "the prompt offers a desktop shortcut (ticked) and the Start menu (not ticked), in front of the widgets");
                if (shot != null) Capture(prompt, shot);
            }
            prompt.StartMenu.Checked = startMenu;
            prompt.Controls.OfType<Button>().Single(b => b.Name == "prompt-ok").PerformClick();
        };
        timer.Start(); if (during != null) during(); Pump(ms); timer.Stop(); timer.Dispose();
        return seen;
    }
    static void Capture(Form form, string file)
    {
        using (var bmp = new Bitmap(form.Width, form.Height))
        {
            using (var g = Graphics.FromImage(bmp)) { IntPtr dc = g.GetHdc(); PrintWindow(form.Handle, dc, 2); g.ReleaseHdc(dc); }
            bmp.Save(file);
        }
        Console.WriteLine("SHOT " + file);
    }
    static string Link(string folder) { return Path.Combine(folder, Lang.T("桌面课笺") + ".lnk"); }

    public static void Run(AppController app, string path)
    {
        Assert(app.ShortcutsManaged && !app.RunningFromTemporaryFolder && !app.Data.Settings.ShortcutsOffered, "a new user has not been asked yet, and the test folders stand in for the desktop and Start menu");

        // First start: the prompt appears by itself about a second after start-up.
        int prompts = Answer(2500, true, Path.Combine(path, "shortcut-prompt-" + (Lang.IsEnglish ? "en" : "zh") + ".png"));
        Assert(prompts == 1 && app.Data.Settings.ShortcutsOffered, "the prompt appears once on the first start and is remembered");
        Assert(File.Exists(Link(desk)) && AppShortcuts.Target(Link(desk)) == AppController.TestProgram, "desktop shortcut created, pointing at the program");
        Assert(File.Exists(Link(start)) && AppShortcuts.Target(Link(start)) == AppController.TestProgram, "Start-menu shortcut created too, as ticked");
        Assert(app.HasShortcut("desktop") && app.HasShortcut("startmenu"), "both are recognised as ours");

        // Asked only once.
        Assert(Answer(300, false, null, app.OfferShortcuts) == 0, "not asked a second time");

        // Settings: the boxes follow the files and switch them.
        app.OpenSettings(); Pump(100);
        var settings = Application.OpenForms.OfType<SettingsForm>().Single();
        Find<ListBox>(settings, "settings-nav").SelectedIndex = 1; Pump(150);
        var deskBox = Find<CheckBox>(settings, "desktop-shortcut"); var startBox = Find<CheckBox>(settings, "start-menu-shortcut");
        Assert(deskBox.Checked && startBox.Checked && deskBox.Enabled, "settings show both shortcuts as present");
        settings.Size = new Size(1100, 900); Pump(100); Capture(settings, Path.Combine(path, "shortcut-settings-" + (Lang.IsEnglish ? "en" : "zh") + ".png"));
        startBox.Checked = false; Pump(100);
        Assert(!File.Exists(Link(start)) && File.Exists(Link(desk)), "unticking the Start menu removes only that shortcut");
        File.Delete(Link(desk)); app.SettingsChanged(); settings.RefreshData(); Pump(100);
        Assert(!deskBox.Checked, "a shortcut deleted by hand shows as unticked");
        deskBox.Checked = true; Pump(100);
        Assert(File.Exists(Link(desk)), "ticking it creates it again");
        settings.Close(); Pump(100);

        // The folder moved (or a newer version unzipped elsewhere): the shortcut follows at the next start.
        AppController.TestProgram = Program(path, "v2");
        app.RefreshShortcuts();
        Assert(AppShortcuts.Target(Link(desk)) == AppController.TestProgram, "after moving the program, the desktop shortcut points at the new place");

        // Someone else's shortcut with the same name is left alone.
        app.SetShortcut("desktop", false);
        string notepad = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "notepad.exe");
        AppShortcuts.Create(desk, notepad, Lang.T("桌面课笺"));
        Assert(!app.HasShortcut("desktop"), "a shortcut of the same name pointing elsewhere is not ours");
        app.SetShortcut("desktop", false); app.RefreshShortcuts();
        Assert(File.Exists(Link(desk)) && AppShortcuts.Target(Link(desk)) == notepad, "it is neither deleted nor repointed");
        File.Delete(Link(desk));

        // A user who already has a shortcut is not asked.
        app.Data.Settings.ShortcutsOffered = false; app.SetShortcut("desktop", true);
        Assert(Answer(300, false, null, app.OfferShortcuts) == 0 && app.Data.Settings.ShortcutsOffered, "with a shortcut already there, nothing is asked and the question counts as done");

        // Running straight from the zip (a temp copy): no prompt, no shortcut, asked again from a proper folder.
        string real = AppController.TestProgram;
        AppController.TestProgram = Path.Combine(Path.GetTempPath(), "Temp1_DeskStudy-1.6-Windows.zip", "DeskStudy.exe");
        app.Data.Settings.ShortcutsOffered = false;
        Assert(app.RunningFromTemporaryFolder && Answer(300, false, null, app.OfferShortcuts) == 0 && !app.Data.Settings.ShortcutsOffered, "from the temp folder nothing is asked, and the question waits for a proper start");
        bool refused = false; try { app.SetShortcut("startmenu", true); } catch (InvalidOperationException) { refused = true; }
        Assert(refused && !File.Exists(Link(start)), "creating a shortcut to a temp copy is refused with an explanation");
        AppController.TestProgram = real; app.Data.Settings.ShortcutsOffered = true;

        app.Save(); Assert(app.Flush(), "shortcut choice saved");
    }

    public static void Read(AppController app)
    {
        Assert(app.Data.Settings.ShortcutsOffered && Answer(2000, false, null) == 0, "after a restart the question is not asked again");
        Assert(app.HasShortcut("desktop"), "the desktop shortcut is still recognised");
    }

    static T Find<T>(Control root, string name) where T : Control
    {
        foreach (Control c in root.Controls) { if (c is T && c.Name == name) return (T)c; var inner = Find<T>(c, name); if (inner != null) return inner; }
        return null;
    }
}
