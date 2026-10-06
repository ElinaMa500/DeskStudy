using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows.Forms;

namespace DeskStudy
{
    // Desktop and Start-menu shortcuts to the program. The zip has no installer, so the first start offers them
    // and "显示与布局" turns them on and off. A shortcut is ours when it has our name and points at a DeskStudy.exe.
    internal static class AppShortcuts
    {
        private static readonly string[] Names = { "桌面课笺.lnk", "DeskStudy.lnk" };

        // Opening the exe straight from the zip runs a copy in the temp folder, which Windows deletes later.
        public static bool IsTemporary(string program)
        {
            string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return Path.GetFullPath(program).StartsWith(temp, StringComparison.OrdinalIgnoreCase);
        }
        public static string Find(string folder, string program)
        {
            foreach (string name in Names)
            {
                string path = Path.Combine(folder, name);
                if (File.Exists(path) && IsOurs(Target(path), program)) return path;
            }
            return null;
        }
        public static void Create(string folder, string program, string name)
        {
            Remove(folder, program);
            Directory.CreateDirectory(folder);
            var link = (IShellLinkW)new ShellLink();
            try
            {
                link.SetPath(program); link.SetWorkingDirectory(Path.GetDirectoryName(program));
                link.SetIconLocation(program, 0); link.SetDescription(Lang.T("课表、Todo 与 DDL 桌面组件"));
                ((IPersistFile)link).Save(Path.Combine(folder, name + ".lnk"), true);
            }
            finally { Marshal.ReleaseComObject(link); }
        }
        public static void Remove(string folder, string program)
        {
            foreach (string name in Names)
            {
                string path = Path.Combine(folder, name);
                if (File.Exists(path) && IsOurs(Target(path), program)) File.Delete(path);
            }
        }
        // After the folder was moved or a newer version was unzipped elsewhere, our shortcut follows the running program.
        public static void Refresh(string folder, string program)
        {
            foreach (string name in Names)
            {
                string path = Path.Combine(folder, name), target = File.Exists(path) ? Target(path) : null;
                if (target != null && IsOurs(target, program) && !String.Equals(target, program, StringComparison.OrdinalIgnoreCase))
                    Create(folder, program, Path.GetFileNameWithoutExtension(name));
            }
        }
        public static string Target(string shortcut)
        {
            var link = (IShellLinkW)new ShellLink();
            try
            {
                ((IPersistFile)link).Load(shortcut, 0);
                var path = new StringBuilder(260);
                link.GetPath(path, path.Capacity, IntPtr.Zero, 4); // SLGP_RAWPATH: as stored, without searching for a moved file
                return path.ToString();
            }
            finally { Marshal.ReleaseComObject(link); }
        }
        private static bool IsOurs(string target, string program)
        {
            string file = Path.GetFileName(target ?? "");
            return file.Equals("DeskStudy.exe", StringComparison.OrdinalIgnoreCase) || file.Equals(Path.GetFileName(program), StringComparison.OrdinalIgnoreCase);
        }

