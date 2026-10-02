using System;
using System.Collections.Generic;
using System.Globalization;

namespace DeskStudy
{
    // Interface language (1.5.1). The Chinese text in the code is the key; English comes from Lang.En.
    // The language is fixed for the life of the process: changing it takes effect after a restart.
    public static partial class Lang
    {
        public const string Chinese = "zh-CN", English = "en";
        public static string Current { get; private set; }
        public static bool IsEnglish { get { return Current == English; } }
        static Lang() { Current = Chinese; }

        public static void Use(string language) { Current = language == English ? English : Chinese; }

        // New users start in the Windows display language: Chinese for any Chinese Windows, English otherwise.
        public static string SystemDefault()
        {
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh" ? Chinese : English;
        }

        // Interface text: English when available, otherwise the Chinese original.
        public static string T(string chinese)
        {
            string english;
            return IsEnglish && chinese != null && En.TryGetValue(chinese, out english) ? english : chinese;
        }
        public static string T(string chinese, params object[] values) { return String.Format(CultureInfo.InvariantCulture, T(chinese), values); }
        // Counted text: "{0} 天" → "1 day" / "3 days".
        public static string Count(int count, string chinese, string englishOne, string englishMany)
        {
            return IsEnglish ? String.Format(CultureInfo.InvariantCulture, count == 1 ? englishOne : englishMany, count) : String.Format(CultureInfo.InvariantCulture, chinese, count);
        }

        static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");
        static readonly string[] ZhWeekday = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
        static readonly string[] ZhWeekdayChar = { "日", "一", "二", "三", "四", "五", "六" };

        // "9 月 29 日" / "Sep 29"
        public static string MonthDay(DateTime date) { return IsEnglish ? date.ToString("MMM d", Us) : date.ToString("M 月 d 日", CultureInfo.InvariantCulture); }
        // "2026 年 9 月" / "September 2026"
        public static string MonthTitle(DateTime date) { return IsEnglish ? date.ToString("MMMM yyyy", Us) : date.ToString("yyyy 年 M 月", CultureInfo.InvariantCulture); }
        // "周一" / "Mon"
        public static string Weekday(DayOfWeek day) { return IsEnglish ? Us.DateTimeFormat.AbbreviatedDayNames[(int)day] : ZhWeekday[(int)day]; }
        // "一" / "Mon" (calendar day header)
        public static string WeekdayShort(DayOfWeek day) { return IsEnglish ? Us.DateTimeFormat.AbbreviatedDayNames[(int)day] : ZhWeekdayChar[(int)day]; }
        // "一" / "M" (month grid of the deadline picker)
        public static string WeekdayLetter(DayOfWeek day) { return IsEnglish ? Us.DateTimeFormat.ShortestDayNames[(int)day].Substring(0, 1) : ZhWeekdayChar[(int)day]; }
    }
}
