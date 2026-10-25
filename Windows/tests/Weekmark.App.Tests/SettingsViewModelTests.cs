using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Weekmark.App.Onboarding;
using Weekmark.App.Settings;
using Weekmark.Core;

namespace Weekmark.App.Tests;

public sealed class SettingsViewModelTests : IDisposable
{
    static readonly DateTime Now = new(2026, 10, 25, 14, 0, 0);
    readonly string dir = Directory.CreateTempSubdirectory("wm-settings-").FullName;
    string SettingsPath => Path.Combine(dir, "settings.json");

    const string Ics = """
        BEGIN:VCALENDAR
        BEGIN:VEVENT
        DTSTART;VALUE=DATE:20261224
        DTEND;VALUE=DATE:20261226
        SUMMARY:Winter break
        END:VEVENT
        END:VCALENDAR
        """;

    public void Dispose() => Directory.Delete(dir, recursive: true);

    (SettingsViewModel vm, AppState state, SettingsStore store) Real()
    {
        var store = new SettingsStore(SettingsPath);
        var state = new AppState(store.Load(), store, () => Now);
        return (new SettingsViewModel(state), state, store);
    }

    static SettingsViewModel Demo(out AppState state, Action<AppSettings>? edit = null)
    {
        var s = DemoData.Create(DateOnly.FromDateTime(Now));
        edit?.Invoke(s);
        state = new AppState(s, null, () => Now);
        return new SettingsViewModel(state);
    }

    [AvaloniaFact]
    public void EditsSaveImmediately()
    {
        var (vm, state, store) = Real();
        AppSettings? saved = null;
        store.Saved += s => saved = s;

        vm.SizeIndex = (int)WidgetSize.Xlarge;

        Assert.Equal(WidgetSize.Xlarge, saved?.Size);
        Assert.Equal(WidgetSize.Xlarge, new SettingsStore(SettingsPath).Load().Size);

        // A change made elsewhere (the tray's Size menu) refreshes the window; a clock tick doesn't.
        var notified = 0;
        vm.PropertyChanged += (_, _) => notified++;
        state.Tick();
        Assert.Equal(0, notified);
        state.Settings.Size = WidgetSize.Small;
        state.Save();
        Assert.True(notified > 0);
        Assert.Equal((int)WidgetSize.Small, vm.SizeIndex);
    }

    [AvaloniaFact]
    public void ReadOnlyStoreShowsAWarningInsteadOfSaving()
    {
        // An unreadable file (locked, no permission) makes the store read-only; a directory in its place does the same.
        Directory.CreateDirectory(SettingsPath);
        var (vm, state, _) = Real();
        Assert.Contains("couldn't read its settings file", vm.SaveWarning);

        vm.SizeIndex = (int)WidgetSize.Large;

        Assert.Equal(WidgetSize.Large, state.Settings.Size); // kept in memory for this session
        Assert.True(Directory.Exists(SettingsPath));
    }

    [AvaloniaFact]
    public void ImportIcsAddsHolidays()
    {
        var vm = Demo(out var state);

        var message = vm.ImportIcs(Ics, "Company");

        var added = state.Settings.CustomHolidays.Where(h => h.Source == "Company").ToList();
        Assert.Equal(2, added.Count);
        Assert.Equal([new Day(2026, 12, 24), new Day(2026, 12, 25)], added.Select(h => h.Day));
        Assert.Equal("Imported 2 holidays from Company", message);
        Assert.Contains(vm.Sources, s => s.Name == "Company" && s.Count == "2 days");

        // Importing the same file again replaces its days instead of doubling them.
        vm.ImportIcs(Ics, "Company");
        Assert.Equal(2, state.Settings.CustomHolidays.Count(h => h.Source == "Company"));

        Assert.Equal("No holidays found in Empty", vm.ImportIcs("BEGIN:VCALENDAR\nEND:VCALENDAR", "Empty"));
        Assert.DoesNotContain(vm.Sources, s => s.Name == "Empty");
    }

    [AvaloniaFact]
    public void RemoveSourceRemovesItsHolidays()
    {
        var vm = Demo(out var state);
        vm.ImportIcs(Ics, "Company");
        vm.ImportIcs(Ics.Replace("20261224", "20260101").Replace("20261226", "20260102"), "Team");

        vm.Sources.Single(s => s.Name == "Company").Remove.Execute(null);

        Assert.DoesNotContain(state.Settings.CustomHolidays, h => h.Source == "Company");
        Assert.Single(state.Settings.CustomHolidays, h => h.Source == "Team");
        Assert.Equal(["Team"], vm.Sources.Select(s => s.Name));
    }

    [AvaloniaFact]
    public void MilestoneWeekValidation()
    {
        var vm = Demo(out var state);
        var before = state.Settings.Milestones.Count;
        vm.AddMilestone.Execute(null);
        var d = vm.Draft!;
        d.Name = "Launch";

        d.Year = 2027;
        d.Week = 53;
        Assert.Equal("2027 has only 52 weeks", d.Error);
        Assert.False(d.CanSave);
        vm.SaveDraft.Execute(null);
        Assert.Equal(before, state.Settings.Milestones.Count);

        d.Year = 2026;
        Assert.Null(d.Error);
        Assert.Equal(state.Context.Fmt.LongRange(new WeekRef(2026, 53)), d.Dates); // 28 Dec 2026 – 3 Jan 2027, in the user's order
        Assert.Contains("Jan", d.Dates);
        vm.SaveDraft.Execute(null);
        Assert.Null(vm.Draft);
        Assert.Contains(state.Settings.Milestones, m => m.Name == "Launch" && m.Week == new WeekRef(2026, 53));

        // A stored week that doesn't exist is shown, not thrown on; editing it asks for a valid week.
        state.Settings.Milestones.Add(new Milestone(Guid.NewGuid(), "Broken", new WeekRef(2026, 99)));
        vm.Refresh();
        var broken = vm.Milestones.Single(m => m.Name == "Broken");
        Assert.Equal("CW99 2026 · this week doesn't exist", broken.Detail);
        broken.Edit.Execute(null);
        Assert.Equal("2026 has only 53 weeks", vm.Draft!.Error);
    }

