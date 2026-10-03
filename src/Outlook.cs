using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DeskStudy
{
    // A read-only Outlook calendar, subscribed by its published ICS link.
    public sealed class OutlookOptions
    {
        // The published calendar's ICS address. Empty: not subscribed.
        public string Url { get; set; }
        public bool Show { get; set; }
        public int RefreshMinutes { get; set; }
        public bool Remind { get; set; }
        public int LeadMinutes { get; set; }
        // Colors for categories named by the user. The ICS link carries a category's name but not its color.
        public Dictionary<string, string> CategoryColors { get; set; }
        public OutlookOptions() { Url = ""; Show = true; RefreshMinutes = 30; Remind = true; LeadMinutes = 15; CategoryColors = new Dictionary<string, string>(); }
        public static readonly int[] RefreshChoices = { 15, 30, 60, 120 };
    }

    // One Outlook occurrence, already expanded from its series and converted to this computer's local time.
    public sealed class OutlookEvent
    {
        public string Uid { get; set; }
        public string Title { get; set; }
        // yyyy-MM-ddTHH:mm:ss local. All-day: Start is the first day 00:00, End the day after the last.
        public string Start { get; set; }
        public string End { get; set; }
        public bool AllDay { get; set; }
        public string Location { get; set; }
        public string Notes { get; set; }
        public bool Recurring { get; set; }
        // The first Outlook category, or empty.
        public string Category { get; set; }
        public OutlookEvent() { Uid = ""; Title = ""; Start = ""; End = ""; Location = ""; Notes = ""; Category = ""; }
        public DateTime StartLocal { get { return TimeUtil.ParseLocal(Start); } }
        public DateTime EndLocal { get { return TimeUtil.ParseLocal(End); } }
        // Stable per occurrence: the series and when this one starts.
        public string Key { get { return Uid + "|" + Start; } }
    }

    // What was last downloaded, kept in outlook-cache.json beside data.json so the calendar still shows it offline.
    public sealed class OutlookCache
    {
        public List<OutlookEvent> Events { get; set; }
        public List<string> Categories { get; set; }
        public string LastSyncUtc { get; set; }
        public string LastError { get; set; }
        // Occurrences whose reminder was already shown.
        public List<string> Reminded { get; set; }
        public OutlookCache() { Events = new List<OutlookEvent>(); Categories = new List<string>(); LastSyncUtc = ""; LastError = ""; Reminded = new List<string>(); }
    }

    public static class OutlookLogic
    {
        // Outlook's own blue, for events without a category.
        public const string Color = "#0F6CBD";
        // How far the cache reaches around today.
        public const int DaysBack = 35, DaysAhead = 200;

        // Outlook's preset categories, in the English and Chinese names Outlook gives them.
        static readonly Dictionary<string, string> Presets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Red category", "#D13438" }, { "红色类别", "#D13438" }, { "红色类", "#D13438" },
            { "Orange category", "#F7630C" }, { "橙色类别", "#F7630C" }, { "橙色类", "#F7630C" },
            { "Yellow category", "#E8B200" }, { "黄色类别", "#E8B200" }, { "黄色类", "#E8B200" },
            { "Green category", "#13A10E" }, { "绿色类别", "#13A10E" }, { "绿色类", "#13A10E" },
            { "Blue category", "#0078D4" }, { "蓝色类别", "#0078D4" }, { "蓝色类", "#0078D4" },
            { "Purple category", "#8764B8" }, { "紫色类别", "#8764B8" }, { "紫色类", "#8764B8" },
        };
        // Starting colors for categories the user named; they can be changed in settings.
        static readonly string[] Palette = { "#0078D4", "#13A10E", "#8764B8", "#F7630C", "#D13438", "#038387", "#CA5010", "#4F6BED", "#C239B3", "#498205" };

        public static bool IsPreset(string category) { return Presets.ContainsKey(category ?? ""); }
        public static string DefaultColorFor(string category)
        {
            if (String.IsNullOrEmpty(category)) return Color;
            string preset; if (Presets.TryGetValue(category, out preset)) return preset;
            int hash = 0; foreach (char c in category) hash = unchecked(hash * 31 + c);
            return Palette[(hash & 0x7FFFFFFF) % Palette.Length];
        }
        public static string ColorFor(OutlookOptions options, string category)
        {
            string chosen;
            if (!String.IsNullOrEmpty(category) && options != null && options.CategoryColors != null && options.CategoryColors.TryGetValue(category, out chosen)) return chosen;
            return DefaultColorFor(category);
        }

        public static bool IsCalendarLink(string url)
        {
            Uri uri;
            return Uri.TryCreate((url ?? "").Trim(), UriKind.Absolute, out uri) && (uri.Scheme == "https" || uri.Scheme == "webcal" || uri.Scheme == "webcals");
        }
        // webcal:// is the same address over https.
        public static string DownloadAddress(string url)
        {
            url = (url ?? "").Trim();
            if (url.StartsWith("webcals://", StringComparison.OrdinalIgnoreCase)) return "https://" + url.Substring(10);
            if (url.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase)) return "https://" + url.Substring(9);
            return url;
        }

        // Timed occurrences in the range, as calendar occurrences; one per day for events that cross midnight.
        public static List<Occurrence> Timed(OutlookCache cache, OutlookOptions options, DateTime from, DateTime to)
        {
            var list = new List<Occurrence>();
            if (cache == null) return list;
            foreach (OutlookEvent e in cache.Events.Where(x => !x.AllDay))
            {
                DateTime start = e.StartLocal, end = e.EndLocal;
                for (DateTime day = start.Date; day <= (end > start ? end.AddTicks(-1) : start).Date && day <= to.Date; day = day.AddDays(1))
                {
                    if (day < from.Date) continue;
                    DateTime s = start > day ? start : day, f = end < day.AddDays(1) ? end : day.AddDays(1).AddMinutes(-1);
                    if (f <= s) f = s.AddMinutes(30);
                    if (f.Date > s.Date) f = s.Date.AddDays(1).AddMinutes(-1);
                    list.Add(new Occurrence {
                        External = e, KeyDate = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Date = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        StartTime = s.ToString("HH:mm", CultureInfo.InvariantCulture), EndTime = f.ToString("HH:mm", CultureInfo.InvariantCulture),
                        Title = e.Title, Location = e.Location, Notes = e.Notes, Color = ColorFor(options, e.Category)
                    });
                }
            }
            return list;
        }

        // All-day events touching the range.
        public static List<OutlookEvent> AllDay(OutlookCache cache, DateTime from, DateTime to)
        {
            if (cache == null) return new List<OutlookEvent>();
            return cache.Events.Where(e => e.AllDay && e.StartLocal.Date <= to.Date && e.EndLocal.Date > from.Date).OrderBy(e => e.Start, StringComparer.Ordinal).ToList();
        }

        // Reminders due now for timed events: from lead minutes before the start until the start. Each occurrence once.
        public static List<ReminderRecord> Reminders(OutlookCache cache, OutlookOptions options, DateTime utcNow)
        {
            var result = new List<ReminderRecord>();
            if (cache == null || options == null || !options.Remind || options.Url == "") return result;
            utcNow = utcNow.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(utcNow, DateTimeKind.Utc) : utcNow.ToUniversalTime();
            var reminded = new HashSet<string>(cache.Reminded);
            foreach (OutlookEvent e in cache.Events.Where(x => !x.AllDay))
            {
                DateTime start = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(e.StartLocal, DateTimeKind.Unspecified), TimeZoneInfo.Local);
                DateTime remind = start.AddMinutes(-options.LeadMinutes);
                // At the start itself (lead 0) the reminder stays valid for a minute.
                if (utcNow < remind || utcNow >= (options.LeadMinutes == 0 ? start.AddMinutes(1) : start)) continue;
                string key = Hash(e.Key);
                if (!reminded.Add(key)) continue;
                cache.Reminded.Add(key);
                result.Add(new ReminderRecord {
                    Id = Guid.NewGuid().ToString("N"), TaskId = "outlook:" + key, Title = e.Title, BookName = "Outlook", PageTitle = e.Location,
                    DueLocal = e.Start, FiredUtc = utcNow.ToString("o"), Kind = options.LeadMinutes == 0 ? "due" : "advance"
                });
            }
            // Forget reminders for occurrences no longer in the cache.
            var live = new HashSet<string>(cache.Events.Select(x => Hash(x.Key)));
            cache.Reminded.RemoveAll(k => !live.Contains(k));
            return result;
        }

        public static string Hash(string value)
        {
            using (var sha = SHA1.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? ""))).Replace("-", "").ToLowerInvariant();
        }
    }
}
