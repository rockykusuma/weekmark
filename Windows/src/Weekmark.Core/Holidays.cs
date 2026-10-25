using System.Text.Json;
using System.Text.Json.Serialization;

namespace Weekmark.Core;

[JsonConverter(typeof(HolidayRegionConverter))]
public enum HolidayRegion { Us, Gb, De, Dk, Nl, Fr, Se, No, Pl, In }

/// <summary>JSON lowercase raw values ("us", "gb", ..., "in"), as on the Mac.</summary>
public sealed class HolidayRegionConverter : JsonConverter<HolidayRegion>
{
    public override HolidayRegion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        HolidayRegionExtensions.Guess(reader.GetString() ?? "") ?? throw new JsonException("Unknown holiday region.");

    public override void Write(Utf8JsonWriter writer, HolidayRegion value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Code());
}

public static class HolidayRegionExtensions
{
    public static string Code(this HolidayRegion r) => r.ToString().ToLowerInvariant();

    public static string Title(this HolidayRegion r) => r switch
    {
        HolidayRegion.Us => "United States",
        HolidayRegion.Gb => "United Kingdom (England & Wales)",
        HolidayRegion.De => "Germany (national)",
        HolidayRegion.Dk => "Denmark",
        HolidayRegion.Nl => "Netherlands",
        HolidayRegion.Fr => "France",
        HolidayRegion.Se => "Sweden",
        HolidayRegion.No => "Norway",
        HolidayRegion.Pl => "Poland",
        _ => "India (national; import your company list for festivals)",
    };

    public static string Flag(this HolidayRegion r) => r switch
    {
        HolidayRegion.Us => "🇺🇸",
        HolidayRegion.Gb => "🇬🇧",
        HolidayRegion.De => "🇩🇪",
        HolidayRegion.Dk => "🇩🇰",
        HolidayRegion.Nl => "🇳🇱",
        HolidayRegion.Fr => "🇫🇷",
        HolidayRegion.Se => "🇸🇪",
        HolidayRegion.No => "🇳🇴",
        HolidayRegion.Pl => "🇵🇱",
        _ => "🇮🇳",
    };

    /// <summary>Region from a two-letter country code ("UK" reads as Gb); null when unsupported.</summary>
    public static HolidayRegion? Guess(string twoLetterRegion)
    {
        var s = twoLetterRegion.ToLowerInvariant();
        if (s == "uk") return HolidayRegion.Gb;
        foreach (var r in Enum.GetValues<HolidayRegion>())
            if (r.Code() == s) return r;
        return null;
    }
}

