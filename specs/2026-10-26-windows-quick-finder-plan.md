# Windows Quick Finder Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Windows parity item 3: a Spotlight-style Quick Finder popup opened by a global hotkey, the tray/widget menu or `--find`, with a hotkey preset setting in Settings and on the Welcome screen.

**Architecture:** The lookups already exist (`Weekmark.Core.QueryEngine.Evaluate`, used by the widget's search field). A new `QuickFinderWindow` + `QuickFinderViewModel` (folder `Weekmark.App/Finder/`) shows every result for the query. The global hotkey is a Win32 `RegisterHotKey` on the widget's HWND, whose existing `WndProc` hook raises `HotKeyPressed`; the widget re-registers when the `hotkey` setting changes and records failure in `AppState.HotKeyTaken`. `App.axaml.cs` routes the hotkey, the tray's "Quick Finder…" item and `--find` to the window.

**Tech Stack:** C#/.NET 10, Avalonia 11.3, CommunityToolkit.Mvvm, xUnit + Avalonia.Headless.XUnit.

**Spec:** the design approved in chat on 2026-10-26, copied below. The Mac sources it mirrors: `Sources/Weekmark/QuickFinder.swift` (whole file), `Sources/Weekmark/HotKey.swift`, `Sources/Weekmark/Settings.swift` (`HotkeyPreset` 40-66), `Sources/Weekmark/App.swift` (`--find` 96-102, menu 457-458), `Sources/Weekmark/SettingsUI.swift` (General ~51-56, onboarding ~445).

### Spec (approved design)

1. **The popup.** Press **Ctrl+Alt+W** in any app. A search box opens near the top middle of the screen the mouse is on, with the current week badge ("CW44") on its right. Type `46`, `Dec 12`, `+6w`, `46-50`, `until CW52` or a milestone name; every result is listed, not only the first as in the widget. **Enter** copies the selected result, shows "Copied", then closes. **Ctrl+Enter** shows that week in the widget (as "Weeks in" does). **↑↓** select, **Esc** or a click elsewhere closes, double-clicking a row copies it. Focus goes back to the app you were in.
2. **Other ways to open it:** a "Quick Finder…   Ctrl+Alt+W" item in the tray and widget menu, and `Weekmark.exe --find 46`.
3. **Hotkey setting** in Settings → General and on the Welcome screen: Ctrl+Alt+W (default), Alt+Win+W, Ctrl+Alt+Win+W, Ctrl+Alt+Space, Off — the Mac's presets with ⌃=Ctrl, ⌥=Alt, ⌘=Win, saved under the Mac's key and values. If Windows refuses the shortcut (another app has it), Settings says so in red.
4. **Not included:** a record-any-shortcut field (the Mac only has presets).

## Global Constraints

- Work only under `Windows/` (plus this plan). No Mac changes. No new NuGet packages.
- Settings key `hotkey`, JSON values exactly `ctrlOptW`, `optCmdW`, `ctrlOptCmdW`, `ctrlOptSpace`, `off`; default `ctrlOptW`.
- Exact copy:
  - preset titles "Ctrl+Alt+W", "Alt+Win+W", "Ctrl+Alt+Win+W", "Ctrl+Alt+Space", "Off"
  - menu item "Quick Finder…" plus `"   {title}"` unless Off (U+2026)
  - placeholder "46 · Dec 12 · +6w · 46-50 · until CW52 · milestone"
  - footer hints "Enter Copy", "Ctrl+Enter Show in widget", "↑↓ Select", "Esc Close"; after a copy "✓ Copied “{text}”"
  - Settings label "Quick Finder hotkey", help "Press it from any app to look up a week, date, range or countdown, then press Enter to copy."
  - warning "{title} is taken by another app. Pick another."
- Win32 codes: MOD_ALT 0x1, MOD_CONTROL 0x2, MOD_WIN 0x8, MOD_NOREPEAT 0x4000, VK W 0x57, VK SPACE 0x20, WM_HOTKEY 0x0312, hotkey id 1.
- Win32 calls only behind `OperatingSystem.IsWindows()`; tests run on macOS/Linux headless.
- Tests: `~/.dotnet/dotnet test` from `Windows/`. Commit style: emoji conventional (`✨ feat(windows): …`).

## Review Focus

1. Pressing the hotkey while the finder is open closes it (toggle), and reopening starts empty: query "", selection 0, no "Copied" line. → Task 2 test `ToggleAndReopenStartFresh`.
2. Enter on an error/hint row (no `CopyText`) copies nothing and keeps the finder open. → Task 2 test `EnterOnErrorDoesNothing`.
3. Ctrl+Enter on a row without a week (an error) does nothing and stays open. → Task 2 test `CtrlEnterWithoutWeekDoesNothing`.
4. Typing again inside the 0.7 s "Copied" delay clears "Copied" and the finder stays open. → Task 2 test `TypingAfterCopyCancelsClose`.
5. An unknown stored `hotkey` value loads as the default and is written back unchanged. → Task 1 test `UnknownHotkeyKeepsStoredValue`.

---

### Task 1: Hotkey preset in Core

**Files:**
- Create: `Windows/src/Weekmark.Core/Settings/HotkeyPreset.cs`
- Modify: `Windows/src/Weekmark.Core/Settings/AppSettings.cs`, `Windows/src/Weekmark.Core/Settings/SettingsStore.cs` (key switch ~line 100, and the source-gen `JsonSerializerContext` `C`, wherever it is declared)
- Test: `Windows/tests/Weekmark.Core.Tests/SettingsStoreTests.cs` (or a new `HotkeyPresetTests.cs`)

**Interfaces:**
- Produces: `public enum HotkeyPreset { CtrlOptW, OptCmdW, CtrlOptCmdW, CtrlOptSpace, Off }` with `[JsonStringEnumMemberName]` values from Global Constraints (same converter pattern as `WidgetSize`); `public static class HotkeyPresetExtensions { string Title(this HotkeyPreset); (uint Modifiers, uint Key)? Win32(this HotkeyPreset); }` — `Modifiers` excludes MOD_NOREPEAT; `AppSettings.Hotkey` (`[JsonPropertyName("hotkey")]`, default `CtrlOptW`).

- [ ] **Step 1: Write failing tests**
  - `HotkeyTitles`: titles in enum order equal `["Ctrl+Alt+W", "Alt+Win+W", "Ctrl+Alt+Win+W", "Ctrl+Alt+Space", "Off"]`.
  - `HotkeyWin32Codes`: CtrlOptW → (0x3, 0x57); OptCmdW → (0x9, 0x57); CtrlOptCmdW → (0xB, 0x57); CtrlOptSpace → (0x3, 0x20); Off → null.
  - `HotkeyLoadsAndSaves`: a file with `"hotkey":"optCmdW"` loads `OptCmdW`; set `CtrlOptSpace`, save, the file has `"hotkey":"ctrlOptSpace"`. A missing key loads `CtrlOptW`.
  - `UnknownHotkeyKeepsStoredValue`: `"hotkey":"cmdShiftK"` loads `CtrlOptW`; saving without touching it writes `"cmdShiftK"` back (follow the existing rejected-value tests in `SettingsStoreTests.cs`).
- [ ] **Step 2:** Run `~/.dotnet/dotnet test tests/Weekmark.Core.Tests` — the new tests fail (don't compile).
- [ ] **Step 3:** Implement the enum, extensions, `AppSettings.Hotkey`, the `case "hotkey"` in `SettingsStore.Parse` via `Scalar`, and register the type with the serializer context.
- [ ] **Step 4:** Run the Core tests — all pass.
- [ ] **Step 5:** Commit `✨ feat(windows): hotkey preset setting`.

### Task 2: Quick Finder window

**Files:**
- Create: `Windows/src/Weekmark.App/Finder/QuickFinderViewModel.cs`, `Windows/src/Weekmark.App/Finder/QuickFinderWindow.axaml`, `Windows/src/Weekmark.App/Finder/QuickFinderWindow.axaml.cs`
- Modify: `Windows/src/Weekmark.App/Platform/WindowsLayering.cs` (add `GetCursorPos` import and `internal static PixelPoint? CursorPosition()`, null off Windows)
- Test: `Windows/tests/Weekmark.App.Tests/QuickFinderTests.cs`

**Interfaces:**
- Consumes: `QueryEngine.Evaluate(string, WeekContext)`, `QueryResult` (`CopyText`, `Week`, `Kind`), `WidgetWindow.CopyAsync(TopLevel, string)` (static, never throws), `WidgetWindow.ThemeFor(AppearanceMode)`, `WindowsLayering.RoundCorners(Window)`.
- Produces:
  - `public sealed partial class QuickFinderViewModel : ObservableObject` — `QuickFinderViewModel(AppState state)`; `[ObservableProperty] string query = ""` (a change sets `Selection = 0`, clears `Copied`, re-evaluates); `IReadOnlyList<QueryResult> Results`; `[ObservableProperty] int selection`; `QueryResult? Selected`; `[ObservableProperty] string? copied`; `string Badge => $"CW{state.Context.Current.Week}"`; `void Move(int delta)` (wraps; no-op when empty); `void Refresh()`; `public const string Placeholder`.
  - `public partial class QuickFinderWindow : Window` — `QuickFinderWindow(AppState state)`; `event Action<WeekRef>? ShowInWidgetRequested`; `void ShowFinder(string query = "")` (resets VM, positions, shows, activates, focuses the field); `void Toggle()` (visible → `CloseFinder()`, else `ShowFinder()`); `void CloseFinder()` (hides; the window is reused); `internal QuickFinderViewModel ViewModel`; `internal TextBox Field` (x:Name); `internal static PixelPoint FinderPosition(PixelRect workingArea, double scaling, double widthDip = 640)`.

Behaviour (from `QuickFinder.swift`): width 640 DIPs, `SizeToContent="Height"`, `SystemDecorations="None"`, `ShowInTaskbar="False"`, `Topmost="True"`, theme from `S.Appearance`, rounded corners on Windows, a subtle 1 px border. Position: horizontally centred on the working area of the screen under `CursorPosition()` (fallback `Screens.Primary`), top at 28 % of the working-area height. Header: field (22 px) with the placeholder, then the `Badge` pill in the accent colour. Rows: a 30×30 rounded tile tinted per `Kind` with a plain Unicode glyph that renders in Segoe UI (no emoji, no icon font), `Title` (14 semibold), `Subtitle` (12, dimmed), optional `Detail` (11, more dimmed); the selected row is highlighted and shows "Enter Copy" when it has `CopyText`. Footer: the hints, or the "✓ Copied" line. Keys, handled tunnelling on the field: Down/Up → `Move(±1)`; Esc → `CloseFinder()`; Enter → copy; Ctrl+Enter → if `Selected.Week` is set, `CloseFinder()` then raise `ShowInWidgetRequested`; Ctrl+C with an empty query → copy. Copy: if `Selected.CopyText` is set, `CopyAsync`, set `Copied`, and close after 0.7 s only if `Copied` still holds that text. A click on a row selects it and keeps keyboard focus in the field; a double-click copies. `Deactivated` → `CloseFinder()`. Query re-evaluates on every keystroke (no debounce).

- [ ] **Step 1: Write failing tests** (headless, demo `AppState` as in `WidgetRenderTests.Demo`)
  - `EmptyQueryShowsOverview`: `Results[0].Kind == QueryKind.Week` and `Results[0].Week == state.Context.Current`.
  - `QueryListsResults`: `Query = "46"` → one `Week` result for CW46 of the current year; `Selection == 0`.
  - `MoveWraps`: on the overview (≥2 results) `Move(-1)` selects the last, `Move(1)` returns to 0.
  - `KeysDriveTheFinder`: shown window; type "46" into `Field` → results update; Down changes `Selection` when there are ≥2 results; Enter puts the CW46 copy text on the clipboard and sets `Copied`; Esc hides (`IsVisible == false`).
  - `CtrlEnterShowsInWidget`: query "46", Ctrl+Enter raises `ShowInWidgetRequested` with CW46 and hides.
  - `CtrlCWithEmptyQueryCopies`: empty query, Ctrl+C copies the current week's copy text.
  - `ToggleAndReopenStartFresh`, `EnterOnErrorDoesNothing` (query "zzzz"), `CtrlEnterWithoutWeekDoesNothing`, `TypingAfterCopyCancelsClose` — per Review Focus 1–4.
  - `ClickKeepsFocusInField`: clicking the second row sets `Selection == 1` and `Field.IsFocused` stays true.
  - `FinderPositionCentresNearTop`: `FinderPosition(new PixelRect(0, 0, 1920, 1040), 1.25)` == `new PixelPoint(560, 291)`; with working area x offset 1920 → x 2480.
- [ ] **Step 2:** Run `~/.dotnet/dotnet test tests/Weekmark.App.Tests --filter QuickFinder` — fails (doesn't compile).
- [ ] **Step 3:** Implement the view model, window and `CursorPosition()`.
- [ ] **Step 4:** Run the whole App test project — all pass.
- [ ] **Step 5:** Commit `✨ feat(windows): Quick Finder window`.

### Task 3: Hotkey, menu item and `--find`

**Files:**
- Modify: `Windows/src/Weekmark.App/Platform/WindowsLayering.cs` (`RegisterHotKey`, `UnregisterHotKey` imports), `Windows/src/Weekmark.App/Widget/WidgetWindow.axaml.cs`, `Windows/src/Weekmark.App/AppState.cs`, `Windows/src/Weekmark.App/Tray/TrayController.cs`, `Windows/src/Weekmark.App/Program.cs`, `Windows/src/Weekmark.App/App.axaml.cs`
- Test: `Windows/tests/Weekmark.App.Tests/TrayTests.cs`, `Windows/tests/Weekmark.App.Tests/QuickFinderTests.cs`; fix menu-index assertions in existing tests (`TrayTests`, `YearEntryTests`) shifted by the new item

**Interfaces:**
- Consumes: Task 1 `HotkeyPreset`, `Win32()`, `Title()`, `AppSettings.Hotkey`; Task 2 `QuickFinderWindow` (`ShowFinder`, `Toggle`, `ShowInWidgetRequested`).
- Produces: `AppState.HotKeyTaken` (`public bool HotKeyTaken { get; set; }`, runtime only, never saved); `WidgetWindow.HotKeyPressed` (`public event Action?`); `WidgetWindow.WndProc` made `internal` for the test; `TrayController.FinderRequested` (`public event Action?`); `Options.Find` (`string?`).

Behaviour:
- Widget: `ApplyHotKey()` runs in `Opened` after the WndProc hook is installed and from `ApplySettings`; it does nothing before the hook exists or when the preset equals the one last applied. On Windows it unregisters id 1, then for a non-null `Win32()` calls `RegisterHotKey(hwnd, 1, Modifiers | MOD_NOREPEAT, Key)` and sets `state.HotKeyTaken = !ok`; `Off` sets it false. `Closed` unregisters. `WndProc`: `WM_HOTKEY` with wParam 1 posts `HotKeyPressed` to the UI thread. A recreated widget registers again in its own `Opened`.
- Tray: `Item(…"Quick Finder…" + hotkey suffix…, () => FinderRequested?.Invoke())` directly before "Year at a Glance…", as on the Mac.
- Options: `--find <query>` → `Find = query`; `--find` with no value (or followed by another `--flag`) → `""`; absent → null. Extend the doc comment.
- App: one lazily created `QuickFinderWindow`; `ShowInWidgetRequested += r => Widget!.ShowWeek(r)`; `Widget.HotKeyPressed += () => finder.Toggle()` in `CreateWidget`; `tray.FinderRequested += () => finder.ShowFinder()`; `o.Find is { } q` → post `ShowFinder(q)` at start-up; `--screenshot` captures the finder first when it is open.

- [ ] **Step 1: Write failing tests**
  - `FindFlag`: `Options.Parse(["--find", "46"]).Find == "46"`; `["--find"]` → `""`; `["--find", "--demo-data"]` → `""`; `[]` → null.
  - `MenuHasQuickFinderBeforeYear`: the item before "Year at a Glance…" is "Quick Finder…   Ctrl+Alt+W"; with `Hotkey = Off` it is "Quick Finder…"; invoking it raises `FinderRequested`.
  - `HotKeyMessageRaisesPressed`: `widget.WndProc(0, 0x0312, 1, 0, ref handled)` then run the dispatcher → `HotKeyPressed` raised once; wParam 2 → not raised.
- [ ] **Step 2:** Run the App tests — the new tests fail.
- [ ] **Step 3:** Implement; fix the index-based assertions the new menu item shifts.
- [ ] **Step 4:** Run the whole solution's tests — all pass.
- [ ] **Step 5:** Commit `✨ feat(windows): global hotkey and menu item open the Quick Finder`.

### Task 4: Hotkey setting, Welcome, docs and render

**Files:**
- Modify: `Windows/src/Weekmark.App/Settings/SettingsViewModel.cs`, `Windows/src/Weekmark.App/Settings/SettingsWindow.axaml` (General tab, after Appearance), `Windows/src/Weekmark.App/Onboarding/WelcomeWindow.axaml` (+ `.axaml.cs` view model; a row after "Public holidays"), `Windows/README.md`, `Windows/TESTING.md`, `Windows/release-notes/0.2.0.md`
- Test: `Windows/tests/Weekmark.App.Tests/SettingsViewModelTests.cs`, `Windows/tests/Weekmark.App.Tests/WidgetRenderTests.cs` (or `QuickFinderTests.cs`)

**Interfaces:**
- Consumes: Task 1 `HotkeyPreset`, `Title()`; Task 3 `AppState.HotKeyTaken`; Task 2 `QuickFinderWindow`.
- Produces: `SettingsViewModel.Hotkeys` (`public static string[]`, titles in enum order), `SettingsViewModel.HotkeyIndex` (`Pick` pattern like `AppearanceIndex`), `SettingsViewModel.HotkeyWarning` (`string?`: the warning when `state.HotKeyTaken` and the preset is not Off, else null; refreshed with the other properties on `state.Changed`); `WelcomeViewModel.HotkeyIndex` (seeded from settings; `Finish()` writes it).

- [ ] **Step 1: Write failing tests**
  - `HotkeyIndexSavesPreset`: `HotkeyIndex = 3` → `S.Hotkey == CtrlOptSpace` and saved; `Hotkeys[0] == "Ctrl+Alt+W"`.
  - `HotkeyWarningWhenTaken`: `state.HotKeyTaken = true` → `HotkeyWarning == "Ctrl+Alt+W is taken by another app. Pick another."`; after `HotkeyIndex = 4` (Off) → null.
  - `WelcomeKeepsAndWritesHotkey`: settings `OptCmdW` → `WelcomeViewModel.HotkeyIndex == 1`; set 3, `Finish()` → `CtrlOptSpace`.
  - Render: save `renders/quick-finder-light.png` and `renders/quick-finder-dark.png` of the finder with query "46" the way the existing render tests save theirs, asserting the frame is not blank.
- [ ] **Step 2:** Run the App tests — the new tests fail.
- [ ] **Step 3:** Implement. Settings: a row "Quick Finder hotkey" + ComboBox, the help line, and the warning in red (`IsVisible` when non-null). Welcome: a "Quick Finder hotkey" row. Docs: a README "Quick Finder" section (hotkey, keys, `--find`); TESTING a new item 19 "Quick Finder" (hotkey from another app in front, focus returns afterwards, Ctrl+Enter shows the week, the taken warning by registering the same shortcut in another app, AltGr layouts such as Polish or German still type their characters) and renumber Uninstall to 20; release notes 0.2.0 bullets for the Quick Finder and the hotkey setting.
- [ ] **Step 4:** Run the whole solution's tests — all pass.
- [ ] **Step 5:** Commit `✨ feat(windows): Quick Finder hotkey setting` and `📝 docs(windows): Quick Finder`.
