# Windows Automatic Updates Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Windows parity item 4: Weekmark for Windows checks the GitHub releases for a newer Windows build, downloads it in the background, and installs it on restart, with a "Check for Updates…" menu item and an Updates section in Settings.

**Architecture:** Velopack (already the installer, `Velopack` 1.2.161, `VelopackApp.Build().Run()` in `Program.cs`) does the work: `UpdateManager` + `GithubSource(repo, null, prerelease: true)`. Its `GitBase.GetReleaseFeed` skips releases without `releases.{channel}.json`, so the Mac `v*` releases in the same repo are ignored, and the pack-time channels `win-x64`/`win-arm64` keep each architecture on its own feed. A downloaded update is applied by Velopack on the next start (`VelopackApp` auto-apply is on by default) or at once via "Restart to Update". A small `Updater` (observable state + timer) sits between Velopack and the UI behind a one-method-per-call `IUpdateEngine` seam so it can be tested headless. CI uploads each architecture's full `.nupkg` and `releases.win-<arch>.json` to the GitHub release.

**Tech Stack:** C#/.NET 10, Avalonia 11.3, CommunityToolkit.Mvvm, Velopack 1.2.161, xUnit + Avalonia.Headless.XUnit, GitHub Actions.

**Spec:** the design approved in chat on 2026-10-26, copied below. Mac reference: `Sources/Weekmark/Updater.swift`, `App.swift:501` (menu item), `SettingsUI.swift:74-80` (Updates section).

### Spec (approved design)

1. **For the user:** about a minute after Weekmark starts, and then once a day, it checks GitHub for a newer Windows version (x64 copies get x64 updates, ARM64 copies ARM64). If there is one it downloads quietly; then a **"Restart to Update to 0.3.1"** item appears at the top of the tray/widget menu. If you never click it, the update is installed the next time Weekmark starts, so the next start runs the new version. It works for the installed copy and the Portable zip; copies built from source say "Updates work in installed copies" and never check.
2. **Controls:** tray/widget menu **"Check for Updates…"** next to Settings, which opens Settings → General at the Updates section and checks at once. **Settings → General → Updates:** "Check for updates automatically" (on by default), **Check Now**, a status line ("Checking…", "Weekmark 0.3.0 is the latest version", "Downloading 0.3.1…", "0.3.1 is ready" + **Restart Now**, "Couldn't check: …"), and "Last checked: …".
3. **Release side:** each `windows-v*` release also publishes the update files (full package + update list per architecture). No delta packages.
4. **Privacy:** the privacy page, the Windows README and the About text say Weekmark for Windows checks GitHub once a day and that it can be turned off.
5. **Rollout:** ships as 0.3.0; 0.2.0 users install it by hand once.
6. **Not included:** a choice between stable and preview builds.

## Global Constraints

- Work under `Windows/`, `.github/workflows/windows.yml`, `docs/privacy.html` and this plan only. No new NuGet packages (Velopack is already referenced; keep `VPK_VERSION` and the package version in step).
- Update source: `new GithubSource("https://github.com/rockykusuma/weekmark", null, prerelease: true)`; `UpdateManager` with no explicit channel.
- New settings key `automaticUpdateChecks` (bool, default `true`), Windows only, loaded like the other bools in `SettingsStore.Parse`.
- Timing: first automatic check 60 s after start, then every 24 h while running; only when the setting is on and the copy is installed (`UpdateManager.IsInstalled`); never with `--demo-data`.
- Exact copy:
  - menu "Check for Updates…" (U+2026), placed directly before "Settings…"; "Restart to Update to {version}" as the first menu item when an update is ready
  - Settings section title "Updates"; toggle "Check for updates automatically"; buttons "Check Now", "Restart Now"
  - status lines: "Checking…", "Weekmark {current} is the latest version", "Downloading {version}…", "{version} is ready", "Couldn't check: {message}", "Updates work in installed copies."
  - "Last checked: {time}" (today → `HH:mm` local short time; earlier → short date + time); "Last checked: never"
  - About tab second line: "Runs on your PC. No account, no tracking.&#10;The only network request is the daily update check."
