using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Weekmark.Core;

[JsonConverter(typeof(WeekNumberingConverter))]
public enum WeekNumbering { Iso, Us }

/// <summary>JSON "iso"/"us"; the Mac's legacy "system" reads as Iso.</summary>
public sealed class WeekNumberingConverter : JsonConverter<WeekNumbering>
{
    public override WeekNumbering Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() switch
        {
            "iso" or "system" => WeekNumbering.Iso,
            "us" => WeekNumbering.Us,
            var s => throw new JsonException($"Unknown week numbering '{s}'."),
        };

    public override void Write(Utf8JsonWriter writer, WeekNumbering value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value == WeekNumbering.Us ? "us" : "iso");
}

/// <summary>A week identified by its week-numbering year and week number (e.g. 2026-W46).</summary>
public readonly record struct WeekRef(
    [property: JsonPropertyName("year")] int Year,
    [property: JsonPropertyName("week")] int Week) : IComparable<WeekRef>
{
    public int CompareTo(WeekRef o) => (Year, Week).CompareTo((o.Year, o.Week));
}

/// <summary>A calendar-independent Gregorian civil date.</summary>
public readonly record struct Day(
    [property: JsonPropertyName("y")] int Y,
    [property: JsonPropertyName("m")] int M,
    [property: JsonPropertyName("d")] int D) : IComparable<Day>
{
    public DateOnly ToDate() => new(Y, M, D);
    public static Day From(DateOnly d) => new(d.Year, d.Month, d.Day);
    public int CompareTo(Day o) => (Y, M, D).CompareTo((o.Y, o.M, o.D));
}

public sealed class WeekCalendar(WeekNumbering numbering)
{
    public WeekNumbering Numbering { get; } = numbering;

    bool IsIso => Numbering == WeekNumbering.Iso;

    public DayOfWeek FirstDay => IsIso ? DayOfWeek.Monday : DayOfWeek.Sunday;

    public WeekRef WeekOf(DateOnly d)
    {
        var dt = d.ToDateTime(TimeOnly.MinValue);
        if (IsIso) return new(ISOWeek.GetYear(dt), ISOWeek.GetWeekOfYear(dt));
        // US weeks are numbered from their Saturday: the week holding 1 Jan is week 1.
        var sat = d.AddDays(6 - (int)d.DayOfWeek);
        var jan1 = (int)new DateOnly(sat.Year, 1, 1).DayOfWeek;
        return new(sat.Year, (sat.DayOfYear - 1 + jan1) / 7 + 1);
    }

    public DateOnly StartOfWeek(WeekRef r)
    {
        if (IsIso) return DateOnly.FromDateTime(ISOWeek.ToDateTime(r.Year, r.Week, DayOfWeek.Monday));
        return FirstWeekStart(r.Year).AddDays(7 * (r.Week - 1));
    }

    public DateOnly StartOfWeek(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek - (int)FirstDay + 7) % 7));

    public int WeeksIn(int weekYear) =>
        IsIso ? ISOWeek.GetWeeksInYear(weekYear)
              : (FirstWeekStart(weekYear + 1).DayNumber - FirstWeekStart(weekYear).DayNumber) / 7;

    public WeekRef AddWeeks(WeekRef r, int n) => WeekOf(StartOfWeek(r).AddDays(7 * n));

    /// <summary>Whole weeks from a to b (b - a).</summary>
    public int WeeksBetween(WeekRef a, WeekRef b) => (StartOfWeek(b).DayNumber - StartOfWeek(a).DayNumber) / 7;

    public IReadOnlyList<DateOnly> DaysOf(WeekRef r)
    {
        var s = StartOfWeek(r);
        return Enumerable.Range(0, 7).Select(s.AddDays).ToList();
    }

    public static bool IsWorkday(DateOnly d) => d.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday;

    // Sunday on or before 1 Jan.
    static DateOnly FirstWeekStart(int year)
    {
        var jan1 = new DateOnly(year, 1, 1);
        return jan1.AddDays(-(int)jan1.DayOfWeek);
    }
}
