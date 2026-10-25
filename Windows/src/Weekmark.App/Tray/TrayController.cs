using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.Input;
using Weekmark.App.Platform;
using Weekmark.App.Widget;
using Weekmark.Core;

namespace Weekmark.App.Tray;

/// <summary>
/// The notification-area icon: the week number (or the app icon when <c>showInMenuBar</c> is off), a tooltip and the
/// app menu, a port of the Mac's status item (<c>menuNeedsUpdate</c>). Left click floats the widget until it loses focus.
/// </summary>
public sealed class TrayController : IDisposable
{
    static readonly (WidgetSize Size, string Title)[] Sizes =
        [(WidgetSize.Small, "Small"), (WidgetSize.Medium, "Medium"), (WidgetSize.Large, "Large"), (WidgetSize.Xlarge, "Extra Large")];
    static readonly (Corner Corner, string Title)[] Corners =
        [(Corner.TopLeft, "Top Left"), (Corner.TopRight, "Top Right"), (Corner.BottomLeft, "Bottom Left"), (Corner.BottomRight, "Bottom Right")];

    readonly AppState state;
    readonly TrayIcon icon = new();
    readonly NativeMenu menu = new();
    readonly IPlatformSettings? platform = Application.Current?.PlatformSettings;
    WidgetWindow widget = null!;
    string? iconKey, menuKey;
    bool disposed;

    public TrayController(AppState state, WidgetWindow widget)
    {
        this.state = state;
        icon.Menu = menu;
        icon.Clicked += (_, _) => Widget.BringForward();
        state.Changed += Refresh; // settings, the 30 s tick and midnight
        if (platform is not null) platform.ColorValuesChanged += OnColorsChanged; // taskbar light/dark
        Widget = widget;
        icon.IsVisible = true;
    }

    /// <summary>"Settings…" was chosen; the settings window (Task 11) listens.</summary>
    public event Action? SettingsRequested;
    public event Action? YearRequested;
    public event Action? QuitRequested;

    public TrayIcon Icon => icon;
    public NativeMenu Menu => menu;

    /// <summary>The app sets a new widget when Windows destroyed the old one.</summary>
    public WidgetWindow Widget
    {
        get => widget;
        set
        {
            widget = value;
            value.MenuFor = hover => ToMenuItems(BuildMenu(Snapshot(), hover));
            value.ScalingChanged += (_, _) => Refresh(); // DPI change
            value.Screens.Changed += (_, _) => Refresh(); // monitors for "Move to", primary DPI
            value.PropertyChanged += (_, e) => { if (e.Property == Visual.IsVisibleProperty) Refresh(); }; // Show/Hide label
            Refresh();
        }
    }

    void OnColorsChanged(object? sender, PlatformColorValues e) => Refresh();

    void Refresh()
    {
        if (disposed) return; // quitting hides the widget after the icon is gone
        var snap = Snapshot();
        var week = snap.Current;

        // ponytail: one bitmap at the primary monitor's tray size (16 px × scale); the taskbar lives there by default.
        var px = (int)Math.Round(16 * (widget.Screens.Primary?.Scaling ?? 1));
        var light = TaskbarTheme.IsLight();
        var key = state.Settings.ShowInMenuBar ? $"{week.Week} {px} {light}" : "app";
        if (key != iconKey)
        {
            iconKey = key;
            icon.Icon = state.Settings.ShowInMenuBar
                ? new WindowIcon(WeekIconRenderer.Render(week.Week, px, light))
                : new WindowIcon(AssetLoader.Open(new Uri("avares://Weekmark/Assets/weekmark.ico")));
        }
        icon.ToolTipText = $"CW {week.Week} · {snap.RangeText} {week.Year}";

        // Rebuilt every tick but only swapped in when something changed, so an open menu isn't disturbed. The items are
        // replaced, not the menu: the macOS exporter throws when a tray icon is given a different NativeMenu.
        var items = BuildMenu(snap);
        var shape = Describe(items, new StringBuilder()).ToString();
        if (shape != menuKey)
        {
            menuKey = shape;
            menu.Items.Clear();
            foreach (var item in items) menu.Items.Add(item);
        }
    }

