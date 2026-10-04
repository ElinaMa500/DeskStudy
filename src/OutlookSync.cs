using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Collections.Generic;
using System.Windows.Forms;

namespace DeskStudy
{
    // Downloads the published Outlook calendar in the background, keeps the result in outlook-cache.json,
    // and refreshes it on a timer. The link is only ever sent to the address the user pasted.
    public sealed partial class AppController
    {
        private OutlookCache outlook = new OutlookCache();
        private System.Windows.Forms.Timer outlookTimer;
        private SynchronizationContext uiContext;
        private bool outlookBusy;
        private DateTime outlookLastAttemptUtc = DateTime.MinValue;
        // Replaced by the tests, so they never touch the network.
        internal Func<string, string> OutlookDownload = DownloadCalendar;

        public OutlookCache Outlook { get { return outlook; } }
        public bool OutlookSyncing { get { return outlookBusy; } }
        private string OutlookCachePath { get { return Path.Combine(Store.DirectoryPath, "outlook-cache.json"); } }

        private void StartOutlook()
        {
            uiContext = SynchronizationContext.Current;
            LoadOutlookCache();
            outlookTimer = new System.Windows.Forms.Timer { Interval = 60000 };
            outlookTimer.Tick += delegate { SyncOutlookIfDue(); };
            outlookTimer.Start();
            // A first look shortly after start, without delaying it.
            var first = new System.Windows.Forms.Timer { Interval = 3000 };
            first.Tick += delegate { first.Stop(); first.Dispose(); SyncOutlookIfDue(); };
            first.Start();
        }
        private void StopOutlook() { if (outlookTimer != null) { outlookTimer.Stop(); outlookTimer.Dispose(); outlookTimer = null; } }

        private void SyncOutlookIfDue()
        {
            OutlookOptions o = Data.Settings.Calendar.Outlook;
            if (Exiting || o.Url == "" || outlookBusy) return;
            DateTime last = outlookLastAttemptUtc;
            DateTime synced; if (outlook.LastSyncUtc != "" && DateTime.TryParse(outlook.LastSyncUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out synced) && synced.ToUniversalTime() > last) last = synced.ToUniversalTime();
            if ((DateTime.UtcNow - last).TotalMinutes >= o.RefreshMinutes) SyncOutlook();
        }

        // Starts a download now. Results arrive on the UI thread; the calendar and settings refresh then.
        public void SyncOutlook()
        {
            string url = Data.Settings.Calendar.Outlook.Url;
            if (Exiting || outlookBusy) return;
            if (url == "")
            {
                outlook = new OutlookCache(); SaveOutlookCache(); PublishOutlook(); return;
            }
            outlookBusy = true; outlookLastAttemptUtc = DateTime.UtcNow; PublishOutlook();
            Func<string, string> download = OutlookDownload;
            DateTime from = DateTime.Today.AddDays(-OutlookLogic.DaysBack), to = DateTime.Today.AddDays(OutlookLogic.DaysAhead);
            ThreadPool.QueueUserWorkItem(delegate
            {
                OutlookCache fresh = null; string error = "";
                try
                {
                    string text = download(OutlookLogic.DownloadAddress(url));
                    if (text == null || text.IndexOf("BEGIN:VCALENDAR", StringComparison.OrdinalIgnoreCase) < 0) throw new InvalidDataException(Lang.T("这个链接返回的不是日历（ICS）文件。"));
                    fresh = new OutlookCache { Events = IcsParser.Parse(text, from, to) };
                }
                catch (Exception ex) { error = Friendly(ex); }
                Post(delegate { FinishOutlookSync(url, fresh, error); });
            });
        }

        private void FinishOutlookSync(string url, OutlookCache fresh, string error)
        {
            outlookBusy = false;
            if (Exiting) return;
            // The link was changed while downloading: that result no longer applies.
            if (url != Data.Settings.Calendar.Outlook.Url) { SyncOutlook(); return; }
            if (fresh != null)
            {
                fresh.LastSyncUtc = DateTime.UtcNow.ToString("o");
                fresh.Reminded = outlook.Reminded;
                var seen = new System.Collections.Generic.List<string>();
                foreach (OutlookEvent e in fresh.Events) if (e.Category != "" && !seen.Contains(e.Category)) seen.Add(e.Category);
                seen.Sort(StringComparer.CurrentCulture);
                fresh.Categories = seen;
                outlook = fresh;
            }
            else outlook.LastError = error;
            SaveOutlookCache(); PublishOutlook();
        }

        private void PublishOutlook() { PublishChanges(); }

        private void Post(Action action)
        {
            if (uiContext != null) uiContext.Post(delegate { action(); }, null);
            else action();
        }

