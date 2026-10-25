# Windows Year at a Glance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A "Year at a Glance" window on Windows that mirrors the Mac's `Sources/Weekmark/YearView.swift`: 12 month cards with week numbers, opened from the widget and the tray.

**Architecture:** The widget's theme brushes and month-row styles move to one shared styles file, and the month rows become a small `WeekRows` control that both the widget's month grid and the new year window use. A `YearViewModel` (pure logic, headless-testable) builds 12 month cards from the existing `WeekSnapshot.MonthRows` and `WeekDetails.For`. A `YearWindow` shows it, remembers its frame in a new Windows-only settings key, and is opened by `App.OpenYear()` from a widget button, a tray item and `--year`.

**Tech Stack:** C# / .NET 10, Avalonia 11.3.22, CommunityToolkit.Mvvm, xUnit + Avalonia.Headless.XUnit.

**Spec:** No separate design document. The design was agreed in chat on 2026-10-25 and is copied here, with `Sources/Weekmark/YearView.swift` and `showYear()` in `Sources/Weekmark/App.swift` as the reference:

- Opens from a calendar button next to the widget's month arrows and from "Year at a Glance…" in the tray menu.
- 12 months with week numbers, today, the current week, holidays in red, milestone flags and sprint-start lines (the widget's month-grid marks).
- Hover a week: details at the bottom. Click: pin it (click again: unpin). Ctrl+C or the Copy button copies e.g. "CW51 (14–20 Dec)".
- ← → or the header arrows change year; "This Year" jumps back; Esc closes.
- Resizable; the number of month columns follows the width. Remembers its size and position.

## Global Constraints

- No new NuGet packages. Avalonia stays 11.3.22.
- Window title `Year at a Glance`; tray item `Year at a Glance…`; widget button tooltip and automation name `Year at a Glance`.
- Header copy: `{n} calendar weeks`, plus ` · week {w} now, {left} to go` when the shown year is the current week's year. Buttons `This Year`, tooltips `Previous year (←)` / `Next year (→)`. Copy button `Copy “{copyText}”`.
- Sizes from the Mac: default content 1060×820, minimum 520×440; month columns at least 220 wide with 14 spacing, 20 side padding; card padding 10, corner radius 12; CW column 30 wide; year text 34 bold; subtitle 13; month title 13 semibold (accent for the current month); footer title 13 semibold, subtitle 11, one line with trailing ellipsis.
- Years are limited to 2..9998 (the widget's `IsValid` range); nothing may throw at the ends.
- Settings stay Mac-compatible: the new key `yearWindow` is Windows-only; an unreadable value is preserved on save by the existing `Rejected` mechanism.
- Anything captured automatically (`--screenshot`, render tests) uses `DemoData`, never the user's settings.
- Environment: run `git` as `/usr/bin/git` (a hook rewrites `git`); `dotnet` is `~/.dotnet/dotnet`. Test command: `~/.dotnet/dotnet test Windows/Weekmark.sln`.
- Commits: emoji conventional style (`✨ feat(windows): …`, `♻️ refactor(windows): …`, `✅ test: …`, `📝 docs(windows): …`), no `Co-Authored-By` line.

## Review Focus

1. A pin or hover on a week that spans two months (CW5 2026 is 26 Jan–1 Feb) must mark the row in both cards — Task 2 test `PinMarksTheWeekInEveryCard`.
2. A Settings save that switches ISO ↔ US while the window is open must clear the pin and rebuild, never leaving a highlighted row that now means another week — Task 2 test `NumberingChangeClearsThePin`.
3. The 30 s clock tick must not rebuild the cards (that drops the hover under the pointer); a new day must — Task 2 test `ClockTickKeepsCardsNewDayRebuilds`.
4. A saved frame on a monitor that is no longer connected must open centred, not off-screen — Task 3 test `IsReachableOnlyWhenTheTitleBarIsOnAScreen`, Task 4 test `InitialFrameIgnoresAnUnreachableSave`.
5. Stepping past year 2 or 9998 must stay put without throwing (week rows around 0001-01-01 or 9999-12-31) — Task 2 test `YearStaysWithinTheSupportedRange`.

---

### Task 1: Shared theme and `WeekRows` (refactor, no visible change)

**Files:**
- Create: `Windows/src/Weekmark.App/Widget/WidgetTheme.axaml` (a `<Styles>` file whose `Styles.Resources` hold the theme dictionaries and geometries now in `WidgetView.axaml`'s `UserControl.Resources`, plus the `TextBlock`, `.secondary`, `.tertiary`, `daynum.*`, month-row (`Panel.row…`, `TextBlock.week…`) and `Button.pill…` styles; also a new `StreamGeometry x:Key="CalendarIcon"` for Task 5, stroke-drawn, about 10×10: `M1,2.5 H10 V10 H1 Z M1,4.5 H10 M3.5,1 V3.5 M7.5,1 V3.5`)
- Create: `Windows/src/Weekmark.App/Widget/WeekRows.axaml` and `WeekRows.axaml.cs`
- Modify: `Windows/src/Weekmark.App/Widget/WidgetView.axaml` (include the theme with `<StyleInclude Source="avares://Weekmark/Widget/WidgetTheme.axaml" />`; keep only widget-only styles: card backgrounds, weekday strip, details, search field)
- Modify: `Windows/src/Weekmark.App/Widget/MonthGrid.axaml` / `.axaml.cs` (rows become `<w:WeekRows Rows="{Binding Rows}" />`; the row handlers move to `WeekRows`; the wheel and header stay)
- Modify: `Windows/src/Weekmark.App/Widget/WidgetViewModel.cs` (models and row building below)
- Test: `Windows/tests/Weekmark.App.Tests/WidgetRenderTests.cs`

**Interfaces:**
- Produces, in `WeekRows.axaml.cs`:
  - `public interface IWeekPicker { WeekRef? Hover { get; set; } void Pin(WeekRef r); }` — `WidgetViewModel` implements it with its existing `Hover` and `Pin`.
  - `public partial class WeekRows : UserControl` with `public static readonly StyledProperty<IReadOnlyList<MonthRowModel>?> RowsProperty` / `Rows`. Pointer enter/exit/left-press on a row call `row.Picker.Hover = …` / `row.Picker.Pin(row.Week)` exactly as `MonthGrid`'s handlers do today (left button only, `e.Handled = true` on press so the widget doesn't start a drag). Spacer rows ignore the pointer.
- Produces, in `WidgetViewModel.cs`:
  - `MonthDayModel(string Number, bool IsToday, bool OutOfMonth, bool IsHoliday, bool IsPast = false, string? Tip = null)`; the row template binds `Classes.past` to `IsPast` and `ToolTip.Tip` to `Tip`.
  - `MonthRowModel(WeekRef week, IReadOnlyList<MonthDayModel> days, bool isCurrent, bool sprintStart, IBrush? flag, string? tip, IWeekPicker picker, bool isSpacer = false)` with `Picker` and `IsSpacer` properties; the template gives spacer rows `Classes.spacer`, styled `Opacity="0"` and `IsHitTestVisible="False"` so every card keeps six rows of equal height.
  - `public static IReadOnlyList<MonthRowModel> MonthRowModel.Build(WeekSnapshot snap, DateOnly month, WeekContext ctx, IReadOnlyList<Milestone> milestones, SprintConfig sprint, IWeekPicker picker, bool forYear)` — the body of today's `RebuildMonth` row building. `forYear: false` must produce exactly today's rows (out-of-month days dimmed, `IsPast` false). `forYear: true`: out-of-month days get `Number = ""`; rows with no day in `month` are `IsSpacer`; `IsPast = snap.IsPast(d)`; `Tip` = the day's holiday names joined with ", " (null when none). Day tips are set in both modes.

- [ ] **Step 1: Capture the widget before the change.** Run `~/.dotnet/dotnet test Windows/Weekmark.sln --filter WidgetRendersAllSizesAndThemes` and copy the PNGs it writes (`renders/` under the test bin directory) to a scratch folder.
- [ ] **Step 2: Write the failing test** `MonthRowBuildForYearHidesOtherMonths` in `WidgetRenderTests.cs` (AvaloniaFact, demo state at 2026-10-25): `MonthRowModel.Build(snap, new DateOnly(2026, 2, 1), ctx, s.Milestones, s.Sprint, picker, forYear: true)` returns 6 rows; `rows[5].IsSpacer` (February 2026 fills five ISO weeks); `rows[0].Days[0].Number == ""` (26 January) and `rows[0].Days[6].Number == "1"`; `rows[0].Days[6].IsPast` is true. With `forYear: false` no row is a spacer and `rows[0].Days[0].Number == "26"`.
- [ ] **Step 3: Run it to see it fail** (compile error: no `Build`).
- [ ] **Step 4: Implement** the files above. `WidgetViewModel.RebuildMonth` calls `MonthRowModel.Build(..., this, forYear: false)`.
- [ ] **Step 5: Run the whole suite** — all 53 App and 112 Core tests pass plus the new one; then compare the new `renders/` PNGs with Step 1's byte for byte (`cmp`). Any difference is a regression to fix, not to accept.
- [ ] **Step 6: Commit** `♻️ refactor(windows): month rows and widget theme are shared for the year window`.

### Task 2: `YearViewModel`

**Files:**
- Create: `Windows/src/Weekmark.App/Year/YearViewModel.cs`
- Test: `Windows/tests/Weekmark.App.Tests/YearViewModelTests.cs`

**Interfaces:**
- Consumes: `IWeekPicker`, `MonthRowModel.Build(..., forYear: true)` (Task 1); `AppState`, `WeekSnapshot`, `WeekDetails.For`, `DateTemplate.MonthLong`.
- Produces:
  - `public sealed record MonthCardModel(string Title, bool IsCurrentMonth, IReadOnlyList<MonthRowModel> Rows);`
  - `public sealed partial class YearViewModel(AppState state) : ObservableObject, IWeekPicker, IDisposable`, subscribing to `state.Changed`:
    - `int Year { get; set; }` — starts at `ctx.Today.Year` (calendar year, as the Mac); the setter clamps to 2..9998.
    - Observable: `string YearText`, `string Subtitle`, `bool ShowThisYear` (`Year != ctx.Today.Year`), `IReadOnlyList<string> Weekdays` (one letter, as the widget), `IReadOnlyList<MonthCardModel> Months` (12; title `MonthLong`; `IsCurrentMonth` when today is in it), `WeekRef? Pinned`, `string DetailsTitle`, `string DetailsSubtitle` (`"{subtitle} · {detail}"` when there is a detail), `bool DetailsPinned` (shown week == `Pinned`), `bool DetailsCurrent` (shown week == current week), `string CopyText`, `string CopyLabel` (`Copy “{CopyText}”`).
    - `WeekRef? Hover { get; set; }` and `void Pin(WeekRef r)` (toggles: pinning the pinned week unpins). Both only update row flags and details, never rebuild `Months`.
    - Details show `Hover ?? Pinned ?? current week`, from `WeekDetails.For`; `CopyText` is its `CopyText`.
    - `[RelayCommand]` `PreviousYear`, `NextYear`, `ThisYear`. Changing year rebuilds `Months` and clears `Hover`; the pin stays (as the Mac).
    - Rebuilds `Months` only when `(ctx.Today, state.Version, Year)` changes. When the numbering changed since the last build, `Pinned` and `Hover` are cleared first.
    - `public static int ColumnsFor(double width)` = `Math.Clamp((int)((width - 40 + 14) / (220 + 14)), 1, 12)`.

- [ ] **Step 1: Write the failing tests** (AvaloniaFact; `AppState` over `DemoData.Create` with a clock you can move, starting 2026-10-25 14:00, a Sunday in ISO CW43 2026):
  - `OpensOnThisYearWithTheCurrentWeek`: `Year == 2026`, `YearText == "2026"`, `Subtitle == "53 calendar weeks · week 43 now, 10 to go"`, `!ShowThisYear`, 12 months, `Months[0].Title == "January"`, only `Months[9].IsCurrentMonth`, `DetailsTitle.StartsWith("CW43 · ")`, `DetailsCurrent`.
  - `EveryCardHasSixRows`: every `Rows.Count == 6`; `Months[1].Rows[5].IsSpacer`; `Months[0].Rows[0].Week == new WeekRef(2026, 1)`; the last non-spacer row of `Months[11]` is `new WeekRef(2026, 53)`.
  - `StepsYearsAndComesBack`: `NextYear` → `Year == 2027`, `Subtitle == "52 calendar weeks"`, `ShowThisYear`; `ThisYear` → 2026.
  - `YearStaysWithinTheSupportedRange`: `Year = 9998; NextYear()` → 9998 with 12 months built; `Year = 2; PreviousYear()` → 2; `Year = 0` → 2.
  - `HoverThenPinThenCurrent`: `Hover = (2026,50)` → title starts `CW50 · `; `Pin((2026,49))`, `Hover = null` → `CW49 · `, `DetailsPinned`; `Pin((2026,49))` again → `Pinned == null`, details back to CW43.
  - `PinMarksTheWeekInEveryCard`: `Pin((2026,5))` → the CW5 row is `IsPinned` in both `Months[0]` and `Months[1]`; `Months` is the same instance as before the pin.
  - `CopyFollowsTheShownWeek`: `CopyText.StartsWith("CW43 (")`, `CopyLabel == $"Copy “{CopyText}”"`; after `NextYear` and `Hover = (2027,2)`, `CopyText.StartsWith("CW2 2027 (")`.
  - `NumberingChangeClearsThePin`: `Pin((2026,5))`; set `Settings.Numbering = WeekNumbering.Us`; `state.Save()` → `Pinned == null`, no row `IsPinned`.
  - `ClockTickKeepsCardsNewDayRebuilds`: move the clock 1 minute and `state.Tick()` → `Months` same instance; move it to the next day and tick → a new instance.
  - `ColumnsFollowTheWidth`: `ColumnsFor(1060) == 4`, `ColumnsFor(520) == 2`, `ColumnsFor(300) == 1`, `ColumnsFor(5000) == 12`.
- [ ] **Step 2: Run them to see them fail** (no `YearViewModel`).
- [ ] **Step 3: Implement** `YearViewModel.cs`.
- [ ] **Step 4: Run the suite** — all pass.
- [ ] **Step 5: Commit** `✨ feat(windows): year view model with month cards, pin and details`.

### Task 3: Remembered frame (Core)

**Files:**
- Modify: `Windows/src/Weekmark.Core/Settings/AppSettings.cs`, `Settings/SettingsStore.cs` (one `case "yearWindow"` using `Scalar`), `Settings/JsonShapes.cs` (`[JsonSerializable(typeof(WindowFrame))]`), `Placement.cs`
- Test: the existing Core settings and placement test files (`Windows/tests/Weekmark.Core.Tests/`)

**Interfaces:**
- Produces:
  - `public sealed record WindowFrame([property: JsonPropertyName("x")] double X, [property: JsonPropertyName("y")] double Y, [property: JsonPropertyName("width")] double Width, [property: JsonPropertyName("height")] double Height);` — X/Y are the window position in physical pixels, Width/Height its client size in DIPs (what Avalonia's `Position` and `Width`/`Height` use).
  - `AppSettings.YearWindow` (`[JsonPropertyName("yearWindow")] public WindowFrame? YearWindow { get; set; }`, omitted when null).
  - `public static bool PlacementMath.IsReachable(double x, double y, IReadOnlyList<ScreenInfo> screens)` — true when the point `(x + 48, y + 16)` (inside the title bar) lies in some screen's `WorkArea`; false for no screens or non-finite input.

- [ ] **Step 1: Write the failing tests**: `YearWindowRoundTrips` (save a frame `(100, 80, 1060, 820)`, load → equal; JSON contains `"yearWindow": {` with keys `x`, `y`, `width`, `height`); `UnreadableYearWindowIsKeptOnSave` (`"yearWindow": "oops"` loads as null and is written back unchanged); `IsReachableOnlyWhenTheTitleBarIsOnAScreen` (one 1920×1080 screen with a 40 px taskbar: `(100,100)` true, `(3000,100)` false, `(100,1060)` false, `(-40,100)` true, `NaN` false, empty list false).
- [ ] **Step 2: Run them to see them fail.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run the suite** — all pass.
- [ ] **Step 5: Commit** `✨ feat(windows): settings remember the year window frame`.

### Task 4: `YearWindow`

**Files:**
- Create: `Windows/src/Weekmark.App/Year/YearWindow.axaml`, `YearWindow.axaml.cs`
- Modify: `Windows/src/Weekmark.App/Widget/WidgetWindow.axaml.cs` (make the screen list and theme mapping reusable, below)
- Test: `Windows/tests/Weekmark.App.Tests/YearWindowTests.cs`

**Interfaces:**
- Consumes: `YearViewModel`, `MonthCardModel`, `WeekRows` (Tasks 1–2); `WindowFrame`, `AppSettings.YearWindow`, `PlacementMath.IsReachable` (Task 3).
- Produces:
  - In `WidgetWindow`: `internal static List<ScreenInfo> ScreenInfos(Screens screens)` (the instance method calls it) and `internal static ThemeVariant ThemeFor(AppearanceMode m)` (used by `ApplySettings`).
  - `public partial class YearWindow : Window` with `public YearWindow(AppState state)`; it creates and disposes its `YearViewModel`.
  - `internal static (PixelPoint? Position, Size Size) YearWindow.InitialFrame(WindowFrame? saved, IReadOnlyList<ScreenInfo> screens)` — saved and `IsReachable` → its position and size (size clamped to the 520×440 minimum); otherwise no position (centre on screen) and 1060×820.
  - `internal Func<string, Task> Copy` — defaults to the window clipboard (same exception handling as `WidgetWindow.CopyAsync`); tests replace it.

Layout (mirror `YearView.swift`): `DockPanel` — header on top (year text, subtitle, then right-aligned `This Year` pill (visible with `ShowThisYear`), ‹ › square pills), footer at the bottom above a 1 px `Divider` line (title + subtitle on the left with the details colour classes, Copy button on the right with `HotKey="Ctrl+C"`), and a vertical `ScrollViewer` with an `ItemsControl` of `Months` in a `UniformGrid` whose `Columns` follows `YearViewModel.ColumnsFor(Bounds.Width)` on resize. Each card: rounded border with a faint fill (add `CardFill` `#0B000000` / `#0BFFFFFF` to the shared theme), month title (accent when `IsCurrentMonth`), CW + weekday header and `<w:WeekRows Rows="{Binding Rows}" />`. Include `WidgetTheme.axaml`; window chrome and icon as `SettingsWindow.axaml`; `RequestedThemeVariant = WidgetWindow.ThemeFor(settings.Appearance)`, re-applied on `state.Changed`. Keys (on `KeyDown`, handled): Left → `PreviousYear`, Right → `NextYear`, Escape → `Close()`. On `Closing`, store the frame in `state.Settings.YearWindow` and call `state.Save()`.

- [ ] **Step 1: Write the failing tests** (AvaloniaFact, demo state at 2026-10-25):
  - `YearWindowRendersBothThemes`: show the window light and dark, `CaptureRenderedFrame()` is not null, save `renders/year-light.png` / `year-dark.png` beside the widget renders.
  - `ArrowKeysStepTheYearAndEscapeCloses`: Right → `Year == 2027`; Left → 2026; Escape → the window closed.
  - `CtrlCCopiesTheShownWeek`: replace `Copy` with a recorder; Ctrl+C (or clicking the Copy button) records the view model's `CopyText`.
  - `ClosingSavesTheFrame`: close → `state.Settings.YearWindow` is not null with `Width == 1060` and `Height == 820`.
  - `InitialFrameIgnoresAnUnreachableSave`: `InitialFrame(new(5000, 100, 900, 700), oneScreen)` → no position, 1060×820; `InitialFrame(new(100, 100, 300, 200), oneScreen)` → position (100,100), size 520×440; `InitialFrame(null, oneScreen)` → no position.
- [ ] **Step 2: Run them to see them fail.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run the suite** — all pass. Open `year-light.png` and `year-dark.png` and check them against the Mac layout: 4 columns at 1060 wide, today circled, CW43 row tinted, holiday days red, flags on milestone weeks.
- [ ] **Step 5: Commit** `✨ feat(windows): Year at a Glance window`.

### Task 5: Entry points and docs

**Files:**
- Modify: `Windows/src/Weekmark.App/Widget/MonthGrid.axaml` (calendar square pill between `Today` and ‹, `CalendarIcon` stroked like the chevrons, `ToolTip.Tip` and `AutomationProperties.Name` `Year at a Glance`, `Command="{Binding ShowYearCommand}"`), `WidgetViewModel.cs` (`public event Action? YearRequested;` raised by `[RelayCommand] void ShowYear()`), `WidgetWindow.axaml.cs` (`public event Action? YearRequested` forwarded from its view model)
- Modify: `Windows/src/Weekmark.App/Tray/TrayController.cs` (`public event Action? YearRequested;` item `Year at a Glance…` right after the `Copy “…”` item)
- Modify: `Windows/src/Weekmark.App/App.axaml.cs` (`void OpenYear()`: one window at a time like `OpenSettings`, `Activate()`; subscribed for every widget `CreateWidget` makes and for the tray; `--year` opens it at launch; the screenshot captures it first: `yearWindow ?? settingsWindow ?? welcomeWindow ?? Widget.View`)
- Modify: `Windows/src/Weekmark.App/Program.cs` (`Options` gains `bool Year = false` from `--year`; doc comment updated)
- Modify: `Windows/README.md` (features and command-line flags), `Windows/TESTING.md` (a new numbered item: open from the widget button and the tray; hover, click, Ctrl+C paste, ← → and This Year, Esc; resize changes the columns; close and reopen keeps size and position; unplug the monitor it was on → it opens centred), `Windows/release-notes/0.1.0.md` (one bullet)
- Test: `Windows/tests/Weekmark.App.Tests/TrayTests.cs`, `WidgetSearchTests.cs` (or a new `YearEntryTests.cs`), the existing `Options` tests if any

**Interfaces:**
- Consumes: `YearWindow(AppState)` (Task 4), `CalendarIcon` (Task 1).

- [ ] **Step 1: Write the failing tests**: the tray menu's item after `Copy “…”` is `Year at a Glance…` and invoking its command raises `YearRequested`; `WidgetViewModel.ShowYearCommand.Execute(null)` raises `YearRequested`; the rendered widget contains a `Button` whose `AutomationProperties.Name` is `Year at a Glance`; `Options.Parse(["--year"]).Year` is true and `Options.Parse([]).Year` false.
- [ ] **Step 2: Run them to see them fail.**
- [ ] **Step 3: Implement**, then the docs.
- [ ] **Step 4: Run the suite** — all pass; widget renders still match Task 1's apart from the new button.
- [ ] **Step 5: Commit** `✨ feat(windows): open Year at a Glance from the widget and the tray`, and the docs separately as `📝 docs(windows): Year at a Glance`.
