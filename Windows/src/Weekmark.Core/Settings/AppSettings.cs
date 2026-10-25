using System.Text.Json;
using System.Text.Json.Serialization;

namespace Weekmark.Core;

[JsonConverter(typeof(JsonStringEnumConverter<WidgetSize>))]
public enum WidgetSize
{
    [JsonStringEnumMemberName("small")] Small, [JsonStringEnumMemberName("medium")] Medium,
    [JsonStringEnumMemberName("large")] Large, [JsonStringEnumMemberName("xlarge")] Xlarge,
}

[JsonConverter(typeof(JsonStringEnumConverter<WindowLayer>))]
public enum WindowLayer { [JsonStringEnumMemberName("desktop")] Desktop, [JsonStringEnumMemberName("floating")] Floating }

[JsonConverter(typeof(JsonStringEnumConverter<BackgroundStyle>))]
public enum BackgroundStyle
{
    [JsonStringEnumMemberName("glass")] Glass, [JsonStringEnumMemberName("solid")] Solid,
    [JsonStringEnumMemberName("clear")] Clear,
}

[JsonConverter(typeof(JsonStringEnumConverter<AppearanceMode>))]
public enum AppearanceMode
{
    [JsonStringEnumMemberName("system")] System, [JsonStringEnumMemberName("light")] Light,
    [JsonStringEnumMemberName("dark")] Dark,
}

public static class WidgetSizeExtensions
{
    public static double Scale(this WidgetSize s) => s switch
    {
        WidgetSize.Small => 0.8,
        WidgetSize.Large => 1.3,
        WidgetSize.Xlarge => 1.7,
        _ => 1.0,
    };
}

/// <summary>Same keys and inner shapes as the Mac; values are native JSON here. Defaults apply to missing keys.</summary>
public sealed class AppSettings
{
    [JsonPropertyName("numbering")] public WeekNumbering Numbering { get; set; } = WeekNumbering.Iso;
    [JsonPropertyName("size")] public WidgetSize Size { get; set; } = WidgetSize.Medium;
    [JsonPropertyName("layer")] public WindowLayer Layer { get; set; } = WindowLayer.Desktop;
    [JsonPropertyName("background")] public BackgroundStyle Background { get; set; } = BackgroundStyle.Glass;
    [JsonPropertyName("appearance")] public AppearanceMode Appearance { get; set; } = AppearanceMode.System;
    [JsonPropertyName("showMonth")] public bool ShowMonth { get; set; } = true;
    [JsonPropertyName("showProgress")] public bool ShowProgress { get; set; } = true;
    [JsonPropertyName("showPlanning")] public bool ShowPlanning { get; set; } = true;
    [JsonPropertyName("showMilestones")] public bool ShowMilestones { get; set; } = true;
    [JsonPropertyName("showInMenuBar")] public bool ShowInMenuBar { get; set; } = true;
    [JsonPropertyName("locked")] public bool Locked { get; set; }
    [JsonPropertyName("onboardingDone")] public bool OnboardingDone { get; set; }
    [JsonPropertyName("startWithWindows")] public bool StartWithWindows { get; set; } = true;
    [JsonPropertyName("sprint")] public SprintConfig Sprint { get; set; } = new();
    [JsonPropertyName("quarter")] public QuarterConfig Quarter { get; set; } = new();
    [JsonPropertyName("milestones")] public List<Milestone> Milestones { get; set; } = [];
    [JsonPropertyName("regions")] public List<HolidayRegion> Regions { get; set; } = [];
    [JsonPropertyName("customHolidays")] public List<Holiday> CustomHolidays { get; set; } = [];
    [JsonPropertyName("screenID")] public string? ScreenID { get; set; }
    [JsonPropertyName("offsetX")] public double? OffsetX { get; set; }
    [JsonPropertyName("offsetTop")] public double? OffsetTop { get; set; }

    /// <summary>Keys this version does not know; written back unchanged on save.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement> Unknown { get; set; } = [];

    public HolidayStore BuildHolidays() => new(Regions, CustomHolidays);
    public WeekCalendar BuildCalendar() => new(Numbering);
}
