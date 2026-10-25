using System.Globalization;
using System.Text.Json;

namespace Weekmark.Core.Tests;

public class QueryEngineTests
{
    static WeekContext Ctx(JsonElement e)
    {
        var cal = new WeekCalendar(e.GetProperty("numbering").GetString() == "us" ? WeekNumbering.Us : WeekNumbering.Iso);
        var fmt = new WeekFormatter(cal, WeekFormatter.DayFirstFor(CultureInfo.GetCultureInfo(e.GetProperty("locale").GetString()!)));
        var now = DateTime.ParseExact(e.GetProperty("now").GetString()!, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
        var store = new HolidayStore(e.GetProperty("regions").EnumerateArray().Select(x => HolidayRegionExtensions.Guess(x.GetString()!)!.Value).ToList(), []);
        return new(cal, fmt, now, store, e.GetProperty("milestones").Deserialize<List<Milestone>>()!, e.GetProperty("sprint").Deserialize<SprintConfig>()!);
    }

    static string? Str(JsonElement e, string k) => e.GetProperty(k).ValueKind == JsonValueKind.Null ? null : e.GetProperty(k).GetString();
    static int? Int(JsonElement e, string k) => e.GetProperty(k).ValueKind == JsonValueKind.Null ? null : e.GetProperty(k).GetInt32();

    [Fact]
    public void QueriesMatchSwift_All()
    {
        var all = Vectors.Root.GetProperty("queries").EnumerateArray().ToList();
        var vectors = all;
        Assert.NotEmpty(vectors);
        foreach (var v in vectors)
        {
            var q = v.GetProperty("query").GetString()!;
            var got = QueryEngine.Evaluate(q, Ctx(v));
            var want = v.GetProperty("results").EnumerateArray().ToList();
            var label = $"{v.GetProperty("locale").GetString()} '{q}'";
            Assert.True(want.Count == got.Count, $"{label}: {want.Count} results expected, got {got.Count}");
            for (var i = 0; i < want.Count; i++)
            {
                var w = want[i];
                Assert.True(w.GetProperty("kind").GetString() == got[i].Kind.ToString().ToLowerInvariant(), $"{label}[{i}] kind");
                Assert.True(Str(w, "title") == got[i].Title, $"{label}[{i}] title: {got[i].Title}");
                Assert.True(Str(w, "subtitle") == got[i].Subtitle, $"{label}[{i}] subtitle: {got[i].Subtitle}");
                Assert.True(Str(w, "detail") == got[i].Detail, $"{label}[{i}] detail: {got[i].Detail}");
                Assert.True(Str(w, "copyText") == got[i].CopyText, $"{label}[{i}] copyText: {got[i].CopyText}");
                var wk = w.GetProperty("week");
                WeekRef? wantWeek = wk.ValueKind == JsonValueKind.Null ? null : new(wk.GetProperty("year").GetInt32(), wk.GetProperty("week").GetInt32());
                Assert.True(wantWeek == got[i].Week, $"{label}[{i}] week");
                Assert.True(Int(w, "weeks") == got[i].Weeks, $"{label}[{i}] weeks");
                Assert.True(Int(w, "workdays") == got[i].Workdays, $"{label}[{i}] workdays");
            }
        }
        Assert.True(vectors.Count == 268, $"only {vectors.Count} of {all.Count} vectors covered");
    }

    [Fact]
    public void JunkNeverThrows()
    {
        var first = Vectors.Root.GetProperty("queries")[0];
        var bad = new Milestone(Guid.NewGuid(), "broken", new(2026, 99));
        var ctxs = new List<WeekContext>();
        foreach (var (num, now) in new[] { ("iso", "2026-06-10T09:00"), ("us", "2026-12-28T09:00"), ("iso", "2027-01-01T00:00") })
        {
            var c = Ctx(first);
            var cal = new WeekCalendar(num == "us" ? WeekNumbering.Us : WeekNumbering.Iso);
            ctxs.Add(c with { Cal = cal, Fmt = new WeekFormatter(cal, true), Now = DateTime.ParseExact(now, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture),
                Holidays = new HolidayStore([HolidayRegion.Gb], []), Milestones = [new(Guid.NewGuid(), "Launch", new(2026, 40)), bad] });
        }
        string[] atoms = ["0", "1", "5", "46", "53", "99", "2027", "9999", "99999999999999999999", "cw", "kw", "w", "week", "+", "-", "..", "–", "—", "→", "->",
            " to ", "until ", "in ", " ago", "d", "weeks", " ", "/", "launch", "🙂", "é", "٣", "²", "today", "next week", "dec", "12"];
        var rng = new Random(12345);
        var inputs = new List<string> { "🙂🙂", new string('9', 200), "CW99999", "-", "to", "46-", "+520w", "-520w", "+0", "cw0", "0-0", "2-9998", "1 9999", "9998 w53", "9998 w52", "until 9998 w52", "2000 w1-9998 w52" };
        for (var i = 0; i < 2000; i++)
            inputs.Add(string.Concat(Enumerable.Range(0, rng.Next(1, 7)).Select(_ => atoms[rng.Next(atoms.Length)])));
        foreach (var s in inputs)
            foreach (var ctx in ctxs)
                Assert.NotEmpty(QueryEngine.Evaluate(s, ctx));
    }

    [Fact]
    public void ThrowingDetectorAndImpossibleMilestone()
    {
        var c = Ctx(Vectors.Root.GetProperty("queries")[0]);
        var ctx = c with { DateDetector = (_, _) => throw new InvalidOperationException(), Milestones = [new(Guid.NewGuid(), "ghostly", new(2026, 99))] };
        Assert.Equal(QueryKind.Error, Assert.Single(QueryEngine.Evaluate("24 Dec 2026", ctx)).Kind);
        Assert.Equal(QueryKind.Error, Assert.Single(QueryEngine.Evaluate("ghostly", ctx)).Kind);
        Assert.NotEmpty(QueryEngine.Evaluate("until 24 Dec 2026", ctx));
    }
}
