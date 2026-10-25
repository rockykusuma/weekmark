using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Weekmark.App.Platform;
using Weekmark.App.Tray;
using Weekmark.App.Widget;
using Weekmark.Core;

namespace Weekmark.App.Tests;

public sealed class TrayTests
{
    static readonly DateTime Now = new(2026, 10, 25, 12, 0, 0);
    static readonly int[] TraySizes = [16, 20, 24, 32, 40];

    static byte[] Pixels(Bitmap b)
    {
        var (w, h) = (b.PixelSize.Width, b.PixelSize.Height);
        var bytes = new byte[w * h * 4];
        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try { b.CopyPixels(new PixelRect(0, 0, w, h), handle.AddrOfPinnedObject(), bytes.Length, w * 4); }
        finally { handle.Free(); }
        return bytes;
    }

    static int Alpha(byte[] px, int size, int x, int y) => px[(y * size + x) * 4 + 3]; // alpha is last in RGBA and BGRA

    /// <summary>A shown widget and its tray on the demo data, formatted as en-GB (day first).</summary>
    static (AppState State, WidgetWindow Widget, TrayController Tray) Demo(Action<AppSettings>? edit = null)
    {
        CultureInfo.CurrentCulture = new CultureInfo("en-GB");
        var s = DemoData.Create(DateOnly.FromDateTime(Now));
        edit?.Invoke(s);
        var state = new AppState(s, null, () => Now);
        var widget = new WidgetWindow(state, new FallbackLayering());
        widget.Show();
        return (state, widget, new TrayController(state, widget));
    }

    /// <summary>Separators are items with the header "-".</summary>
    static List<NativeMenuItem> Items(NativeMenu m) => m.Items.OfType<NativeMenuItem>().ToList();
    static NativeMenuItem Item(NativeMenu m, string header) => Items(m).Single(i => i.Header == header);

    [AvaloniaFact]
    public void IconRendersAtTraySizes()
    {
        var dir = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "renders")).FullName;
        foreach (var size in TraySizes)
        {
            var dark = WeekIconRenderer.Render(43, size, lightTaskbar: false);
            var light = WeekIconRenderer.Render(43, size, lightTaskbar: true);
            dark.Save(Path.Combine(dir, $"tray-{size}-dark.png"));
            light.Save(Path.Combine(dir, $"tray-{size}-light.png"));
            Assert.Equal(new PixelSize(size, size), dark.PixelSize);
            var (d, l) = (Pixels(dark), Pixels(light));
            var ink = Enumerable.Range(0, size * size).Count(i => d[i * 4 + 3] > 0);
            Assert.True(ink > size, $"{size} px: only {ink} drawn pixels");
            Assert.NotEqual(d, l);
        }
    }

    [AvaloniaFact]
    public void TwoDigitAndOneDigitWeeksFit()
    {
        foreach (var week in new[] { 1, 53 })
            foreach (var size in TraySizes)
            {
                var px = Pixels(WeekIconRenderer.Render(week, size, lightTaskbar: false));
                var border = Enumerable.Range(0, size).SelectMany(i => new[] { (i, 0), (i, size - 1), (0, i), (size - 1, i) });
                Assert.All(border, p => Assert.Equal(0, Alpha(px, size, p.Item1, p.Item2)));
                // As large as fits: the digits reach (nearly) across the icon in one direction.
                var inked = Enumerable.Range(0, size * size).Where(i => px[i * 4 + 3] > 0).ToList();
                int Extent(Func<int, int> axis) => inked.Max(axis) - inked.Min(axis) + 1;
                Assert.True(Math.Max(Extent(i => i % size), Extent(i => i / size)) >= size - 4, $"week {week} at {size} px is too small");
            }
    }

    [AvaloniaFact]
    public void MenuLabels()
    {
        var (state, widget, tray) = Demo();
        var top = Items(tray.Menu);
        Assert.Equal("Week 43 of 53 · 19–25 Oct, 2026", top[0].Header);
        Assert.False(top[0].IsEnabled);
        Assert.Equal("Q4 2026 · Sprint 21, day 10 of 10", top[1].Header);
        Assert.False(top[1].IsEnabled);
        Assert.Equal("Copy “CW43 (19–25 Oct)”", top[2].Header);
        Assert.Equal(["-", "Hide Widget", "Position", "Size", "-", "Settings…", "Quit Weekmark"], top.Skip(3).Select(i => i.Header));
        Assert.Equal("CW 43 · 19–25 Oct 2026", tray.Icon.ToolTipText);

        var position = Item(tray.Menu, "Position").Menu!;
        Assert.Equal(["On Desktop (behind windows)", "Float Above Windows", "-", "Lock Position", "-",
            "Snap to Top Left", "Snap to Top Right", "Snap to Bottom Left", "Snap to Bottom Right"], Items(position).Select(i => i.Header));
        Assert.True(Item(position, "On Desktop (behind windows)").IsChecked);
        var size = Item(tray.Menu, "Size").Menu!;
        Assert.Equal(["Small", "Medium", "Large", "Extra Large"], Items(size).Select(i => i.Header));
        Assert.True(Item(size, "Medium").IsChecked);

        // Menu actions change the settings and the menu follows.
        Item(size, "Large").Command!.Execute(null);
        Assert.Equal(WidgetSize.Large, state.Settings.Size);
        Assert.True(Item(Item(tray.Menu, "Size").Menu!, "Large").IsChecked);
        Item(tray.Menu, "Hide Widget").Command!.Execute(null);
        Assert.False(widget.IsVisible);
        Item(tray.Menu, "Show Widget").Command!.Execute(null);
        Assert.True(widget.IsVisible);

        // showInMenuBar off: the app icon replaces the number.
        var numberIcon = tray.Icon.Icon;
        state.Settings.ShowInMenuBar = false;
        state.Save();
        Assert.NotSame(numberIcon, tray.Icon.Icon);
    }

    [AvaloniaFact]
    public void InvalidSprintSettingsOnlyDropTheSprintPart()
    {
        var (_, _, tray) = Demo(s => s.Sprint = s.Sprint with { LengthWeeks = int.MaxValue / 7 });
        Assert.Equal("Q4 2026", Items(tray.Menu)[1].Header);
        Assert.StartsWith("Copy", Items(tray.Menu)[2].Header);
    }

    [AvaloniaFact]
    public async Task CopyPutsTextOnClipboard()
    {
        var (_, widget, tray) = Demo();
        Item(tray.Menu, "Copy “CW43 (19–25 Oct)”").Command!.Execute(null);
        Assert.Equal("CW43 (19–25 Oct)", await widget.Clipboard!.TryGetTextAsync());
    }

    [AvaloniaFact]
    public void TrayClickFloatsTheWidgetUntilItLosesFocus()
    {
        var (_, widget, _) = Demo();
        widget.Hide();
        widget.BringForward();
        Assert.True(widget.IsVisible);
        Assert.True(widget.Topmost);
        // Headless has no focus manager (activating another window leaves this one active): raise what Windows would.
        var impl = widget.PlatformImpl!;
        ((Action)impl.GetType().GetProperty("Deactivated")!.GetValue(impl)!)();
        Assert.False(widget.Topmost);
        Assert.True(widget.IsVisible);
    }
}
