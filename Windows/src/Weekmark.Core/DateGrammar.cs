using System.Text.RegularExpressions;

namespace Weekmark.Core;

/// <summary>Explicit date parser replacing the Mac's NSDataDetector. Whole-input only; never throws; invalid dates are null.</summary>
public static partial class DateGrammar
{
    const string Months = "january|february|march|april|may|june|july|august|september|october|november|december|jan|feb|mar|apr|jun|jul|aug|sept|sep|oct|nov|dec";
    const string WeekdayNames = "sunday|monday|tuesday|wednesday|thursday|friday|saturday|sun|mon|tue|wed|thu|fri|sat";
    static readonly string[] Abbr = ["jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec"];
    const RegexOptions O = RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;

    [GeneratedRegex(@"^(\d{4})-(\d{1,2})-(\d{1,2})$", O | RegexOptions.ECMAScript)] private static partial Regex Iso();
    [GeneratedRegex(@"^(\d{1,2})[/.-](\d{1,2})[/.-](\d{4}|\d{2})$", O | RegexOptions.ECMAScript)] private static partial Regex Numeric();
    [GeneratedRegex(@"^(\d{1,2})(?:st|nd|rd|th)?\s+(?:of\s+)?(" + Months + @")\.?,?(?:\s+(\d{4}))?$", O | RegexOptions.ECMAScript)] private static partial Regex DayMonth();
    [GeneratedRegex(@"^(" + Months + @")\.?\s+(\d{1,2})(?:st|nd|rd|th)?,?(?:\s+(\d{4}))?$", O | RegexOptions.ECMAScript)] private static partial Regex MonthDay();
    [GeneratedRegex(@"^(?:(next|last)\s+)?(" + WeekdayNames + @")$", O)] private static partial Regex Weekday();

    public static DateOnly? Parse(string input, DateOnly today, bool dayFirst)
    {
        var s = Regex.Replace(input.Trim().TrimEnd('.').Trim(), @"\s+", " ").ToLowerInvariant();
        if (s.Length == 0) return null;
        switch (s)
        {
            case "today": return today;
            case "tomorrow": return Add(today, 1);
            case "yesterday": return Add(today, -1);
        }
        if (Weekday().Match(s) is { Success: true } w)
        {
            var target = WeekdayNames.Split('|').ToList().FindIndex(n => n == w.Groups[2].Value) % 7;
            var ahead = (target - (int)today.DayOfWeek + 7) % 7;
            return w.Groups[1].Value == "last" ? Add(today, -(ahead == 0 ? 7 : 7 - ahead)) : Add(today, ahead == 0 ? 7 : ahead);
        }
        if (Iso().Match(s) is { Success: true } i) return Make(Int(i, 1), Int(i, 2), Int(i, 3));
        if (Numeric().Match(s) is { Success: true } n)
        {
            int a = Int(n, 1), b = Int(n, 2), y = Int(n, 3);
            if (n.Groups[3].Length == 2) y += 2000;
            var dayIsFirst = a > 12 || (b <= 12 && dayFirst);
            return dayIsFirst ? Make(y, b, a) : Make(y, a, b);
        }
        if (DayMonth().Match(s) is { Success: true } dm) return WithYear(dm, 2, Int(dm, 1), 3, today);
        if (MonthDay().Match(s) is { Success: true } md) return WithYear(md, 1, Int(md, 2), 3, today);
        return null;
    }

    static int Int(Match m, int g) => int.Parse(m.Groups[g].Value, System.Globalization.CultureInfo.InvariantCulture);

    static DateOnly? Add(DateOnly d, int n) => d.DayNumber + n is var k && k >= 0 && k <= DateOnly.MaxValue.DayNumber ? d.AddDays(n) : null;

    static DateOnly? WithYear(Match m, int monthGroup, int day, int yearGroup, DateOnly today)
    {
        var name = m.Groups[monthGroup].Value;
        var month = Array.IndexOf(Abbr, m.Groups[monthGroup].Value[..3]) + 1;
        if (m.Groups[yearGroup].Success) return Make(Int(m, yearGroup), month, day);
        // Year-less: this year if on/after today, else next.
        return Make(today.Year, month, day) is { } d && d >= today ? d : Make(today.Year + 1, month, day);
    }

    static DateOnly? Make(int y, int m, int d) =>
        y is >= 1 and <= 9999 && m is >= 1 and <= 12 && d >= 1 && d <= DateTime.DaysInMonth(y, m) ? new DateOnly(y, m, d) : null;
}
