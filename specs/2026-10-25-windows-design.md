# Weekmark for Windows — design

Date: 2026-10-25 · Status: approved

## Why

Many of Rakesh's colleagues and friends use Windows laptops. They have seen Weekmark on his Mac and want the
same thing: **the calendar week number always visible on the desktop.** That is the heart of the Windows
version. Everything else (lookup, planning, year view) is secondary and can come in a later version.

Success: colleagues install it on their own (often company-managed) laptops without help, keep it running every
day, and stop asking "what week is it?". It must never disagree with the Mac app about a week number, holiday or
working-day count.

## Who builds and tests it

- Rakesh has **only a Mac.** There is no Windows machine for development.
- Windows builds are produced by **GitHub Actions on Windows runners.**
- **Colleagues are the testers** on real Windows laptops. Their feedback comes back to Rakesh in chat.

This drives the technology choice and the testing approach below.

## Technology (Claude's call, accepted by Rakesh)

- **C# on .NET 10 (LTS)** with **Avalonia 11** for the UI.
  - Avalonia runs on macOS too, so the real app (minus Windows-only pieces) can be run and looked at on Rakesh's
    Mac during development.
  - It draws its own UI (Skia), no embedded browser: light enough for something on screen all day.
- Ruled out: Swift on Windows (UI tooling immature, cannot be built from a Mac), Electron (too heavy for a small
  widget), WPF/WinUI (cannot be run on a Mac at all).
- The **core logic is ported from Swift `Sources/CWCore` to C#** (~1,100 lines: week maths, holidays and `.ics`
  import, sprints, Program Increments, quarters, milestones, the lookup engine). The macOS app keeps its Swift
  code; nothing in the Mac/iOS apps changes, except bug fixes (below).