- Status messages from exceptions are one line (first line of `Exception.Message`, trimmed).
- Win32/Velopack calls never take the app down: every engine call is wrapped; failures become the "Couldn't check" state.
- Tests: `~/.dotnet/dotnet test Windows/Weekmark.sln`. Commit style: emoji conventional (`✨ feat(windows): …`).

## Review Focus

1. Offline, DNS failure or GitHub rate limit (403) during a check → "Couldn't check: …", no crash, the daily timer keeps running and the next check retries. → Task 1 test `CheckFailureShowsMessageAndRetriesLater`.
2. "Check Now" while an automatic check (or download) is running → one check, not two; the button is disabled meanwhile. → Task 1 test `ConcurrentChecksRunOnce`.
3. Turning "Check for updates automatically" off stops the timer; turning it on again schedules the next check (60 s), without an immediate check. → Task 1 test `ToggleStopsAndRestartsTimer`.
4. A copy that is not installed (source build, `dotnet run`) never calls the engine, the menu has no update items, Settings says "Updates work in installed copies." → Task 1 test `NotInstalledNeverChecks` + Task 2 test `MenuHidesUpdateItemsWhenUnavailable`.
5. A download that fails midway → "Couldn't check: …" (not "ready"), and the next check starts over. → Task 1 test `DownloadFailureIsNotReady`.
6. The x64 and ARM64 update files must not overwrite each other in the release job. → Task 3 workflow check that fails the job on a duplicate asset name.

---

### Task 1: Setting and Updater service

**Files:**
- Modify: `Windows/src/Weekmark.Core/Settings/AppSettings.cs`, `Windows/src/Weekmark.Core/Settings/SettingsStore.cs`
- Create: `Windows/src/Weekmark.App/Updates/Updater.cs` (Updater, `UpdateStage`, `IUpdateEngine`, `VelopackEngine`)
- Test: `Windows/tests/Weekmark.Core.Tests/SettingsStoreTests.cs`, `Windows/tests/Weekmark.App.Tests/UpdaterTests.cs`

**Interfaces:**
- Produces:
  - `AppSettings.AutomaticUpdateChecks` (`[JsonPropertyName("automaticUpdateChecks")] bool`, default `true`).
  - `public enum UpdateStage { Unavailable, Idle, Checking, UpToDate, Downloading, Ready, Failed }`
  - `public interface IUpdateEngine { bool IsInstalled { get; } string? CurrentVersion { get; } Task<string?> CheckAsync(); Task DownloadAsync(Action<int> progress); void ApplyAndRestart(); }` — `CheckAsync` returns the newer version or null and remembers the `UpdateInfo` for `DownloadAsync`/`ApplyAndRestart`.
  - `internal sealed class VelopackEngine : IUpdateEngine` (wraps `UpdateManager(new GithubSource(...))`; `ApplyAndRestart` calls `ApplyUpdatesAndRestart(info.TargetFullRelease)`).
  - `public sealed partial class Updater : ObservableObject, IDisposable` — `Updater(AppState state, IUpdateEngine engine, bool autoChecks = true)` (`autoChecks: false` for demo runs); `[ObservableProperty] UpdateStage stage`; `string StatusText`; `string? ReadyVersion`; `string LastCheckedText`; `bool CanCheck` (not Unavailable/Checking/Downloading); `Task CheckNow()`; `void RestartToUpdate()`; `event Action? Changed` (raised on every state change, for the tray menu); `internal TimeSpan FirstDelay = 60 s`, `internal TimeSpan Interval = 24 h` (tests shorten them).

