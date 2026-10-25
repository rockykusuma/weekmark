using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Weekmark.App.Platform;
using Weekmark.App.Widget;
using Weekmark.Core;

namespace Weekmark.App.Tests;

public sealed class WidgetRenderTests
{
    static readonly DateTime Now = new(2026, 10, 25, 14, 0, 0);

    static AppState Demo(Action<AppSettings>? edit = null)
    {
        var s = DemoData.Create(DateOnly.FromDateTime(Now));
        edit?.Invoke(s);
        return new AppState(s, null, () => Now);
    }

    static Window Show(AppState state, ThemeVariant theme)
    {
        var w = new Window
        {
            Content = new WidgetView { DataContext = new WidgetViewModel(state) },
            SizeToContent = SizeToContent.WidthAndHeight,
            Background = Brushes.Transparent,
            RequestedThemeVariant = theme,
        };
        w.Show();
        return w;
    }

    /// <summary>Pixels as RGBA bytes, whatever the frame's native order.</summary>
    static byte[] Rgba(WriteableBitmap frame)
    {
        using var fb = frame.Lock();
        var bytes = new byte[fb.RowBytes * fb.Size.Height];
        Marshal.Copy(fb.Address, bytes, 0, bytes.Length);
        if (fb.Format == PixelFormat.Bgra8888)
            for (var i = 0; i < bytes.Length; i += 4) (bytes[i], bytes[i + 2]) = (bytes[i + 2], bytes[i]);
        return bytes;
    }

    [AvaloniaFact]
    public void WidgetRendersAllSizesAndThemes()
    {
        var dir = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "renders")).FullName;
        foreach (var size in Enum.GetValues<WidgetSize>())
            foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                var w = Show(Demo(s => s.Size = size), theme);
                var frame = w.CaptureRenderedFrame();
                Assert.NotNull(frame);
                frame.Save(Path.Combine(dir, $"widget-{size}-{theme}.png"));

                // 300 points wide at the size's scale, plus 12 unscaled points of shadow room per side.
                var expected = 300 * size.Scale() + 24;
                Assert.InRange(frame.PixelSize.Width, expected - 2, expected + 2);
                Assert.True(frame.PixelSize.Height > frame.PixelSize.Width, $"{size}/{theme}: height {frame.PixelSize.Height}");

                // The panel covers most of the frame, and the big week number is drawn in the accent blue.
                var px = Rgba(frame);
                int opaque = 0, accent = 0;
                for (var i = 0; i < px.Length; i += 4)
                {
                    if (px[i + 3] > 200) opaque++;
                    if (px[i + 3] > 200 && px[i] < 30 && px[i + 1] is > 100 and < 150 && px[i + 2] > 200) accent++;
                }
                var total = px.Length / 4;
                Assert.True(opaque > total / 2, $"{size}/{theme}: only {opaque} of {total} pixels drawn");
                Assert.True(accent > 300, $"{size}/{theme}: only {accent} accent pixels");
                w.Close();
            }
    }

    [AvaloniaFact]
    public void WidgetHidesSectionsWhenOff()
    {
        var on = Show(Demo(), ThemeVariant.Light);
        Assert.Contains(on.GetVisualDescendants().OfType<MonthGrid>(), m => m.IsEffectivelyVisible);

        var off = Show(Demo(s => s.ShowMonth = false), ThemeVariant.Light);
        Assert.DoesNotContain(off.GetVisualDescendants().OfType<MonthGrid>(), m => m.IsEffectivelyVisible);
        Assert.True(off.CaptureRenderedFrame()!.PixelSize.Height < on.CaptureRenderedFrame()!.PixelSize.Height - 100);
    }

    [AvaloniaFact]
    public void DemoNeverTouchesSettingsFile()
    {
        var dir = Directory.CreateTempSubdirectory("weekmark-demo").FullName;
        try
        {
            var path = Path.Combine(dir, "settings.json");
            var original = "{\"size\":\"large\",\"milestones\":[],\"screenID\":\"X\",\"offsetX\":5,\"offsetTop\":7}"u8.ToArray();
            File.WriteAllBytes(path, original);

            var state = AppState.FromOptions(Options.Parse(["--demo-data"]), path);
            Assert.Equal(2, state.Settings.Milestones.Count); // the demo data, not the file
            var w = new WidgetWindow(state, new FallbackLayering());
            w.Show();
            w.SavePlacement();
            state.Settings.Size = WidgetSize.Small;
            state.Save();
            w.Close();

            Assert.Equal(original, File.ReadAllBytes(path));
            Assert.Equal([path], Directory.GetFiles(dir));
        }
        finally { Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void HoverAndPinDriveDetailsAndBrowsing()
    {
        var vm = new WidgetViewModel(Demo());
        Assert.True(vm.Details.IsHint);

        var cw46 = new WeekRef(2026, 46);
        vm.Pin(cw46);
        Assert.Equal(cw46, vm.Pinned);
        Assert.Equal(1, vm.MonthOffset); // CW46's Thursday is in November
        Assert.True(vm.IsBrowsing);
        Assert.StartsWith("CW46 · ", vm.Details.Title);
        Assert.True(vm.Details.IsPinned);

        vm.Hover = new WeekRef(2026, 45);
        Assert.StartsWith("CW45 · ", vm.Details.Title);
        Assert.False(vm.Details.IsPinned);

        vm.Pin(new WeekRef(2026, 60)); // a week that doesn't exist is ignored
        Assert.Equal(cw46, vm.Pinned);

        vm.Reset();
        Assert.Null(vm.Pinned);
        Assert.Equal(0, vm.MonthOffset);
        Assert.False(vm.IsBrowsing);

        vm.Step(-2);
        Assert.Equal("August 2026", vm.MonthTitle);
    }

    [AvaloniaFact]
    public void InvalidStoredMilestoneWeeksAreSkipped()
    {
        // Hand-edited settings: week 60 doesn't exist. The row shows without a countdown and can't be pinned.
        var state = Demo(s => s.Milestones.Add(new(Guid.NewGuid(), "Bad", new WeekRef(2026, 60))));
        var vm = new WidgetViewModel(state);
        var bad = Assert.Single(vm.Milestones, m => m.Name == "Bad");
        Assert.Null(bad.Countdown);
        Assert.False(bad.CanPin);
        var w = Show(state, ThemeVariant.Light);
        Assert.NotNull(w.CaptureRenderedFrame());
    }
}
