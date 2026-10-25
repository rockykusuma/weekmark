using System.Globalization;
using System.Text;

namespace Weekmark.Core;

public enum QueryKind { Week, Range, Date, Relative, Countdown, Milestone, Holiday, Hint, Error }

public sealed record QueryResult(QueryKind Kind, string Title, string Subtitle, string? Detail = null, string? CopyText = null,
    WeekRef? Week = null, int? Weeks = null, int? Workdays = null);

public abstract record WeekParse
{
    public sealed record None : WeekParse;
    public sealed record Week(WeekRef Ref) : WeekParse;
    public sealed record Invalid(string Message) : WeekParse;
}

/// <summary>Port of the Mac's QueryEngine. Never throws: out-of-range weeks and numbers read as "no match".</summary>
public static class QueryEngine
{
    public const string Examples = "Try 46 · CW3 2027 · Dec 12 · +6w · 46-50 · until CW52";

    // A detector that throws reads as "not a date": nothing on the query path may throw.
    static DateOnly? Detect(string s, WeekContext ctx)
    {
        try { return ctx.DateDetector(s, ctx); }
        catch (Exception) { return null; }
    }

    // ---- Entry point ----

    public static IReadOnlyList<QueryResult> Evaluate(string input, WeekContext ctx)
    {
        var q = input.Trim();
        var lower = q.ToLowerInvariant();
        if (q.Length == 0) return Overview(ctx);

        if (Countdown(lower, ctx) is { } c) return [c];
        if (Keyword(lower, ctx) is { } k) return [k, .. MilestoneMatches(lower, ctx)];
        if (Relative(lower, ctx) is { } r) return [r];
        if (Range(lower, ctx) is { } g) return [g];
        switch (ParseWeek(lower, ctx))
        {
            case WeekParse.Week w: return [WeekResult(w.Ref, ctx)];
            case WeekParse.Invalid i: return [new(QueryKind.Error, i.Message, Examples)];
        }
        var ms = MilestoneMatches(lower, ctx);
        if (Detect(q, ctx) is { } d && DateResult(d, ctx) is { } dr) return [dr, .. ms];
        if (ms.Count > 0) return ms;
        return [new(QueryKind.Error, $"No match for “{q}”", Examples)];
    }

    /// <summary>Shown when the query is empty: this week, next milestones and holiday, and a hint.</summary>
    public static IReadOnlyList<QueryResult> Overview(WeekContext ctx)
    {
        var f = ctx.Fmt;
        var o = new List<QueryResult> { WeekResult(ctx.Current, ctx) };
        foreach (var m in Milestones.Upcoming(ctx.Milestones.Where(m => Valid(m.Week, ctx)), ctx.Today, ctx.Cal).Take(3))
            o.Add(MilestoneResult(m, ctx));
        if (ctx.Holidays.Upcoming(ctx.Today, 1).FirstOrDefault() is { } h && h.Day.TryToDate() is { } d)
        {
            var wr = ctx.Cal.WeekOf(d);
            o.Add(new(QueryKind.Holiday, $"Next holiday: {h.Name}",
                $"{f.Format(d, DateTemplate.WeekdayLongDayMonth)} · CW{wr.Week} · {f.Relative(ctx.Cal.WeeksBetween(ctx.Current, wr))}",
                CopyText: f.CopyText(wr, ctx.Current.Year), Week: wr));
        }
        o.Add(new(QueryKind.Hint, "Type a week, date, range or offset", Examples));
        return o;
    }

    // ---- Week tokens ----

