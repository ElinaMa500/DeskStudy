using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using DeskStudy;
public static class NotebookLayoutCoreTests
{
    static int count;
    static void Assert(bool v, string name) { if (!v) throw new Exception(name); count++; Console.WriteLine("PASS " + name); }
    static JavaScriptSerializer Json() { return new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 }; }
    static Dictionary<string, object> Map(object o) { return (Dictionary<string, object>)o; }
    static void Reject(Action action, string name) { bool rejected = false; try { action(); } catch (InvalidDataException) { rejected = true; } Assert(rejected, name); }
    public static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "DeskStudy-LayoutCore-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var store = new AppStore(root); var d = store.Data;
            Assert(d.Settings.NotebookLayout == "Card" && SettingsLogic.EffectiveAppearance(d, "todo").BackgroundColor == "#F4F6F3", "fresh settings use Card and its defaults");
            d.Settings.NotebookLayout = "Paper";
            Assert(SettingsLogic.EffectiveAppearance(d, "ddl").BackgroundColor == "#FBF5E8" && SettingsLogic.EffectiveAppearance(d, "calendar").BackgroundColor == "#F7F8FB", "paper defaults apply only to notebooks");
            d.Settings.NotebookLayout = "Clean"; Assert(SettingsLogic.EffectiveAppearance(d, "todo").BackgroundColor == "#FFFFFF", "clean card defaults to the reference white background");
            d.Settings.NotebookLayout = "Journal"; Assert(SettingsLogic.EffectiveAppearance(d, "ddl").BackgroundColor == "#FAF8F1" && SettingsLogic.EffectiveAppearance(d, "calendar").BackgroundColor == "#F7F8FB", "journal defaults to the reference paper color for notebooks only");
            d.Settings.NotebookLayout = "Paper";
            SettingsLogic.MarkAppearanceCustomized(d.Settings.GlobalAppearance, "FontSize"); d.Settings.GlobalAppearance.FontSize = 12;
            d.Settings.AppearanceOverrides["todo"] = new AppearanceOptions { FontSize = 14 };
            Assert(SettingsLogic.EffectiveAppearance(d, "todo").FontSize == 14 && SettingsLogic.EffectiveAppearance(d, "ddl").FontSize == 12 && SettingsLogic.EffectiveAppearance(d, "todo").BackgroundColor == "#FBF5E8", "explicit fields follow component/global/layout priority without clobbering defaults");
            d.Settings.GlobalAppearance.BackgroundColor = "#F7F8FB"; SettingsLogic.MarkAppearanceCustomized(d.Settings.GlobalAppearance, "BackgroundColor");
            foreach (var layout in SettingsLogic.NotebookLayouts) { d.Settings.NotebookLayout = layout; Assert(SettingsLogic.EffectiveAppearance(d, "todo").BackgroundColor == "#F7F8FB", layout + " preserves explicitly selected old-default background"); }
            d.Settings.NotebookLayout = "Paper";
            var copy = SettingsLogic.CopyAppearance(d.Settings.GlobalAppearance); copy.CustomizedFields.Clear(); Assert(d.Settings.GlobalAppearance.CustomizedFields.Count == 2, "appearance copy does not alias custom-field tracking");
            d.Settings.GlobalAppearance = new AppearanceOptions { Theme = "Dark" }; d.Settings.AppearanceOverrides.Clear();
            Assert(SettingsLogic.EffectiveAppearance(d, "todo").BackgroundColor == "#252B36", "dark theme without a custom color receives a readable dark background");
            d.Books[0].Pages[0].Text = "完整的历史中文备注"; d.Books[0].Pages[0].Tasks.Add(new TaskItem { Text = "历史完成任务", Completed = true });
            store.Save(); var restored = new AppStore(root); Assert(restored.Data.Settings.NotebookLayout == "Paper" && restored.Data.Books[0].Pages[0].Tasks[0].Completed, "valid selection and content survive disk restart");
            string oldFile = Path.Combine(root, "data.json"); var oldRoot = Map(Json().DeserializeObject(File.ReadAllText(oldFile))); var settings = Map(oldRoot["Settings"]); settings.Remove("NotebookLayout"); Map(settings["GlobalAppearance"]).Remove("CustomizedFields");
            foreach (var a in Map(settings["AppearanceOverrides"]).Values) Map(a).Remove("CustomizedFields");
            string oldJson = Json().Serialize(oldRoot); File.WriteAllText(oldFile, oldJson);
            var migrated = new AppStore(root); var safety = Directory.GetFiles(root, "migration-notebook-layouts-*.json");
            Assert(migrated.Data.Settings.NotebookLayout == "Card" && safety.Length == 1 && File.ReadAllText(safety[0]) == oldJson && File.ReadAllText(oldFile) == oldJson, "old v2 lacks layout: Card default plus exact recoverable backup before overwrite");
            Assert(migrated.Data.Settings.GlobalAppearance.CustomizedFields.Count == 4 && migrated.Data.Books[0].Pages[0].Text == "完整的历史中文备注" && migrated.Data.Books[0].Pages[0].Tasks[0].Completed, "legacy appearance values and all notebook content preserved");
            migrated.Save(); var again = new AppStore(root); Assert(Directory.GetFiles(root, "migration-notebook-layouts-*.json").Length == 1, "migration completes once without repeated backup creation");
            string stable = File.ReadAllText(oldFile), state = Json().Serialize(again.Data), invalid = Path.Combine(root, "invalid.json");
            var invalidRoot = Map(Json().DeserializeObject(stable)); Map(invalidRoot["Settings"])["NotebookLayout"] = "Unknown"; File.WriteAllText(invalid, Json().Serialize(invalidRoot)); Reject(delegate { again.Import(invalid); }, "invalid layout rejected by import");
            Assert(File.ReadAllText(oldFile) == stable && Json().Serialize(again.Data) == state, "invalid layout cannot mutate live or saved content");
            invalidRoot = Map(Json().DeserializeObject(stable)); Map(Map(invalidRoot["Settings"])["GlobalAppearance"])["CustomizedFields"] = new[] { "Unknown" }; File.WriteAllText(invalid, Json().Serialize(invalidRoot)); Reject(delegate { again.Import(invalid); }, "invalid appearance metadata rejected by import");
            again.Data.Settings.NotebookLayout = "Journal"; again.Save();
            Assert(Convert.ToInt32(Map(Json().DeserializeObject(File.ReadAllText(oldFile)))["Version"]) == 3 && again.Data.Version == 2, "1.3-only layout is written as data version 3 so 1.2 refuses it instead of blanking");
            var reopened = new AppStore(root);
            Assert(reopened.Data.Settings.NotebookLayout == "Journal" && reopened.Data.Books[0].Pages[0].Text == "完整的历史中文备注" && reopened.LoadWarning == "", "version 3 data reopens in 1.3 with layout and content intact");
            reopened.Data.Settings.NotebookLayout = "Card"; reopened.Save();
            Assert(Convert.ToInt32(Map(Json().DeserializeObject(File.ReadAllText(oldFile)))["Version"]) == 2, "switching back to a 1.2 layout writes version 2 again");
            Assert(!reopened.Data.Settings.FramedWindowBounds && reopened.Data.Settings.WidgetMode == "Desktop" && reopened.Data.Settings.ShowHotkey == "Ctrl+Alt+Shift+D", "data saved by this version carries the window mode and needs no bounds conversion");
            var framed = Map(Json().DeserializeObject(File.ReadAllText(oldFile))); Map(framed["Settings"]).Remove("WidgetMode"); Map(framed["Settings"]).Remove("ShowHotkey"); File.WriteAllText(oldFile, Json().Serialize(framed));
            var legacy = new AppStore(root);
            Assert(legacy.LoadWarning == "" && legacy.Data.Settings.FramedWindowBounds && legacy.Data.Settings.WidgetMode == "Desktop", "data from before 1.5 opens in desktop mode and is marked for a one-time bounds conversion");
            var window = new WindowState { X = 100, Y = 200, Width = 500, Height = 600 };
            SettingsLogic.ShiftFrame(window, 9, 38, 9, 9, true);
            Assert(window.X == 109 && window.Y == 238 && window.Width == 482 && window.Height == 553, "removing the frame keeps the content rectangle");
            SettingsLogic.ShiftFrame(window, 9, 38, 9, 9, false);
            Assert(window.X == 100 && window.Y == 200 && window.Width == 500 && window.Height == 600, "restoring the frame returns the original bounds");
            HotkeySpec key;
            Assert(HotkeySpec.TryParse("Ctrl+Alt+Shift+D", out key) && key.Modifiers == 7 && key.VirtualKey == 'D' && HotkeySpec.Format(key.Modifiers, key.VirtualKey) == "Ctrl+Alt+Shift+D", "the default shortcut parses and formats back");
            Assert(HotkeySpec.TryParse("alt+f9", out key) && key.VirtualKey == 0x78 && HotkeySpec.TryParse("Ctrl+`", out key) && key.VirtualKey == 0xC0, "function keys and the backtick key are accepted");
            Assert(!HotkeySpec.TryParse("D", out key) && !HotkeySpec.TryParse("Shift+D", out key) && !HotkeySpec.TryParse("Ctrl+Alt", out key) && !HotkeySpec.TryParse("Ctrl+A+B", out key) && !HotkeySpec.TryParse("Ctrl+F13", out key), "shortcuts without Ctrl, Alt or Win, without a key, or with two keys are rejected");
            legacy.Data.Settings.WidgetMode = "Floating"; Reject(delegate { legacy.Save(); }, "an unknown window mode is rejected before saving");
            legacy.Data.Settings.WidgetMode = "Standard"; legacy.Data.Settings.ShowHotkey = "D"; Reject(delegate { legacy.Save(); }, "an invalid shortcut is rejected before saving");
            legacy.Data.Settings.ShowHotkey = ""; legacy.Save();
            var standard = new AppStore(root);
            Assert(standard.Data.Settings.WidgetMode == "Standard" && standard.Data.Settings.ShowHotkey == "" && !standard.Data.Settings.FramedWindowBounds, "standard mode and a disabled shortcut survive a restart");
            Console.WriteLine("ALL " + count + " NOTEBOOK LAYOUT CORE CHECKS PASSED"); return 0;
        }
        catch(Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
