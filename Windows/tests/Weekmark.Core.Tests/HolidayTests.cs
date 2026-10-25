using System.Text.Json;

namespace Weekmark.Core.Tests;

public class HolidayTests
{
    static DateOnly Date(JsonElement e) => DateOnly.ParseExact(e.GetString()!, "yyyy-MM-dd");

    [Fact]
    public void HolidaysMatchSwift()
    {
        var regions = Vectors.Root.GetProperty("holidays").EnumerateObject().ToList();
        Assert.Equal(10, regions.Count);
        foreach (var r in regions)
        {
            var region = HolidayRegionExtensions.Guess(r.Name)!.Value;
            foreach (var y in r.Value.EnumerateObject())
            {
                var expected = y.Value.EnumerateArray().Select(e => (Date(e[0]), e[1].GetString()!)).ToList();
                var rules = HolidayRules.For(region, int.Parse(y.Name));
                var actual = rules.Select(h => (h.Day.ToDate(), h.Name)).ToList();
                Assert.NotEmpty(expected);
                Assert.True(expected.SequenceEqual(actual), $"{r.Name} {y.Name}");
                Assert.All(rules, h => Assert.Equal(r.Name, h.Source));
            }
        }
    }

    [Fact]
    public void WorkingDaysMatchSwift()
    {
        var entries = Vectors.Root.GetProperty("workingDays").EnumerateArray().ToList();
        Assert.NotEmpty(entries);
        foreach (var e in entries)
        {
            var regions = e.GetProperty("regions").EnumerateArray().Select(x => HolidayRegionExtensions.Guess(x.GetString()!)!.Value).ToList();
            var store = new HolidayStore(regions, []);
            Assert.Equal(e.GetProperty("n").GetInt32(), store.WorkingDays(Date(e.GetProperty("from")), Date(e.GetProperty("to"))));
        }
    }

    [Fact]
    public void IcsMatchesSwift()
    {
        var samples = Vectors.Root.GetProperty("ics").EnumerateArray().ToList();
        Assert.NotEmpty(samples);
        foreach (var s in samples)
        {
            var source = s.GetProperty("source").GetString()!;
            // Compared as raw strings: the Mac keeps impossible dates (2026-02-30) unvalidated.
            var expected = s.GetProperty("holidays").EnumerateArray().Select(h => (h[0].GetString()!, h[1].GetString()!, h[2].GetString()!)).ToList();
            var actual = IcsParser.Parse(s.GetProperty("text").GetString()!, source)
                .Select(h => ($"{h.Day.Y:0000}-{h.Day.M:00}-{h.Day.D:00}", h.Name, h.Source)).ToList();
            Assert.True(expected.SequenceEqual(actual), s.GetProperty("name").GetString());
        }
    }

    [Fact]
    public void IcsStopsAtYear9999()
    {
        string Ev(string start, string? end) =>
            $"BEGIN:VEVENT\nDTSTART;VALUE=DATE:{start}\n" + (end is null ? "" : $"DTEND;VALUE=DATE:{end}\n") + "SUMMARY:X\nEND:VEVENT\n";
        // DTEND month 13 rolls to 10000-01-15, beyond what .NET can represent.
        var days = IcsParser.Parse(Ev("99991215", "99991315"), "Company").Select(h => h.Day).ToList();
        Assert.Equal(new Day(9999, 12, 15), days[0]);
        Assert.Equal(new Day(9999, 12, 31), days[^1]);
        Assert.Equal(17, days.Count);
        Assert.Single(IcsParser.Parse(Ev("99991231", null), "Company"));
    }

    [Fact]
    public void GuessRegion()
    {
        Assert.Equal(HolidayRegion.De, HolidayRegionExtensions.Guess("DE"));
        Assert.Equal(HolidayRegion.Gb, HolidayRegionExtensions.Guess("UK"));
        Assert.Null(HolidayRegionExtensions.Guess("JP"));
    }

    [Fact]
    public void JsonShapes()
    {
        Assert.Equal("\"in\"", JsonSerializer.Serialize(HolidayRegion.In));
        Assert.Equal(HolidayRegion.Gb, JsonSerializer.Deserialize<HolidayRegion>("\"gb\""));
        Assert.Equal("{\"day\":{\"y\":2026,\"m\":12,\"d\":25},\"name\":\"X\",\"source\":\"custom\"}",
            JsonSerializer.Serialize(new Holiday(new Day(2026, 12, 25), "X", "custom")));
    }

    [Fact]
    public void StoreQueries()
    {
        var store = new HolidayStore([HolidayRegion.De, HolidayRegion.Gb], []);
        Assert.True(store.IsHoliday(new Day(2026, 12, 25)));
        Assert.Equal(["1. Weihnachtstag", "Christmas Day"], store.On(new Day(2026, 12, 25)).Select(h => h.Name));
        var up = store.Upcoming(new DateOnly(2026, 12, 20), 3);
        Assert.Equal(3, up.Count);
        Assert.Equal(new Day(2026, 12, 25), up[0].Day);
        Assert.Empty(HolidayStore.Empty.Upcoming(new DateOnly(2026, 1, 1)));
    }
}
