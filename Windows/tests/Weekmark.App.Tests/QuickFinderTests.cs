using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Weekmark.App.Finder;
using Weekmark.Core;

namespace Weekmark.App.Tests;

public sealed class QuickFinderTests
{
    static readonly DateTime Now = new(2026, 10, 25, 14, 0, 0); // Sunday, CW43

    static AppState Demo()
    {
        CultureInfo.CurrentCulture = new CultureInfo("en-GB");
        return new AppState(DemoData.Create(DateOnly.FromDateTime(Now)), null, () => Now);
    }

    static QuickFinderWindow Shown(AppState state, string query = "")
    {
        var w = new QuickFinderWindow(state);
        w.ShowFinder(query);
        w.UpdateLayout();
        return w;
    }

    static void Press(QuickFinderWindow w, Key key, RawInputModifiers mods = RawInputModifiers.None) =>
        w.KeyPress(key, mods, PhysicalKey.None, null);

    [AvaloniaFact]
    public void EmptyQueryShowsOverview()
    {
        var state = Demo();
        var vm = new QuickFinderViewModel(state);
        Assert.Equal(QueryKind.Week, vm.Results[0].Kind);
        Assert.Equal(state.Context.Current, vm.Results[0].Week);
        Assert.Equal("CW43", vm.Badge);
    }

    [AvaloniaFact]
    public void QueryListsResults()
    {
        var state = Demo();
        var vm = new QuickFinderViewModel(state) { Selection = 1 };
        vm.Query = "46";
        var r = Assert.Single(vm.Results);
        Assert.Equal(QueryKind.Week, r.Kind);
        Assert.Equal(new WeekRef(state.Context.Current.Year, 46), r.Week);
        Assert.Equal(0, vm.Selection);
    }

    [AvaloniaFact]
    public void MoveWraps()
    {
        var vm = new QuickFinderViewModel(Demo());
        Assert.True(vm.Results.Count >= 2);
        vm.Move(-1);
        Assert.Equal(vm.Results.Count - 1, vm.Selection);
        vm.Move(1);
        Assert.Equal(0, vm.Selection);
    }

    [AvaloniaFact]
    public async Task KeysDriveTheFinder()
    {
        var w = Shown(Demo());
        Assert.True(w.Field.IsFocused);
        Press(w, Key.Down);
        Assert.Equal(1, w.ViewModel.Selection);

        w.KeyTextInput("46");
        Assert.Equal("46", w.ViewModel.Query);
        var r = Assert.Single(w.ViewModel.Results);
        Press(w, Key.Enter);
        Assert.Equal(r.CopyText, await w.Clipboard!.TryGetTextAsync());
        Assert.Equal(r.CopyText, w.ViewModel.Copied);

        Press(w, Key.Escape);
        Assert.False(w.IsVisible);
    }

    [AvaloniaFact]
    public void CtrlEnterShowsInWidget()
    {
        var state = Demo();
        var w = Shown(state, "46");
        WeekRef? got = null;
        w.ShowInWidgetRequested += r => got = r;
        Press(w, Key.Enter, RawInputModifiers.Control);
        Assert.Equal(new WeekRef(state.Context.Current.Year, 46), got);
        Assert.False(w.IsVisible);
    }

    [AvaloniaFact]
    public async Task CtrlCWithEmptyQueryCopies()
    {
        var w = Shown(Demo());
        Press(w, Key.C, RawInputModifiers.Control);
        Assert.Equal(w.ViewModel.Results[0].CopyText, await w.Clipboard!.TryGetTextAsync());
        Assert.NotNull(w.ViewModel.Copied);
    }

    [AvaloniaFact]
    public void ToggleAndReopenStartFresh()
    {
        var w = Shown(Demo());
        w.KeyTextInput("46");
        Press(w, Key.Enter);
        w.Toggle();
        Assert.False(w.IsVisible);
        w.Toggle();
        Assert.True(w.IsVisible);
        Assert.Equal("", w.ViewModel.Query);
        Assert.Equal("", w.Field.Text ?? "");
        Assert.Equal(0, w.ViewModel.Selection);
        Assert.Null(w.ViewModel.Copied);
        Assert.True(w.Field.IsFocused);
    }

    [AvaloniaFact]
    public async Task EnterOnErrorDoesNothing()
    {
        var w = Shown(Demo());
        await w.Clipboard!.SetTextAsync("before");
        w.KeyTextInput("zzzz");
        Assert.Equal(QueryKind.Error, w.ViewModel.Selected!.Kind);
        Press(w, Key.Enter);
        Assert.Equal("before", await w.Clipboard.TryGetTextAsync());
        Assert.Null(w.ViewModel.Copied);
        Assert.True(w.IsVisible);
    }

    [AvaloniaFact]
    public void CtrlEnterWithoutWeekDoesNothing()
    {
        var w = Shown(Demo());
        w.KeyTextInput("zzzz");
        var raised = false;
        w.ShowInWidgetRequested += _ => raised = true;
        Press(w, Key.Enter, RawInputModifiers.Control);
        Assert.False(raised);
        Assert.True(w.IsVisible);
    }

    [AvaloniaFact]
    public async Task TypingAfterCopyCancelsClose()
    {
        var w = Shown(Demo(), "46");
        Press(w, Key.Enter);
        Assert.NotNull(w.ViewModel.Copied);
        w.KeyTextInput("1");
        Assert.Null(w.ViewModel.Copied);
        await Task.Delay(1000);
        Dispatcher.UIThread.RunJobs();
        Assert.True(w.IsVisible);
    }

    [AvaloniaFact]
    public async Task CopyClosesAfterTheDelay()
    {
        var w = Shown(Demo(), "46");
        Press(w, Key.Enter);
        await Task.Delay(1000);
        Dispatcher.UIThread.RunJobs();
        Assert.False(w.IsVisible);
    }

    [AvaloniaFact]
    public void ClickKeepsFocusInField()
    {
        var w = Shown(Demo());
        var row = w.GetVisualDescendants().OfType<ListBoxItem>().ElementAt(1);
        var p = row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), w)!.Value;
        w.MouseDown(p, MouseButton.Left);
        w.MouseUp(p, MouseButton.Left);
        Assert.Equal(1, w.ViewModel.Selection);
        Assert.True(w.Field.IsFocused);
    }

    [AvaloniaFact]
    public void FinderPositionCentresNearTop()
    {
        Assert.Equal(new PixelPoint(560, 291), QuickFinderWindow.FinderPosition(new PixelRect(0, 0, 1920, 1040), 1.25));
        Assert.Equal(new PixelPoint(2480, 291), QuickFinderWindow.FinderPosition(new PixelRect(1920, 0, 1920, 1040), 1.25));
    }
}
