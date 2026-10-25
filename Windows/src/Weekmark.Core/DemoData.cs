namespace Weekmark.Core;

/// <summary>Sample settings for screenshots and <c>--demo-data</c>: never personal data.</summary>
public static class DemoData
{
    public static AppSettings Create(DateOnly today)
    {
        var cal = new WeekCalendar(WeekNumbering.Iso);
        return new AppSettings
        {
            Sprint = new SprintConfig { Enabled = true, LengthWeeks = 2, Anchor = new Day(2026, 1, 5), SprintsPerPI = 5 },
            Regions = [HolidayRegion.Gb],
            Milestones =
            [
                new(new Guid("6F0C2B1E-5D4A-4C3B-9A8E-1F2D3C4B5A60"), "Code freeze", cal.WeekOf(today.AddDays(21)), MilestoneColor.Purple),
                new(new Guid("6F0C2B1E-5D4A-4C3B-9A8E-1F2D3C4B5A61"), "Release 5.0", cal.WeekOf(today.AddDays(35)), MilestoneColor.Orange),
            ],
        };
    }
}