- **Bugs found in the Swift core while porting are fixed in Swift** (with a test), not copied into C#. Both apps
  then share the corrected behaviour; the fix reaches Mac users with the next Mac release. *(Claude's call.)*

## Version 1 (what colleagues test first)

### Desktop widget
Same content and look as the Mac widget (`Sources/Weekmark/WidgetView.swift`), adapted to Windows type and
materials:
- Big week number, date range, year ring ("43 of 53 · 10 wks left").
- Planning chips: quarter, sprint ("Sprint 21 · day 5/10"), PI.
- 7-day strip with today and holidays marked; week progress bar.
- Up to 3 upcoming milestones with week and working-day countdowns.
- Month grid with a CW column: holidays in red, milestone flags in their colour, sprint-start lines, the current
  week highlighted. Hover a week to see its details; click to pin; scroll or ‹ › to change month; "Today" resets.
- Sizes: Small / Medium / Large / Extra Large (same scale factors as the Mac: 0.8, 1.0, 1.3, 1.7).
- Background: acrylic/Mica-style translucency where Windows supports it, otherwise solid; plus Solid and Clear.
- Appearance: System / Light / Dark.
- **Two layers**, as on the Mac: *On desktop (behind windows)* and *Float above windows*. "On desktop" should
  survive Win+D / "Show desktop" if at all possible; if not, it must reappear by itself afterwards.
- Drag to move (lockable). Remembers its position **per monitor** and returns to that monitor when it is
  reconnected; clamps on-screen if the monitor is gone.
- No taskbar button, not in Alt+Tab.
- The Mac's Quick Finder text field and Year button are **not** in version 1 (they arrive with version 2).

### Tray icon
- Icon in the notification area that **shows the week number itself** (rendered per DPI), tooltip
  "CW 43 · 19–25 Oct 2026".
- Menu: header line (week, dates, quarter/sprint), **Copy "CW43 (19–25 Oct)"**, Show/Hide widget, Position
  (layer, lock, snap to corners, move to monitor), Size, Settings…, Quit.
- Windows 11 hides new tray icons in the overflow by default: onboarding tells users how to pin it.

### Settings window
Mirrors the Mac settings (`Sources/Weekmark/SettingsUI.swift`) minus Mac-only items:
- **General:** week numbering (ISO / US), start with Windows (on by default), appearance.
  There is **no "System" option on Windows**: Windows' own week settings (including "First Full Week", which macOS
  doesn't have) could give a different number from a Mac. Default is ISO, or US when the Windows region is the
  United States. A Mac `numbering` value of `system` read from imported settings is treated as ISO.
  *(Claude's call.)*
- **Widget:** size, background, layer, which sections to show (planning, progress, milestones, month).
- **Planning:** sprints (length, anchor date, first number, name, sprints per PI, first PI), quarters (fiscal start
  month, by month / 13-week).
- **Holidays:** the 10 built-in countries (US, GB, DE, DK, NL, FR, SE, NO, PL, IN), first guessed from the Windows
  region; import a company `.ics` file.
- **Milestones:** add, edit, delete, colour.
- **About:** version, link to the website and source.

### First run
A small welcome window: week numbering, holiday country (pre-filled from the Windows region), the "pin the tray
icon" tip, and "start with Windows" (on). Then the widget appears in the top-right of the primary monitor.

### Dates and copy text
The Mac formats dates with the macOS locale through Apple's template APIs, which .NET doesn't have. Windows
version 1 matches the Mac's **English** output: copy text ("CW46 (9–15 Nov)", "CW1 2027 (28 Dec – 3 Jan)"), ranges
and titles use English month and weekday names, day-first or month-first according to the Windows regional format
(month-first for en-US, day-first otherwise). Test vectors cover en-GB and en-US. Other languages are a later
addition, in line with "translations" being a non-goal. *(Claude's call.)*

### Windows versions
Windows 10 22H2 and Windows 11, x64 and ARM64. Windows 11 gets Mica/acrylic; Windows 10 gets acrylic or the solid
fallback. The "on desktop" layering is tested on both. *(Claude's call: company laptops are often still on
Windows 10.)*

### Storage
Settings live in a JSON file under `%APPDATA%\Weekmark\`, using **the same keys and JSON shapes as the Mac**
(`numbering`, `sprint`, `quarter`, `milestones`, `regions`, `customHolidays`, …), so an import/export or future sync
needs no translation.

## Version 2 (after colleagues have used version 1)

- **Lookup** with a global hotkey (default to be chosen then; note that Ctrl+Alt equals AltGr on many European
  keyboards, so Ctrl+Alt+W may clash with typing): same syntax as the Mac (`46`, `Dec 12`, `+6w`, `46-50`,
  `until CW52`, milestone names). The Mac uses macOS's `NSDataDetector` for free-form dates; Windows needs an
  explicit date grammar covering the documented forms (month-name dates, numeric dates, weekday words like
  "next friday"), matched against the Mac's results.
- **Year at a Glance** window, as on the Mac.
- **Update notifications / in-place updates.**
- "Weeks in 2026" submenu in the tray, as on the Mac menu bar.

## Non-goals

- Syncing with the Mac, iPhone or Watch (iCloud is not available on Windows). Each Windows copy keeps its own data.
- Microsoft Store listing; Windows 11 Widgets board integration.
- Company-wide policy deployment (Group Policy / registry), the Windows equivalent of
  `packaging/mdm/Weekmark-Company.mobileconfig`. Possible later.
- Translations. Shipping the Avalonia app for macOS or Linux.

## Anti-goals (would be a failure even if it works)

- Any disagreement with the Mac app on week numbers, holidays, working days, sprint/PI/quarter labels or copy text.
- Needing admin rights to install or run (many colleagues have company-managed laptops).
- Feeling like a web page, or using more memory than a small native utility should. Working target: under
  about 100 MB working set while idle, measured on the CI Windows runner and reported per build; trimming or
  Native AOT only if measurements say it's needed. *(Claude's call.)*
- Network access beyond an optional update check; any tracking.
- Personal data in screenshots or CI artifacts (the 1.2 release briefly published a real milestone): anything
  captured automatically must use sample data.

## How correctness is guaranteed: shared test vectors

The Swift test suite (`Tests/CWCoreTests`, 33 tests) is the reference. Add a step on the Swift side that writes
**expected answers to a JSON file** committed in the repo, for example:
- week number and week-year for every day across several years, ISO and US;
- weeks in year;
- every built-in holiday per country for a span of years (Easter-based and substitute-day rules included);
- working days per week and for ranges;
- sprint / PI / quarter info for sample dates and configurations;
- `.ics` parsing of sample files;
- copy text and date-range formatting for sample weeks (in a fixed locale);
- (version 2) lookup results for a list of queries at a fixed "now".

The C# tests load the same file and must match exactly. The Swift side should fail its tests if the committed
file is out of date, so the two apps cannot drift apart silently. The C# core tests run on macOS too.

## Build, release and testing

- Code lives in this repo under `Windows/` (solution, core library, core tests, app).
- CI (GitHub Actions): core tests on macOS/Linux and Windows; app built and published on Windows for **x64 and
  ARM64**, self-contained (no .NET install needed).
- **Install without admin rights:** a per-user installer (installs under the user's profile) and/or a portable zip.
- **Code signing (Rakesh's decision): apply for free open-source signing through the SignPath Foundation; ship
  unsigned until it's approved.**
  - Unsigned builds are fine meanwhile. The download page and the colleague checklist explain the
    "Windows protected your PC → More info → Run anyway" step, and point anyone whose laptop blocks unsigned apps
    to building from source (below).
  - SignPath signs artifacts produced by GitHub Actions from the public repo, so release builds must come from CI,
    and the pipeline needs a signing step that can be switched on once approval arrives without other changes.
  - The application itself (project description, code-signing policy page, roles) is prepared as part of the
    work; Rakesh submits it.
- **Build and run from source on Windows (Rakesh's request):** anyone can `git clone` the repo on a Windows
  machine and run Weekmark with only the free .NET SDK installed (no Visual Studio, no admin rights beyond
  installing the SDK). `Windows/README.md` gives the exact steps: install the SDK (e.g. via `winget`), clone,
  one command to run, one command to produce a local self-contained build. The same commands run the app on
  Rakesh's Mac (without the Windows-only parts). Locally built apps don't carry the "downloaded from the internet"
  mark, so they normally don't trigger the SmartScreen warning, though strict IT policies can still block them.
- **A screenshot of the real Windows build** taken on the CI Windows runner with sample data, attached to the run,
  so Rakesh can see the actual Windows rendering without a PC.
- Windows releases go on this repo's GitHub Releases page but **must not become the "latest" release**: the
  website's "Download for macOS" button, README and Homebrew rely on `releases/latest` being the Mac DMG.
  Use separate tags (e.g. `windows-v0.1.0`).
- A short test checklist for colleagues (install, widget on desktop, survives Show desktop, multi-monitor, tray
  icon, settings, holidays for their country, restart Windows) ships alongside the build.
- The website gets a Windows download link once colleagues confirm version 1 works.

## Left to the builder

- Project structure inside `Windows/`, MVVM style, and libraries (tray icon, installer/updater tooling).
- The Win32 technique for "on desktop" layering and Show-desktop survival.
- Exact vector-file format and how the Swift side generates it.
- Visual details within "same content and look as the Mac widget".