    WeekSnapshot Snapshot() => new(state.Context.Now, state.Context.Cal, state.Context.Fmt);

    /// <summary>"Add Milestone…" was chosen; <see cref="BuildMenu"/> offers it for the week under the widget's right click.</summary>
    public event Action<WeekRef>? AddMilestoneRequested;

    internal List<NativeMenuItemBase> BuildMenu(WeekSnapshot snap, WeekRef? addMilestone = null)
    {
        var s = state.Settings;
        var ctx = state.Context;
        var week = snap.Current;
        var copy = ctx.Fmt.CopyText(week, week.Year);

        var menu = new List<NativeMenuItemBase>();
        menu.Add(Label($"Week {week.Week} of {snap.WeeksInYear} · {snap.RangeText}, {week.Year}"));
        if (PlanningLine(ctx, s) is { } planning) menu.Add(Label(planning));
        menu.Add(Item($"Copy “{copy}”", () => _ = Widget.CopyAsync(copy)));
        menu.Add(new NativeMenuItemSeparator());
        if (addMilestone is { } w)
        {
            menu.Add(Item($"Add Milestone in CW{w.Week}…", () => AddMilestoneRequested?.Invoke(w)));
            menu.Add(new NativeMenuItemSeparator());
        }
        menu.Add(Item("Year at a Glance…", () => YearRequested?.Invoke()));
        menu.Add(WeeksMenu(snap, week.Year));
        menu.Add(WeeksMenu(snap, week.Year + 1));
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(Item(widget.IsVisible ? "Hide Widget" : "Show Widget", () =>
        {
            if (Widget.IsVisible) Widget.Hide(); // the layering's recovery never re-shows a hidden widget
            else Widget.Show(); // Avalonia raises Opened on every show, and Opened re-applies the layer
        }));

        var position = new NativeMenu();
        position.Add(Choice("On Desktop (behind windows)", s.Layer == WindowLayer.Desktop, () => s.Layer = WindowLayer.Desktop));
        position.Add(Choice("Float Above Windows", s.Layer == WindowLayer.Floating, () => s.Layer = WindowLayer.Floating));
        position.Add(new NativeMenuItemSeparator());
        position.Add(Choice("Lock Position", s.Locked, () => s.Locked = !s.Locked, NativeMenuItemToggleType.CheckBox));
        position.Add(new NativeMenuItemSeparator());
        foreach (var (corner, title) in Corners) position.Add(Item($"Snap to {title}", () => Widget.SnapTo(corner)));
        var screens = widget.ScreenInfos();
        if (screens.Count > 1)
        {
            var current = widget.CurrentMonitor();
            position.Add(new NativeMenuItemSeparator());
            foreach (var screen in screens)
            {
                var item = Item($"Move to {screen.Id}", () => Widget.SnapTo(Corner.TopRight, screen.Id));
                (item.ToggleType, item.IsChecked) = (NativeMenuItemToggleType.Radio, screen.Id == current);
                position.Add(item);
            }
        }
        menu.Add(new NativeMenuItem("Position") { Menu = position });

        var size = new NativeMenu();
        foreach (var (value, title) in Sizes) size.Add(Choice(title, s.Size == value, () => s.Size = value));
        menu.Add(new NativeMenuItem("Size") { Menu = size });

        menu.Add(new NativeMenuItemSeparator());
        menu.Add(Item("Settings…", () => SettingsRequested?.Invoke()));
        menu.Add(Item("Quit Weekmark", () => QuitRequested?.Invoke()));
        return menu;
    }

