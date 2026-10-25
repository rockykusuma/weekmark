using System.Globalization;

namespace Weekmark.Core.Tests;

public class WeekFormatterTests
{
    static readonly WeekCalendar Cal = new(WeekNumbering.Iso);

    static WeekFormatter For(string locale) => new(Cal, locale == "en-GB");

    static readonly (string Key, DateTemplate T)[] Templates =
    [
        ("EEE", DateTemplate.Weekday), ("EEEdMMM", DateTemplate.WeekdayDayMonth), ("EEEdMMMyyyy", DateTemplate.WeekdayDayMonthYear),
        ("dMMM", DateTemplate.DayMonth), ("MMM", DateTemplate.Month), ("MMMM", DateTemplate.MonthLong), ("MMMMyyyy", DateTemplate.MonthYear),
        ("EEEEMMMMd", DateTemplate.WeekdayLongMonthDay), ("EEEEdMMMM", DateTemplate.WeekdayLongDayMonth), ("EEEEdMMMMyyyy", DateTemplate.WeekdayLongDayMonthYear),
    ];

    [Theory]
    [InlineData("en-GB")]
    [InlineData("en-US")]
    public void TemplatesMatchSwift(string locale)
    {
        var f = For(locale);
        var tpl = Vectors.Root.GetProperty("format").GetProperty(locale).GetProperty("templates");
        foreach (var (key, t) in Templates)
        {
            var rows = tpl.GetProperty(key).EnumerateArray().ToList();
            Assert.NotEmpty(rows);
            foreach (var r in rows)
                Assert.Equal(r[1].GetString(), f.Format(DateOnly.ParseExact(r[0].GetString()!, "yyyy-MM-dd"), t));
        }
    }

    [Theory]
    [InlineData("en-GB")]
    [InlineData("en-US")]
    public void RangesMatchSwift(string locale)
    {
        var f = For(locale);
        foreach (var (name, fn) in new (string, Func<WeekRef, string>)[] { ("longRange", f.LongRange), ("shortRange", f.ShortRange), ("months", f.Months) })
        {
            var rows = Vectors.Root.GetProperty("format").GetProperty(locale).GetProperty(name).EnumerateArray().ToList();
            Assert.NotEmpty(rows);
            foreach (var r in rows)
                Assert.Equal(r[2].GetString(), fn(new WeekRef(r[0].GetInt32(), r[1].GetInt32())));
        }
    }

    [Theory]
    [InlineData("en-GB")]
    [InlineData("en-US")]
    public void CopyTextMatchesSwift(string locale)
    {
        var f = For(locale);
        var rows = Vectors.Root.GetProperty("format").GetProperty(locale).GetProperty("copyText").EnumerateArray().ToList();
        Assert.NotEmpty(rows);
        foreach (var r in rows)
            Assert.Equal(r[3].GetString(), f.CopyText(new WeekRef(r[0].GetInt32(), r[1].GetInt32()), r[2].GetInt32()));
    }

    [Fact]
    public void RelativeText()
    {
        var f = For("en-GB");
        Assert.Equal("This week", f.Relative(0));
        Assert.Equal("Next week", f.Relative(1));
        Assert.Equal("Last week", f.Relative(-1));
        Assert.Equal("In 3 weeks", f.Relative(3));
        Assert.Equal("2 weeks ago", f.Relative(-2));
    }

    [Theory]
    [InlineData("en-US", false)]
    [InlineData("en-GB", true)]
    [InlineData("de-DE", true)]
    [InlineData("en-IN", true)]
    public void DayFirstForCultures(string name, bool expected) =>
        Assert.Equal(expected, WeekFormatter.DayFirstFor(CultureInfo.GetCultureInfo(name)));
}
