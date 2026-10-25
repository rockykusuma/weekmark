using Avalonia.Headless.XUnit;
using Weekmark.App.Widget;
using Weekmark.Core;

namespace Weekmark.App.Tests;

public sealed class WeekRefreshTests
{
    [AvaloniaFact]
    public void TickAtMidnightMovesToTheNewWeekAndIntraDayTicksOnlyMoveProgress()
    {
        // Sunday 31 May 2026 (ISO week 22) → Monday 1 June 2026 (week 23, and a new month).
        var now = new DateTime(2026, 5, 31, 14, 0, 0);
        var state = new AppState(DemoData.Create(DateOnly.FromDateTime(now)), null, () => now);
        var vm = new WidgetViewModel(state);
        Assert.Equal("22", vm.Header.Week);
        Assert.Equal("May 2026", vm.MonthTitle);

        // Intra-day tick: only the week progress moves; rows and hover survive.
        var hovered = new WeekRef(2026, 21);
        vm.Hover = hovered;
        var (header, days, rows, progress) = (vm.Header, vm.Days, vm.Rows, vm.Progress);
        now = now.AddHours(3);
        state.Tick();
        Assert.NotEqual(progress, vm.Progress);
        Assert.Same(header, vm.Header);
        Assert.Same(days, vm.Days);
        Assert.Same(rows, vm.Rows);
        Assert.Equal(hovered, vm.Hover);
        Assert.True(vm.Rows.Single(r => r.Week == hovered).IsHover);

        // Past midnight (what the midnight timer, resume and time-change ticks do): a new week and month.
        now = new DateTime(2026, 6, 1, 0, 0, 30);
        state.Tick();
        Assert.Equal("23", vm.Header.Week);
        Assert.Equal("June 2026", vm.MonthTitle);
        Assert.Equal("1", vm.Days[0].Number);
        Assert.True(vm.Days[0].IsToday);
        Assert.Contains(vm.Rows, r => r.IsCurrent && r.Week == new WeekRef(2026, 23));
    }
}
