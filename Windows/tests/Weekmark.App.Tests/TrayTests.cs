using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
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
    public void MenuListsTheWeeksOfThisYearAndNext()
    {
        var (_, _, tray) = Demo();
        var top = Items(tray.Menu);
        Assert.Equal(["-", "Quick Finder…   Ctrl+Alt+W", "Year at a Glance…", "Weeks in 2026", "Weeks in 2027", "-", "Hide Widget", "Position", "Size", "-", "Settings…", "Quit Weekmark"],
            top.Skip(3).Select(i => i.Header));
        var weeks = Items(Item(tray.Menu, "Weeks in 2026").Menu!);
        Assert.Equal("January", weeks[0].Header);
        Assert.False(weeks[0].IsEnabled);
        Assert.Equal(53, weeks.Count(i => i.Header!.StartsWith("CW")));
        Assert.Equal(12, weeks.Count(i => !i.IsEnabled && i.Header != "-"));
        var current = Item(Item(tray.Menu, "Weeks in 2026").Menu!, "CW 43    19–25 Oct");
        Assert.True(current.IsChecked);
        Assert.Contains(weeks, i => i.Header == "CW 46    9–15 Nov   ⚑ Code freeze");
        Assert.Contains(weeks, i => i.Header == "CW 52    21–27 Dec   · 4d");
        Assert.False(weeks.Single(i => i.Header!.StartsWith("CW 44")).IsChecked);
        Assert.Equal(52, Items(Item(tray.Menu, "Weeks in 2027").Menu!).Count(i => i.Header!.StartsWith("CW")));
    }

    [AvaloniaFact]
    public void ChoosingAWeekBringsTheWidgetForwardAndPinsIt()
    {
        var (_, widget, tray) = Demo();
        widget.ViewModel.Query = "50";
        Item(Item(tray.Menu, "Weeks in 2026").Menu!, "CW 46    9–15 Nov   ⚑ Code freeze").Command!.Execute(null);
        Assert.True(widget.Topmost);
        Assert.Equal(new WeekRef(2026, 46), widget.ViewModel.Pinned);
        Assert.Equal("", widget.ViewModel.Query);
    }

    [AvaloniaFact]
    public void AddMilestoneItemAppearsOnlyForAWeek()
    {
        var (state, _, tray) = Demo();
        var snap = new WeekSnapshot(state.Context.Now, state.Context.Cal, state.Context.Fmt);
        var with = tray.BuildMenu(snap, new WeekRef(2026, 46)).OfType<NativeMenuItem>().Select(i => i.Header).ToList();
        Assert.Equal(["-", "Add Milestone in CW46…", "-", "Quick Finder…   Ctrl+Alt+W"], with.Skip(3).Take(4));
        WeekRef? asked = null;
        tray.AddMilestoneRequested += w => asked = w;
        tray.BuildMenu(snap, new WeekRef(2026, 46)).OfType<NativeMenuItem>().Single(i => i.Header == "Add Milestone in CW46…").Command!.Execute(null);
        Assert.Equal(new WeekRef(2026, 46), asked);
        Assert.DoesNotContain(tray.BuildMenu(snap).OfType<NativeMenuItem>(), i => i.Header!.StartsWith("Add Milestone"));
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
        Assert.Equal(["-", "Quick Finder…   Ctrl+Alt+W", "Year at a Glance…", "Weeks in 2026", "Weeks in 2027", "-", "Hide Widget", "Position", "Size", "-", "Settings…", "Quit Weekmark"], top.Skip(3).Select(i => i.Header));
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

    sealed class RecordingLayering : IWindowLayering
    {
        public List<WindowLayer> Applied { get; } = [];
        public void Apply(Window w, WindowLayer layer) => Applied.Add(layer);
        public void HideFromTaskSwitcher(Window w) { }
        public List<(bool Allow, bool HandBack)> Activation { get; } = [];
        public void AllowActivation(Window w, bool allow, bool handBack = true) => Activation.Add((allow, handBack));
    }

    [AvaloniaFact]
    public void ShowWidgetPutsItBackOnItsLayer()
    {
        CultureInfo.CurrentCulture = new CultureInfo("en-GB");
        var state = new AppState(DemoData.Create(DateOnly.FromDateTime(Now)), null, () => Now);
        var layering = new RecordingLayering();
        var widget = new WidgetWindow(state, layering);
        widget.Show();
        var tray = new TrayController(state, widget);

        Item(tray.Menu, "Hide Widget").Command!.Execute(null);
        layering.Applied.Clear();
        Item(tray.Menu, "Show Widget").Command!.Execute(null);
        Assert.Equal([WindowLayer.Desktop], layering.Applied);
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

    static List<MenuItem> MenuItems(ContextMenu m) => m.Items.OfType<MenuItem>().ToList(); // separators are Separator controls

    [AvaloniaFact]
    public void RightClickShowsTheTrayMenuOnTheWidget()
    {
        var (_, widget, _) = Demo();
        var menu = widget.ShowContextMenu()!;
        var items = MenuItems(menu);
        Assert.Equal("Copy “CW43 (19–25 Oct)”", (string?)items[2].Header);
        Assert.DoesNotContain(items, i => ((string?)i.Header)!.StartsWith("Add Milestone"));
        var size = items.Single(i => (string?)i.Header == "Size");
        Assert.True(size.Items.OfType<MenuItem>().Single(i => (string?)i.Header == "Medium").IsChecked);
        Assert.False(items[0].IsEnabled);
        Assert.Contains(menu.Items, x => x is Separator);
    }

    [AvaloniaFact]
    public void RightClickOnAWeekOffersAddMilestoneForThatWeek()
    {
        var (_, widget, tray) = Demo();
        WeekRef? asked = null;
        tray.AddMilestoneRequested += w => asked = w;
        widget.ViewModel.Hover = new WeekRef(2026, 45);
        var add = MenuItems(widget.ShowContextMenu()!).Single(i => (string?)i.Header == "Add Milestone in CW45…");
        widget.ViewModel.Hover = null; // the pointer left the row before the click
        add.Command!.Execute(null);
        Assert.Equal(new WeekRef(2026, 45), asked);
    }

    [AvaloniaFact]
    public void UnderscoresInMenuTextShowAsTyped()
    {
        var (_, widget, _) = Demo(s =>
        {
            var i = s.Milestones.FindIndex(m => m.Name == "Code freeze");
            s.Milestones[i] = s.Milestones[i] with { Name = "v2_launch" };
        });
        var weeks = MenuItems(widget.ShowContextMenu()!).Single(i => (string?)i.Header == "Weeks in 2026");
        Assert.Contains(weeks.Items.OfType<MenuItem>(), i => (string?)i.Header == "CW 46    9–15 Nov   ⚑ v2__launch"); // "_" alone marks an access key
    }

    [AvaloniaFact]
    public void RightClickOnAWeekRowOpensTheMenuWithoutPinning()
    {
        var (_, widget, tray) = Demo();
        widget.CaptureRenderedFrame();
        var handled = new List<bool>();
        widget.AddHandler(Control.ContextRequestedEvent, (_, e) => handled.Add(e.Handled), RoutingStrategies.Bubble, handledEventsToo: true);
        var row = widget.GetVisualDescendants().OfType<Panel>().First(p => p.Classes.Contains("row") && p.DataContext is MonthRowModel { IsSpacer: false });
        var pt = row.TranslatePoint(new Point(40, 5), widget)!.Value;
        widget.MouseMove(pt);
        Assert.NotNull(widget.ViewModel.Hover);
        widget.MouseDown(pt, MouseButton.Right);
        widget.MouseUp(pt, MouseButton.Right);
        Assert.Null(widget.ViewModel.Pinned);
        Assert.Equal([true], handled); // the widget's menu took the request
        tray.Dispose();
    }

    [AvaloniaFact]
    public void ChoosingAWeekFromTheWidgetMenuKeepsItInFront()
    {
        CultureInfo.CurrentCulture = new CultureInfo("en-GB");
        var state = new AppState(DemoData.Create(DateOnly.FromDateTime(Now)), null, () => Now);
        var layering = new RecordingLayering();
        var widget = new WidgetWindow(state, layering);
        widget.Show();
        var tray = new TrayController(state, widget);
        widget.CaptureRenderedFrame();
        var menu = widget.ShowContextMenu()!;
        Dispatcher.UIThread.RunJobs();
        var weeks = MenuItems(menu).Single(i => (string?)i.Header == "Weeks in 2026");
        weeks.IsSubMenuOpen = true;
        Dispatcher.UIThread.RunJobs();
        var week = weeks.Items.OfType<MenuItem>().First(i => i.IsEnabled && i.Command is not null);
        week.Focus();
        week.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = week });
        Dispatcher.UIThread.RunJobs();
        Assert.False(menu.IsOpen);
        Assert.Equal((false, false), layering.Activation.Last()); // the foreground stays with the widget
        Assert.Equal(WindowLayer.Floating, layering.Applied.Last()); // still floating
        tray.Dispose();
    }

    [AvaloniaFact]
    public void MenuHasQuickFinderBeforeYear()
    {
        var (_, _, tray) = Demo();
        var items = Items(tray.Menu);
        var year = items.FindIndex(i => i.Header == "Year at a Glance…");
        Assert.Equal("Quick Finder…   Ctrl+Alt+W", items[year - 1].Header);
        var raised = 0;
        tray.FinderRequested += () => raised++;
        items[year - 1].Command!.Execute(null);
        Assert.Equal(1, raised);

        var (_, _, off) = Demo(s => s.Hotkey = HotkeyPreset.Off);
        Assert.Contains(Items(off.Menu), i => i.Header == "Quick Finder…");
    }

    [AvaloniaFact]
    public void HotKeyMessageRaisesPressed()
    {
        var (_, widget, _) = Demo();
        var pressed = 0;
        widget.HotKeyPressed += () => pressed++;
        var handled = false;
        widget.WndProc(0, 0x0312, 2, 0, ref handled);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(0, pressed);
        widget.WndProc(0, 0x0312, 1, 0, ref handled);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, pressed);
    }

    [Fact]
    public void FindFlag()
    {
        Assert.Equal("46", Options.Parse(["--find", "46"]).Find);
        Assert.Equal("", Options.Parse(["--find"]).Find);
        Assert.Equal("", Options.Parse(["--find", "--demo-data"]).Find);
        Assert.Null(Options.Parse([]).Find);
    }
}