    /// <summary>"Weeks in YYYY", a port of the Mac's <c>weeksMenu</c>: month labels, "CW n  range", "· 4d" short weeks and
    /// "⚑ milestones". The current week is ticked, not bold: a NativeMenu can't do bold.</summary>
    NativeMenuItem WeeksMenu(WeekSnapshot snap, int year)
    {
        var ctx = state.Context;
        var sub = new NativeMenu();
        var last = 0;
        for (var w = 1; w <= ctx.Cal.WeeksIn(year); w++)
        {
            var r = new WeekRef(year, w);
            var thu = ctx.Cal.StartOfWeek(r).AddDays(3);
            if (thu.Month != last)
            {
                if (last != 0) sub.Add(new NativeMenuItemSeparator());
                sub.Add(Label(ctx.Fmt.Format(thu, DateTemplate.MonthLong)));
                last = thu.Month;
            }
            var title = $"CW {w,2}    {ctx.Fmt.ShortRange(r)}";
            var wd = ctx.Holidays.WorkingDays(r, ctx.Cal);
            if (wd < 5) title += $"   · {wd}d";
            var names = ctx.Milestones.Where(m => m.Week == r).Select(m => m.Name).ToList();
            if (names.Count > 0) title += $"   ⚑ {string.Join(", ", names)}";
            var item = Item(title, () => Widget.ShowWeek(r));
            if (r == snap.Current) (item.ToggleType, item.IsChecked) = (NativeMenuItemToggleType.CheckBox, true);
            sub.Add(item);
        }
        return new NativeMenuItem($"Weeks in {year}") { Menu = sub };
    }

    /// <summary>"Q4 2026 · Sprint 21, day 10 of 10", as on the Mac: no line without quarters, and the sprint part only when
    /// <see cref="Sprints.Info"/> finds one (it returns null for unusable sprint settings).</summary>
    static string? PlanningLine(WeekContext ctx, AppSettings s)
    {
        if (Quarters.Info(ctx.Today, s.Quarter, ctx.Cal) is not { } q) return null;
        return Sprints.Info(ctx.Today, s.Sprint, ctx.Cal, ctx.Holidays) is { } sp
            ? $"{q.Label} · {s.Sprint.Name} {sp.Number}, day {Math.Max(sp.Workday, 1)} of {sp.WorkdaysTotal}"
            : q.Label;
    }

    /// <summary>The tray's items as Avalonia controls, for the widget's right-click menu.</summary>
    internal static List<Control> ToMenuItems(IEnumerable<NativeMenuItemBase> items) => items.Select(i => i switch
    {
        NativeMenuItemSeparator => (Control)new Separator(), // before NativeMenuItem: separators are items with the header "-"
        NativeMenuItem n => new MenuItem
        {
            Header = n.Header, IsEnabled = n.IsEnabled, Command = n.Command, IsChecked = n.IsChecked,
            ToggleType = Enum.Parse<MenuItemToggleType>(n.ToggleType.ToString()),
            ItemsSource = n.Menu is { } sub ? ToMenuItems(sub.Items) : null,
        },
        _ => throw new NotSupportedException(),
    }).ToList();

    static NativeMenuItem Label(string text) => new(text) { IsEnabled = false };
    static NativeMenuItem Item(string text, Action action) => new(text) { Command = new RelayCommand(action) };

    NativeMenuItem Choice(string text, bool isChecked, Action change, NativeMenuItemToggleType type = NativeMenuItemToggleType.Radio)
    {
        var item = Item(text, () => { change(); state.Save(); });
        (item.ToggleType, item.IsChecked) = (type, isChecked);
        return item;
    }

    static StringBuilder Describe(IEnumerable<NativeMenuItemBase> items, StringBuilder sb)
    {
        foreach (var item in items.OfType<NativeMenuItem>()) // separators are items with the header "-"
        {
            sb.Append(item.Header).Append(item.IsEnabled ? "" : " [off]").Append(item.IsChecked ? " [x]" : "").Append('\n');
            if (item.Menu is { } sub) Describe(sub.Items, sb.Append("{\n")).Append("}\n");
        }
        return sb;
    }

    public void Dispose()
    {
        disposed = true;
        state.Changed -= Refresh;
        if (platform is not null) platform.ColorValuesChanged -= OnColorsChanged;
        icon.Dispose();
    }
}
