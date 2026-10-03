using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DeskStudy
{
    // Reads an iCalendar (.ics) file as Outlook publishes it and expands it into single occurrences
    // in this computer's local time, within a window around today.
    public static class IcsParser
    {
        sealed class Line { public string Name; public Dictionary<string, string> Params; public string Value; }
        sealed class Component { public string Kind; public List<Line> Lines = new List<Line>(); public List<Component> Children = new List<Component>(); }
        sealed class Stamp { public DateTime Wall; public TimeZoneInfo Zone; public bool Utc; public bool DateOnly; }
        sealed class Rule
        {
            public string Freq = ""; public int Interval = 1; public int Count; public Stamp Until;
            public List<KeyValuePair<int, DayOfWeek>> ByDay = new List<KeyValuePair<int, DayOfWeek>>();
            public List<int> ByMonthDay = new List<int>(), ByMonth = new List<int>(), BySetPos = new List<int>();
            public DayOfWeek WeekStart = DayOfWeek.Monday;
        }

        public static List<OutlookEvent> Parse(string text, DateTime windowStart, DateTime windowEnd)
        {
            Component root = Read(text ?? "");
            Component calendar = root.Children.FirstOrDefault(c => c.Kind == "VCALENDAR") ?? root;
            var zones = new Dictionary<string, TimeZoneInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (Component z in calendar.Children.Where(c => c.Kind == "VTIMEZONE"))
            {
                string id = Value(z, "TZID"); if (id == "" || zones.ContainsKey(id)) continue;
                TimeZoneInfo zone = SystemZone(id) ?? BuildZone(id, z);
                if (zone != null) zones[id] = zone;
            }
            var events = calendar.Children.Where(c => c.Kind == "VEVENT").ToList();
            // Single changed occurrences, by series and by the start they replace.
            var changed = new Dictionary<string, Component>(StringComparer.Ordinal);
            foreach (Component e in events.Where(e => Get(e, "RECURRENCE-ID") != null))
            {
                Stamp id = ParseStamp(Get(e, "RECURRENCE-ID"), zones);
                if (id != null) changed[Value(e, "UID") + "|" + Key(id)] = e;
            }
            var result = new List<OutlookEvent>();
            DateTime from = windowStart.Date, to = windowEnd.Date.AddDays(1);
            foreach (Component e in events.Where(e => Get(e, "RECURRENCE-ID") == null))
            {
                try
                {
                    if (Value(e, "STATUS").Equals("CANCELLED", StringComparison.OrdinalIgnoreCase)) continue;
                    Stamp start = ParseStamp(Get(e, "DTSTART"), zones); if (start == null) continue;
                    TimeSpan length = Length(e, start, zones);
                    string uid = Value(e, "UID");
                    Line ruleLine = Get(e, "RRULE");
                    var excluded = new HashSet<string>(e.Lines.Where(l => l.Name == "EXDATE").SelectMany(l => l.Value.Split(',').Select(v => ParseStamp(new Line { Name = "EXDATE", Params = l.Params, Value = v.Trim() }, zones))).Where(s => s != null).Select(Key));
                    IEnumerable<DateTime> starts = ruleLine == null ? new[] { start.Wall } : Expand(start, ParseRule(ruleLine.Value, zones), to.AddDays(1), from.AddDays(-1));
                    foreach (DateTime wall in starts)
                    {
                        var instance = new Stamp { Wall = wall, Zone = start.Zone, Utc = start.Utc, DateOnly = start.DateOnly };
                        string key = Key(instance);
                        if (excluded.Contains(key)) continue;
                        Component source = e; Stamp actual = instance; TimeSpan actualLength = length;
                        Component replacement;
                        if (ruleLine != null && changed.TryGetValue(uid + "|" + key, out replacement))
                        {
                            if (Value(replacement, "STATUS").Equals("CANCELLED", StringComparison.OrdinalIgnoreCase)) continue;
                            source = replacement; actual = ParseStamp(Get(replacement, "DTSTART"), zones) ?? instance; actualLength = Length(replacement, actual, zones);
                        }
                        OutlookEvent item = Build(source, uid, actual, actualLength, ruleLine != null);
                        if (item.EndLocal <= from || item.StartLocal >= to) continue;
                        result.Add(item);
                    }
                }
                catch (FormatException) { }
                catch (ArgumentException) { }
                catch (OverflowException) { }
            }
            return result.OrderBy(x => x.Start, StringComparer.Ordinal).ThenBy(x => x.Title, StringComparer.Ordinal).ToList();
        }

        static OutlookEvent Build(Component e, string uid, Stamp start, TimeSpan length, bool recurring)
        {
            bool allDay = start.DateOnly || Value(e, "X-MICROSOFT-CDO-ALLDAYEVENT").Equals("TRUE", StringComparison.OrdinalIgnoreCase);
            DateTime localStart, localEnd;
            if (allDay)
            {
                localStart = start.Wall.Date;
                localEnd = start.Wall.Add(length).Date; if (localEnd <= localStart) localEnd = localStart.AddDays(1);
            }
            else
            {
                localStart = ToLocal(start, start.Wall); localEnd = ToLocal(start, start.Wall.Add(length));
                if (localEnd < localStart) localEnd = localStart;
            }
            string categories = Value(e, "CATEGORIES");
            return new OutlookEvent {
                Uid = uid, Title = Value(e, "SUMMARY"), Location = Value(e, "LOCATION"), Notes = Value(e, "DESCRIPTION").Trim(), AllDay = allDay, Recurring = recurring,
                Start = localStart.ToString(TimeUtil.LocalFormat, CultureInfo.InvariantCulture), End = localEnd.ToString(TimeUtil.LocalFormat, CultureInfo.InvariantCulture),
                Category = categories.Split(',').Select(c => c.Trim()).FirstOrDefault(c => c != "") ?? ""
            };
        }

        static TimeSpan Length(Component e, Stamp start, Dictionary<string, TimeZoneInfo> zones)
        {
            Stamp end = ParseStamp(Get(e, "DTEND"), zones);
            if (end != null)
            {
                if (start.DateOnly || end.DateOnly) return end.Wall.Date - start.Wall.Date;
                // Different zones at each end: compare real instants.
                return Utc(end, end.Wall) - Utc(start, start.Wall);
            }
            string duration = Value(e, "DURATION");
            if (duration != "") return Duration(duration);
            return start.DateOnly ? TimeSpan.FromDays(1) : TimeSpan.Zero;
        }

        // ---- reading
        static Component Read(string text)
        {
            // Unfold: a line starting with a space or tab continues the previous one.
            text = text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n ", "").Replace("\n\t", "");
            var root = new Component { Kind = "" }; var stack = new Stack<Component>(); stack.Push(root);
            foreach (string raw in text.Split('\n'))
            {
                if (raw.Length == 0) continue;
                Line line = ParseLine(raw); if (line == null) continue;
                if (line.Name == "BEGIN") { var c = new Component { Kind = line.Value.Trim().ToUpperInvariant() }; stack.Peek().Children.Add(c); stack.Push(c); }
                else if (line.Name == "END") { if (stack.Count > 1) stack.Pop(); }
                else stack.Peek().Lines.Add(line);
            }
            return root;
        }

        static Line ParseLine(string raw)
        {
            int colon = -1; bool quoted = false;
            for (int i = 0; i < raw.Length; i++) { if (raw[i] == '"') quoted = !quoted; else if (raw[i] == ':' && !quoted) { colon = i; break; } }
            if (colon <= 0) return null;
            string head = raw.Substring(0, colon);
            var parts = new List<string>(); var current = new StringBuilder(); quoted = false;
            foreach (char c in head) { if (c == '"') quoted = !quoted; if (c == ';' && !quoted) { parts.Add(current.ToString()); current.Clear(); } else current.Append(c); }
            parts.Add(current.ToString());
            var line = new Line { Name = parts[0].Trim().ToUpperInvariant(), Params = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), Value = raw.Substring(colon + 1) };
            foreach (string p in parts.Skip(1)) { int eq = p.IndexOf('='); if (eq > 0) line.Params[p.Substring(0, eq).Trim()] = p.Substring(eq + 1).Trim().Trim('"'); }
            return line;
        }

        static Line Get(Component c, string name) { return c.Lines.FirstOrDefault(l => l.Name == name); }
        static string Value(Component c, string name) { Line l = Get(c, name); return l == null ? "" : Unescape(l.Value); }
        static string Unescape(string value)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '\\' && i + 1 < value.Length) { char n = value[++i]; sb.Append(n == 'n' || n == 'N' ? '\n' : n); }
                else sb.Append(value[i]);
            }
            return sb.ToString();
        }

        // ---- time
        static Stamp ParseStamp(Line line, Dictionary<string, TimeZoneInfo> zones)
        {
            if (line == null) return null;
            string v = line.Value.Trim(); string type; line.Params.TryGetValue("VALUE", out type);
            if (v.Length == 8 || (type != null && type.Equals("DATE", StringComparison.OrdinalIgnoreCase)))
                return new Stamp { Wall = DateTime.ParseExact(v.Substring(0, 8), "yyyyMMdd", CultureInfo.InvariantCulture), DateOnly = true, Zone = TimeZoneInfo.Local };
            bool utc = v.EndsWith("Z", StringComparison.OrdinalIgnoreCase);
            DateTime wall = DateTime.ParseExact(v.TrimEnd('Z', 'z').Substring(0, 15), "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);
            if (utc) return new Stamp { Wall = wall, Utc = true, Zone = TimeZoneInfo.Utc };
            string tzid; TimeZoneInfo zone = null;
            if (line.Params.TryGetValue("TZID", out tzid)) { tzid = tzid.Trim('"'); if (!zones.TryGetValue(tzid, out zone)) zone = SystemZone(tzid); }
            return new Stamp { Wall = wall, Zone = zone ?? TimeZoneInfo.Local };
        }

        // Identity of an occurrence for EXDATE and RECURRENCE-ID: the instant it starts (the day, for all-day events).
        static string Key(Stamp s)
        {
            if (s.DateOnly) return s.Wall.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            return Utc(s, s.Wall).ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);
        }

        static DateTime Utc(Stamp s, DateTime wall)
        {
            if (s.Utc) return DateTime.SpecifyKind(wall, DateTimeKind.Utc);
            TimeZoneInfo tz = s.Zone ?? TimeZoneInfo.Local;
            wall = DateTime.SpecifyKind(wall, DateTimeKind.Unspecified);
            int guard = 0; while (tz.IsInvalidTime(wall) && guard++ < 180) wall = wall.AddMinutes(1);
            if (tz.IsAmbiguousTime(wall)) return DateTime.SpecifyKind(wall - tz.GetAmbiguousTimeOffsets(wall).Min(), DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeToUtc(wall, tz);
        }
        static DateTime ToLocal(Stamp s, DateTime wall) { return TimeZoneInfo.ConvertTimeFromUtc(Utc(s, wall), TimeZoneInfo.Local); }

        static TimeSpan Duration(string value)
        {
            // P[n]W, P[n]DT[n]H[n]M[n]S, with an optional sign.
            int sign = value.StartsWith("-") ? -1 : 1; value = value.TrimStart('+', '-');
            if (!value.StartsWith("P")) return TimeSpan.Zero;
            TimeSpan total = TimeSpan.Zero; string number = ""; bool time = false;
            foreach (char c in value.Substring(1))
            {
                if (Char.IsDigit(c)) { number += c; continue; }
                int n = number == "" ? 0 : Int32.Parse(number, CultureInfo.InvariantCulture); number = "";
                switch (c)
                {
                    case 'T': time = true; break;
                    case 'W': total += TimeSpan.FromDays(7 * n); break;
                    case 'D': total += TimeSpan.FromDays(n); break;
                    case 'H': total += TimeSpan.FromHours(n); break;
                    case 'M': total += time ? TimeSpan.FromMinutes(n) : TimeSpan.Zero; break;
                    case 'S': total += TimeSpan.FromSeconds(n); break;
                }
            }
            return sign < 0 ? -total : total;
        }

        // Windows zone names (what Outlook writes) first, then a few common IANA names.
        static readonly Dictionary<string, string> Iana = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Europe/London", "GMT Standard Time" }, { "Asia/Shanghai", "China Standard Time" }, { "Asia/Hong_Kong", "China Standard Time" },
            { "Asia/Taipei", "Taipei Standard Time" }, { "Asia/Tokyo", "Tokyo Standard Time" }, { "Asia/Singapore", "Singapore Standard Time" },
            { "Europe/Paris", "Romance Standard Time" }, { "Europe/Berlin", "W. Europe Standard Time" }, { "Europe/Dublin", "GMT Standard Time" },
            { "America/New_York", "Eastern Standard Time" }, { "America/Chicago", "Central Standard Time" }, { "America/Denver", "Mountain Standard Time" },
            { "America/Los_Angeles", "Pacific Standard Time" }, { "Australia/Sydney", "AUS Eastern Standard Time" }, { "UTC", "UTC" }, { "Etc/UTC", "UTC" },
        };
        static TimeZoneInfo SystemZone(string id)
        {
            string mapped;
            foreach (string candidate in new[] { id, Iana.TryGetValue(id, out mapped) ? mapped : null })
            {
                if (candidate == null) continue;
                try { return TimeZoneInfo.FindSystemTimeZoneById(candidate); }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { }
            }
            return null;
        }

        // A zone Windows does not know: built from the file's own STANDARD / DAYLIGHT rules.
        static TimeZoneInfo BuildZone(string id, Component zone)
        {
            Component standard = zone.Children.FirstOrDefault(c => c.Kind == "STANDARD"), daylight = zone.Children.FirstOrDefault(c => c.Kind == "DAYLIGHT");
            if (standard == null) return null;
            try
            {
                TimeSpan baseOffset = Offset(Value(standard, "TZOFFSETTO"));
                if (daylight == null) return TimeZoneInfo.CreateCustomTimeZone(id, baseOffset, id, id);
                TimeSpan delta = Offset(Value(daylight, "TZOFFSETTO")) - baseOffset;
                TimeZoneInfo.TransitionTime start = Transition(daylight), end = Transition(standard);
                if (start.Equals(default(TimeZoneInfo.TransitionTime)) || end.Equals(default(TimeZoneInfo.TransitionTime)) || delta == TimeSpan.Zero)
                    return TimeZoneInfo.CreateCustomTimeZone(id, baseOffset, id, id);
                var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(DateTime.MinValue.Date, DateTime.MaxValue.Date, delta, start, end);
                return TimeZoneInfo.CreateCustomTimeZone(id, baseOffset, id, id, id, new[] { rule });
            }
            catch (ArgumentException) { return null; }
            catch (InvalidTimeZoneException) { return null; }
            catch (FormatException) { return null; }
        }
        static TimeSpan Offset(string value)
        {
            value = value.Trim(); int sign = value.StartsWith("-") ? -1 : 1; value = value.TrimStart('+', '-');
            var span = new TimeSpan(Int32.Parse(value.Substring(0, 2), CultureInfo.InvariantCulture), Int32.Parse(value.Substring(2, 2), CultureInfo.InvariantCulture), 0);
            return sign < 0 ? -span : span;
        }
        static TimeZoneInfo.TransitionTime Transition(Component c)
        {
            Line startLine = Get(c, "DTSTART"); if (startLine == null) return default(TimeZoneInfo.TransitionTime);
            DateTime at = DateTime.ParseExact(startLine.Value.Trim().Substring(0, 15), "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);
            DateTime timeOfDay = new DateTime(1, 1, 1, at.Hour, at.Minute, at.Second);
            Line ruleLine = Get(c, "RRULE");
            if (ruleLine == null) return TimeZoneInfo.TransitionTime.CreateFixedDateRule(timeOfDay, at.Month, at.Day);
            Rule rule = ParseRule(ruleLine.Value, null);
            int month = rule.ByMonth.Count > 0 ? rule.ByMonth[0] : at.Month;
            if (rule.ByDay.Count > 0)
            {
                int week = rule.ByDay[0].Key; week = week < 0 || week > 4 ? 5 : week == 0 ? 1 : week;
                return TimeZoneInfo.TransitionTime.CreateFloatingDateRule(timeOfDay, month, week, rule.ByDay[0].Value);
            }
            return TimeZoneInfo.TransitionTime.CreateFixedDateRule(timeOfDay, month, rule.ByMonthDay.Count > 0 ? rule.ByMonthDay[0] : at.Day);
        }

        // ---- recurrence
        static readonly string[] Days = { "SU", "MO", "TU", "WE", "TH", "FR", "SA" };
        static Rule ParseRule(string text, Dictionary<string, TimeZoneInfo> zones)
        {
            var rule = new Rule();
            foreach (string part in text.Split(';'))
            {
                int eq = part.IndexOf('='); if (eq <= 0) continue;
                string key = part.Substring(0, eq).Trim().ToUpperInvariant(), value = part.Substring(eq + 1).Trim();
                switch (key)
                {
                    case "FREQ": rule.Freq = value.ToUpperInvariant(); break;
                    case "INTERVAL": rule.Interval = Math.Max(1, Int32.Parse(value, CultureInfo.InvariantCulture)); break;
                    case "COUNT": rule.Count = Int32.Parse(value, CultureInfo.InvariantCulture); break;
                    case "UNTIL": rule.Until = ParseStamp(new Line { Name = "UNTIL", Params = new Dictionary<string, string>(), Value = value }, zones ?? new Dictionary<string, TimeZoneInfo>()); break;
                    case "BYMONTHDAY": rule.ByMonthDay = value.Split(',').Select(v => Int32.Parse(v, CultureInfo.InvariantCulture)).ToList(); break;
                    case "BYMONTH": rule.ByMonth = value.Split(',').Select(v => Int32.Parse(v, CultureInfo.InvariantCulture)).ToList(); break;
                    case "BYSETPOS": rule.BySetPos = value.Split(',').Select(v => Int32.Parse(v, CultureInfo.InvariantCulture)).ToList(); break;
                    case "WKST": rule.WeekStart = (DayOfWeek)Array.IndexOf(Days, value.ToUpperInvariant()); break;
                    case "BYDAY":
                        foreach (string d in value.Split(','))
                        {
                            string day = d.Trim().ToUpperInvariant(), number = day.Substring(0, day.Length - 2);
                            rule.ByDay.Add(new KeyValuePair<int, DayOfWeek>(number == "" || number == "+" ? 0 : Int32.Parse(number, CultureInfo.InvariantCulture), (DayOfWeek)Array.IndexOf(Days, day.Substring(day.Length - 2))));
                        }
                        break;
                }
            }
            return rule;
        }

        // Starts of the series in its own wall time, in order, up to the limit. Earlier ones are skipped but still counted.
        static IEnumerable<DateTime> Expand(Stamp first, Rule rule, DateTime limitLocal, DateTime fromLocal)
        {
            DateTime start = first.Wall, limit = limitLocal.AddDays(2);
            DateTime? until = null;
            if (rule.Until != null) until = rule.Until.DateOnly ? rule.Until.Wall.Date.AddDays(1).AddTicks(-1) : rule.Until.Utc ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(rule.Until.Wall, DateTimeKind.Utc), first.Utc ? TimeZoneInfo.Utc : first.Zone ?? TimeZoneInfo.Local) : rule.Until.Wall;
            int produced = 0, guard = 0;
            DateTime period = PeriodStart(start, rule);
            while (guard++ < 20000)
            {
                List<DateTime> candidates = Candidates(period, start, rule);
                foreach (DateTime c in candidates)
                {
                    if (c < start) continue;
                    if (until.HasValue && c > until.Value) yield break;
                    if (rule.Count > 0 && produced >= rule.Count) yield break;
                    if (c > limit) yield break;
                    produced++;
                    if (c.AddDays(31) >= fromLocal) yield return c;
                }
                period = Next(period, rule);
                if (period > limit) yield break;
            }
        }

        static DateTime PeriodStart(DateTime start, Rule rule)
        {
            switch (rule.Freq)
            {
                case "WEEKLY": return start.Date.AddDays(-(((int)start.DayOfWeek - (int)rule.WeekStart + 7) % 7));
                case "MONTHLY": return new DateTime(start.Year, start.Month, 1);
                case "YEARLY": return new DateTime(start.Year, 1, 1);
                default: return start.Date;
            }
        }
        static DateTime Next(DateTime period, Rule rule)
        {
            switch (rule.Freq)
            {
                case "WEEKLY": return period.AddDays(7 * rule.Interval);
                case "MONTHLY": return period.AddMonths(rule.Interval);
                case "YEARLY": return period.AddYears(rule.Interval);
                default: return period.AddDays(rule.Interval);
            }
        }

        static List<DateTime> Candidates(DateTime period, DateTime start, Rule rule)
        {
            TimeSpan time = start.TimeOfDay; var days = new List<DateTime>();
            switch (rule.Freq)
            {
                case "DAILY":
                    if ((rule.ByMonth.Count == 0 || rule.ByMonth.Contains(period.Month)) && (rule.ByDay.Count == 0 || rule.ByDay.Any(d => d.Value == period.DayOfWeek))) days.Add(period);
                    break;
                case "WEEKLY":
                    var weekdays = rule.ByDay.Count > 0 ? rule.ByDay.Select(d => d.Value).ToList() : new List<DayOfWeek> { start.DayOfWeek };
                    for (int i = 0; i < 7; i++) { DateTime d = period.AddDays(i); if (weekdays.Contains(d.DayOfWeek) && (rule.ByMonth.Count == 0 || rule.ByMonth.Contains(d.Month))) days.Add(d); }
                    break;
                case "MONTHLY":
                    if (rule.ByMonth.Count == 0 || rule.ByMonth.Contains(period.Month)) days.AddRange(InMonth(period.Year, period.Month, start, rule));
                    break;
                case "YEARLY":
                    foreach (int month in rule.ByMonth.Count > 0 ? rule.ByMonth : new List<int> { start.Month }) days.AddRange(InMonth(period.Year, month, start, rule));
                    break;
            }
            days = days.Distinct().OrderBy(d => d).ToList();
            if (rule.BySetPos.Count > 0)
                days = rule.BySetPos.Select(p => p > 0 ? (p <= days.Count ? (DateTime?)days[p - 1] : null) : (-p <= days.Count ? (DateTime?)days[days.Count + p] : null)).Where(d => d.HasValue).Select(d => d.Value).Distinct().OrderBy(d => d).ToList();
            return days.Select(d => d.Date + time).ToList();
        }

        static IEnumerable<DateTime> InMonth(int year, int month, DateTime start, Rule rule)
        {
            int length = DateTime.DaysInMonth(year, month);
            var result = new List<DateTime>();
            if (rule.ByMonthDay.Count > 0)
                foreach (int d in rule.ByMonthDay) { int day = d > 0 ? d : length + d + 1; if (day >= 1 && day <= length) result.Add(new DateTime(year, month, day)); }
            if (rule.ByDay.Count > 0)
            {
                var byDay = new List<DateTime>();
                foreach (var d in rule.ByDay)
                {
                    var matching = Enumerable.Range(1, length).Select(n => new DateTime(year, month, n)).Where(x => x.DayOfWeek == d.Value).ToList();
                    if (d.Key == 0) byDay.AddRange(matching);
                    else if (d.Key > 0 && d.Key <= matching.Count) byDay.Add(matching[d.Key - 1]);
                    else if (d.Key < 0 && -d.Key <= matching.Count) byDay.Add(matching[matching.Count + d.Key]);
                }
                result = rule.ByMonthDay.Count > 0 ? result.Intersect(byDay).ToList() : byDay;
            }
            if (rule.ByMonthDay.Count == 0 && rule.ByDay.Count == 0 && start.Day <= length) result.Add(new DateTime(year, month, start.Day));
            return result;
        }
    }
}
