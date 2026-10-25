using System.Text.Json;

namespace Weekmark.Core.Tests;

public class WeekMathTests
{
    static WeekNumbering Parse(string key) => key == "iso" ? WeekNumbering.Iso : WeekNumbering.Us;

    [Theory]
    [InlineData("iso")]
    [InlineData("us")]
    public void WeeksMatchSwift(string key)
    {
        var cal = new WeekCalendar(Parse(key));
        var entries = Vectors.Root.GetProperty("weeks").GetProperty(key).EnumerateArray().ToList();
        Assert.NotEmpty(entries);
        foreach (var e in entries)
        {
            var date = DateOnly.ParseExact(e[0].GetString()!, "yyyy-MM-dd");
            var expected = new WeekRef(e[1].GetInt32(), e[2].GetInt32());
            var actual = cal.WeekOf(date);
            Assert.True(expected == actual, $"{key} {date}: expected {expected}, got {actual}");
            var start = cal.StartOfWeek(actual);
            Assert.True(start <= date && date < start.AddDays(7), $"{key} {date}: start {start}");
            Assert.Equal(cal.FirstDay, start.DayOfWeek);
        }
    }

    [Theory]
    [InlineData("iso")]
    [InlineData("us")]
    public void WeeksInMatchSwift(string key)
    {
        var cal = new WeekCalendar(Parse(key));
        var props = Vectors.Root.GetProperty("weeksIn").GetProperty(key).EnumerateObject().ToList();
        Assert.NotEmpty(props);
        foreach (var p in props)
            Assert.Equal(p.Value.GetInt32(), cal.WeeksIn(int.Parse(p.Name)));
    }

    [Theory]
    [InlineData(WeekNumbering.Iso)]
    [InlineData(WeekNumbering.Us)]
    public void AddWeeksAndWeeksBetweenRoundTrip(WeekNumbering n)
    {
        var cal = new WeekCalendar(n);
        for (var y = 2015; y <= 2040; y++)
            for (var w = 1; w <= cal.WeeksIn(y); w++)
            {
                var r = new WeekRef(y, w);
                for (var k = -60; k <= 60; k++)
                {
                    var t = cal.AddWeeks(r, k);
                    Assert.Equal(k, cal.WeeksBetween(r, t));
                    Assert.Equal(r, cal.AddWeeks(t, -k));
                }
            }
    }

    [Fact]
    public void SystemReadsAsIso() =>
        Assert.Equal(WeekNumbering.Iso, JsonSerializer.Deserialize<WeekNumbering>("\"system\""));

    [Fact]
    public void JsonShapes()
    {
        Assert.Equal("\"us\"", JsonSerializer.Serialize(WeekNumbering.Us));
        Assert.Equal("{\"year\":2026,\"week\":46}", JsonSerializer.Serialize(new WeekRef(2026, 46)));
        Assert.Equal("{\"y\":2026,\"m\":11,\"d\":9}", JsonSerializer.Serialize(new Day(2026, 11, 9)));
        Assert.Equal(new Day(2026, 11, 9), JsonSerializer.Deserialize<Day>("{\"y\":2026,\"m\":11,\"d\":9}"));
    }
}
