# Windows Menus and Settings Extras Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Windows parity item 2: the widget's right-click menu (the tray menu plus "Add Milestone in CW…"), "Weeks in YYYY" submenus, manual company holidays with "Remove All" and "Coming up", and "Show Welcome Screen Again".

**Architecture:** `TrayController.BuildMenu` stays the single menu definition. It gains the "Weeks in" submenus and an optional "Add Milestone in CW…" item. The widget shows the same items as an Avalonia `ContextMenu`, converted from the `NativeMenu` items, through a `MenuFor` callback the tray installs on the widget. Settings additions are view-model properties and commands bound in `SettingsWindow.axaml`. App-level routing (opening Settings on a prefilled milestone, opening the welcome window) lives in `App.axaml.cs`.

**Tech Stack:** C#/.NET 10, Avalonia 11.3, CommunityToolkit.Mvvm, xUnit + Avalonia.Headless.XUnit.

**Spec:** the design approved in chat on 2026-10-26, copied below. The Mac sources it mirrors are `Sources/Weekmark/App.swift` (`menuNeedsUpdate` 428-503, `weeksMenu` 400-426, `showFinder` 299-308) and `Sources/Weekmark/SettingsUI.swift` (Holidays ~187-226, General ~95, `MilestonesPane.applyPrefill` ~296).

### Spec (approved design)

1. **Right-click menu on the widget.** On the Mac, right-clicking the widget opens the same menu as the menu bar icon. Windows does the same, with the tray menu: Copy, Year at a Glance, Show/Hide, Position, Size, Settings, Quit. Right-clicking a week row in the month grid adds **"Add Milestone in CW46…"** at the top. It opens Settings on Milestones with a new milestone already set to that week, and the cursor goes to the Name field. Right-clicking elsewhere on the widget doesn't show this item, as on the Mac.
2. **"Weeks in 2026" and "Weeks in 2027"** go in both the tray menu and the widget menu. Each lists every week, grouped under month headings, like "CW 46   9–15 Nov". A week also shows its working days when holidays make it shorter, and a ⚑ with the milestone name. The current week is ticked. Clicking a week brings the widget forward and jumps its month grid to that week (no copy).
3. **Settings → Holidays:**
   - add your own holiday (Name, a date picker and an Add button), going into the same "Added by you" list the Mac uses
   - "Remove All" for company holidays
   - "Coming up": a read-only list of the next 6 holidays with date and CW
4. **Settings → General:** a "Show Welcome Screen Again" button.
5. *(Already done: clicking an upcoming milestone in the widget pins its week, in `WidgetView.OnMilestonePressed`. No task.)*

**Not included:** Quick Finder and Check for Updates.

## Global Constraints

- Work only under `Windows/` (plus this plan). There are no Mac source changes.
- Exact copy:
  - "Add Milestone in CW{week}…" (U+2026)
  - "Weeks in {year}"
  - week item "CW {week,2}    {ShortRange}", with optional "   · {n}d" and "   ⚑ {names}"
  - "Show Welcome Screen Again"
  - "Remove All"
  - "Coming up"
  - "No holidays selected"
  - "Add"
  - holiday name fallback "Holiday"
  - milestones empty state "None yet. Tip: right-click a week in the widget to add one."
- Manual holidays are `new Holiday(Day, name, "custom")` in `AppSettings.CustomHolidays`, the key `customHolidays` the Mac shares. No new settings keys.
- Every settings edit goes through `SettingsViewModel.Edit` (saves straight away, as today).
- Automated captures and tests use `DemoData`, never real settings.
- Run tests with `~/.dotnet/dotnet test` from `Windows/`. Use `/usr/bin/git` for git (a hook rewrites `git`).
- Commits use the emoji conventional style, e.g. `✨ feat(windows): …`, with no Co-Authored-By line.

## Review Focus