Behaviour: `Unavailable` when `!engine.IsInstalled` (and then nothing else ever runs). Otherwise `Idle`; the timer (a `DispatcherTimer`) fires `CheckNow` after `FirstDelay`, then every `Interval`, while `state.Settings.AutomaticUpdateChecks` is true (re-evaluated on `state.Changed`). `CheckNow`: no-op unless `CanCheck` (a `Ready` copy may check again: a newer build replaces the ready one); `Checking` → engine null → `UpToDate`; version → `Downloading` → `Ready`; any exception → `Failed` with the one-line message. `LastChecked` is set when a check finishes (success or failure), in memory only. A ready update is not re-downloaded by later checks that find the same version.

- [ ] **Step 1: Write failing tests** with a fake `IUpdateEngine` (scripted results, call counters, a `TaskCompletionSource` to hold a check open):
  - `AutomaticUpdateChecksDefaultsOn` (Core): a missing key loads `true`; `"automaticUpdateChecks":false` loads false and saves back false.
  - `UpToDate`: engine returns null → `Stage == UpToDate`, `StatusText == "Weekmark 0.3.0 is the latest version"` (engine `CurrentVersion = "0.3.0"`), `LastCheckedText` starts with "Last checked: " and isn't "never".
  - `UpdateDownloadsThenReady`: engine returns "0.3.1" → `Downloading` with "Downloading 0.3.1…" while held, then `Ready`, "0.3.1 is ready", `ReadyVersion == "0.3.1"`; `RestartToUpdate()` calls `ApplyAndRestart` once.
  - `CheckFailureShowsMessageAndRetriesLater`, `ConcurrentChecksRunOnce`, `ToggleStopsAndRestartsTimer`, `NotInstalledNeverChecks`, `DownloadFailureIsNotReady` — per Review Focus 1–5. Use short `FirstDelay`/`Interval` and the headless dispatcher.
  - `LastCheckedNever`: before any check, `LastCheckedText == "Last checked: never"`.