    [AvaloniaFact]
    public void EditAndDeleteMilestone()
    {
        var vm = Demo(out var state);
        var first = vm.Milestones[0];
        first.Edit.Execute(null);
        vm.Draft!.Name = "Renamed";
        vm.Draft.ColorIndex = (int)MilestoneColor.Green;
        vm.SaveDraft.Execute(null);
        Assert.Contains(state.Settings.Milestones, m => m.Name == "Renamed" && m.Color == MilestoneColor.Green);

        var count = state.Settings.Milestones.Count;
        vm.Milestones.Single(m => m.Name == "Renamed").Delete.Execute(null);
        Assert.Equal(count - 1, state.Settings.Milestones.Count);
    }

    [AvaloniaFact]
    public void PlanningEditsAreClamped()
    {
        var vm = Demo(out var state);
        vm.SprintLength = 99;
        vm.SprintsPerPI = 500;
        vm.FiscalMonthIndex = 12; // one past December
        Assert.Equal(52, state.Settings.Sprint.LengthWeeks);
        Assert.Equal(100, state.Settings.Sprint.SprintsPerPI);
        Assert.Equal(12, state.Settings.Quarter.FiscalStartMonth);

        // A bad stored anchor shows as no date and leaves sprints off the preview without throwing.
        state.Settings.Sprint = state.Settings.Sprint with { Anchor = new Day(2026, 2, 30) };
        vm.Refresh();
        Assert.Null(vm.SprintAnchor);
        Assert.StartsWith("Pick any past sprint", vm.SprintPreview);
    }

    [AvaloniaTheory]
    [InlineData("US", WeekNumbering.Us, HolidayRegion.Us)]
    [InlineData("DE", WeekNumbering.Iso, HolidayRegion.De)]
    [InlineData("JP", WeekNumbering.Iso, null)]
    public void FirstRunDefaults(string region, WeekNumbering numbering, HolidayRegion? holidays)
    {
        var state = new AppState(new AppSettings(), null, () => Now);
        var vm = new WelcomeViewModel(state, region);
        Assert.Equal(numbering, vm.Numbering);
        Assert.Equal(holidays, vm.Region);
        Assert.True(vm.StartWithWindows);

        vm.Finish();
        Assert.True(state.Settings.OnboardingDone);
        Assert.Equal(numbering, state.Settings.Numbering);
        Assert.Equal(holidays is { } h ? [h] : [], state.Settings.Regions);
    }

    [AvaloniaFact]
    public void SettingsWindowRenders()
    {
        var renders = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "renders")).FullName;
        var vm = Demo(out var state, s => s.Milestones.Add(new Milestone(Guid.NewGuid(), "Bad week", new WeekRef(2027, 53))));
        var w = new SettingsWindow(vm);
        w.Show();
        var tabs = w.GetVisualDescendants().OfType<TabControl>().Single();
        Assert.Equal(["General", "Widget", "Planning", "Holidays", "Milestones", "About"],
            tabs.Items.Cast<TabItem>().Select(t => (string)t.Header!));
        for (var i = 0; i < tabs.ItemCount; i++)
        {
            tabs.SelectedIndex = i;
            var frame = w.CaptureRenderedFrame();
            Assert.NotNull(frame);
            frame.Save(Path.Combine(renders, $"settings-{i}.png"));
        }

        // Controls write back through their bindings (NumericUpDown's decimal to an int, the date picker to a Day).
        tabs.SelectedIndex = 2;
        w.CaptureRenderedFrame();
        w.GetVisualDescendants().OfType<NumericUpDown>().First().Value = 3;
        w.GetVisualDescendants().OfType<CalendarDatePicker>().Single().SelectedDate = new DateTime(2026, 3, 2);
        Assert.Equal(3, state.Settings.Sprint.LengthWeeks);
        Assert.Equal(new Day(2026, 3, 2), state.Settings.Sprint.Anchor);

        // The milestone editor with a rejected week, and an imported calendar in the holiday list.
        vm.ImportIcs(Ics, "Company");
        tabs.SelectedIndex = 3;
        w.CaptureRenderedFrame()!.Save(Path.Combine(renders, "settings-holidays-imported.png"));
        vm.AddMilestone.Execute(null);
        vm.Draft!.Year = 2027;
        vm.Draft.Week = 53;
        tabs.SelectedIndex = 4;
        w.CaptureRenderedFrame()!.Save(Path.Combine(renders, "settings-milestone-editor.png"));
        var editor = w.GetVisualDescendants().OfType<MilestoneEditor>().Single();
        Assert.True(editor.IsEffectivelyVisible);
        var texts = editor.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.True(texts.Contains("2027 has only 52 weeks"), string.Join(" | ", texts));
        w.Close();

        var welcome = new WelcomeWindow(new WelcomeViewModel(state, "GB"));
        welcome.Show();
        Assert.NotNull(welcome.CaptureRenderedFrame());
        welcome.Close();
    }
}
