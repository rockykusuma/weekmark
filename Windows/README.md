# Weekmark for Windows (preview)

The calendar week number on your Windows desktop, same content and look as the Mac widget. C# / .NET 10 + Avalonia.

## Download a build

- Until the first `windows-v0.1.0` pre-release exists: open the latest successful **Windows** run on the
  [Actions page](https://github.com/rockykusuma/weekmark/actions/workflows/windows.yml) and download the artifact
  `weekmark-win-x64` or `weekmark-win-arm64`. It downloads as a zip: right-click it, **Extract all**, then run the
  `…-Setup.exe` from the extracted folder (not from inside the zip view).
- After that: [Releases](https://github.com/rockykusuma/weekmark/releases) (Windows builds are marked pre-release).
- x64 or ARM64? Check Settings → System → About → "System type". "x64-based processor" (almost all laptops) → the
  x64 download; "ARM-based processor" (Snapdragon / Copilot+ PCs) → ARM64. The wrong one says "This app can't run on
  your PC".
- Setup installs per user, without admin rights. The Portable zip runs from any folder.
- The builds are **unsigned** until the SignPath Foundation application is approved. If SmartScreen says "Windows
  protected your PC", click **More info**, then **Run anyway**. If your laptop doesn't allow that, build from source.

## Build and run from source

You only need the free .NET SDK (no Visual Studio, no admin rights beyond installing the SDK).

1. Install the SDK, either way:
   - `winget install Microsoft.DotNet.SDK.10`, then open a new terminal so it finds `dotnet`.
   - Without admin rights: install it per user with Microsoft's script. In PowerShell:
     ```powershell
     Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile "$env:TEMP\dotnet-install.ps1"
     powershell -NoProfile -ExecutionPolicy Bypass -File "$env:TEMP\dotnet-install.ps1" -Channel 10.0 -InstallDir "$env:LOCALAPPDATA\dotnet"
     $env:PATH = "$env:LOCALAPPDATA\dotnet;$env:PATH"   # this PowerShell window only
     ```
     (`-ExecutionPolicy Bypass` applies to that one script only; Windows blocks downloaded scripts by default.)
2. Get the code: `git clone https://github.com/rockykusuma/weekmark` then `cd weekmark`.
   No git? Download the zip from https://github.com/rockykusuma/weekmark (Code, Download ZIP), unzip it, and open a
   terminal in that folder.
3. Run it: `dotnet run --project Windows/src/Weekmark.App`
   To try it with sample data without touching your settings: `dotnet run --project Windows/src/Weekmark.App -- --demo-data`
   Runs from source (`dotnet run`, a Debug build) don't register Weekmark to start with Windows; a `dotnet publish -c
   Release` build (step 4) does, like the installed app.
4. Make a local self-contained build:
   ```powershell
   dotnet publish Windows/src/Weekmark.App -c Release -r win-x64 --self-contained -o out
   out\Weekmark.exe
   ```
   On an ARM64 PC use `-r win-arm64`.

Copies you build yourself carry no "downloaded from the internet" mark, so they usually don't trigger SmartScreen.
Strict company policies can still block them.

The same `dotnet run` command runs the app on a Mac (without the Windows-only parts: desktop layering, tray icon,
start with Windows).

## Year at a Glance

The calendar button in the widget's month header, or "Year at a Glance…" in the tray menu, opens all 12 months with
their week numbers. Hover a week for its details, click to pin it, ← → change the year, Ctrl+C copies the week shown at
the bottom (the hovered one, else the pinned one, else this week), This Year jumps back, Esc closes. The window remembers its size and position.

## Menus and holidays

- **Right-click the widget** for the app menu (Year at a Glance, Settings and so on). Right-click a week in the month
  grid for **Add Milestone in CW…**: Settings opens on the Milestones tab with that week filled in and the Name field ready.
- **Weeks in YYYY** lists every week of the year, in the tray menu and the widget's menu. Choosing a week brings the
  widget forward on that month.
- **Settings → Holidays:** add a holiday of your own (name and date), **Remove All** your company holidays, and see the
  next six under **Coming up**.
- **Settings → General → Show Welcome Screen Again** reopens the first-run welcome.

## Search field

The field at the bottom of the widget, under the month, answers questions about weeks and dates. Click it and type.

| Type | Example | Result |
|---|---|---|
| Week | `46`, `CW46`, `KW 46`, `3 2027`, `2027-W3` | Dates, how far away, working days, holidays, milestones, sprint |
| Date | `Dec 12`, `12 Dec`, `24/12/2026`, `2026-12-24`, `tomorrow`, `friday`, `next friday` | Which CW it falls in |
| Offset | `+6w`, `-2w`, `+10d`, `in 6 weeks`, `3 weeks ago` | Target week or date |
| Range | `46-50`, `CW52–CW2`, `46 to 48` | Number of weeks and working days; holidays in the range are listed |
| Countdown | `until CW52`, `to dec 24` | Weeks or days, and working days, left |
| Milestone | `release` | Matching milestones |
| Keywords | `today`, `next week`, `last week` | Current, next or previous week |

Enter copies the result · Esc or × clears the field · it clears itself after 2 minutes idle.
Dates without a year mean the next one (`Dec 12` in late December is next year). Numeric dates like `03/04/2026`
follow your Windows region's order (day first or month first). From week 40 onward, a week number of 12 or lower
means next year.

## Quick Finder

Press **Ctrl+Alt+W** from any app to open a small search box near the top of the screen. It answers the same
questions as the search field above, and it works while the widget is hidden.

- Enter copies the selected result and closes the box.
- Ctrl+Enter shows that week in the widget.
- Up and Down pick a result. Esc closes the box.
- Press the hotkey again to close it.

Change the hotkey, or turn it off, in Settings → General → Quick Finder hotkey. If Windows refuses the
shortcut because another app has it, Settings says so in red. You can also open it from the tray menu or the widget menu, or start it with `--find [query]`.

On keyboards where AltGr+W types a character (Czech, Slovak, Hungarian, Croatian), pick Alt+Win+W in Settings.

## Command-line flags

`--demo-data` (sample data, settings untouched), `--settings [tab]`, `--onboarding`, `--year`, `--screenshot <path>`,
`--exit-after <seconds>`, `--autostart`, `--find [query]` (opens the Quick Finder).

## Test it

See [TESTING.md](TESTING.md).
