using System.Globalization;

namespace Weekmark.Core;

// Swift template names: EEE, EEEdMMM, EEEdMMMyyyy, dMMM, MMM, MMMM, MMMMyyyy, EEEEMMMMd, EEEEdMMMM.
public enum DateTemplate { Weekday, WeekdayDayMonth, WeekdayDayMonthYear, DayMonth, Month, MonthLong, MonthYear, WeekdayLongMonthDay, WeekdayLongDayMonth }

/// <summary>English strings matching the Mac's ICU output for en-GB (dayFirst) and en-US.</summary>
public sealed class WeekFormatter(WeekCalendar cal, bool dayFirst)
{
    static readonly string[] MonthNames = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];
    static readonly string[] Days = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

    public static bool DayFirstFor(CultureInfo c) => c.Name != "en-US";

    static string Mon(DateOnly d) => MonthNames[d.Month - 1][..3];

    public string Format(DateOnly d, DateTemplate t)
    {
        var wd = Days[(int)d.DayOfWeek];
        var dm = dayFirst ? $"{d.Day} {Mon(d)}" : $"{Mon(d)} {d.Day}";
        return t switch
        {
            DateTemplate.Weekday => wd[..3],
            DateTemplate.WeekdayDayMonth => dayFirst ? $"{wd[..3]} {dm}" : $"{wd[..3]}, {dm}", // en-GB has no comma here
            DateTemplate.WeekdayDayMonthYear => $"{wd[..3]}, {dm}{(dayFirst ? " " : ", ")}{d.Year}",
            DateTemplate.DayMonth => dm,
            DateTemplate.Month => Mon(d),
            DateTemplate.MonthLong => MonthNames[d.Month - 1],
            DateTemplate.MonthYear => $"{MonthNames[d.Month - 1]} {d.Year}",
            DateTemplate.WeekdayLongMonthDay or DateTemplate.WeekdayLongDayMonth =>
                dayFirst ? $"{wd} {d.Day} {MonthNames[d.Month - 1]}" : $"{wd}, {MonthNames[d.Month - 1]} {d.Day}",
            _ => throw new ArgumentOutOfRangeException(nameof(t)),
        };
    }

    /// <summary>"Mon 9 Nov – Sun 15 Nov 2026".</summary>
    public string LongRange(WeekRef r)
    {
        var days = cal.DaysOf(r);
        DateOnly s = days[0], e = days[^1];
        return s.Year == e.Year
            ? $"{Format(s, DateTemplate.WeekdayDayMonth)} – {Format(e, DateTemplate.WeekdayDayMonth)} {e.Year}"
            : $"{Format(s, DateTemplate.WeekdayDayMonthYear)} – {Format(e, DateTemplate.WeekdayDayMonthYear)}";
    }

    public string ShortRange(WeekRef r)
    {
        var days = cal.DaysOf(r);
        return ShortInterval(days[0], days[^1]);
    }

    public string ShortInterval(DateOnly s, DateOnly e)
    {
        if (s.Month != e.Month || s.Year != e.Year) return $"{Format(s, DateTemplate.DayMonth)} – {Format(e, DateTemplate.DayMonth)}";
        return dayFirst ? $"{s.Day}–{e.Day} {Mon(s)}" : $"{Mon(s)} {s.Day}–{e.Day}";
    }

    public string Relative(int weeks) => weeks switch
    {
        0 => "This week",
        1 => "Next week",
        -1 => "Last week",
        > 0 => $"In {weeks} weeks",
        _ => $"{-weeks} weeks ago",
    };

    public string Months(WeekRef r)
    {
        var days = cal.DaysOf(r);
        string a = Format(days[0], DateTemplate.MonthLong), b = Format(days[^1], DateTemplate.MonthLong);
        return a == b ? a : $"{a} / {b}";
    }

    public string CopyText(WeekRef r, int currentYear) =>
        $"CW{r.Week}{(r.Year == currentYear ? "" : $" {r.Year}")} ({ShortRange(r)})";
}
