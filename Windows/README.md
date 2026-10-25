# Weekmark for Windows (preview)

The calendar week number on your Windows desktop, same content and look as the Mac widget. C# / .NET 10 + Avalonia.

## Download a build

- Until the first `windows-v0.1.0` pre-release exists: open the latest successful **Windows** run on the
  [Actions page](https://github.com/rockykusuma/weekmark/actions/workflows/windows.yml) and download the artifact
  `weekmark-win-x64` (most PCs) or `weekmark-win-arm64` (Snapdragon / ARM laptops).
- After that: [Releases](https://github.com/rockykusuma/weekmark/releases) (Windows builds are marked pre-release).
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

## Command-line flags

`--demo-data` (sample data, settings untouched), `--settings [tab]`, `--onboarding`, `--screenshot <path>`,
`--exit-after <seconds>`, `--autostart`.

## Test it

See [TESTING.md](TESTING.md).