1. **The week chosen for "Add Milestone in CW…" must not change** if the pointer leaves the row before the item is clicked (hover clears on exit). Task 2 pins this: clear `Hover` after building the menu, then run the item.
2. **Choosing a week from "Weeks in" while a search is typed** must pin that week and clear the search, not leave the query's result showing. Task 1 pins this.
3. **An unsaved new milestone** keeps its typed name when "Add Milestone in CW…" is chosen again; only the week changes. Task 3 pins this.
4. **The widget sitting behind windows (Desktop layer):** its context menu must still open above other windows. This can't be tested headlessly, so Task 4 adds it to `TESTING.md`.
5. **The "Weeks in" submenus (~65 items) on a 1080p laptop at 125%** must scroll rather than run off-screen, in both the tray (Win32) and the widget (Avalonia popup). Task 4 adds this to `TESTING.md`.

---

### Task 1: "Weeks in YYYY" submenus and the Add Milestone item in the tray menu

**Files:**
- Modify: `Windows/src/Weekmark.App/Tray/TrayController.cs`
- Modify: `Windows/src/Weekmark.App/Widget/WidgetWindow.axaml.cs`
- Test: `Windows/tests/Weekmark.App.Tests/TrayTests.cs`

**Interfaces:**
- Produces:
  - `internal List<NativeMenuItemBase> TrayController.BuildMenu(WeekSnapshot snap, WeekRef? addMilestone = null)`
  - `WeekSnapshot TrayController.Snapshot()` (private helper, also used by `Refresh`)
  - `public event Action<WeekRef>? TrayController.AddMilestoneRequested`
  - `public void WidgetWindow.ShowWeek(WeekRef r)`: `BringForward()` then the view model's `Pin(r)`
  - `internal WidgetViewModel WidgetWindow.ViewModel => vm;`

- [ ] **Step 1: Write the failing tests** in `TrayTests.cs`, using the existing `Demo()` helper (Now = Sun 2026-10-25, CW43; demo milestones are "Code freeze" at CW46 and "Release 5.0" at CW48; GB holidays):

```csharp
[AvaloniaFact]
public void MenuListsTheWeeksOfThisYearAndNext()
{
    var (_, _, tray) = Demo();
    var top = Items(tray.Menu);
    Assert.Equal(["-", "Year at a Glance…", "Weeks in 2026", "Weeks in 2027", "-", "Hide Widget", "Position", "Size", "-", "Settings…", "Quit Weekmark"],
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
    Assert.Equal(["-", "Add Milestone in CW46…", "-", "Year at a Glance…"], with.Skip(3).Take(4));
    WeekRef? asked = null;
    tray.AddMilestoneRequested += w => asked = w;
    tray.BuildMenu(snap, new WeekRef(2026, 46)).OfType<NativeMenuItem>().Single(i => i.Header == "Add Milestone in CW46…").Command!.Execute(null);
    Assert.Equal(new WeekRef(2026, 46), asked);
    Assert.DoesNotContain(tray.BuildMenu(snap).OfType<NativeMenuItem>(), i => i.Header!.StartsWith("Add Milestone"));
}
```

Also update the existing assertion in the menu-contents test (`top.Skip(3)` list, currently `["Year at a Glance…", "-", "Hide Widget", …]`) to the new order above.

- [ ] **Step 2: Run the tests and check they fail**

Run: `~/.dotnet/dotnet test --filter "FullyQualifiedName~TrayTests"`
Expected: FAIL. The new tests won't compile until `BuildMenu`, `ViewModel` and `AddMilestoneRequested` exist.

- [ ] **Step 3: Implement**
  - New top-level order in `BuildMenu`:
    1. header
    2. planning line
    3. Copy
    4. separator
    5. `[Add Milestone in CW{w.Week}… , separator]`, only when `addMilestone` is non-null; the command raises `AddMilestoneRequested(w)` with the week captured at build time
    6. Year at a Glance…
    7. `WeeksMenu(snap, year)`, then `WeeksMenu(snap, year + 1)`, where `year` is `snap.Current.Year`
    8. separator
    9. Show/Hide … (unchanged from here on)
  - `WeeksMenu` is a port of the Mac's `weeksMenu`, with the copy fixed by the spec:

```csharp
// for w in 1..ctx.Cal.WeeksIn(year): r = new WeekRef(year, w); thu = ctx.Cal.StartOfWeek(r).AddDays(3) (the Mac's start + 3, any numbering)
//   new month (thu.Month != last): separator first unless it is the first month, then Label(ctx.Fmt.Format(thu, DateTemplate.MonthLong))
//   title = $"CW {w,2}    {ctx.Fmt.ShortRange(r)}"
//   wd = ctx.Holidays.WorkingDays(r, ctx.Cal); if (wd < 5) title += $"   · {wd}d"
//   names = milestones in week r (s.Milestones, in stored order); if any: title += $"   ⚑ {string.Join(", ", names)}"
//   item = Item(title, () => Widget.ShowWeek(r)); current week (r == snap.Current): ToggleType CheckBox, IsChecked true
```

  - `Refresh` keeps calling `BuildMenu(Snapshot())`. `Describe` already includes submenus, so a new week's tick changes the shape.
  - Add `WidgetWindow.ShowWeek` and `ViewModel`.
  - **Behaviour change to call out:** the Mac shows the week text in bold for the current week, but `NativeMenu` can't do bold. A tick alone is fine.

- [ ] **Step 4: Run the tests and check they pass**

Run: `~/.dotnet/dotnet test`
Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
/usr/bin/git add Windows/src/Weekmark.App/Tray/TrayController.cs Windows/src/Weekmark.App/Widget/WidgetWindow.axaml.cs Windows/tests/Weekmark.App.Tests/TrayTests.cs
/usr/bin/git commit -m "✨ feat(windows): Weeks in YYYY lists in the tray menu"
```

### Task 2: Right-click menu on the widget

**Files:**
- Modify: `Windows/src/Weekmark.App/Tray/TrayController.cs`
- Modify: `Windows/src/Weekmark.App/Widget/WidgetWindow.axaml.cs`
- Test: `Windows/tests/Weekmark.App.Tests/TrayTests.cs`

**Interfaces:**
- Consumes: `BuildMenu(snap, addMilestone)` and `AddMilestoneRequested` (Task 1).
- Produces:
  - `internal static List<Control> TrayController.ToMenuItems(IEnumerable<NativeMenuItemBase> items)`
  - `internal Func<WeekRef?, IReadOnlyList<Control>>? WidgetWindow.MenuFor { get; set; }`, set by the tray's `Widget` setter to `hover => ToMenuItems(BuildMenu(Snapshot(), hover))`
  - `internal ContextMenu? WidgetWindow.ShowContextMenu()`: builds the menu from `MenuFor(vm.Hover)`, opens it on `View` and returns it; null when `MenuFor` is null

- [ ] **Step 1: Write the failing tests**

```csharp
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
```

- [ ] **Step 2: Run the tests and check they fail**

Run: `~/.dotnet/dotnet test --filter "FullyQualifiedName~TrayTests"`
Expected: FAIL (no `ShowContextMenu`).

- [ ] **Step 3: Implement**
  - `ToMenuItems` maps each item:
    - `NativeMenuItemSeparator` → `new Separator()`. Check this first: separators are `NativeMenuItem`s with the header "-".
    - `NativeMenuItem` → `new MenuItem { Header, IsEnabled, Command, ToggleType, IsChecked, ItemsSource }`. Map `ToggleType` from `NativeMenuItemToggleType` by name to `MenuItemToggleType`. `ItemsSource` is the recursive `ToMenuItems(sub.Items)` when `Menu` is set.
  - In the `WidgetWindow` constructor, handle `ContextRequested`: `if (ShowContextMenu() is not null) e.Handled = true;`.
  - Right-button presses must keep neither pinning a week nor starting a drag. They don't today, because `WeekRows.OnRowPressed` and `WidgetWindow.OnPointerPressed` both require the left button; keep it that way.

- [ ] **Step 4: Run the tests and check they pass**

Run: `~/.dotnet/dotnet test`
Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
/usr/bin/git add Windows/src/Weekmark.App/Tray/TrayController.cs Windows/src/Weekmark.App/Widget/WidgetWindow.axaml.cs Windows/tests/Weekmark.App.Tests/TrayTests.cs
/usr/bin/git commit -m "✨ feat(windows): right-click the widget for the app menu and Add Milestone in CW…"
```

