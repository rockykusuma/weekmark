using System.Text.Json;
using Weekmark.Core;

namespace Weekmark.Core.Tests;

public sealed class HotkeyPresetTests : IDisposable
{
    readonly string dir = Directory.CreateTempSubdirectory("weekmark-hotkey").FullName;
    string P => Path.Combine(dir, "settings.json");

    public void Dispose() => Directory.Delete(dir, true);

    string SavedHotkey() => JsonDocument.Parse(File.ReadAllText(P)).RootElement.GetProperty("hotkey").GetString()!;

    [Fact]
    public void HotkeyTitles() =>
        Assert.Equal(["Ctrl+Alt+W", "Alt+Win+W", "Ctrl+Alt+Win+W", "Ctrl+Alt+Space", "Off"],
            Enum.GetValues<HotkeyPreset>().Select(p => p.Title()));

    [Fact]
    public void HotkeyWin32Codes()
    {
        Assert.Equal(((uint)0x3, (uint)0x57), HotkeyPreset.CtrlOptW.Win32());
        Assert.Equal(((uint)0x9, (uint)0x57), HotkeyPreset.OptCmdW.Win32());
        Assert.Equal(((uint)0xB, (uint)0x57), HotkeyPreset.CtrlOptCmdW.Win32());
        Assert.Equal(((uint)0x3, (uint)0x20), HotkeyPreset.CtrlOptSpace.Win32());
        Assert.Null(HotkeyPreset.Off.Win32());
    }

    [Fact]
    public void HotkeyLoadsAndSaves()
    {
        var store = new SettingsStore(P);
        Assert.Equal(HotkeyPreset.CtrlOptW, store.Load().Hotkey);
        File.WriteAllText(P, """{"hotkey":"optCmdW"}""");
        var s = store.Load();
        Assert.Equal(HotkeyPreset.OptCmdW, s.Hotkey);
        s.Hotkey = HotkeyPreset.CtrlOptSpace;
        Assert.True(store.Save(s));
        Assert.Equal("ctrlOptSpace", SavedHotkey());
    }

    [Fact]
    public void UnknownHotkeyKeepsStoredValue()
    {
        File.WriteAllText(P, """{"hotkey":"cmdShiftK"}""");
        var store = new SettingsStore(P);
        var s = store.Load();
        Assert.Equal(HotkeyPreset.CtrlOptW, s.Hotkey);
        Assert.True(store.Save(s));
        Assert.Equal("cmdShiftK", SavedHotkey());
    }
}
