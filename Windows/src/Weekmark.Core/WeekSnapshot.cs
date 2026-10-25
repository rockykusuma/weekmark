namespace Weekmark.Core;

public sealed record MonthRow(int Id, WeekRef Ref, IReadOnlyList<DateOnly> Days, bool IsCurrent);

/// <summary>Everything the widget needs to draw "now", computed once per tick.</summary>
public sealed class WeekSnapshot
{
    readonly WeekCalendar cal;
    readonly WeekFormatter fmt;
    readonly DateOnly today;
    readonly DateOnly weekStart;

    public WeekSnapshot(DateTime now, WeekCalendar cal, WeekFormatter fmt)
    {
        this.cal = cal;
        this.fmt = fmt;
        today = DateOnly.FromDateTime(now);
        Current = cal.WeekOf(today);
        weekStart = cal.StartOfWeek(today);
        Days = cal.DaysOf(Current);
        WeekProgress = Math.Clamp((now - weekStart.ToDateTime(TimeOnly.MinValue)).TotalDays / 7, 0, 1);
        WeeksInYear = cal.WeeksIn(Current.Year);
    }

    public WeekRef Current { get; }
    public int WeeksInYear { get; }
    public IReadOnlyList<DateOnly> Days { get; }
    public double WeekProgress { get; }
    public int WeeksLeft => Math.Max(0, WeeksInYear - Current.Week);
    public double YearProgress => (double)Current.Week / Math.Max(1, WeeksInYear);
    public string RangeText => fmt.ShortInterval(Days[0], Days[6]);
    public string TodayText => fmt.Format(today, DateTemplate.WeekdayLongMonthDay);

    static DateOnly FirstOfMonth(DateOnly d) => new(d.Year, d.Month, 1);

    public DateOnly DisplayedMonth(int offset) => FirstOfMonth(today).AddMonths(offset);
    public string MonthTitle(DateOnly month) => fmt.Format(month, DateTemplate.MonthYear);

    public bool IsToday(DateOnly d) => d == today;
    public bool IsPast(DateOnly d) => d < today;
    public bool IsWeekend(DateOnly d) => !WeekCalendar.IsWorkday(d);
    public bool IsIn(DateOnly d, DateOnly month) => d.Year == month.Year && d.Month == month.Month;

    /// <summary>Always 6 rows so the widget height stays constant month to month.</summary>
    public IReadOnlyList<MonthRow> MonthRows(DateOnly month)
    {
        var start = cal.StartOfWeek(month);
        var rows = new List<MonthRow>(6);
        for (var i = 0; i < 6; i++, start = start.AddDays(7))
            rows.Add(new(i, cal.WeekOf(start), Enumerable.Range(0, 7).Select(start.AddDays).ToList(), start == weekStart));
        return rows;
    }

    /// <summary>Month offset (from the current month) of the month owning a week: the month of its middle day.</summary>
    public int MonthOffsetFor(WeekRef r)
    {
        var mid = cal.StartOfWeek(r).AddDays(3);
        return (mid.Year - today.Year) * 12 + mid.Month - today.Month;
    }
}

public static class WeekClock
{
    /// <summary>The next local midnight strictly after <paramref name="nowLocal"/>.</summary>
    public static DateTime NextMidnight(DateTime nowLocal) => nowLocal.Date.AddDays(1);
}
