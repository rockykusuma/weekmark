using System.Text.Json;
using System.Text.Json.Nodes;

namespace Weekmark.Core.Tests;

public class PlanningTests
{
    static DateOnly Date(JsonElement e) => DateOnly.ParseExact(e.GetString()![..10], "yyyy-MM-dd");
    static WeekCalendar Cal(JsonElement e) => new(e.GetString() == "us" ? WeekNumbering.Us : WeekNumbering.Iso);
    static WeekRef Ref(JsonElement e) => new(e.GetProperty("year").GetInt32(), e.GetProperty("week").GetInt32());
    static HolidayStore Store(JsonElement e) =>
        new(e.EnumerateArray().Select(x => HolidayRegionExtensions.Guess(x.GetString()!)!.Value).ToList(), []);
    static int? OptInt(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : e.GetInt32();

    [Fact]
    public void SprintsMatchSwift()
    {
        var entries = Vectors.Root.GetProperty("sprints").EnumerateArray().ToList();
        Assert.NotEmpty(entries);
        foreach (var e in entries)
        {
            var cfg = e.GetProperty("config").Deserialize<SprintConfig>()!;
            var cal = Cal(e.GetProperty("numbering"));
            var today = Date(e.GetProperty("date"));
            var info = Sprints.Info(today, cfg, cal, Store(e.GetProperty("regions")));
            var x = e.GetProperty("info");
            if (x.ValueKind == JsonValueKind.Null) Assert.Null(info);
            else
            {
                Assert.NotNull(info);
                Assert.Equal(x.GetProperty("number").GetInt32(), info.Number);
                Assert.Equal(Date(x.GetProperty("start")), info.Start);
                Assert.Equal(Date(x.GetProperty("end")), info.End);
                Assert.Equal(Ref(x.GetProperty("firstWeek")), info.FirstWeek);
                Assert.Equal(Ref(x.GetProperty("lastWeek")), info.LastWeek);
                Assert.Equal(x.GetProperty("workday").GetInt32(), info.Workday);
                Assert.Equal(x.GetProperty("workdaysTotal").GetInt32(), info.WorkdaysTotal);
                Assert.Equal(OptInt(x.GetProperty("pi")), info.Pi);
                Assert.Equal(OptInt(x.GetProperty("sprintInPI")), info.SprintInPI);
                Assert.Equal(x.GetProperty("sprintsPerPI").GetInt32(), info.SprintsPerPI);
            }
            foreach (var s in e.GetProperty("startsSprint").EnumerateArray())
                Assert.Equal(s[2].GetBoolean(), Sprints.StartsSprint(new(s[0].GetInt32(), s[1].GetInt32()), cfg, cal));
        }
    }

    [Fact]
    public void QuartersMatchSwift()
    {
        var entries = Vectors.Root.GetProperty("quarters").EnumerateArray().ToList();
        Assert.NotEmpty(entries);
        foreach (var e in entries)
        {
            var q = Quarters.Info(Date(e.GetProperty("date")), e.GetProperty("config").Deserialize<QuarterConfig>()!, Cal(e.GetProperty("numbering")));
            var x = e.GetProperty("info");
            if (x.ValueKind == JsonValueKind.Null) Assert.Null(q);
            else
            {
                Assert.Equal(x.GetProperty("quarter").GetInt32(), q!.Quarter);
                Assert.Equal(x.GetProperty("label").GetString(), q.Label);
            }
        }
    }

    [Fact]
    public void MilestoneStatusMatchSwift()
    {
        var entries = Vectors.Root.GetProperty("milestones").EnumerateArray().ToList();
        Assert.NotEmpty(entries);
        foreach (var e in entries)
        {
            var m = new Milestone(Guid.NewGuid(), "M", Ref(e.GetProperty("week")), MilestoneColor.Orange);
            var s = Milestones.Status(m, Date(e.GetProperty("now")), Cal(e.GetProperty("numbering")), Store(e.GetProperty("regions")));
            var x = e.GetProperty("status");
            Assert.NotNull(s);
            Assert.Equal(x.GetProperty("weeksAway").GetInt32(), s.WeeksAway);
            Assert.Equal(x.GetProperty("workdaysLeft").GetInt32(), s.WorkdaysLeft);
            Assert.Equal(s.WeeksAway < 0, s.IsPast);
        }
    }

    [Fact]
    public void SprintIndexIsNegativeBeforeAnchor()
    {
        var cfg = new SprintConfig { Enabled = true }; // anchor Mon 2026-01-05, 2-week sprints
        var cal = new WeekCalendar(WeekNumbering.Iso);
        Assert.Equal(-1, Sprints.Index(new(2026, 1, 4), cfg, cal));
        Assert.Equal(-2, Sprints.Index(new(2025, 12, 21), cfg, cal));
        Assert.Equal(0, Sprints.Index(new(2026, 1, 5), cfg, cal));
    }

    [Fact]
    public void InvalidSprintAnchorReturnsNothing()
    {
        var cal = new WeekCalendar(WeekNumbering.Iso);
        foreach (var anchor in new[] { new Day(2026, 13, 40), new Day(0, 1, 1), new Day(2026, 2, 30) })
        {
            var cfg = new SprintConfig { Enabled = true, Anchor = anchor };
            Assert.Null(Sprints.Index(new(2026, 3, 1), cfg, cal));
            Assert.Null(Sprints.Info(new(2026, 3, 1), cfg, cal, HolidayStore.Empty));
            Assert.False(Sprints.StartsSprint(new(2026, 10), cfg, cal));
        }
    }

    [Fact]
    public void ImpossibleSprintLengthReturnsNothing()
    {
        var cal = new WeekCalendar(WeekNumbering.Iso);
        foreach (var length in new[] { 53, int.MaxValue / 7, int.MaxValue })
        {
            var cfg = new SprintConfig { Enabled = true, LengthWeeks = length };
            Assert.Null(Sprints.Index(new(2026, 3, 1), cfg, cal));
            Assert.Null(Sprints.Info(new(2026, 3, 1), cfg, cal, HolidayStore.Empty));
            Assert.False(Sprints.StartsSprint(new(2026, 10), cfg, cal));
        }
        var year = new SprintConfig { Enabled = true, LengthWeeks = 52 };
        Assert.NotNull(Sprints.Info(new(9998, 12, 31), year, cal, HolidayStore.Empty)); // the last browsable week stays in range
    }

    [Fact]
    public void ImpossibleMilestoneWeekReturnsNull()
    {
        foreach (var n in new[] { WeekNumbering.Iso, WeekNumbering.Us })
        {
            var cal = new WeekCalendar(n);
            foreach (var w in new WeekRef[] { new(2026, 99), new(2026, 0), new(0, 5), new(2026, 54) })
            {
                var m = new Milestone(Guid.NewGuid(), "x", w, MilestoneColor.Red);
                Assert.Null(Milestones.Status(m, new(2026, 10, 25), cal, HolidayStore.Empty));
                Milestones.Upcoming([m], new(2026, 10, 25), cal);
            }
        }
    }

    [Fact]
    public void UpcomingFiltersAndSorts()
    {
        var cal = new WeekCalendar(WeekNumbering.Iso);
        var a = new Milestone(Guid.NewGuid(), "a", new(2026, 50), MilestoneColor.Red);
        var b = new Milestone(Guid.NewGuid(), "b", new(2026, 44), MilestoneColor.Red);
        var c = new Milestone(Guid.NewGuid(), "c", new(2026, 43), MilestoneColor.Red); // 2026-10-25 is W43
        var d = new Milestone(Guid.NewGuid(), "d", new(2026, 42), MilestoneColor.Red);
        Assert.Equal(["c", "b", "a"], Milestones.Upcoming([a, b, c, d], new(2026, 10, 25), cal).Select(m => m.Name));
    }

    [Fact]
    public void MacJsonShapesRoundTrip()
    {
        const string ms = """[{"id":"00000000-0000-4000-8000-00000000000A","name":"Code freeze","week":{"year":2026,"week":46},"color":"purple"}]""";
        var list = JsonSerializer.Deserialize<List<Milestone>>(ms)!;
        Assert.Equal(MilestoneColor.Purple, list[0].Color);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(ms), JsonNode.Parse(JsonSerializer.Serialize(list))));

        const string sp = """{"enabled":true,"lengthWeeks":3,"anchor":{"y":2025,"m":12,"d":31},"firstNumber":20,"name":"Iteration","sprintsPerPI":4,"firstPI":3}""";
        var cfg = JsonSerializer.Deserialize<SprintConfig>(sp)!;
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(sp), JsonNode.Parse(JsonSerializer.Serialize(cfg))));
        Assert.Equal(new SprintConfig { Enabled = true }, JsonSerializer.Deserialize<SprintConfig>("""{"enabled":true}"""));

        const string q = """{"enabled":true,"fiscalStartMonth":4,"mode":"weeks"}""";
        var qc = JsonSerializer.Deserialize<QuarterConfig>(q)!;
        Assert.Equal(QuarterMode.Weeks, qc.Mode);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(q), JsonNode.Parse(JsonSerializer.Serialize(qc))));
        Assert.Equal(new QuarterConfig(), JsonSerializer.Deserialize<QuarterConfig>("{}"));
    }
}
