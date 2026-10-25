using System.Text.Json;
using System.Text.Json.Serialization;

namespace Weekmark.Core;

// Mac JSON shapes: camelCase keys, missing keys fall back to the defaults below.
public sealed record SprintConfig
{
    [JsonPropertyName("enabled")] public bool Enabled { get; init; } = false;
    [JsonPropertyName("lengthWeeks")] public int LengthWeeks { get; init; } = 2;
    /// <summary>Any date in the first sprint's first week.</summary>
    [JsonPropertyName("anchor")] public Day Anchor { get; init; } = new(2026, 1, 5);
    [JsonPropertyName("firstNumber")] public int FirstNumber { get; init; } = 1;
    [JsonPropertyName("name")] public string Name { get; init; } = "Sprint";
    /// <summary>0 = Program Increments off.</summary>
    [JsonPropertyName("sprintsPerPI")] public int SprintsPerPI { get; init; } = 0;
    [JsonPropertyName("firstPI")] public int FirstPI { get; init; } = 1;
}

/// <summary>Start inclusive, End exclusive.</summary>
public sealed record SprintInfo(int Number, DateOnly Start, DateOnly End, WeekRef FirstWeek, WeekRef LastWeek,
    int Workday, int WorkdaysTotal, int? Pi, int? SprintInPI, int SprintsPerPI);

public static class Sprints
{
    static int FloorDiv(int a, int b) => (int)Math.Floor((double)a / b);

    // Lengths are 1–6 weeks in the Mac's settings; up to a year keeps every sprint's dates inside DateOnly for years 2–9998.
    static DateOnly? AnchorStart(SprintConfig c, WeekCalendar cal) =>
        c.Enabled && c.LengthWeeks is > 0 and <= 52 && c.Anchor.Y is >= 2 and <= 9998 && c.Anchor.TryToDate() is { } a
            ? cal.StartOfWeek(a) : null;

    public static int? Index(DateOnly d, SprintConfig c, WeekCalendar cal) =>
        AnchorStart(c, cal) is { } a ? FloorDiv(d.DayNumber - a.DayNumber, c.LengthWeeks * 7) : null;

    public static SprintInfo? Info(DateOnly today, SprintConfig c, WeekCalendar cal, HolidayStore h)
    {
        if (Index(today, c, cal) is not { } idx) return null;
        var start = AnchorStart(c, cal)!.Value.AddDays(idx * c.LengthWeeks * 7);
        var end = start.AddDays(c.LengthWeeks * 7);
        int? pi = null, inPI = null;
        if (c.SprintsPerPI > 0)
        {
            var p = FloorDiv(idx, c.SprintsPerPI);
            pi = c.FirstPI + p;
            inPI = idx - p * c.SprintsPerPI + 1;
        }
        return new(c.FirstNumber + idx, start, end, cal.WeekOf(start), cal.WeekOf(end.AddDays(-1)),
            h.WorkingDays(start, today.AddDays(1)), h.WorkingDays(start, end), pi, inPI, c.SprintsPerPI);
    }

    /// <summary>True if a sprint starts in this week (month-grid boundaries).</summary>
    public static bool StartsSprint(WeekRef r, SprintConfig c, WeekCalendar cal)
    {
        var s = cal.StartOfWeek(r);
        return Index(s, c, cal) is { } i && Index(s.AddDays(-1), c, cal) is { } p && i != p;
    }
}

[JsonConverter(typeof(JsonStringEnumConverter<QuarterMode>))]
public enum QuarterMode { [JsonStringEnumMemberName("months")] Months, [JsonStringEnumMemberName("weeks")] Weeks }

public sealed record QuarterConfig
{
    [JsonPropertyName("enabled")] public bool Enabled { get; init; } = true;
    /// <summary>1 = January (calendar year).</summary>
    [JsonPropertyName("fiscalStartMonth")] public int FiscalStartMonth { get; init; } = 1;
    [JsonPropertyName("mode")] public QuarterMode Mode { get; init; } = QuarterMode.Months;
}

/// <summary>Label is "Q4 2026" or "Q3 FY27".</summary>
public sealed record QuarterInfo(int Quarter, string Label);

public static class Quarters
{
    public static QuarterInfo? Info(DateOnly d, QuarterConfig c, WeekCalendar cal)
    {
        if (!c.Enabled) return null;
        if (c.Mode == QuarterMode.Weeks)
        {
            var r = cal.WeekOf(d);
            var wq = Math.Min(4, (r.Week - 1) / 13 + 1);
            return new(wq, $"Q{wq} {r.Year}");
        }
        var start = Math.Clamp(c.FiscalStartMonth, 1, 12);
        var q = (d.Month - start + 12) % 12 / 3 + 1;
        if (start == 1) return new(q, $"Q{q} {d.Year}");
        var fy = d.Month >= start ? d.Year + 1 : d.Year; // fiscal year named by its end year
        return new(q, $"Q{q} FY{fy % 100:00}");
    }
}

[JsonConverter(typeof(JsonStringEnumConverter<MilestoneColor>))]
public enum MilestoneColor
{
    [JsonStringEnumMemberName("orange")] Orange, [JsonStringEnumMemberName("red")] Red,
    [JsonStringEnumMemberName("pink")] Pink, [JsonStringEnumMemberName("purple")] Purple,
    [JsonStringEnumMemberName("blue")] Blue, [JsonStringEnumMemberName("teal")] Teal,
    [JsonStringEnumMemberName("green")] Green,
}

/// <summary>Swift encodes UUIDs as uppercase strings.</summary>
public sealed class UpperGuidConverter : JsonConverter<Guid>
{
    public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetGuid();
    public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString().ToUpperInvariant());
}

public sealed record Milestone(
    [property: JsonPropertyName("id"), JsonConverter(typeof(UpperGuidConverter))] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("week")] WeekRef Week,
    [property: JsonPropertyName("color")] MilestoneColor Color = MilestoneColor.Orange);

/// <summary>WeeksAway: 0 = due this week, negative = past. WorkdaysLeft: today (inclusive) to the end of the milestone week.</summary>
public sealed record MilestoneStatus(int WeeksAway, int WorkdaysLeft)
{
    public bool IsPast => WeeksAway < 0;
}

public static class Milestones
{
    /// <summary>Null when the milestone's week does not exist (hand-edited settings), as on the Mac.</summary>
    public static MilestoneStatus? Status(Milestone m, DateOnly today, WeekCalendar cal, HolidayStore h)
    {
        if (m.Week.Year is < 2 or > 9998 || m.Week.Week < 1 || m.Week.Week > cal.WeeksIn(m.Week.Year)) return null;
        var weeks = cal.WeeksBetween(cal.WeekOf(today), m.Week);
        return new(weeks, weeks < 0 ? 0 : h.WorkingDays(today, cal.StartOfWeek(m.Week).AddDays(7)));
    }

    public static IReadOnlyList<Milestone> Upcoming(IEnumerable<Milestone> list, DateOnly today, WeekCalendar cal)
    {
        var cur = cal.WeekOf(today);
        return list.Where(m => m.Week.CompareTo(cur) >= 0).OrderBy(m => m.Week).ToList();
    }
}