        [ComImport, Guid("00021401-0000-0000-C000-000000000046")] private class ShellLink { }
        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int size, IntPtr findData, uint flags);
            void GetIDList(out IntPtr idList);
            void SetIDList(IntPtr idList);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int size);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder dir, int size);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int size);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
            void GetHotkey(out short hotkey);
            void SetHotkey(short hotkey);
            void GetShowCmd(out int showCmd);
            void SetShowCmd(int showCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int size, out int icon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int icon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string relative, uint reserved);
            void Resolve(IntPtr hwnd, uint flags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
        }
    }

    // Asked once, on the first start by hand (not at sign-in): which shortcuts to create.
    internal sealed class ShortcutPrompt : Form
    {
        public readonly CheckBox Desktop, StartMenu;
        public ShortcutPrompt(Icon icon)
        {
            SuspendLayout(); AutoScaleDimensions = new SizeF(96F, 96F); AutoScaleMode = AutoScaleMode.Dpi;
            Text = Lang.T("桌面课笺"); Icon = icon; ClientSize = new Size(420, 236); FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false; TopMost = true;
            Font = new Font("Microsoft YaHei UI", 9.5F); BackColor = Ui.Background;
            var heading = new Label { Text = Lang.T("欢迎使用桌面课笺"), AutoSize = true, Location = new Point(24, 20), ForeColor = Ui.Text, Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold) };
            var detail = Ui.Label(Lang.T("要创建快捷方式，方便以后打开吗？"), 9.5F, Ui.Muted); detail.Location = new Point(24, 54);
            Desktop = new CheckBox { Name = "prompt-desktop", Text = Lang.T("在桌面创建快捷方式"), AutoSize = true, Checked = true, Location = new Point(27, 88) };
            StartMenu = new CheckBox { Name = "prompt-start-menu", Text = Lang.T("添加到开始菜单"), AutoSize = true, Location = new Point(27, 118) };
            var later = Ui.Label(Lang.T("以后可在「设置 → 显示与布局」中更改。"), 8.5F, Ui.Muted); later.Location = new Point(24, 156);
            var ok = Ui.Button(Lang.T("好的"), delegate { DialogResult = DialogResult.OK; }); ok.Name = "prompt-ok"; ok.AutoSize = false; ok.SetBounds(316, 188, 80, 32);
            Controls.AddRange(new Control[] { heading, detail, Desktop, StartMenu, later, ok });
            AcceptButton = ok;
            ResumeLayout(true);
        }
    }

    public sealed partial class AppController
    {
        // Tests give their own folders and program path; otherwise only the program on the real data folder touches
        // the user's desktop and Start menu, like the sign-in entry.
        internal static string TestDesktop, TestStartMenu, TestProgram;
        private string ProgramPath { get { return TestProgram ?? Application.ExecutablePath; } }
        private string ShortcutFolder(string where)
        {
            if (TestDesktop != null) return where == "desktop" ? TestDesktop : TestStartMenu;
            if (!UsesSystemStartup) return null;
            return Environment.GetFolderPath(where == "desktop" ? Environment.SpecialFolder.DesktopDirectory : Environment.SpecialFolder.Programs);
        }
        public bool ShortcutsManaged { get { return ShortcutFolder("desktop") != null; } }
        public bool RunningFromTemporaryFolder { get { return AppShortcuts.IsTemporary(ProgramPath); } }
        public bool HasShortcut(string where)
        {
            string folder = ShortcutFolder(where);
            try { return folder != null && AppShortcuts.Find(folder, ProgramPath) != null; }
            catch (Exception ex) { if (ex is IOException || ex is UnauthorizedAccessException || ex is COMException) return false; throw; }
        }
        public void SetShortcut(string where, bool on)
        {
            string folder = ShortcutFolder(where); if (folder == null) return;
            if (on && RunningFromTemporaryFolder) throw new InvalidOperationException(Lang.T("程序正从临时文件夹运行（可能是直接在压缩包里打开的）。请先把压缩包解压到固定的文件夹，再创建快捷方式。"));
            if (on) AppShortcuts.Create(folder, ProgramPath, Lang.T("桌面课笺")); else AppShortcuts.Remove(folder, ProgramPath);
        }
        internal void RefreshShortcuts()
        {
            if (RunningFromTemporaryFolder) return;
            foreach (string where in new[] { "desktop", "startmenu" })
            {
                string folder = ShortcutFolder(where);
                if (folder != null) { try { AppShortcuts.Refresh(folder, ProgramPath); } catch (Exception) { } }
            }
        }
        // Asked once. Skipped (and asked again next time) while running from the temp folder; marked done without asking
        // when a shortcut already exists.
        public void OfferShortcuts()
        {
            if (Exiting || Data.Settings.ShortcutsOffered || !ShortcutsManaged || RunningFromTemporaryFolder) return;
            if (!HasShortcut("desktop") && !HasShortcut("startmenu"))
                using (var prompt = new ShortcutPrompt(AppIcon))
                {
                    if (prompt.ShowDialog() == DialogResult.OK)
                    {
                        try { if (prompt.Desktop.Checked) SetShortcut("desktop", true); if (prompt.StartMenu.Checked) SetShortcut("startmenu", true); }
                        catch (Exception ex) { MessageBox.Show(Lang.T("没能创建快捷方式，可以稍后在设置中心重试。\n\n") + ex.Message, Lang.T("快捷方式"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                    }
                }
            Data.Settings.ShortcutsOffered = true; SettingsChanged();
        }
    }
}
