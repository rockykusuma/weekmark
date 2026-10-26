using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Weekmark.App.Platform;
using Weekmark.App.Widget;
using Weekmark.Core;

namespace Weekmark.App.Finder;

/// <summary>The Spotlight-style Quick Finder popup. One window, reused: closing only hides it.</summary>
public partial class QuickFinderWindow : Window
{
    const double WidthDip = 640;

    readonly AppState state = null!;
    readonly QuickFinderViewModel vm = null!;

    /// <summary>For the XAML designer only.</summary>
    public QuickFinderWindow() => InitializeComponent();

    public QuickFinderWindow(AppState state)
    {
        this.state = state;
        vm = new QuickFinderViewModel(state);
        DataContext = vm;
        InitializeComponent();
        Rows.SelectionChanged += (_, _) => { if (Rows.SelectedIndex >= 0) vm.Selection = Rows.SelectedIndex; };
        Rows.DoubleTapped += (_, _) => Copy();
        // The list is fed by hand: a bound ItemsSource would reset the selection and write -1 back.
        vm.PropertyChanged += OnVmChanged;
        SyncRows();
        Field.AddHandler(KeyDownEvent, OnFieldKey, RoutingStrategies.Tunnel);
        Deactivated += (_, _) => CloseFinder();
        Opened += (_, _) => { if (OperatingSystem.IsWindows()) WindowsLayering.RoundCorners(this); };
        state.Changed += ApplyTheme;
        ApplyTheme();
    }

    public event Action<WeekRef>? ShowInWidgetRequested;

    internal QuickFinderViewModel ViewModel => vm;

    // One glyph and tint per kind: plain symbols Segoe UI (or its symbol fallback) draws, no emoji.
    public static readonly IValueConverter KindGlyph = new FuncValueConverter<QueryKind, string>(k => k switch
    {
        QueryKind.Week => "▦",
        QueryKind.Range => "↔",
        QueryKind.Date => "◷",
        QueryKind.Relative => "»",
        QueryKind.Countdown => "⧖",
        QueryKind.Milestone => "⚑",
        QueryKind.Holiday => "☀",
        QueryKind.Hint => "?",
        _ => "!",
    });

    public static readonly IValueConverter KindColor = new FuncValueConverter<QueryKind, IBrush>(k => new SolidColorBrush(Color.Parse(k switch
    {
        QueryKind.Week => "#007AFF",
        QueryKind.Range => "#AF52DE",
        QueryKind.Date => "#32ADE6",
        QueryKind.Relative => "#30B0C7",
        QueryKind.Countdown or QueryKind.Milestone => "#FF9500",
        QueryKind.Holiday or QueryKind.Error => "#FF3B30",
        _ => "#8E8E93",
    })));

    void ApplyTheme() => RequestedThemeVariant = WidgetWindow.ThemeFor(state.Settings.Appearance);

    void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(vm.Results) or nameof(vm.Selection)) SyncRows();
    }

    void SyncRows()
    {
        if (!ReferenceEquals(Rows.ItemsSource, vm.Results)) Rows.ItemsSource = vm.Results;
        if (Rows.SelectedIndex != vm.Selection) Rows.SelectedIndex = vm.Selection;
    }

    public void ShowFinder(string query = "")
    {
        vm.Query = query;
        vm.Selection = 0; // the query may not have changed
        vm.Copied = null;
        vm.Refresh();
        var cursor = OperatingSystem.IsWindows() ? WindowsLayering.CursorPosition() : null;
        var screen = (cursor is { } p ? Screens.ScreenFromPoint(p) : null) ?? Screens.Primary;
        if (screen is not null) Position = FinderPosition(screen.WorkingArea, screen.Scaling);
        Show();
        Activate();
        Field.Focus();
        Field.CaretIndex = Field.Text?.Length ?? 0;
    }

    public void Toggle()
    {
        if (IsVisible) CloseFinder();
        else ShowFinder();
    }

    public void CloseFinder() => Hide();

    /// <summary>Horizontally centred on the working area, top at 28 % of its height (device pixels).</summary>
    internal static PixelPoint FinderPosition(PixelRect workingArea, double scaling, double widthDip = WidthDip) =>
        new(workingArea.X + (int)Math.Round((workingArea.Width - widthDip * scaling) / 2),
            workingArea.Y + (int)Math.Round(workingArea.Height * 0.28));

    void OnFieldKey(object? sender, KeyEventArgs e)
    {
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        switch (e.Key)
        {
            case Key.Down: vm.Move(1); break;
            case Key.Up: vm.Move(-1); break;
            case Key.Escape: CloseFinder(); break;
            case Key.Enter when ctrl:
                if (vm.Selected?.Week is { } week)
                {
                    CloseFinder();
                    ShowInWidgetRequested?.Invoke(week);
                }
                break;
            case Key.Enter: Copy(); break;
            case Key.C when ctrl && vm.Query.Length == 0: Copy(); break;
            default: return;
        }
        e.Handled = true;
    }

    async void Copy()
    {
        if (vm.Selected?.CopyText is not { } text) return;
        vm.Copied = text;
        await WidgetWindow.CopyAsync(this, text); // never throws
        await Task.Delay(700);
        if (vm.Copied == text) CloseFinder(); // typing or a reopen in between cleared it
    }
}
