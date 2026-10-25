using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Weekmark.App.Platform;
using Weekmark.Core;

namespace Weekmark.App.Widget;

/// <summary>
/// The borderless desktop widget. Placement is per monitor in physical pixels (<see cref="PlacementMath"/>);
/// a monitor's id is its Avalonia <see cref="Screen.DisplayName"/> (the monitor's name on Windows), with
/// "#n" appended (left to right) when several connected monitors share a name. Names survive unplugging and
/// replugging, unlike monitor handles; twins can swap, in which case the widget still lands on-screen.
/// </summary>
public partial class WidgetWindow : Window
{
    readonly AppState state = null!;
    readonly IWindowLayering layering = null!;
    readonly WidgetViewModel vm = null!;
    readonly DispatcherTimer saveDelay = new() { Interval = TimeSpan.FromMilliseconds(400) };
    readonly DispatcherTimer floatEnd = new() { Interval = TimeSpan.FromSeconds(8) };
    bool placing, floating;

    /// <summary>For the XAML designer only.</summary>
    public WidgetWindow() => InitializeComponent();

    public WidgetWindow(AppState state, IWindowLayering layering, bool glassAllowed = true)
    {
        InitializeComponent();
        GlassAllowed = glassAllowed;
        this.state = state;
        this.layering = layering;
        DataContext = vm = new WidgetViewModel(state);

        saveDelay.Tick += (_, _) => { saveDelay.Stop(); SavePlacement(); };
        floatEnd.Tick += (_, _) => { floatEnd.Stop(); floating = false; ApplyLayer(); };
        state.Changed += ApplySettings;
        Screens.Changed += OnScreensChanged;
        PositionChanged += (_, _) => { if (!placing) { saveDelay.Stop(); saveDelay.Start(); } }; // save once the drag settles
        Opened += (_, _) =>
        {
            layering.HideFromTaskSwitcher(this);
            if (OperatingSystem.IsWindows())
            {
                WindowsLayering.RoundCorners(this);
                Win32Properties.AddWndProcHookCallback(this, WndProc);
            }
            ApplyLayer();
            Place();
        };
        Closed += (_, _) =>
        {
            state.Changed -= ApplySettings;
            Screens.Changed -= OnScreensChanged;
            saveDelay.Stop();
            floatEnd.Stop();
            vm.Dispose();
        };

        ApplySettings();
        Place();
    }

    AppSettings S => state.Settings;

    /// <summary>Off for <c>--screenshot</c>: a PNG has no desktop behind it, so it shows the solid look.</summary>
    bool GlassAllowed { get; }

    bool? glassRequested;

    void ApplySettings()
    {
        RequestedThemeVariant = S.Appearance switch
        {
            AppearanceMode.Light => ThemeVariant.Light,
            AppearanceMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
        var glass = S.Background == BackgroundStyle.Glass && GlassAllowed;
        if (glass != glassRequested) // re-requesting the backdrop on every tick could make it flicker
        {
            glassRequested = glass;
            TransparencyLevelHint = glass
                ? [WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Blur, WindowTransparencyLevel.Transparent]
                : [WindowTransparencyLevel.Transparent];
        }
        UpdateGlass();
        ApplyLayer(); // every tick: also recovers the layer after Show desktop or an Explorer restart
    }

    void UpdateGlass() => vm.GlassAvailable = GlassAllowed && (ActualTransparencyLevel == WindowTransparencyLevel.Mica
        || ActualTransparencyLevel == WindowTransparencyLevel.AcrylicBlur || ActualTransparencyLevel == WindowTransparencyLevel.Blur);

    void ApplyLayer()
    {
        if (!IsVisible) return; // not shown yet, or hidden on purpose: layering must not re-show it
        layering.Apply(this, floating ? WindowLayer.Floating : S.Layer);
    }

    /// <summary>A second launch asks for the widget: float it above windows for a few seconds, then back to its layer.</summary>
    public void BringForward()
    {
        // ponytail: fixed 8 s float; the tray (Task 10) can end it on focus loss instead.
        Show();
        floating = true;
        ApplyLayer();
        floatEnd.Stop();
        floatEnd.Start();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (state is null) return; // designer
        if (change.Property == ClientSizeProperty) Place(); // a new size or section keeps it on-screen
        else if (change.Property == ActualTransparencyLevelProperty) UpdateGlass();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || S.Locked || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        BeginMoveDrag(e);
    }

    void OnScreensChanged(object? sender, EventArgs e) => Place();

    (double W, double H) PhysicalSize()
    {
        View.Measure(Size.Infinity); // also valid before the first layout pass
        return (View.DesiredSize.Width * DesktopScaling, View.DesiredSize.Height * DesktopScaling);
    }

    /// <summary>Restores the saved per-monitor position, clamped on-screen; top-right of the primary monitor by default.</summary>
    public void Place()
    {
        var screens = ScreenInfos();
        if (screens.Count == 0) return;
        var saved = S.ScreenID is { } id && S.OffsetX is { } x && S.OffsetTop is { } t ? new SavedPlacement(id, x, t) : null;
        var (w, h) = PhysicalSize();
        var (px, py) = PlacementMath.Restore(saved, screens, w, h);
        var p = new PixelPoint((int)Math.Round(px), (int)Math.Round(py));
        if (p == Position) return;
        placing = true;
        Position = p;
        placing = false;
    }

    public void SavePlacement()
    {
        var screens = ScreenInfos();
        if (screens.Count == 0) return;
        var (w, h) = PhysicalSize();
        var p = PlacementMath.Capture(Position.X, Position.Y, w, h, screens);
        if (S.ScreenID == p.ScreenId && S.OffsetX == p.OffsetX && S.OffsetTop == p.OffsetTop) return;
        S.ScreenID = p.ScreenId;
        S.OffsetX = p.OffsetX;
        S.OffsetTop = p.OffsetTop;
        state.Save();
    }

    List<ScreenInfo> ScreenInfos()
    {
        var all = Screens.All.OrderBy(s => s.Bounds.X).ThenBy(s => s.Bounds.Y).ToList();
        static RectD R(PixelRect r) => new(r.X, r.Y, r.Width, r.Height);
        return all.Select(s =>
        {
            var name = string.IsNullOrEmpty(s.DisplayName) ? "Display" : s.DisplayName;
            var twins = all.Where(o => o.DisplayName == s.DisplayName).ToList();
            var id = twins.Count > 1 ? $"{name}#{twins.IndexOf(s) + 1}" : name;
            return new ScreenInfo(id, R(s.Bounds), R(s.WorkingArea), s.IsPrimary);
        }).ToList();
    }

    // Windows: refresh at once after a clock/time-zone change or resume from sleep (otherwise within 30 s).
    nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam, ref bool handled)
    {
        const uint WM_TIMECHANGE = 0x1E, WM_POWERBROADCAST = 0x218;
        const nint PBT_APMRESUMESUSPEND = 0x7, PBT_APMRESUMEAUTOMATIC = 0x12;
        if (msg == WM_TIMECHANGE || (msg == WM_POWERBROADCAST && wParam is PBT_APMRESUMESUSPEND or PBT_APMRESUMEAUTOMATIC))
            Dispatcher.UIThread.Post(state.Tick);
        return 0;
    }
}
