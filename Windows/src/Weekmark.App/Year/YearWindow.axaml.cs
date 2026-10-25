using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Weekmark.App.Widget;
using Weekmark.Core;

namespace Weekmark.App.Year;

public partial class YearWindow : Window
{
    const double DefaultWidth = 1060, DefaultHeight = 820, MinWidthDip = 520, MinHeightDip = 440;

    readonly AppState state;
    readonly YearViewModel vm;

    /// <param name="firstScreen">Where a first open (nothing saved) centres: the widget's monitor; the primary one when null.</param>
    public YearWindow(AppState state, ScreenInfo? firstScreen = null)
    {
        this.state = state;
        vm = new YearViewModel(state);
        DataContext = vm;
        InitializeComponent();
        var screens = WidgetWindow.ScreenInfos(Screens);
        var target = firstScreen ?? screens.FirstOrDefault(x => x.IsPrimary) ?? screens.FirstOrDefault();
        var scaling = target is null ? 1 : Screens.All.FirstOrDefault(x => x.Bounds.X == target.Bounds.X && x.Bounds.Y == target.Bounds.Y)?.Scaling ?? 1;
        var (pos, size) = InitialFrame(state.Settings.YearWindow, screens, target, scaling);
        Width = size.Width;
        Height = size.Height;
        if (pos is { } p)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = p;
        }
        else WindowStartupLocation = WindowStartupLocation.CenterScreen;

        Copy = t => WidgetWindow.CopyAsync(this, t);
        state.Changed += ApplyTheme;
        ApplyTheme();
        SizeChanged += (_, _) => UpdateColumns();
        AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel); // before a focused button or the scroller takes the arrows
        Closing += (_, _) =>
        {
            Remember();
            if (normalFrame is null) return;
            state.Settings.YearWindow = normalFrame;
            state.Save();
        };
        Closed += (_, _) =>
        {
            state.Changed -= ApplyTheme;
            vm.Dispose();
        };
        Opened += (_, _) => { Remember(); UpdateColumns(); };
        PositionChanged += (_, _) => Remember();
        SizeChanged += (_, _) => Remember();
    }

    WindowFrame? normalFrame;

    /// <summary>A minimized or maximized window has no usable frame (minimized sits near -32000): keep the last normal one.</summary>
    void Remember()
    {
        if (IsVisible && WindowState == WindowState.Normal) normalFrame = new WindowFrame(Position.X, Position.Y, Width, Height);
    }

    internal YearViewModel ViewModel => vm;

    /// <summary>Replaced by tests.</summary>
    internal Func<string, Task> Copy { get; set; }

    /// <summary>
    /// A reachable saved frame wins. Otherwise the default size, shrunk to fit <paramref name="target"/>'s work area
    /// (<paramref name="scaling"/> pixels per DIP, 40 DIP kept for the title bar) and centred on it; no position without a target.
    /// </summary>
    internal static (PixelPoint? Position, Size Size) InitialFrame(WindowFrame? saved, IReadOnlyList<ScreenInfo> screens, ScreenInfo? target = null, double scaling = 1)
    {
        if (saved is { } f && PlacementMath.IsReachable(f.X, f.Y, screens))
            return (new PixelPoint((int)Math.Round(f.X), (int)Math.Round(f.Y)), new Size(Math.Max(f.Width, MinWidthDip), Math.Max(f.Height, MinHeightDip)));
        if (target is null) return (null, new Size(DefaultWidth, DefaultHeight));
        var wa = target.WorkArea;
        var size = new Size(Math.Max(MinWidthDip, Math.Min(DefaultWidth, wa.Width / scaling)),
                            Math.Max(MinHeightDip, Math.Min(DefaultHeight, wa.Height / scaling - 40)));
        return (new PixelPoint((int)Math.Round(wa.X + (wa.Width - size.Width * scaling) / 2), (int)Math.Round(wa.Y + (wa.Height - size.Height * scaling) / 2)), size);
    }

    void ApplyTheme() => RequestedThemeVariant = WidgetWindow.ThemeFor(state.Settings.Appearance);

    void UpdateColumns()
    {
        if (Cards.GetVisualDescendants().OfType<UniformGrid>().FirstOrDefault() is { } grid)
            grid.Columns = YearViewModel.ColumnsFor(ClientSize.Width);
    }

    void OnKey(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left: vm.PreviousYearCommand.Execute(null); break;
            case Key.Right: vm.NextYearCommand.Execute(null); break;
            case Key.Escape: Close(); break;
            default: return;
        }
        e.Handled = true;
    }

    void OnCopy(object? sender, RoutedEventArgs e) => _ = Copy(vm.CopyText);
}
