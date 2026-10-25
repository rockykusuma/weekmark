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
   - `winget install Microsoft.DotNet.SDK.10`
   - Without admin rights: install it per user with Microsoft's script
     ```powershell
     Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile dotnet-install.ps1
     .\dotnet-install.ps1 -Channel 10.0 -InstallDir $env:LOCALAPPDATA\dotnet
     $env:PATH = "$env:LOCALAPPDATA\dotnet;$env:PATH"   # this PowerShell window only
     ```
2. Get the code: `git clone https://github.com/rockykusuma/weekmark` then `cd weekmark`.
   No git? Download the zip from https://github.com/rockykusuma/weekmark (Code, Download ZIP), unzip it, and open a
   terminal in that folder.
3. Run it: `dotnet run --project Windows/src/Weekmark.App`
   To try it with sample data without touching your settings: `dotnet run --project Windows/src/Weekmark.App -- --demo-data`
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

## Command-line flags

`--demo-data` (sample data, settings untouched), `--settings [tab]`, `--onboarding`, `--screenshot <path>`,
`--exit-after <seconds>`, `--autostart`.

## Test it

See [TESTING.md](TESTING.md).
