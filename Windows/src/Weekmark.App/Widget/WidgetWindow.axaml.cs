using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
    bool placing, floating, hooked, dragging, typing;

    public event Action? YearRequested;

    /// <summary>For the XAML designer only.</summary>
    public WidgetWindow() => InitializeComponent();

    public WidgetWindow(AppState state, IWindowLayering layering, bool glassAllowed = true)
    {
        InitializeComponent();
        GlassAllowed = glassAllowed;
        this.state = state;
        this.layering = layering;
        DataContext = vm = new WidgetViewModel(state);

        saveDelay.Tick += (_, _) => { saveDelay.Stop(); dragging = false; SavePlacement(); };
        floatEnd.Tick += (_, _) => FloatTimeout();
        Deactivated += (_, _) => { EndTyping(); if (floating) EndFloat(); }; // clicking away keeps the query
        View.Field.GotFocus += (_, _) => BeginTyping();
        View.Field.LostFocus += (_, _) => EndTyping(clearFocus: false); // focus is already leaving
        View.Field.AddHandler(KeyDownEvent, OnFieldKey, RoutingStrategies.Tunnel);
        vm.YearRequested += () => YearRequested?.Invoke();
        vm.WasReset += handBack => EndTyping(handBack: handBack);
        state.Changed += ApplySettings;
        Screens.Changed += OnScreensChanged;
        ScalingChanged += (_, _) => KeepOnScreen(); // another monitor's scale: same size in DIPs, new size in pixels
        // Only a user drag saves (once it settles). Windows also moves the widget itself when its monitor goes away
        // (unplug, undock, sleep, RDP) and Place() then follows: saving those moves would forget the saved monitor.
        PositionChanged += (_, _) => { if (dragging && !placing) { saveDelay.Stop(); saveDelay.Start(); } };
        Opened += (_, _) => // on every show, also after Hide Widget: that re-applies the layer
        {
            layering.HideFromTaskSwitcher(this);
            if (OperatingSystem.IsWindows())
            {
                WindowsLayering.RoundCorners(this);
                if (!hooked) Win32Properties.AddWndProcHookCallback(this, WndProc);
                hooked = true;
            }
            ApplyLayer();
            Place();
        };
        Closed += (_, _) =>
        {
            EndTyping();
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
        RequestedThemeVariant = ThemeFor(S.Appearance);
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
    internal WidgetViewModel ViewModel => vm;

    /// <summary>The tray's "Weeks in YYYY": floats the widget and pins that week.</summary>
    public void ShowWeek(WeekRef r)
    {
        BringForward();
        vm.Pin(r);
    }

    /// <summary>
    /// The tray's left click, a second launch, or a manual launch: shows the widget above other windows and focuses it.
    /// It goes back to its layer when it loses focus, or after 8 s if Windows never gave it focus.
    /// </summary>
    public void BringForward()
    {
        Show();
        floating = true;
        ApplyLayer();
        Activate();
        floatEnd.Stop();
        floatEnd.Start();
    }

    /// <summary>
    /// 8 s after floating began. Focused: Deactivated ends it later. Never focused (Windows refused the foreground, or
    /// the layering broke): no Deactivated or LostFocus will come, so typing and floating end here.
    /// </summary>
    internal void FloatTimeout()
    {
        floatEnd.Stop();
        if (!IsActive) { EndTyping(); if (floating) EndFloat(); } // the tray's BringForward floats without typing
    }

    internal bool FloatPending => floatEnd.IsEnabled;

    void EndFloat()
    {
        floatEnd.Stop();
        floating = false;
        ApplyLayer();
    }

    /// <summary>A click in the search field: the widget may take keyboard focus and floats so what is typed stays in view.</summary>
    void BeginTyping()
    {
        if (typing) return;
        typing = true;
        floating = true;
        floatEnd.Stop();
        floatEnd.Start(); // if Windows never activates the widget, FloatTimeout ends typing
        layering.AllowActivation(this, true);
        ApplyLayer();
        Activate();
    }

    /// <summary>
    /// Esc, Enter, a click elsewhere or the idle reset: focus goes back and the widget returns to its layer.
    /// <paramref name="handBack"/> false (idle reset) never hands the foreground to the previous app: the user may be
    /// typing somewhere else by then, or still in the field, and keystrokes must not land in another app.
    /// </summary>
    void EndTyping(bool clearFocus = true, bool handBack = true)
    {
        if (!typing) return;
        typing = false;
        layering.AllowActivation(this, false, handBack);
        if (clearFocus) FocusManager?.ClearFocus();
        EndFloat();
    }

    async void OnFieldKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { e.Handled = true; vm.Reset(); } // Reset raises WasReset → EndTyping
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            var copy = CopyResult();
            EndTyping();
            await copy; // never throws
        }
    }

    /// <summary>Copies the lookup result's copy text; an error (or no result) copies nothing.</summary>
    internal Task CopyResult()
    {
        vm.FlushSearch(); // Enter right after a keystroke answers the latest text
        return vm.Result is { Kind: not QueryKind.Error, CopyText: { } t } ? CopyAsync(t) : Task.CompletedTask;
    }

    internal Task CopyAsync(string text) => CopyAsync(this, text);

    internal static async Task CopyAsync(TopLevel top, string text)
    {
        // Another app holding the clipboard just means nothing is copied this time.
        try
        {
            if (top.Clipboard is { } clipboard) await clipboard.SetTextAsync(text);
        }
        catch (Exception e) when (e is InvalidOperationException or System.Runtime.InteropServices.COMException or TimeoutException) { }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (state is null) return; // designer
        if (change.Property == ClientSizeProperty) KeepOnScreen(); // a new size or section keeps it on-screen
        else if (change.Property == ActualTransparencyLevelProperty) UpdateGlass();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || S.Locked || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var pressedAt = Position;
        dragging = true;
        BeginMoveDrag(e);
        if (OperatingSystem.IsWindows()) EndDrag(pressedAt); // Win32's move loop is modal: the drag is over when it returns
    }

    /// <summary>Saves only if the widget moved: the move loop also returns after a plain click, and saving then would
    /// replace an unplugged saved monitor with the one the widget was moved to.</summary>
    internal void EndDrag(PixelPoint pressedAt)
    {
        dragging = false;
        saveDelay.Stop();
        if (Position != pressedAt) SavePlacement();
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
        saveDelay.Stop(); // a restore is never a drag to save
        dragging = false;
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

    /// <summary>
    /// After a size or scale change: keeps the widget on the monitor it is on now, nudged fully on-screen. Never
    /// restores the saved placement: crossing onto a monitor with another scale resizes the widget mid-move, and
    /// returning it to the saved monitor then made drags ping-pong and undid "Move to". Not while it is being moved.
    /// </summary>
    void KeepOnScreen()
    {
        if (placing || dragging) return;
        var screens = ScreenInfos();
        if (screens.Count == 0) return;
        var (w, h) = PhysicalSize();
        var here = PlacementMath.Capture(Position.X, Position.Y, w, h, screens);
        var (px, py) = PlacementMath.Restore(here, screens, w, h); // the current monitor, clamped
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

    /// <summary>The monitor that owns the widget (by its centre), as its saved placement records it.</summary>
    internal string? CurrentMonitor()
    {
        var screens = ScreenInfos();
        if (screens.Count == 0) return null;
        var (w, h) = PhysicalSize();
        return PlacementMath.Capture(Position.X, Position.Y, w, h, screens).ScreenId;
    }

    /// <summary>Moves the widget to a corner of a monitor (the current one when <paramref name="screenId"/> is null) and saves it.</summary>
    internal void SnapTo(Corner corner, string? screenId = null)
    {
        var id = screenId ?? CurrentMonitor();
        if (ScreenInfos().FirstOrDefault(s => s.Id == id) is not { } screen) return;
        // ponytail: sized at the current monitor's scale; moving to a monitor with another scale resizes the widget
        // afterwards, and KeepOnScreen() then clamps it on that monitor (the margin may shrink). Re-snap if it shows.
        var (w, h) = PhysicalSize();
        var (x, y) = PlacementMath.Snap(screen, corner, w, h);
        placing = true;
        Position = new PixelPoint((int)Math.Round(x), (int)Math.Round(y));
        placing = false;
        SavePlacement();
    }

    /// <summary>Connected monitors, left to right, with the ids used for saved placement.</summary>
    internal List<ScreenInfo> ScreenInfos() => ScreenInfos(Screens);

    internal static ThemeVariant ThemeFor(AppearanceMode m) => m switch
    {
        AppearanceMode.Light => ThemeVariant.Light,
        AppearanceMode.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };

    internal static List<ScreenInfo> ScreenInfos(Screens screens)
    {
        var all = screens.All.OrderBy(s => s.Bounds.X).ThenBy(s => s.Bounds.Y).ToList();
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