### Task 3: Add Milestone opens Settings prefilled; Show Welcome Screen Again

**Files:**
- Modify: `Windows/src/Weekmark.App/Settings/SettingsViewModel.cs`
- Modify: `Windows/src/Weekmark.App/Settings/SettingsWindow.axaml`, `SettingsWindow.axaml.cs`
- Modify: `Windows/src/Weekmark.App/Settings/MilestoneEditor.axaml`, `MilestoneEditor.axaml.cs`
- Modify: `Windows/src/Weekmark.App/App.axaml.cs`
- Test: `Windows/tests/Weekmark.App.Tests/SettingsViewModelTests.cs`

**Interfaces:**
- Consumes: `TrayController.AddMilestoneRequested` (Task 1).
- Produces:
  - `public void SettingsViewModel.NewMilestoneIn(WeekRef week)`
  - `public void SettingsWindow.AddMilestone(WeekRef week)`
  - `public void MilestoneEditor.FocusName()`
  - `public event Action? SettingsViewModel.WelcomeRequested` and `[RelayCommand] void ShowWelcome()`, which generates `ShowWelcomeCommand`
  - `void App.AddMilestone(WeekRef w)` and `void App.OpenWelcome()`

- [ ] **Step 1: Write the failing tests** in `SettingsViewModelTests.cs`, using the existing `Demo(out state)` helper:

```csharp
[AvaloniaFact]
public void AddMilestoneInAWeekStartsANewMilestoneThere()
{
    var vm = Demo(out _);
    vm.NewMilestoneIn(new WeekRef(2026, 46));
    Assert.Equal(("New milestone", 2026, 46, ""), (vm.Draft!.Title, vm.Draft.Year, vm.Draft.Week, vm.Draft.Name));
    vm.Draft.Name = "Launch";
    vm.NewMilestoneIn(new WeekRef(2027, 3)); // again before saving: the typed name stays
    Assert.Equal(("Launch", 2027, 3), (vm.Draft.Name, vm.Draft.Year, vm.Draft.Week));
}

[AvaloniaFact]
public void AddMilestoneOpensTheMilestonesTabWithTheNameFocused()
{
    var vm = Demo(out _);
    var w = new SettingsWindow(vm, "general");
    w.Show();
    w.AddMilestone(new WeekRef(2026, 46));
    Dispatcher.UIThread.RunJobs();
    Assert.Equal(Array.IndexOf(SettingsWindow.TabNames, "milestones"), w.Tabs.SelectedIndex);
    Assert.Equal(46, vm.Draft!.Week);
    Assert.True(w.Editor.NameBox.IsFocused);
}

[AvaloniaFact]
public void ShowWelcomeAsksTheAppToOpenIt()
{
    var vm = Demo(out _);
    var asked = 0;
    vm.WelcomeRequested += () => asked++;
    vm.ShowWelcomeCommand.Execute(null);
    Assert.Equal(1, asked);
}
```

- [ ] **Step 2: Run the tests and check they fail**