    /// <summary>Accepts "46", "CW46", "KW 46", "w46", "week 46", "46 2027", "46/2027", "2027-W46", "2027 46".</summary>
    public static WeekParse ParseWeek(string input, WeekContext ctx)
    {
        var s = input.ToLowerInvariant().Trim();
        if (s.StartsWith('+') || s.StartsWith('-')) return new WeekParse.None();
        foreach (var p in new[] { "calendar week", "week", "cw", "kw", "wk" }) s = s.Replace(p, "w");
        var letters = string.Concat(s.EnumerateRunes().Where(Rune.IsLetter).Select(r => r.ToString()));
        if (letters.Length != 0 && letters != "w") return new WeekParse.None();
        var nums = NumberRuns(s).Select(r => long.TryParse(r, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : (long?)null)
            .OfType<long>().ToList();
        long week, year = 0;
        switch (nums.Count)
        {
            case 1:
                if (nums[0] >= 100) return new WeekParse.None();
                week = nums[0];
                break;
            case 2:
                var big = nums.Where(n => n >= 1000).ToList();
                var small = nums.Where(n => n < 100).ToList();
                if (big.Count != 1 || small.Count != 1) return new WeekParse.None();
                year = big[0]; week = small[0];
                break;
            default:
                return new WeekParse.None();
        }
        var y = year != 0 ? year : ctx.Current.Year;
        // Late in the year, a small week number almost always means next year.
        if (year == 0 && ctx.Current.Week >= 40 && week <= 12) y++;
        if (y is < 2 or > 9998) return new WeekParse.None();
        var maxW = ctx.Cal.WeeksIn((int)y);
        return week >= 1 && week <= maxW ? new WeekParse.Week(new((int)y, (int)week)) : new WeekParse.Invalid($"{y} has only {maxW} weeks");
    }

    // Runs of Unicode "number" characters, as Swift's split(whereSeparator: !isNumber).
    static List<string> NumberRuns(string s)
    {
        var runs = new List<string>();
        var sb = new StringBuilder();
        foreach (var r in s.EnumerateRunes())
        {
            if (Rune.IsNumber(r)) sb.Append(r.ToString());
            else if (sb.Length > 0) { runs.Add(sb.ToString()); sb.Clear(); }
        }
        if (sb.Length > 0) runs.Add(sb.ToString());
        return runs;
    }

    // ---- Safe week arithmetic ----

    static bool Valid(WeekRef r, WeekContext ctx) =>
        r.Year is >= 2 and <= 9998 && r.Week >= 1 && r.Week <= ctx.Cal.WeeksIn(r.Year);

    static WeekRef? AddWeeks(WeekRef r, int n, WeekContext ctx)
    {
        if (!Valid(r, ctx)) return null;
        try { var x = ctx.Cal.AddWeeks(r, n); return Valid(x, ctx) ? x : null; }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    // ---- Result builders ----

    public static QueryResult WeekResult(WeekRef r, WeekContext ctx)
    {
        var d = WeekDetails.For(r, ctx);
        return new(QueryKind.Week, d.Title, d.Subtitle, d.Detail, d.CopyText, d.Week, d.Weeks, d.Workdays);
    }

    static QueryResult MilestoneResult(Milestone m, WeekContext ctx)
    {
        var st = Milestones.Status(m, ctx.Today, ctx.Cal, ctx.Holidays);
        var f = ctx.Fmt;
        var left = st is null ? "" : st.IsPast ? "done" : $"{st.WorkdaysLeft} working days left";
        return new(QueryKind.Milestone, $"{m.Name} · CW{m.Week.Week}",
            $"{f.Relative(st?.WeeksAway ?? 0)} · {left} · {f.ShortRange(m.Week)}",
            CopyText: $"{m.Name}: {f.CopyText(m.Week, ctx.Current.Year)}", Week: m.Week, Weeks: st?.WeeksAway, Workdays: st?.WorkdaysLeft);
    }

    // ponytail: milestones whose week doesn't exist (hand-edited settings) are skipped; the Mac would print a garbage range.
    static List<QueryResult> MilestoneMatches(string lower, WeekContext ctx) =>
        new StringInfo(lower).LengthInTextElements < 2 ? [] :
        ctx.Milestones.Where(m => Valid(m.Week, ctx) && m.Name.ToLowerInvariant().Contains(lower, StringComparison.Ordinal))
            .Select(m => MilestoneResult(m, ctx)).ToList();

    // An edge date (year 1 or 9999) can fall outside the week domain: read as "not a date".
    static QueryResult? DateResult(DateOnly date, WeekContext ctx)
    {
        try { return BuildDateResult(date, ctx); }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException) { return null; }
    }

    static QueryResult BuildDateResult(DateOnly date, WeekContext ctx)
    {
        var wr = ctx.Cal.WeekOf(date);
        var b = WeekResult(wr, ctx);
        var hol = ctx.Holidays.On(Day.From(date)).Select(h => h.Name).ToList();
        var detail = string.Join(" · ", new[] { hol.Count == 0 ? null : "Holiday: " + string.Join(", ", hol), b.Detail }.OfType<string>());
        return new(QueryKind.Date, $"{ctx.Fmt.Format(date, DateTemplate.WeekdayLongDayMonthYear)} is in CW{wr.Week}",
            $"{ctx.Fmt.ShortRange(wr)} · {ctx.Fmt.Relative(b.Weeks ?? 0)}",
            detail.Length == 0 ? null : detail, b.CopyText, wr, b.Weeks, b.Workdays);
    }

    // ---- Keywords / relative / range / countdown ----

    static QueryResult? Keyword(string lower, WeekContext ctx)
    {
        int? off = lower switch
        {
            "today" or "now" or "this week" or "current" => 0,
            "next week" => 1,
            "last week" or "previous week" => -1,
            _ => null,
        };
        return off is { } n && AddWeeks(ctx.Current, n, ctx) is { } r ? WeekResult(r, ctx) : null;
    }

    /// <summary>"+6", "+6w", "-2 weeks", "+10d", "in 6 weeks", "3 weeks ago", "6 weeks".</summary>
    static QueryResult? Relative(string lower, WeekContext ctx)
    {
        var s = lower.Replace(" ", "");
        var sign = 0;
        if (s.StartsWith("in", StringComparison.Ordinal)) { s = s[2..]; sign = 1; }
        if (s.EndsWith("ago", StringComparison.Ordinal)) { s = s[..^3]; sign = -1; }
        if (s.StartsWith('+')) { s = s[1..]; sign = 1; } else if (s.StartsWith('-')) { s = s[1..]; sign = -1; }
        var digits = string.Concat(s.EnumerateRunes().TakeWhile(Rune.IsNumber).Select(r => r.ToString()));
        if (!long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var nl) || nl > 520) return null;
        var n = (int)nl;
        var unit = s[digits.Length..];
        var isDays = unit is "d" or "day" or "days";
        var isWeeks = unit is "" or "w" or "wk" or "wks" or "week" or "weeks";
        if (!isDays && !isWeeks) return null;
        if (sign == 0) { if (unit.Length == 0) return null; sign = 1; } // bare "6" is a week number, not an offset
        var amount = sign * n;
        var f = ctx.Fmt;
        if (isDays)
        {
            var d = ctx.Today.AddDays(amount);
            var wr = ctx.Cal.WeekOf(d);
            var wd = amount >= 0
                ? ctx.Holidays.WorkingDays(ctx.Today.AddDays(1), d.AddDays(1))
                : ctx.Holidays.WorkingDays(d, ctx.Today);
            return new(QueryKind.Relative,
                $"{(amount >= 0 ? "+" : "−")}{n} day{(n == 1 ? "" : "s")} → {f.Format(d, DateTemplate.WeekdayLongDayMonthYear)}",
                $"CW{wr.Week} · {f.ShortRange(wr)} · {wd} working days {(amount >= 0 ? "away" : "ago")}",
                CopyText: $"{f.Format(d, DateTemplate.WeekdayDayMonthYear)} (CW{wr.Week})", Week: wr, Workdays: wd);
        }
        if (AddWeeks(ctx.Current, amount, ctx) is not { } r) return null;
        var b = WeekResult(r, ctx);
        return new(QueryKind.Relative,
            $"{(amount >= 0 ? "+" : "−")}{n} week{(n == 1 ? "" : "s")} → CW{r.Week}{(r.Year != ctx.Current.Year ? $" {r.Year}" : "")}",
            $"{f.LongRange(r)} · {b.Workdays ?? 0} working days",
            b.Detail, b.CopyText, r, amount, b.Workdays);
    }

