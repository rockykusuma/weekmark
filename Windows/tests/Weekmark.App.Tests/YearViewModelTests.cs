using Avalonia.Headless.XUnit;
using Weekmark.App.Year;
using Weekmark.Core;

namespace Weekmark.App.Tests;

public sealed class YearViewModelTests
{
    DateTime now = new(2026, 10, 25, 14, 0, 0); // Sunday, ISO CW43

    (YearViewModel Vm, AppState State) Make(Action<AppSettings>? edit = null)
    {
        var s = DemoData.Create(DateOnly.FromDateTime(now));
        edit?.Invoke(s);
        var state = new AppState(s, null, () => now);
        return (new YearViewModel(state), state);
    }

    [AvaloniaFact]
    public void OpensOnThisYearWithTheCurrentWeek()
    {
        var (vm, _) = Make();
        Assert.Equal(2026, vm.Year);
        Assert.Equal("2026", vm.YearText);
        Assert.Equal("53 calendar weeks · week 43 now, 10 to go", vm.Subtitle);
        Assert.False(vm.ShowThisYear);
        Assert.Equal(12, vm.Months.Count);
        Assert.Equal("January", vm.Months[0].Title);
        Assert.Equal([9], vm.Months.Select((m, i) => (m, i)).Where(t => t.m.IsCurrentMonth).Select(t => t.i));
        Assert.StartsWith("CW43 · ", vm.DetailsTitle);
        Assert.True(vm.DetailsCurrent);
    }

    [AvaloniaFact]
    public void EveryCardHasSixRows()
    {
        var (vm, _) = Make();
        Assert.All(vm.Months, m => Assert.Equal(6, m.Rows.Count));
        Assert.True(vm.Months[1].Rows[5].IsSpacer);
        Assert.Equal(new WeekRef(2026, 1), vm.Months[0].Rows[0].Week);
        Assert.Equal(new WeekRef(2026, 53), vm.Months[11].Rows.Last(r => !r.IsSpacer).Week);
    }

    [AvaloniaFact]
    public void StepsYearsAndComesBack()
    {
        var (vm, _) = Make();
        vm.NextYearCommand.Execute(null);
        Assert.Equal(2027, vm.Year);
        Assert.Equal("52 calendar weeks", vm.Subtitle);
        Assert.True(vm.ShowThisYear);
        vm.ThisYearCommand.Execute(null);
        Assert.Equal(2026, vm.Year);
    }

    [AvaloniaFact]
    public void YearStaysWithinTheSupportedRange()
    {
        var (vm, _) = Make();
        vm.Year = 9998;
        vm.NextYearCommand.Execute(null);
        Assert.Equal(9998, vm.Year);
        Assert.Equal(12, vm.Months.Count);
        vm.Year = 2;
        vm.PreviousYearCommand.Execute(null);
        Assert.Equal(2, vm.Year);
        vm.Year = 0;
        Assert.Equal(2, vm.Year);
    }

    [AvaloniaFact]
    public void HoverThenPinThenCurrent()
    {
        var (vm, _) = Make();
        vm.Hover = new WeekRef(2026, 50);
        Assert.StartsWith("CW50 · ", vm.DetailsTitle);
        vm.Pin(new WeekRef(2026, 49));
        vm.Hover = null;
        Assert.StartsWith("CW49 · ", vm.DetailsTitle);
        Assert.True(vm.DetailsPinned);
        vm.Pin(new WeekRef(2026, 49));
        Assert.Null(vm.Pinned);
        Assert.StartsWith("CW43 · ", vm.DetailsTitle);
    }

    [AvaloniaFact]
    public void PinMarksTheWeekInEveryCard()
    {
        var (vm, _) = Make();
        var months = vm.Months;
        vm.Pin(new WeekRef(2026, 5));
        var w = new WeekRef(2026, 5);
        Assert.True(vm.Months[0].Rows.Single(r => r.Week == w).IsPinned);
        Assert.True(vm.Months[1].Rows.Single(r => r.Week == w).IsPinned);
        Assert.Same(months, vm.Months);
    }

    [AvaloniaFact]
    public void CopyFollowsTheShownWeek()
    {
        var (vm, _) = Make();
        Assert.StartsWith("CW43 (", vm.CopyText);
        Assert.Equal($"Copy “{vm.CopyText}”", vm.CopyLabel);
        vm.NextYearCommand.Execute(null);
        vm.Hover = new WeekRef(2027, 2);
        Assert.StartsWith("CW2 2027 (", vm.CopyText);
    }

    [AvaloniaFact]
    public void NumberingChangeClearsThePin()
    {
        var (vm, state) = Make();
        vm.Pin(new WeekRef(2026, 5));
        state.Settings.Numbering = WeekNumbering.Us;
        state.Save();
        Assert.Null(vm.Pinned);
        Assert.DoesNotContain(vm.Months.SelectMany(m => m.Rows), r => r.IsPinned);
    }

    [AvaloniaFact]
    public void ClockTickKeepsCardsNewDayRebuilds()
    {
        var (vm, state) = Make();
        var months = vm.Months;
        now = now.AddMinutes(1);
        state.Tick();
        Assert.Same(months, vm.Months);
        now = now.AddDays(1);
        state.Tick();
        Assert.NotSame(months, vm.Months);
    }

    [Theory]
    [InlineData(1060, 4)]
    [InlineData(520, 2)]
    [InlineData(300, 1)]
    [InlineData(5000, 12)]
    public void ColumnsFollowTheWidth(double width, int columns) => Assert.Equal(columns, YearViewModel.ColumnsFor(width));
}
