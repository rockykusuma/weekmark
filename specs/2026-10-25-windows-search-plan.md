# Weekmark for Windows: widget search field — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A search field at the bottom of the Windows widget that answers the Mac's lookup syntax (weeks, dates, offsets, ranges, countdowns, milestones) live, highlights the week, and copies the result on Enter.

**Architecture:** Port the Mac's `QueryEngine` (`Sources/CWCore/QueryEngine.swift`) to `Weekmark.Core` and prove it against new Swift-exported vectors. The Mac's free-form date detection (`NSDataDetector`) is replaced by an explicit date grammar. The widget gets a text field driving the existing pin/highlight machinery; the widget may take keyboard focus only while the field is in use.

**Tech Stack:** as v1 (.NET 10, Avalonia 11.3.22, xUnit, Avalonia.Headless.XUnit; Swift/XCTest for the vector export).

**Spec:** `specs/2026-10-25-windows-design.md` ("Version 2 → Lookup") plus the decisions Rakesh approved in chat on 2026-10-25, recorded here:
- Field at the bottom of the widget like the Mac, placeholder `46 · Dec 12 · +6w · 46-50`; live result in the details line; the month grid jumps to the result week and highlights it orange.
- Syntax as the Mac: weeks (`51`, `CW51`, `KW 51`, `3 2027`, `2027-W3`, late-year rule), dates (`Dec 12`, `12 Dec`, numeric dates in the Windows region's order, `2026-12-12`, `today`, `tomorrow`, `next friday`), offsets (`+6w`, `-2w`, `+10d`, `in 6 weeks`, `3 weeks ago`), ranges (`46-50`, `CW52–CW2`), countdowns (`until CW52`, `to dec 24`), milestone names.
- Enter copies the result's copy text (Claude's call); Esc or × clears and returns to today.
- Only the listed date forms are understood; anything else shows "No match …" with examples (Claude's call).
- The widget takes keyboard focus only while the field is in use, then hands it back (Claude's call).
- Not in scope: the Ctrl+Alt+W lookup panel, Year at a Glance, update checks.

## Global Constraints

- Never disagree with the Mac on any query the vectors cover: titles, subtitles, details and copy texts are byte-identical (English, day-first unless en-US).
- The Mac's `NSDataDetector` resolves relative and year-less dates against the real clock, so vectors cover only absolute dates (with a year); relative/year-less forms follow the rules in Task 3, pinned by C# tests.
- Evaluation order exactly as Swift `evaluate`: empty → overview; countdown; keyword (+ milestone matches); relative; range; week; milestone matches; date (+ milestone matches); else error `No match for “<q>”` with subtitle `Try 46 · CW3 2027 · Dec 12 · +6w · 46-50 · until CW52`.
- Nothing on the query path may throw, whatever the input (any string up to 200 chars, emoji, digits only, huge numbers, invalid settings values).
- Widget behaviour from v1 is unchanged when the field is empty: layering, Win+D recovery, placement, tray.
- Keyboard focus: the widget becomes focusable only after a click in the field; leaving the field (Esc, click elsewhere, Enter) gives focus back and restores the no-activate layering.

## Review Focus

1. **Typing fast or pasting long junk** (`🙂🙂`, 200 digits, `CW99999`, `-`, `to`, `46-`): no exception, a sensible error line. Pinned by Task 2/3 fuzz tests.
2. **Week 1 / week 53 / year boundaries** (`52-2`, `CW53` in a 52-week year, `until CW1` in late December): same answers as the Mac. Pinned by vectors (Task 1).
3. **Day-first vs month-first numeric dates** (`03/04/2027` on en-GB vs en-US; `24/12/2026` unambiguous on both): resolved by the Windows region, unambiguous ones by value. Pinned by Task 3 tests.
4. **Clicking the field and clicking away** while the widget is "On desktop": focus is taken and returned; the widget never stays floating or activatable. Pinned by Task 4 test.
5. **Leaving a query in the field for hours**: it clears after 2 minutes idle (Mac `resetIfIdle`), and a midnight tick doesn't throw with a query active. Pinned by Task 4 test.

---

## File Structure

```
Tests/CWCoreTests/VectorExport.swift          + "queries" section, + template EEEEdMMMMyyyy
Tests/vectors/cwcore-v1.json                  regenerated
Windows/src/Weekmark.Core/QueryEngine.cs      QueryResult, QueryEngine (evaluate, overview, parseWeek, keyword, relative, range, rangeResult, countdown, milestone results)
Windows/src/Weekmark.Core/DateGrammar.cs      explicit date parsing (replaces NSDataDetector)
Windows/src/Weekmark.Core/WeekFormatter.cs    + DateTemplate.WeekdayLongDayMonthYear (EEEEdMMMMyyyy)
Windows/src/Weekmark.App/Widget/*             search field in WidgetView, view-model query state, focus handling
Windows/src/Weekmark.App/Platform/*Layering*  allow/deny activation while typing
Windows/tests/Weekmark.Core.Tests/QueryEngineTests.cs, DateGrammarTests.cs
Windows/tests/Weekmark.App.Tests/WidgetSearchTests.cs
Windows/README.md, Windows/TESTING.md, Windows/release-notes/0.1.0.md
```

---

### Task 1: Swift query vectors

**Files:** Modify `Tests/CWCoreTests/VectorExport.swift`; regenerate `Tests/vectors/cwcore-v1.json`.

**Interfaces:**
- Produces: `"queries": [{"locale", "numbering", "now", "regions", "milestones": [<Mac Milestone JSON>], "sprint": <SprintConfig JSON>, "query", "results": [{"kind", "title", "subtitle", "detail": "…"|null, "copyText": "…"|null, "week": {"year","week"}|null, "weeks": n|null, "workdays": n|null}]}]` from `QueryEngine.evaluate(query, ctx)`; and `"EEEEdMMMMyyyy"` added to `format.<locale>.templates`.
- Queries (each at several `now`s: early year, mid-year, a week ≥ 40, late December; both locales; ISO and US; with and without holidays/milestones/sprint): empty string; `46`, `CW46`, `KW 46`, `w46`, `week 46`, `calendar week 46`, `46 2027`, `46/2027`, `2027-W3`, `2027 3`, `0`, `54`, `53`, `99`, `CW53 2027`; `today`, `now`, `this week`, `current`, `next week`, `last week`, `previous week`; `+6`, `+6w`, `-2w`, `+10d`, `-3d`, `+1d`, `in 6 weeks`, `3 weeks ago`, `6 weeks`, `+600w`; `46-50`, `46 - 50`, `CW52–CW2`, `52-2`, `46 to 48`, `2026-W50..2027-W2`, `50-46`, `46-99`; `until CW52`, `till 3 2027`, `to 46`, `→ 50`, `-> 50`, `today -> CW3 2027`, `until 24 December 2026`, `to 2027-01-15`; milestone names (`freeze`, `release`, `re`, `x`); absolute dates `24 Dec 2026`, `Dec 24 2026`, `December 24, 2026`, `2026-12-24`, `24/12/2026`, `12/24/2026`, `1 January 2027`, `Jan 4, 2027`; junk `hello`, `🙂`, `-`, `to`, `46-`, `cw`.
- Exclude year-less dates and relative day words from vectors (they depend on the real clock in `NSDataDetector`).

- [ ] **Step 1: Extend `VectorExportTests`** with the section above; keep generation deterministic.
- [ ] **Step 2: Regenerate** with `UPDATE_VECTORS=1 swift test --filter VectorExportTests`, then `swift test` (34 tests pass). Spot-check: a `46-50` result title starts with `CW46 – CW50 · 5 weeks`.
- [ ] **Step 3: Confirm** the existing C# suite still passes against the regenerated file (`dotnet test Windows/Weekmark.sln`).
- [ ] **Step 4: Commit** `✅ test: export lookup query vectors for the Windows search field`

### Task 2: C# query engine (non-date paths)

**Files:** Create `Windows/src/Weekmark.Core/QueryEngine.cs`, `Windows/tests/Weekmark.Core.Tests/QueryEngineTests.cs`.

**Interfaces:**
- Consumes: `WeekContext`, `WeekDetails.For`, `WeekCalendar`, `WeekFormatter`, `HolidayStore`, `Milestones`, `Sprints`.
- Produces: `enum QueryKind { Week, Range, Date, Relative, Countdown, Milestone, Holiday, Hint, Error }`; `sealed record QueryResult(QueryKind Kind, string Title, string Subtitle, string? Detail = null, string? CopyText = null, WeekRef? Week = null, int? Weeks = null, int? Workdays = null)`; `static class QueryEngine { const string Examples = "Try 46 · CW3 2027 · Dec 12 · +6w · 46-50 · until CW52"; IReadOnlyList<QueryResult> Evaluate(string input, WeekContext ctx); IReadOnlyList<QueryResult> Overview(WeekContext ctx); WeekParse ParseWeek(string input, WeekContext ctx); QueryResult? RangeResult(WeekRef a, WeekRef b, WeekContext ctx); }` with `WeekParse` = `abstract record WeekParse { None; Week(WeekRef); Invalid(string) }`. Date detection is a seam `Func<string, WeekContext, DateOnly?>` defaulting to "no date" until Task 3.

- [ ] **Step 1: Tests** `QueriesMatchSwift_NonDate` (every `queries` vector whose results contain no `kind:"date"` and whose query is not a countdown to a date), `JunkNeverThrows` (fuzz: 2,000 random strings from a fixed seed incl. emoji, long digit runs, separators → no exception, ≥ 1 result).
- [ ] **Step 2: Run** `dotnet test Windows/Weekmark.sln --filter QueryEngineTests`. Expected: FAIL.
- [ ] **Step 3: Port** `evaluate`, `overview`, `parseWeek`, `keyword`, `relative`, `range`, `rangeResult`, `countdown` (week targets), `milestoneResult`, `milestoneMatches` from `Sources/CWCore/QueryEngine.swift` line by line (use the minus sign `−` and en-dashes exactly as Swift).
- [ ] **Step 4: Run** the tests. Expected: PASS.
- [ ] **Step 5: Commit** `✨ feat(windows): lookup engine ported from the Mac`

### Task 3: Date grammar

**Files:** Create `Windows/src/Weekmark.Core/DateGrammar.cs`, `Windows/tests/Weekmark.Core.Tests/DateGrammarTests.cs`; modify `QueryEngine.cs` (date result + countdown to a date), `WeekFormatter.cs` (+ `DateTemplate.WeekdayLongDayMonthYear`).

**Interfaces:**
- Produces: `static class DateGrammar { DateOnly? Parse(string input, DateOnly today, bool dayFirst); }`, wired as `QueryEngine`'s date detector; `dateResult` port (title `"<EEEEdMMMMyyyy> is in CW<n>"`).
- Grammar (case-insensitive, surrounding whitespace and a trailing `.` ignored): month names and 3-letter abbreviations (+ `sept`); `<d> <month> [<yyyy>]`, `<month> <d>[,] [<yyyy>]`, ordinals (`1st`, `2nd`, `3rd`, `24th`); numeric `d/m/yyyy`, `d.m.yyyy`, `d-m-yyyy` and the month-first equivalents (ambiguous → `dayFirst` decides; a component > 12 decides by value), two-digit years → 20yy; ISO `yyyy-mm-dd`; `today` (only via countdown — `today` alone is the keyword), `tomorrow`, `yesterday`; weekday names and 3-letter abbreviations: `friday` = the next Friday after today (today excluded), `next friday` = same as `friday`, `last friday` = the most recent Friday before today; invalid dates (`31 Feb 2027`) → null.
- Year-less day+month → this year if on/after today, else next year.

- [ ] **Step 1: Tests** `QueriesMatchSwift_All` (every `queries` vector now, including dates and countdowns to dates), `TemplateEEEEdMMMMyyyyMatchesSwift`, `RelativeAndYearlessRules` (today = Sun 2026-10-25: `tomorrow` → 10-26, `yesterday` → 10-24, `friday` → 10-30, `next friday` → 10-30, `last friday` → 10-23, `sunday` → 11-01, `Dec 12` → 2026-12-12, `Jan 5` → 2027-01-05, `25 Oct` → 2026-10-25), `NumericOrder` (`03/04/2027` → 3 Apr when dayFirst, 4 Mar when not; `24/12/2026` and `12/24/2026` → 24 Dec on both), `InvalidDatesAreNull`.
- [ ] **Step 2: Run** `dotnet test Windows/Weekmark.sln --filter "DateGrammarTests|QueryEngineTests"`. Expected: FAIL.
- [ ] **Step 3: Implement** the grammar and wire it in; `WeekContext` needs `dayFirst` (from its `WeekFormatter`).
- [ ] **Step 4: Run** the tests. Expected: PASS.
- [ ] **Step 5: Commit** `✨ feat(windows): date grammar for the lookup`

### Task 4: Search field in the widget

**Files:** Modify `Windows/src/Weekmark.App/Widget/WidgetView.axaml(.cs)`, `WidgetViewModel.cs`, `WidgetWindow.axaml.cs`, `Platform/IWindowLayering.cs`, `Platform/WindowsLayering.cs`; create `Windows/tests/Weekmark.App.Tests/WidgetSearchTests.cs`.

**Interfaces:**
- Consumes: `QueryEngine.Evaluate`, `QueryResult`.
- Produces: `WidgetViewModel.Query` (string, live), `WidgetViewModel.Result` (`QueryResult?`, first result; shown in the details line instead of the hover text — hover still wins while hovering, as on the Mac), the result week becomes the pinned/target week (orange) and the month jumps to it via `MonthOffsetFor` (validated as in v1); `ClearQuery()`; `CopyResult()` (copies `Result.CopyText` via the existing clipboard abstraction); idle reset: query, pin and month offset clear 120 s after the last interaction (Mac `resetIfIdle`); `IWindowLayering.AllowActivation(Window w, bool allow)` — Windows removes/restores `WS_EX_NOACTIVATE`; fallback no-op.
- UI: a rounded field with a magnifier icon under the month grid, placeholder `46 · Dec 12 · +6w · 46-50`, × button while non-empty; result line: title (accent/orange/red for error as Mac `WidgetView.result`) and `subtitle · detail`, single line each with ellipsis; scaled with widget size; hidden sections behave as before.
- Focus: pointer press in the field → `AllowActivation(true)`, `Activate()`, focus the TextBox; Esc → clear + give focus back; Enter → copy + give focus back; losing focus → `AllowActivation(false)` and re-apply the layer.

- [ ] **Step 1: Tests** `TypingShowsTheResultAndPinsTheWeek` (type `51` at now 2026-10-25 → result title starts `CW51 ·`, pinned week (2026,51), month title `December 2026`), `EnterCopiesAndEscClears` (Enter → clipboard gets the result's copy text; Esc → query empty, pin cleared, month back to today), `ErrorShowsNoMatch` (`hello` → title `No match for “hello”`, no pin), `IdleResetsAfterTwoMinutes` (advance the clock 121 s and tick → cleared), `MidnightWithQueryDoesNotThrow`, `FieldTakesAndReturnsActivation` (fake layering records `AllowActivation(true)` on click and `(false)` + a layer apply on Esc), `WidgetRendersWithSearchField` (headless render at all sizes, light/dark; PNGs saved).
- [ ] **Step 2: Run** `dotnet test Windows/Weekmark.sln --filter WidgetSearchTests`. Expected: FAIL.
- [ ] **Step 3: Implement**; the Mac reference for layout and behaviour is `Sources/Weekmark/WidgetView.swift` `finder` / `result` and `Sources/Weekmark/Browser.swift`.
- [ ] **Step 4: Run** all tests, then on the Mac `dotnet run --project Windows/src/Weekmark.App -- --demo-data --screenshot <scratch>.png --exit-after 8`; look at the PNG.
- [ ] **Step 5: Commit** `✨ feat(windows): search field in the widget`

### Task 5: Docs

**Files:** Modify `Windows/README.md` (search syntax table, like the Mac README's "Quick Finder syntax"), `Windows/TESTING.md` (new item: type `51`, `Dec 12`, `+6w`, `46-50`, `until CW52`, Enter copies, Esc clears; click-away returns the widget behind windows), `Windows/release-notes/0.1.0.md` (mention the search field).

- [ ] **Step 1: Write the changes.**
- [ ] **Step 2: Verify** every example in the README table against `QueryEngine.Evaluate` with a small throwaway C# test run (don't commit it) or by reading the vectors.
- [ ] **Step 3: Commit** `📝 docs(windows): search field syntax and test item`