    /// <summary>"46-50", "CW46 – CW50", "46 to 2", "2026-W50..2027-W2".</summary>
    static QueryResult? Range(string lower, WeekContext ctx)
    {
        foreach (var sep in new[] { "..", "–", "—", " to ", "-" })
        {
            var parts = lower.Split(sep);
            if (parts.Length != 2) continue;
            string l = parts[0].Trim(), r = parts[1].Trim();
            if (l.Length == 0 || r.Length == 0 || ParseWeek(l, ctx) is not WeekParse.Week { Ref: var a }) continue;
            WeekRef b;
            switch (ParseWeek(r, ctx))
            {
                case WeekParse.Week rw:
                    var rightHasYear = NumberRuns(r).Any(x => long.TryParse(x, NumberStyles.None, CultureInfo.InvariantCulture, out var v) && v >= 1000);
                    if (rightHasYear) b = rw.Ref;
                    else
                    {
                        // Inherit the left side's year; roll over if the range wraps into next year.
                        var y = rw.Ref.Week < a.Week ? a.Year + 1 : a.Year;
                        if (y > 9998 || rw.Ref.Week > ctx.Cal.WeeksIn(y)) return null;
                        b = new(y, rw.Ref.Week);
                    }
                    break;
                case WeekParse.Invalid i: return new(QueryKind.Error, i.Message, Examples);
                default: continue;
            }
            return RangeResult(a.CompareTo(b) <= 0 ? a : b, a.CompareTo(b) <= 0 ? b : a, ctx);
        }
        return null;
    }

