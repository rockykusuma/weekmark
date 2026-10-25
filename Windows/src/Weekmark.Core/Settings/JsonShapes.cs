using System.Text.Json;
using System.Text.Json.Serialization;

namespace Weekmark.Core;

[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(WeekNumbering))]
[JsonSerializable(typeof(WidgetSize))]
[JsonSerializable(typeof(WindowLayer))]
[JsonSerializable(typeof(BackgroundStyle))]
[JsonSerializable(typeof(AppearanceMode))]
[JsonSerializable(typeof(SprintConfig))]
[JsonSerializable(typeof(QuarterConfig))]
[JsonSerializable(typeof(List<Milestone>))]
[JsonSerializable(typeof(List<HolidayRegion>))]
[JsonSerializable(typeof(List<Holiday>))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;

public static class JsonShapes
{
    /// <summary>The Mac-compatible serializer options (camelCase keys, lowercase enums, native JSON values).</summary>
    public static JsonSerializerOptions Options => SettingsJsonContext.Default.Options;
}
