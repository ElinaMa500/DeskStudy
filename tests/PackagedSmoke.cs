using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

public static class PackagedSmoke
{
    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS " + message); }
    private static void Pump(int duration) { var timer = Stopwatch.StartNew(); while (timer.ElapsedMilliseconds < duration) { Application.DoEvents(); Thread.Sleep(10); } }
    [STAThread] public static int Main(string[] args)
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        try
        {
            string exe = Path.GetFullPath(args[0]);
            Assembly assembly = Assembly.LoadFrom(exe);
            Assert(assembly.GetName().Version.ToString() == "1.4.0.0", "packaged executable has version 1.4.0.0");
            Assert(assembly.EntryPoint != null && assembly.EntryPoint.GetCustomAttributes(typeof(STAThreadAttribute), false).Length == 1, "packaged executable contains its STA Windows application entry point");
            Type type = assembly.GetType("DeskStudy.AppController", true);
            for (int pass = 0; pass < 2; pass++)
            {
                using (var app = (ApplicationContext)Activator.CreateInstance(type, new object[] { args[1], null, false }))
                {
                    Pump(120);
                    Assert(Application.OpenForms.Cast<Form>().Count(f => f.GetType().BaseType.Name == "WidgetForm") == 3, "packaged code opens three desktop widgets, pass " + pass);
                    type.GetMethod("OpenSettings").Invoke(app, null); Pump(120);
                    var settings = Application.OpenForms.Cast<Form>().Single(f => f.GetType().Name == "SettingsForm");
                    Assert(settings.Visible, "packaged code opens the settings center");
                    settings.Close(); Pump(30);
                    Assert(!(bool)type.GetProperty("Exiting").GetValue(app, null), "closing packaged settings keeps the application running");
                    type.GetMethod("Save").Invoke(app, null);
                    type.GetMethod("Shutdown").Invoke(app, null);
                }
            }
            Assert(File.Exists(Path.Combine(args[1], "data.json")), "packaged code saves local data and reopens it successfully");
            Console.WriteLine("PACKAGED SMOKE PASSED: " + exe); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
