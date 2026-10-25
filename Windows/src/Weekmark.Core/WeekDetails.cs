namespace Weekmark.Core;

public sealed record WeekContext(WeekCalendar Cal, WeekFormatter Fmt, DateTime Now, HolidayStore Holidays,
    IReadOnlyList<Milestone> Milestones, SprintConfig Sprint)
{
    /// <summary>Free-form date detection (the Mac's NSDataDetector); null = not a date. Plugged in by the date grammar.</summary>
    public Func<string, WeekContext, DateOnly?> DateDetector { get; init; } = (_, _) => null;
    public DateOnly Today => DateOnly.FromDateTime(Now);
    public WeekRef Current => Cal.WeekOf(Today);
}

/// <summary>The week-details line; port of the Mac's QueryEngine.weekResult.</summary>
public sealed record WeekDetails(string Title, string Subtitle, string? Detail, string CopyText, WeekRef Week, int Weeks, int Workdays)
{
    public static WeekDetails For(WeekRef r, WeekContext ctx)
    {
        var f = ctx.Fmt;
        var weeks = ctx.Cal.WeeksBetween(ctx.Current, r);
        var wd = ctx.Holidays.WorkingDays(r, ctx.Cal);
        var detail = new List<string>();
        foreach (var h in ctx.Holidays.In(r, ctx.Cal))
            if (h.Day.TryToDate() is { } d) detail.Add($"{h.Name} ({f.Format(d, DateTemplate.Weekday)})");
        detail.AddRange(ctx.Milestones.Where(m => m.Week == r).Select(m => $"◆ {m.Name}"));
        if (SprintLabel(r, ctx) is { } s) detail.Add(s);
        if (OtherNumbering(r, ctx) is { } o) detail.Add(o);
        return new($"CW{r.Week} · {f.LongRange(r)}",
            $"{f.Relative(weeks)} · {f.Months(r)} · {wd} working day{(wd == 1 ? "" : "s")}",
            detail.Count == 0 ? null : string.Join(" · ", detail),
            f.CopyText(r, ctx.Current.Year), r, weeks, wd);
    }

    static string? SprintLabel(WeekRef r, WeekContext ctx)
    {
        if (!ctx.Sprint.Enabled || Sprints.Info(ctx.Cal.StartOfWeek(r), ctx.Sprint, ctx.Cal, ctx.Holidays) is not { } i) return null;
        var label = $"{ctx.Sprint.Name} {i.Number}";
        if (i.Pi is { } pi && i.SprintInPI is { } n) label += $" (PI {pi}, {n}/{i.SprintsPerPI})";
        return label;
    }

    /// <summary>Warns when ISO and US numbering disagree for this week (compared on its Wednesday).</summary>
    static string? OtherNumbering(WeekRef r, WeekContext ctx)
    {
        var other = ctx.Cal.Numbering == WeekNumbering.Iso ? WeekNumbering.Us : WeekNumbering.Iso;
        var mid = ctx.Cal.DaysOf(r).First(d => d.DayOfWeek == DayOfWeek.Wednesday);
        var ow = new WeekCalendar(other).WeekOf(mid).Week;
        return ow == r.Week ? null : $"{(other == WeekNumbering.Us ? "US" : "ISO")} numbering: CW{ow}";
    }
}
