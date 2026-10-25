using System.Globalization;
using System.Text.Json;

namespace Weekmark.Core.Tests;

public class WeekDetailsTests
{
    [Fact]
    public void WeekDetailsMatchSwift()
    {
        var entries = Vectors.Root.GetProperty("weekDetails").EnumerateArray().ToList();
        Assert.NotEmpty(entries);
        foreach (var e in entries)
        {
            var cal = new WeekCalendar(e.GetProperty("numbering").GetString() == "us" ? WeekNumbering.Us : WeekNumbering.Iso);
            var fmt = new WeekFormatter(cal, WeekFormatter.DayFirstFor(CultureInfo.GetCultureInfo(e.GetProperty("locale").GetString()!)));
            var now = DateTime.ParseExact(e.GetProperty("now").GetString()!, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
            var store = new HolidayStore(e.GetProperty("regions").EnumerateArray().Select(x => HolidayRegionExtensions.Guess(x.GetString()!)!.Value).ToList(), []);
            var ms = e.GetProperty("milestones").Deserialize<List<Milestone>>()!;
            var ctx = new WeekContext(cal, fmt, now, store, ms, e.GetProperty("sprint").Deserialize<SprintConfig>()!);
            var w = e.GetProperty("week");
            var d = WeekDetails.For(new(w.GetProperty("year").GetInt32(), w.GetProperty("week").GetInt32()), ctx);
            Assert.Equal(e.GetProperty("title").GetString(), d.Title);
            Assert.Equal(e.GetProperty("subtitle").GetString(), d.Subtitle);
            Assert.Equal(e.GetProperty("detail").GetString(), d.Detail);
            Assert.Equal(e.GetProperty("copyText").GetString(), d.CopyText);
        }
    }
}
