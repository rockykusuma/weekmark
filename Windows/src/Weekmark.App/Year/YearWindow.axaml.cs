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

    public YearWindow(AppState state)
    {
        this.state = state;
        vm = new YearViewModel(state);
        DataContext = vm;
        InitializeComponent();
        var (pos, size) = InitialFrame(state.Settings.YearWindow, WidgetWindow.ScreenInfos(Screens));
        Width = size.Width;
        Height = size.Height;
        if (pos is { } p)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = p;
        }
        else WindowStartupLocation = WindowStartupLocation.CenterScreen;

        Copy = CopyAsync;
        state.Changed += ApplyTheme;
        ApplyTheme();
        SizeChanged += (_, _) => UpdateColumns();
        AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel); // before a focused button or the scroller takes the arrows
        Closing += (_, _) =>
        {
            state.Settings.YearWindow = new WindowFrame(Position.X, Position.Y, Width, Height);
            state.Save();
        };
        Closed += (_, _) =>
        {
            state.Changed -= ApplyTheme;
            vm.Dispose();
        };
        Opened += (_, _) => UpdateColumns();
    }

    internal YearViewModel ViewModel => vm;

    /// <summary>Replaced by tests.</summary>
    internal Func<string, Task> Copy { get; set; }

    internal static (PixelPoint? Position, Size Size) InitialFrame(WindowFrame? saved, IReadOnlyList<ScreenInfo> screens) =>
        saved is { } f && PlacementMath.IsReachable(f.X, f.Y, screens)
            ? (new PixelPoint((int)Math.Round(f.X), (int)Math.Round(f.Y)), new Size(Math.Max(f.Width, MinWidthDip), Math.Max(f.Height, MinHeightDip)))
            : (null, new Size(DefaultWidth, DefaultHeight));

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

    async Task CopyAsync(string text)
    {
        // Another app holding the clipboard just means nothing is copied this time.
        try
        {
            if (Clipboard is { } clipboard) await clipboard.SetTextAsync(text);
        }
        catch (Exception e) when (e is InvalidOperationException or System.Runtime.InteropServices.COMException or TimeoutException) { }
    }
}