        private void LoadOutlookCache()
        {
            try
            {
                if (!File.Exists(OutlookCachePath)) return;
                var json = new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024 };
                OutlookCache loaded = json.Deserialize<OutlookCache>(File.ReadAllText(OutlookCachePath, Encoding.UTF8));
                if (loaded == null || loaded.Events == null) return;
                if (loaded.Categories == null) loaded.Categories = new System.Collections.Generic.List<string>();
                if (loaded.Reminded == null) loaded.Reminded = new System.Collections.Generic.List<string>();
                loaded.Events.RemoveAll(e => e == null || !Valid(e));
                outlook = loaded;
            }
            catch (Exception) { outlook = new OutlookCache(); }
        }
        private static bool Valid(OutlookEvent e)
        {
            DateTime a, b;
            return DateTime.TryParseExact(e.Start ?? "", TimeUtil.LocalFormat, null, System.Globalization.DateTimeStyles.None, out a)
                && DateTime.TryParseExact(e.End ?? "", TimeUtil.LocalFormat, null, System.Globalization.DateTimeStyles.None, out b) && b >= a;
        }
        private void SaveOutlookCache()
        {
            try
            {
                var json = new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024 };
                string temp = OutlookCachePath + ".tmp";
                File.WriteAllText(temp, json.Serialize(outlook), new UTF8Encoding(false));
                if (File.Exists(OutlookCachePath)) File.Replace(temp, OutlookCachePath, null); else File.Move(temp, OutlookCachePath);
            }
            catch (Exception) { }
        }

        public void SetOutlookTitleColor(string title, string hex)
        {
            Data.Settings.Calendar.Outlook.TitleColors[title ?? ""] = hex; SettingsChanged();
        }

        // Copies the Outlook calendar into the local calendar and stops the subscription. A backup is made first,
        // so the step can be undone from 数据与应用 → 恢复备份.
        public List<CalendarEvent> PlanOutlookImport() { return OutlookLogic.ToLocal(outlook, Data.Settings.Calendar.Outlook); }
        public int ImportOutlook()
        {
            var events = PlanOutlookImport();
            foreach (var w in Widgets) w.Remember();
            Flush(); Store.CreateBackup();
            Data.Events.AddRange(events);
            OutlookOptions o = Data.Settings.Calendar.Outlook;
            o.Url = ""; o.Show = false;
            outlook = new OutlookCache();
            try { if (File.Exists(OutlookCachePath)) File.Delete(OutlookCachePath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            SettingsChanged();
            return events.Count;
        }

        // Outlook reminders, added to the ones from the notebooks.
        private System.Collections.Generic.List<ReminderRecord> OutlookReminders(DateTime utcNow)
        {
            var due = OutlookLogic.Reminders(outlook, Data.Settings.Calendar.Outlook, utcNow);
            if (due.Count > 0)
            {
                Data.ReminderHistory.AddRange(due);
                if (Data.ReminderHistory.Count > 2000) Data.ReminderHistory.RemoveRange(0, Data.ReminderHistory.Count - 2000);
                SaveOutlookCache();
            }
            return due;
        }

        private static string DownloadCalendar(string url)
        {
            // TLS 1.2 is not on by default for older .NET Framework settings.
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Timeout = 30000; request.ReadWriteTimeout = 30000; request.UserAgent = "DeskStudy";
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var memory = new MemoryStream())
            {
                var buffer = new byte[81920]; int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    memory.Write(buffer, 0, read);
                    if (memory.Length > 30 * 1024 * 1024) throw new InvalidDataException(Lang.T("日历文件太大（超过 30 MB）。"));
                }
                return Encoding.UTF8.GetString(memory.ToArray());
            }
        }

        // A short reason for the settings center; never includes the link itself.
        private static string Friendly(Exception ex)
        {
            var web = ex as WebException;
            if (web != null)
            {
                var response = web.Response as HttpWebResponse;
                if (response != null)
                {
                    int code = (int)response.StatusCode;
                    if (code == 404 || code == 410) return Lang.T("找不到这个日历，可能已取消发布（{0}）。", code);
                    if (code == 401 || code == 403) return Lang.T("没有权限读取这个日历（{0}）。", code);
                    return Lang.T("服务器返回错误（{0}）。", code);
                }
                if (web.Status == WebExceptionStatus.Timeout) return Lang.T("连接超时。");
                if (web.Status == WebExceptionStatus.NameResolutionFailure || web.Status == WebExceptionStatus.ConnectFailure) return Lang.T("无法连接网络。");
                return Lang.T("网络错误。");
            }
            if (ex is InvalidDataException) return ex.Message;
            return Lang.T("无法读取日历文件。");
        }
    }
}
