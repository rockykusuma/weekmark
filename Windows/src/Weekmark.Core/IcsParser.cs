using System.Globalization;

namespace Weekmark.Core;

/// <summary>Minimal iCalendar parser for holiday lists: all-day (and timed) VEVENTs, one Holiday per day.</summary>
public static class IcsParser
{
    public static IReadOnlyList<Holiday> Parse(string text, string source)
    {
        text = text.TrimStart('﻿');
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

    static void AddRange(List<Holiday> o, Day s, Day? end, string summary, string source)
    {
        // DTEND is exclusive; at most 31 days per event.
        var first = s.ToDate();
        var last = end is { } e && e.CompareTo(s) > 0 ? e.ToDate().AddDays(-1) : first;
        var name = summary.Length == 0 ? "Holiday" : summary;
        for (var d = first; d <= last && d.DayNumber - first.DayNumber < 31; d = d.AddDays(1))
            o.Add(new Holiday(Day.From(d), name, source));
    }

    static Day? ParseDay(string v)
    {
        // Out-of-range dates (e.g. month 13) are skipped; Swift would roll them over.
        if (v.Length < 8 || !int.TryParse(v.AsSpan(0, 8), NumberStyles.None, CultureInfo.InvariantCulture, out var n)) return null;
        var (y, m, d) = (n / 10000, n / 100 % 100, n % 100);
        return y is >= 1 and <= 9999 && m is >= 1 and <= 12 && d >= 1 && d <= DateTime.DaysInMonth(y, m) ? new Day(y, m, d) : null;
    }
}
