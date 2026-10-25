using System.Globalization;

namespace Weekmark.Core;

/// <summary>Minimal iCalendar parser for holiday lists: all-day (and timed) VEVENTs, one Holiday per day.</summary>
public static class IcsParser
{
    public static IReadOnlyList<Holiday> Parse(string text, string source)
    {
        text = text.TrimStart((char)0xFEFF);
        var raw = text.Replace("\r\n", "\n").Replace("\r", "\n");
        var lines = new List<string>();
        foreach (var l in raw.Split('\n'))
        {
            if ((l.StartsWith(' ') || l.StartsWith('\t')) && lines.Count > 0) lines[^1] += l[1..];
            else lines.Add(l);
        }

        var o = new List<Holiday>();
        var inEvent = false;
        Day? start = null, end = null;
        var summary = "";
        foreach (var line in lines)
        {
            if (line == "BEGIN:VEVENT") { inEvent = true; start = null; end = null; summary = ""; continue; }
            if (line == "END:VEVENT")
            {
                if (inEvent && start is { } s) AddRange(o, s, end, summary, source);
                inEvent = false;
                continue;
            }
            var colon = line.IndexOf(':');
            if (!inEvent || colon < 0) continue;
            var key = line[..colon].Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToUpperInvariant() ?? "";
            var value = line[(colon + 1)..];
            switch (key)
            {
                case "DTSTART": start = ParseDay(value); break;
                case "DTEND": end = ParseDay(value); break;
                case "SUMMARY": summary = value.Replace("\\,", ",").Replace("\\;", ";"); break;
            }
        }
        return o;
    }

    // Raw (unvalidated) days roll over like Foundation's Gregorian calendar: 2026-02-30 is 2026-03-02, month 13 is January.
    // The one accepted divergence: .NET ends at 9999-12-31 (the Mac goes further), so Add returns null past the ends.
    static Day? Add(Day d, int n)
    {
        try { return Day.From(new DateOnly(d.Y, 1, 1).AddMonths(d.M - 1).AddDays(d.D - 1 + n)); }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    static void AddRange(List<Holiday> o, Day s, Day? end, string summary, string source)
    {
        // DTEND is exclusive; at most 31 days per event.
        var last = end is { } e && e.CompareTo(s) > 0 ? Add(e, -1) ?? new Day(9999, 12, 31) : s;
        var name = summary.Length == 0 ? "Holiday" : summary;
        Day? d = s;
        for (var count = 0; d is { } cur && cur.CompareTo(last) <= 0 && count < 31; count++)
        {
            o.Add(new Holiday(cur, name, source));
            d = Add(cur, 1);
        }
    }

    static Day? ParseDay(string v) =>
        v.Length >= 8 && int.TryParse(v.AsSpan(0, 8), NumberStyles.None, CultureInfo.InvariantCulture, out var n)
            ? new Day(n / 10000, n / 100 % 100, n % 100)
            : null;
}
