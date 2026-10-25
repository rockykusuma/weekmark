namespace Weekmark.Core.Tests;

public class WeekSnapshotTests
{
    static WeekSnapshot Snap(DateTime now)
    {
        var cal = new WeekCalendar(WeekNumbering.Iso);
        return new(now, cal, new WeekFormatter(cal, true));
    }

    [Fact]
    public void SnapshotBasics()
    {
        var s = Snap(new(2026, 10, 25, 12, 0, 0));
        Assert.Equal(new WeekRef(2026, 43), s.Current);
        Assert.Equal(53, s.WeeksInYear);
        Assert.Equal(10, s.WeeksLeft);
        var rows = s.MonthRows(new(2026, 10, 1));
        Assert.Equal(6, rows.Count);
        Assert.Contains(new DateOnly(2026, 10, 19), rows.Single(r => r.IsCurrent).Days);
        Assert.Equal(0, s.MonthOffsetFor(new(2026, 43)));
        Assert.Equal("Sunday 25 October", s.TodayText);
    }

    [Fact]
    public void WeekProgressMondayAndSunday()
    {
        Assert.Equal(0.0, Snap(new(2026, 10, 19, 0, 0, 0)).WeekProgress);
        Assert.Equal(6.5 / 7, Snap(new(2026, 10, 25, 12, 0, 0)).WeekProgress, 9);
    }

    [Fact]
    public void NextMidnight()
    {
        Assert.Equal(new DateTime(2026, 10, 26), WeekClock.NextMidnight(new(2026, 10, 25, 23, 59, 30)));
        Assert.Equal(new DateTime(2026, 10, 27), WeekClock.NextMidnight(new(2026, 10, 26)));
    }
}