/// <summary>source is a region code, "custom" or an imported calendar name.</summary>
public sealed record Holiday(
    [property: JsonPropertyName("day")] Day Day,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("source")] string Source)
{
    /// <summary>Keys this version does not know; written back unchanged on save (and kept by <c>with</c> copies).</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public static class HolidayRules
{
    /// <summary>Gregorian Easter Sunday (anonymous Gregorian algorithm).</summary>
    public static Day Easter(int y)
    {
        int a = y % 19, b = y / 100, c = y % 100, d = b / 4, e = b % 4;
        int f = (b + 8) / 25, g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4, k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451;
        return new Day(y, (h + l - 7 * m + 114) / 31, ((h + l - 7 * m + 114) % 31) + 1);
    }

    static Day Add(Day d, int n) => Day.From(d.ToDate().AddDays(n));

    // 1 = Sunday ... 7 = Saturday, as on the Mac.
    static int Wd(Day d) => (int)d.ToDate().DayOfWeek + 1;

    /// <summary>n-th weekday (1=Sun..7=Sat) of a month; n = -1 for the last.</summary>
    static Day Nth(int y, int m, int weekday, int n)
    {
        if (n > 0)
        {
            var first = new Day(y, m, 1);
            return Add(first, (weekday - Wd(first) + 7) % 7 + (n - 1) * 7);
        }
        var next = m == 12 ? new Day(y + 1, 1, 1) : new Day(y, m + 1, 1);
        var last = Add(next, -1);
        return Add(last, -((Wd(last) - weekday + 7) % 7));
    }

    /// <summary>US federal "observed": Saturday to Friday, Sunday to Monday.</summary>
    static Day UsObserved(Day d) => Wd(d) switch { 7 => Add(d, -1), 1 => Add(d, 1), _ => d };

    /// <summary>UK substitute days: weekend (or taken) moves to the next free weekday.</summary>
    static Day UkSubstitute(Day d, Day? taken = null)
    {
        var r = d;
        while (Wd(r) == 1 || Wd(r) == 7 || r == taken) r = Add(r, 1);
        return r;
    }

    public static IReadOnlyList<Holiday> For(HolidayRegion region, int y)
    {
        var e = Easter(y);
        var list = new List<(Day, string)>();
        void A(Day d, string n) => list.Add((d, n));
        Day D(int m, int d) => new(y, m, d);

        switch (region)
        {
            case HolidayRegion.Us:
                A(UsObserved(D(1, 1)), "New Year's Day");
                A(Nth(y, 1, 2, 3), "Martin Luther King Jr. Day");
                A(Nth(y, 2, 2, 3), "Presidents' Day");
                A(Nth(y, 5, 2, -1), "Memorial Day");
                A(UsObserved(D(6, 19)), "Juneteenth");
                A(UsObserved(D(7, 4)), "Independence Day");
                A(Nth(y, 9, 2, 1), "Labor Day");
                A(Nth(y, 10, 2, 2), "Columbus Day");
                A(UsObserved(D(11, 11)), "Veterans Day");
                A(Nth(y, 11, 5, 4), "Thanksgiving");
                A(UsObserved(D(12, 25)), "Christmas Day");
                break;
            case HolidayRegion.Gb:
                A(UkSubstitute(D(1, 1)), "New Year's Day");
                A(Add(e, -2), "Good Friday");
                A(Add(e, 1), "Easter Monday");
                A(Nth(y, 5, 2, 1), "Early May Bank Holiday");
                A(Nth(y, 5, 2, -1), "Spring Bank Holiday");
                A(Nth(y, 8, 2, -1), "Summer Bank Holiday");
                var xmas = UkSubstitute(D(12, 25));
                A(xmas, "Christmas Day");
                A(UkSubstitute(D(12, 26), xmas), "Boxing Day");
                break;
            case HolidayRegion.De:
                A(D(1, 1), "Neujahr"); A(Add(e, -2), "Karfreitag"); A(Add(e, 1), "Ostermontag");
                A(D(5, 1), "Tag der Arbeit"); A(Add(e, 39), "Christi Himmelfahrt"); A(Add(e, 50), "Pfingstmontag");
                A(D(10, 3), "Tag der Deutschen Einheit"); A(D(12, 25), "1. Weihnachtstag"); A(D(12, 26), "2. Weihnachtstag");
                break;
            case HolidayRegion.Dk:
                A(D(1, 1), "Nytårsdag"); A(Add(e, -3), "Skærtorsdag"); A(Add(e, -2), "Langfredag"); A(Add(e, 1), "2. påskedag");
                if (y < 2024) A(Add(e, 26), "Store bededag");
                A(Add(e, 39), "Kristi himmelfartsdag"); A(Add(e, 50), "2. pinsedag");
                A(D(6, 5), "Grundlovsdag"); A(D(12, 24), "Juleaftensdag");
                A(D(12, 25), "1. juledag"); A(D(12, 26), "2. juledag");
                break;
            case HolidayRegion.Nl:
                A(D(1, 1), "Nieuwjaarsdag"); A(Add(e, 1), "Tweede Paasdag");
                var kd = D(4, 27); A(Wd(kd) == 1 ? D(4, 26) : kd, "Koningsdag");
                if (y % 5 == 0) A(D(5, 5), "Bevrijdingsdag");
                A(Add(e, 39), "Hemelvaartsdag"); A(Add(e, 50), "Tweede Pinksterdag");
                A(D(12, 25), "Eerste Kerstdag"); A(D(12, 26), "Tweede Kerstdag");
                break;
            case HolidayRegion.Fr:
                A(D(1, 1), "Jour de l'an"); A(Add(e, 1), "Lundi de Pâques"); A(D(5, 1), "Fête du Travail");
                A(D(5, 8), "Victoire 1945"); A(Add(e, 39), "Ascension"); A(Add(e, 50), "Lundi de Pentecôte");
                A(D(7, 14), "Fête nationale"); A(D(8, 15), "Assomption"); A(D(11, 1), "Toussaint");
                A(D(11, 11), "Armistice"); A(D(12, 25), "Noël");
                break;
            case HolidayRegion.Se:
                A(D(1, 1), "Nyårsdagen"); A(D(1, 6), "Trettondedag jul"); A(Add(e, -2), "Långfredagen");
                A(Add(e, 1), "Annandag påsk"); A(D(5, 1), "Första maj"); A(Add(e, 39), "Kristi himmelsfärdsdag");
                A(D(6, 6), "Nationaldagen");
                // Midsummer Eve: Friday between 19-25 June (de facto day off)
                var jun19 = D(6, 19);
                A(Add(jun19, (6 - Wd(jun19) + 7) % 7), "Midsommarafton");
                A(D(12, 24), "Julafton"); A(D(12, 25), "Juldagen"); A(D(12, 26), "Annandag jul");
                A(D(12, 31), "Nyårsafton");
                break;
            case HolidayRegion.No:
                A(D(1, 1), "Første nyttårsdag"); A(Add(e, -3), "Skjærtorsdag"); A(Add(e, -2), "Langfredag");
                A(Add(e, 1), "Andre påskedag"); A(D(5, 1), "Arbeidernes dag"); A(D(5, 17), "Grunnlovsdag");
                A(Add(e, 39), "Kristi himmelfartsdag"); A(Add(e, 50), "Andre pinsedag");
                A(D(12, 25), "Første juledag"); A(D(12, 26), "Andre juledag");
                break;
            case HolidayRegion.Pl:
                A(D(1, 1), "Nowy Rok"); A(D(1, 6), "Trzech Króli"); A(Add(e, 1), "Poniedziałek Wielkanocny");
                A(D(5, 1), "Święto Pracy"); A(D(5, 3), "Święto Konstytucji"); A(Add(e, 60), "Boże Ciało");
                A(D(8, 15), "Wniebowzięcie NMP"); A(D(11, 1), "Wszystkich Świętych");
                A(D(11, 11), "Święto Niepodległości");
                if (y >= 2025) A(D(12, 24), "Wigilia");
                A(D(12, 25), "Boże Narodzenie"); A(D(12, 26), "Drugi dzień Bożego Narodzenia");
                break;
            case HolidayRegion.In:
                A(D(1, 26), "Republic Day"); A(D(8, 15), "Independence Day"); A(D(10, 2), "Gandhi Jayanti");
                break;
        }
        var code = region.Code();
        return list.Select(x => new Holiday(x.Item1, x.Item2, code)).ToList();
    }
}

/// <summary>Holidays for a set of regions plus custom/imported days, with per-year caching.</summary>
public sealed class HolidayStore
{
    readonly HolidayRegion[] _regions;
    readonly IReadOnlyList<Holiday> _custom;
    readonly Dictionary<int, Dictionary<Day, List<Holiday>>> _cache = [];

    public HolidayStore(IReadOnlyCollection<HolidayRegion> regions, IReadOnlyList<Holiday> custom)
    {
        _regions = regions.Distinct().OrderBy(r => r.Code(), StringComparer.Ordinal).ToArray();
        _custom = custom;
    }

    public static HolidayStore Empty { get; } = new([], []);

    Dictionary<Day, List<Holiday>> Table(int y)
    {
        if (_cache.TryGetValue(y, out var cached)) return cached;
        var t = new Dictionary<Day, List<Holiday>>();
        void Put(Holiday h)
        {
            if (!t.TryGetValue(h.Day, out var l)) t[h.Day] = l = [];
            l.Add(h);
        }
        foreach (var r in _regions)
            foreach (var h in HolidayRules.For(r, y)) Put(h);
        foreach (var h in _custom.Where(h => h.Day.Y == y)) Put(h);
        return _cache[y] = t;
    }

    public IReadOnlyList<Holiday> On(Day d) => Table(d.Y).TryGetValue(d, out var l) ? l : [];

    public bool IsHoliday(Day d) => On(d).Count > 0;

    public IReadOnlyList<Holiday> Upcoming(DateOnly from, int limit = 5)
    {
        var start = Day.From(from);
        var o = new List<Holiday>();
        for (var y = start.Y; y <= start.Y + 1; y++)
        {
            var t = Table(y);
            foreach (var d in t.Keys.Order().Where(d => d.CompareTo(start) >= 0))
            {
                foreach (var h in t[d])
                    if (!o.Any(x => x.Day == h.Day && x.Name == h.Name)) o.Add(h);
                if (o.Count >= limit) return o.Take(limit).ToList();
            }
        }
        return o;
    }

    /// <summary>Working days in [start, endExclusive): Monday-Friday minus holidays.</summary>
    public int WorkingDays(DateOnly start, DateOnly endExclusive)
    {
        var n = 0;
        for (var d = start; d < endExclusive; d = d.AddDays(1))
            if (WeekCalendar.IsWorkday(d) && !IsHoliday(Day.From(d))) n++;
        return n;
    }

    public int WorkingDays(WeekRef r, WeekCalendar cal)
    {
        var s = cal.StartOfWeek(r);
        return WorkingDays(s, s.AddDays(7));
    }

    public IReadOnlyList<Holiday> In(WeekRef r, WeekCalendar cal)
    {
        var seen = new HashSet<(Day, string)>();
        return cal.DaysOf(r).SelectMany(d => On(Day.From(d))).Where(h => seen.Add((h.Day, h.Name))).ToList();
    }
}
