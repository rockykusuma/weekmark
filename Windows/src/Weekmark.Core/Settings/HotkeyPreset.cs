using System.Text.Json.Serialization;

namespace Weekmark.Core;

[JsonConverter(typeof(JsonStringEnumConverter<HotkeyPreset>))]
public enum HotkeyPreset
{
    [JsonStringEnumMemberName("ctrlOptW")] CtrlOptW, [JsonStringEnumMemberName("optCmdW")] OptCmdW,
    [JsonStringEnumMemberName("ctrlOptCmdW")] CtrlOptCmdW, [JsonStringEnumMemberName("ctrlOptSpace")] CtrlOptSpace,
    [JsonStringEnumMemberName("off")] Off,
}

public static class HotkeyPresetExtensions
{
    public static string Title(this HotkeyPreset p) => p switch
    {
        HotkeyPreset.OptCmdW => "Alt+Win+W",
        HotkeyPreset.CtrlOptCmdW => "Ctrl+Alt+Win+W",
        HotkeyPreset.CtrlOptSpace => "Ctrl+Alt+Space",
        HotkeyPreset.Off => "Off",
        _ => "Ctrl+Alt+W",
    };

    /// <summary>RegisterHotKey arguments (MOD_ALT 0x1, MOD_CONTROL 0x2, MOD_WIN 0x8; VK W 0x57, VK SPACE 0x20); null when Off. Excludes MOD_NOREPEAT.</summary>
    public static (uint Modifiers, uint Key)? Win32(this HotkeyPreset p) => p switch
    {
        HotkeyPreset.OptCmdW => (0x9, 0x57),
        HotkeyPreset.CtrlOptCmdW => (0xB, 0x57),
        HotkeyPreset.CtrlOptSpace => (0x3, 0x20),
        HotkeyPreset.Off => null,
        _ => (0x3, 0x57),
    };
}
