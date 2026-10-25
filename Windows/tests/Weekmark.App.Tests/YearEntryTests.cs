using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Weekmark.App.Platform;
using Weekmark.App.Tray;
using Weekmark.App.Widget;
using Weekmark.Core;

namespace Weekmark.App.Tests;

public sealed class YearEntryTests
{
    static readonly DateTime Now = new(2026, 10, 25, 14, 0, 0);

    static AppState Demo() => new(DemoData.Create(DateOnly.FromDateTime(Now)), null, () => Now);

    [Fact]
    public void YearFlag()
    {
        Assert.True(Options.Parse(["--year"]).Year);
        Assert.False(Options.Parse([]).Year);
    }

    [Fact]
    public void ShowYearCommandRaisesYearRequested()
    {
        using var vm = new WidgetViewModel(Demo());
        var raised = 0;
        vm.YearRequested += () => raised++;
        vm.ShowYearCommand.Execute(null);
        Assert.Equal(1, raised);
    }

    [AvaloniaFact]
    public void WidgetHasYearButtonAndForwardsTheRequest()
    {
        var widget = new WidgetWindow(Demo(), new FallbackLayering());
        widget.Show();
        var button = widget.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Year at a Glance");
        var raised = 0;
        widget.YearRequested += () => raised++;
        button.Command!.Execute(null);
        Assert.Equal(1, raised);
    }

    [AvaloniaFact]
    public void TrayItemFollowsCopyAndRaisesYearRequested()
    {
        var state = Demo();
        var widget = new WidgetWindow(state, new FallbackLayering());
        widget.Show();
        var tray = new TrayController(state, widget);
        var items = tray.Menu.Items.OfType<NativeMenuItem>().ToList();
        Assert.StartsWith("Copy", items[2].Header);
        Assert.Equal("Year at a Glance…", items[3].Header);
        var raised = 0;
        tray.YearRequested += () => raised++;
        items[3].Command!.Execute(null);
        Assert.Equal(1, raised);
    }
}