    public static QueryResult? RangeResult(WeekRef a, WeekRef b, WeekContext ctx)
    {
        if (!Valid(a, ctx) || !Valid(b, ctx) || ctx.Cal.WeeksBetween(a, b) < 0) return null;
        var s = ctx.Cal.StartOfWeek(a);
        var e = ctx.Cal.StartOfWeek(b).AddDays(7);
        var last = e.AddDays(-1);
        var weeks = ctx.Cal.WeeksBetween(a, b) + 1;
        var wd = ctx.Holidays.WorkingDays(s, e);
        var f = ctx.Fmt;
        var names = new HashSet<string>();
        for (var i = 0; i < weeks; i++)
            foreach (var h in ctx.Holidays.In(ctx.Cal.AddWeeks(a, i), ctx.Cal)) names.Add(h.Name);
        var ya = a.Year != ctx.Current.Year ? $" {a.Year}" : "";
        var yb = b.Year != a.Year ? $" {b.Year}" : "";
        return new(QueryKind.Range,
            $"CW{a.Week}{ya} – CW{b.Week}{yb} · {weeks} week{(weeks == 1 ? "" : "s")}",
            $"{f.LongRange(s, last)} · {wd} working days",
            names.Count == 0 ? null : $"Includes {names.Count} holiday{(names.Count == 1 ? "" : "s")}: {string.Join(", ", names.Order(StringComparer.Ordinal))}",
            $"CW{a.Week}{ya}–CW{b.Week}{yb} ({f.ShortInterval(s, last)})", a, weeks, wd);
    }

    /// <summary>"until CW52", "to dec 24", "→ 50", "today -> CW3 2027".</summary>
    static QueryResult? Countdown(string lower, WeekContext ctx)
    {
        var s = lower;
        foreach (var p in new[] { "today", "now" }) if (s.StartsWith(p, StringComparison.Ordinal)) s = s[p.Length..];
        s = s.Trim();
        var matched = false;
        foreach (var p in new[] { "until ", "till ", "til ", "to ", "→", "->", "=>" })
            if (s.StartsWith(p, StringComparison.Ordinal)) { s = s[p.Length..].Trim(); matched = true; break; }
        if (!matched || s.Length == 0) return null;
        var f = ctx.Fmt;
        var today = ctx.Today;
        if (ParseWeek(s, ctx) is WeekParse.Week { Ref: var r })
        {
            var start = ctx.Cal.StartOfWeek(r);
            var weeks = ctx.Cal.WeeksBetween(ctx.Current, r);
            var toStart = ctx.Holidays.WorkingDays(today, start);
            var toEnd = ctx.Holidays.WorkingDays(today, start.AddDays(7));
            return new(QueryKind.Countdown, $"{weeks} week{(weeks == 1 ? "" : "s")} until CW{r.Week}",
                $"{toStart} working days until it starts · {toEnd} through its end · {f.ShortRange(r)}",
                CopyText: $"{weeks} weeks / {toEnd} working days until end of CW{r.Week}", Week: r, Weeks: weeks, Workdays: toEnd);
        }
        if (Detect(s, ctx) is { } d && CountdownToDate(d, ctx) is { } cd) return cd;
        return null;
    }

    static QueryResult? CountdownToDate(DateOnly d, WeekContext ctx)
    {
        var f = ctx.Fmt;
        var today = ctx.Today;
        try
        {
            var days = d.DayNumber - today.DayNumber;
            var wd = ctx.Holidays.WorkingDays(today, d);
            var wr = ctx.Cal.WeekOf(d);
            return new(QueryKind.Countdown, $"{days} day{(days == 1 ? "" : "s")} until {f.Format(d, DateTemplate.WeekdayDayMonthYear)}",
                $"{wd} working days · CW{wr.Week} · {f.Relative(ctx.Cal.WeeksBetween(ctx.Current, wr))}",
                CopyText: $"{days} days / {wd} working days until {f.Format(d, DateTemplate.DayMonth)}", Week: wr, Workdays: wd);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException) { return null; }
    }
}