- [ ] **Step 2:** Run the tests — they fail (don't compile).
- [ ] **Step 3:** Implement the setting (+ `case "automaticUpdateChecks"`), `Updater`, `IUpdateEngine`, `VelopackEngine`.
- [ ] **Step 4:** Run the whole solution — all pass.
- [ ] **Step 5:** Commit `✨ feat(windows): update checker`.

### Task 2: Menu items, Settings section and wiring

**Files:**
- Modify: `Windows/src/Weekmark.App/Tray/TrayController.cs`, `Windows/src/Weekmark.App/App.axaml.cs`, `Windows/src/Weekmark.App/Settings/SettingsViewModel.cs`, `Windows/src/Weekmark.App/Settings/SettingsWindow.axaml` (+ `.axaml.cs`)
- Test: `Windows/tests/Weekmark.App.Tests/TrayTests.cs`, `Windows/tests/Weekmark.App.Tests/SettingsViewModelTests.cs`

**Interfaces:**
- Consumes: Task 1 `Updater` (`Stage`, `StatusText`, `ReadyVersion`, `LastCheckedText`, `CanCheck`, `CheckNow`, `RestartToUpdate`, `Changed`), `AppSettings.AutomaticUpdateChecks`.
- Produces: `TrayController.Updater` (settable `Updater?`, null in tests that don't care) and `event Action? CheckForUpdatesRequested`; `SettingsViewModel(AppState state, string? startupExe = null, Updater? updater = null)` with `AutomaticUpdateChecks` (bool, `Set` pattern), `Updater` exposed for bindings, `CheckNowCommand`, `RestartNowCommand`, `ShowUpdates` (false when the updater is null or `Unavailable` — then only the "Updates work in installed copies." line shows); `SettingsWindow.ShowUpdates()` (selects General and brings the Updates section into view).

Behaviour: tray menu — first item "Restart to Update to {ReadyVersion}" when `Stage == Ready`, followed by a separator; "Check for Updates…" directly before "Settings…" unless `Unavailable`. Both menus rebuild when `Updater.Changed` fires (the tray's existing shape-diff refresh). App: one `Updater` created after `AppState` (`autoChecks: !o.DemoData`, engine `VelopackEngine`), handed to the tray and every `SettingsViewModel`; `CheckForUpdatesRequested` → `OpenSettings("general")`, `settingsWindow.ShowUpdates()`, `_ = updater.CheckNow()`; disposed in `Quit`. Settings General: section "Updates" at the end (toggle, Check Now enabled by `CanCheck`, status line, Restart Now visible when Ready, last-checked line). About text per Global Constraints.

- [ ] **Step 1: Write failing tests**
  - `MenuHasCheckForUpdatesBeforeSettings`: with an updater in `Idle`, the item before "Settings…" is "Check for Updates…"; invoking it raises `CheckForUpdatesRequested`.
  - `MenuShowsRestartWhenReady`: updater in `Ready` with "0.3.1" → first item "Restart to Update to 0.3.1", second a separator; invoking it calls the engine's `ApplyAndRestart`.
  - `MenuHidesUpdateItemsWhenUnavailable`: not-installed engine → neither item present.
  - `SettingsUpdatesSection`: `AutomaticUpdateChecks = false` saves the setting; `CheckNowCommand` runs one check on the fake engine; `ShowUpdates` false with a null updater.
- [ ] **Step 2:** Run — the new tests fail.
- [ ] **Step 3:** Implement; fix any index-based menu assertions the new items shift (find by header).
- [ ] **Step 4:** Run the whole solution — all pass.
- [ ] **Step 5:** Commit `✨ feat(windows): Check for Updates in the menu and Settings`.

### Task 3: Release pipeline, privacy and docs

**Files:**
- Modify: `.github/workflows/windows.yml` (package artifact paths, release job), `docs/privacy.html` (Network access), `Windows/README.md`, `Windows/TESTING.md`
- Create: `Windows/release-notes/0.3.0.md`

**Interfaces:**
- Consumes: the pack output in `releases/` (`vpk pack … --channel win-<arch>`).

Behaviour:
- Package job artifact `weekmark-win-<arch>` also carries `releases/*.nupkg` and `releases/releases.*.json`.
- Release job copies those files into `assets/` under their original names (the Setup/Portable renames stay), fails if two files would share a name, and the dry-run listing shows them. Velopack's update download looks assets up by the exact `FileName` in `releases.win-<arch>.json`, so these must not be renamed.
- Verify on the branch with `gh workflow run Windows --ref <branch> -f dry_run=true`: the run succeeds and its dry-run step lists, per architecture, Setup, Portable, one `*-full.nupkg` and `releases.win-<arch>.json`. Put the listing in the report.
- `docs/privacy.html` Network access: replace "The iPhone, Apple Watch and Windows apps make no network requests of their own." with two items: the Windows app checks for updates once a day by asking GitHub's releases API about this project's releases and downloading new versions from GitHub (GitHub sees the IP address; nothing else is sent; turn it off in Settings → General → Updates); the iPhone and Apple Watch apps make no network requests of their own. Plain short sentences in the page's voice.
- README: a short "Updates" section (what it checks, when, how to turn it off, 0.2.0 users install 0.3.0 by hand once). TESTING: new item 20 "Updates" (installed copy: Settings shows "Weekmark 0.3.0 is the latest version" after Check Now; with 0.3.1 published later: the menu shows "Restart to Update to 0.3.1", Restart Now restarts into 0.3.1, and without clicking, the next start runs 0.3.1; Portable zip does the same; turning automatic checks off stops the daily check; offline Check Now shows "Couldn't check: …"); Uninstall becomes 21. Release notes 0.3.0 in the voice of 0.2.0's notes: automatic updates, the one-time manual install from 0.2.0, the privacy note, "Please test" pointing at item 20.

- [ ] **Step 1:** Edit the workflow; run the dry run on the branch and confirm the listing (fix and re-run until it shows the expected files).
- [ ] **Step 2:** Edit privacy page, README, TESTING, release notes.
- [ ] **Step 3:** Run the whole solution's tests once (no code change expected) — all pass.
- [ ] **Step 4:** Commit `👷 ci(windows): publish update packages with each release` and `📝 docs(windows): automatic updates and privacy note`.
