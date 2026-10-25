using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Weekmark.App.Year;
using Weekmark.Core;

namespace Weekmark.App.Tests;

public sealed class YearWindowTests
{
    static readonly DateTime Now = new(2026, 10, 25, 14, 0, 0);
    static readonly ScreenInfo[] OneScreen = [new("Display", new(0, 0, 1920, 1080), new(0, 0, 1920, 1040), true)];

    static AppState Demo(Action<AppSettings>? edit = null)
    {
        var s = DemoData.Create(DateOnly.FromDateTime(Now));
        edit?.Invoke(s);
        return new AppState(s, null, () => Now);
    }

    static YearWindow Open(AppState state)
    {
        var w = new YearWindow(state);
        w.Show();
        return w;
    }

    static void Press(YearWindow w, Key key, RawInputModifiers mods = RawInputModifiers.None) =>
        w.KeyPress(key, mods, key == Key.C ? PhysicalKey.C : PhysicalKey.None, null);

    [AvaloniaFact]
    public void YearWindowRendersBothThemes()
    {
        var dir = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "renders")).FullName;
        foreach (var (theme, name) in new[] { (ThemeVariant.Light, "light"), (ThemeVariant.Dark, "dark") })
        {
            var w = Open(Demo(s => s.Appearance = theme == ThemeVariant.Light ? AppearanceMode.Light : AppearanceMode.Dark));
            var frame = w.CaptureRenderedFrame();
            Assert.NotNull(frame);
            frame.Save(Path.Combine(dir, $"year-{name}.png"));
            w.Close();
        }
    }

    [AvaloniaFact]
    public void ArrowKeysStepTheYearAndEscapeCloses()
    {
        var w = Open(Demo());
        var closed = false;
        w.Closed += (_, _) => closed = true;
        Press(w, Key.Right);
        Assert.Equal(2027, w.ViewModel.Year);
        Press(w, Key.Left);
        Assert.Equal(2026, w.ViewModel.Year);
        Press(w, Key.Escape);
        Assert.True(closed);
    }

    [AvaloniaFact]
    public void CtrlCCopiesTheShownWeek()
    {
        var w = Open(Demo());
        var copied = new List<string>();
        w.Copy = t => { copied.Add(t); return Task.CompletedTask; };
        Press(w, Key.C, RawInputModifiers.Control);
        Assert.Equal([w.ViewModel.CopyText], copied);
        Assert.StartsWith("CW", copied[0]);
        w.Close();
    }

    [AvaloniaFact]
    public void ClosingSavesTheFrame()
    {
        var state = Demo();
        var w = Open(state);
        w.Close();
        Assert.NotNull(state.Settings.YearWindow);
        Assert.Equal(1060, state.Settings.YearWindow!.Width);
        Assert.Equal(820, state.Settings.YearWindow.Height);
    }

    [AvaloniaFact]
    public void ClosingWhileMinimizedKeepsTheLastNormalFrame()
    {
        var state = Demo(s => s.YearWindow = new(100, 100, 900, 700));
        var w = Open(state);
        var expected = new WindowFrame(100, 100, 900, 700);
        w.WindowState = WindowState.Minimized;
        w.Close();
        Assert.Equal(expected, state.Settings.YearWindow);
    }

    [AvaloniaFact]
    public void ClosingSavesAResize()
    {
        var state = Demo();
        var w = Open(state);
        w.Width = 700;
        w.Height = 500;
        w.Close();
        Assert.Equal(700, state.Settings.YearWindow!.Width);
        Assert.Equal(500, state.Settings.YearWindow.Height);
    }

    [AvaloniaFact]
    public void InitialFrameIgnoresAnUnreachableSave()
    {
        Assert.Equal((null, new Size(1060, 820)), YearWindow.InitialFrame(new(5000, 100, 900, 700), OneScreen));
        Assert.Equal((new PixelPoint(100, 100), new Size(520, 440)), YearWindow.InitialFrame(new(100, 100, 300, 200), OneScreen));
        Assert.Null(YearWindow.InitialFrame(null, OneScreen).Position);
    }

    [AvaloniaFact]
    public void FirstOpenFitsAndCentresOnTheTargetScreen()
    {
        // 1080p at 125%: 1040 px work area = 832 DIP, minus 40 for the title bar.
        var (pos, size) = YearWindow.InitialFrame(null, OneScreen, OneScreen[0], 1.25);
        Assert.Equal(new Size(1060, 792), size);
        Assert.Equal(new PixelPoint(298, (int)((1040 - 792 * 1.25) / 2)), pos);
        // 150%: 693 DIP work area.
        Assert.Equal(653, YearWindow.InitialFrame(null, OneScreen, OneScreen[0], 1.5).Size.Height, 0.5);
        // A big second screen: the default size, centred in its work area.
        var big = new ScreenInfo("Big", new(1920, 0, 3840, 2160), new(1920, 0, 3840, 2120), false);
        var (bp, bs) = YearWindow.InitialFrame(null, [OneScreen[0], big], big, 1);
        Assert.Equal(new Size(1060, 820), bs);
        Assert.Equal(new PixelPoint(1920 + (3840 - 1060) / 2, (2120 - 820) / 2), bp);
    }
}
