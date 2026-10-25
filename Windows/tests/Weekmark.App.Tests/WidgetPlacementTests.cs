using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Weekmark.App.Platform;
using Weekmark.App.Widget;
using Weekmark.Core;

namespace Weekmark.App.Tests;

/// <summary>Only a user drag (or a snap) saves the placement; moves Windows makes itself never do.</summary>
public sealed class WidgetPlacementTests : IDisposable
{
    readonly string dir = Directory.CreateTempSubdirectory("wm-placement-").FullName;
    string SettingsPath => Path.Combine(dir, "settings.json");

    public void Dispose() => Directory.Delete(dir, recursive: true);

    (WidgetWindow Widget, SettingsStore Store) Shown()
    {
        var store = new SettingsStore(SettingsPath);
        var state = new AppState(store.Load(), store, () => new DateTime(2026, 10, 25, 14, 0, 0));
        var w = new WidgetWindow(state, new FallbackLayering());
        w.Show();
        Assert.NotEmpty(w.ScreenInfos());
        return (w, store);
    }

    /// <summary>Runs the UI thread for longer than the 400 ms save debounce.</summary>
    static void Settle()
    {
        var frame = new DispatcherFrame(); // a pushed frame runs timers (RunJobs does not)
        DispatcherTimer.RunOnce(() => frame.Continue = false, TimeSpan.FromMilliseconds(700));
        Dispatcher.UIThread.PushFrame(frame);
    }

    string? SavedScreen() => File.Exists(SettingsPath) ? new SettingsStore(SettingsPath).Load().ScreenID : null;

    [AvaloniaFact]
    public void AMoveWindowsMakesNeverSavesThePlacement()
    {
        var (w, _) = Shown();
        w.Position += new PixelPoint(-40, 30); // e.g. the widget's monitor was unplugged
        Settle();
        Assert.Null(SavedScreen());

        w.Position += new PixelPoint(-40, 30);
        w.Place(); // what Screens.Changed does next: the pending move must not be saved either
        Settle();
        Assert.Null(SavedScreen());
        w.Close();
    }

    [AvaloniaFact]
    public void ADragSavesThePlacement()
    {
        var (w, _) = Shown();
        w.MouseDown(new Point(20, 20), MouseButton.Left); // starts the drag (modal on Windows: it saves on return)
        w.Position += new PixelPoint(-40, 30);
        w.MouseUp(new Point(20, 20), MouseButton.Left);
        Settle();
        Assert.NotNull(SavedScreen());
        w.Close();
    }

    [AvaloniaFact]
    public void SnapSaves()
    {
        var (w, _) = Shown();
        w.SnapTo(Corner.BottomLeft);
        Assert.NotNull(SavedScreen());
        w.Close();
    }
}