Run: `~/.dotnet/dotnet test --filter "FullyQualifiedName~SettingsViewModelTests"`
Expected: FAIL (the members don't exist).

- [ ] **Step 3: Implement**
  - `NewMilestoneIn`: if `Draft` is a new draft (`Id is null`), set its `Year` and `Week`. Otherwise (no draft, or editing an existing milestone) set `Draft = NewDraft(null, week, "")`.
    - *Ruling: an unsaved edit of an existing milestone is replaced, as Windows has one editor slot (the Mac's add form is separate from editing).*
  - `MilestoneEditor`:
    - `x:Name="NameBox"` on the Name `TextBox`
    - `FocusName()` posts `NameBox.Focus()` at `DispatcherPriority.Loaded`, because the editor becomes visible only after the draft binding updates
  - `SettingsWindow.axaml`:
    - `x:Name="Editor"` on the `s:MilestoneEditor`
    - the empty state becomes "None yet. Tip: right-click a week in the widget to add one."
    - a final General section: `<TextBlock Classes="section" Text="Welcome" />` and `<Button Content="Show Welcome Screen Again" Command="{Binding ShowWelcomeCommand}" />`
  - `SettingsWindow.AddMilestone`: select the "milestones" tab, call `Vm.NewMilestoneIn(week)`, then `Editor.FocusName()`.
  - `App`:
    - `tray.AddMilestoneRequested += AddMilestone;`
    - `AddMilestone(w)` is `OpenSettings("milestones"); settingsWindow!.AddMilestone(w);`
    - `OpenSettings` subscribes `vm.WelcomeRequested += OpenWelcome` when it creates the window
    - `OpenWelcome()`: one window at a time, the same as `OpenYear` (create with `new WelcomeWindow(new WelcomeViewModel(state!, WelcomeViewModel.CurrentRegion(), startupExe))`, clear the field on `Closed`, `Show`, restore it if minimized, `Activate`)
    - the first-run code in `OnFrameworkInitializationCompleted` calls `OpenWelcome()` instead of building the window inline, and `ShowAfter(welcomeWindow, …)` is unchanged

- [ ] **Step 4: Run the tests and check they pass**

Run: `~/.dotnet/dotnet test`
Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
/usr/bin/git add Windows/src/Weekmark.App Windows/tests/Weekmark.App.Tests/SettingsViewModelTests.cs
/usr/bin/git commit -m "✨ feat(windows): Add Milestone in CW… opens Settings on that week; Show Welcome Screen Again"
```

### Task 4: Holidays: add your own, Remove All, Coming up; docs

**Files:**
- Modify: `Windows/src/Weekmark.App/Settings/SettingsViewModel.cs`
- Modify: `Windows/src/Weekmark.App/Settings/SettingsWindow.axaml`
- Modify: `Windows/README.md`, `Windows/TESTING.md`
- Create: `Windows/release-notes/0.2.0.md`
- Test: `Windows/tests/Weekmark.App.Tests/SettingsViewModelTests.cs`

**Interfaces:**
- Produces, on `SettingsViewModel`:
  - `string NewHolidayName` and `DateTime? NewHolidayDate` (observable; the date defaults to `Ctx.Today`)
  - `IRelayCommand AddHoliday`, disabled while the date is null
  - `IRelayCommand RemoveAllHolidays` and `bool HasCustomHolidays`
  - `IReadOnlyList<UpcomingItem> UpcomingHolidays` and `bool NoUpcoming`
  - `public sealed record UpcomingItem(string Name, string When)`

- [ ] **Step 1: Write the failing tests** (set `CultureInfo.CurrentCulture = new CultureInfo("en-GB")` before building the view model; demo settings have GB holidays and Now is 2026-10-25):

```csharp
[AvaloniaFact]
public void AddingAHolidayGoesIntoAddedByYou()
{
    CultureInfo.CurrentCulture = new CultureInfo("en-GB");
    var vm = Demo(out var state);
    Assert.Equal(new DateTime(2026, 10, 25), vm.NewHolidayDate);
    vm.NewHolidayName = "  Company day ";
    vm.NewHolidayDate = new DateTime(2026, 12, 23);
    vm.AddHoliday.Execute(null);
    Assert.Equal(new Holiday(new Day(2026, 12, 23), "Company day", "custom"), state.Settings.CustomHolidays.Single());
    Assert.Equal("", vm.NewHolidayName);
    Assert.Equal(("Added by you", "1 day"), (vm.Sources.Single().Name, vm.Sources.Single().Count));
    Assert.Equal(("Company day", "Wed, 23 Dec 2026 · CW52"), (vm.UpcomingHolidays[0].Name, vm.UpcomingHolidays[0].When));
    vm.AddHoliday.Execute(null); // empty name
    Assert.Equal("Holiday", state.Settings.CustomHolidays[1].Name);
    vm.NewHolidayDate = null;
    Assert.False(vm.AddHoliday.CanExecute(null));
}

[AvaloniaFact]
public void RemoveAllClearsEveryCompanyHoliday()
{
    var vm = Demo(out var state, s => s.CustomHolidays.AddRange([new(new Day(2026, 12, 24), "Eve", "custom"), new(new Day(2026, 12, 31), "NYE", "Acme")]));
    Assert.True(vm.HasCustomHolidays);
    vm.RemoveAllHolidays.Execute(null);
    Assert.Empty(state.Settings.CustomHolidays);
    Assert.False(vm.HasCustomHolidays);
    Assert.Empty(vm.Sources);
}

[AvaloniaFact]
public void ComingUpListsTheNextSixHolidays()
{
    CultureInfo.CurrentCulture = new CultureInfo("en-GB");
    var vm = Demo(out _);
    Assert.Equal(6, vm.UpcomingHolidays.Count);
    Assert.Equal(new UpcomingItem("Christmas Day", "Fri, 25 Dec 2026 · CW52"), vm.UpcomingHolidays[0]);
    Assert.Equal(new UpcomingItem("Boxing Day", "Mon, 28 Dec 2026 · CW53"), vm.UpcomingHolidays[1]);
    Assert.False(vm.NoUpcoming);
    Assert.True(Demo(out _, s => s.Regions.Clear()).NoUpcoming);
}
```

- [ ] **Step 2: Run the tests and check they fail**

Run: `~/.dotnet/dotnet test --filter "FullyQualifiedName~SettingsViewModelTests"`
Expected: FAIL (the members don't exist).

- [ ] **Step 3: Implement**
  - `AddHoliday` appends `new Holiday(Day.From(DateOnly.FromDateTime(date)), name.Trim() is "" ? "Holiday" : name.Trim(), "custom")` through `Edit`, then clears `NewHolidayName`. Duplicates are allowed, as on the Mac.
  - `RemoveAllHolidays` does `Edit(() => S.CustomHolidays.Clear())`.
  - In `Refresh`:
    - `UpcomingHolidays = Ctx.Holidays.Upcoming(Ctx.Today, 6).Select(h => new UpcomingItem(h.Name, $"{Ctx.Fmt.Format(d, DateTemplate.WeekdayDayMonthYear)} · CW{Ctx.Cal.WeekOf(d).Week}"))`, where `d = h.Day.ToDate()` (or the existing `Day` → `DateOnly` helper)
    - `HasCustomHolidays` and `NoUpcoming` are notified by the existing `OnPropertyChanged(string.Empty)`
  - Holidays tab, under "Company holidays" after the import help text: a row with `TextBox` (Watermark "Name", width 200, bound `NewHolidayName`), `DatePicker` (`SelectedDate="{Binding NewHolidayDate}"`) and `Button Content="Add" Command="{Binding AddHoliday}"`.
  - After the sources list: `Button Content="Remove All"`, visible when `HasCustomHolidays`.
  - Then a section "Coming up": an `ItemsControl` of `UpcomingItem` rows (name left; `When` right, `FontFamily` monospace, opacity 0.7) and `TextBlock Text="No holidays selected"`, visible when `NoUpcoming`.
  - Docs:
    - README feature list: right-click menu, Weeks in YYYY, add holidays by hand, Coming up, Show Welcome Screen Again.
    - `TESTING.md` gets a new numbered item before "Uninstall" (renumber it). It covers right-clicking the widget on the Desktop layer (the menu opens above windows), right-clicking a week (Add Milestone opens Settings on that week with the Name focused), the Weeks in submenus scrolling on the 125% laptop in both tray and widget, choosing a week (the widget comes forward on that month), and adding, removing all and Coming up for holidays.
    - `release-notes/0.2.0.md`, in the same voice as `0.1.0.md`: a short "What's new" with these features.

- [ ] **Step 4: Run the tests and check they pass**

Run: `~/.dotnet/dotnet test`
Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
/usr/bin/git add Windows
/usr/bin/git commit -m "✨ feat(windows): add holidays by hand, Remove All and Coming up"
```
