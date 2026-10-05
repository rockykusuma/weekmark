# Weekmark

**Any calendar week, one keystroke away.**
A native macOS widget for anyone who plans in calendar weeks: software teams, PMs, release managers.

- A big week number, the date range and a 7-day strip on your desktop (on any display; it remembers which one)
- **Quick Finder** (⌃⌥W from any app): type `46`, `Dec 12`, `+6w`, `46-50` or `until CW52`, then press ⏎ to copy **"CW46 (9–15 Nov)"**
- **Milestones** with week and working-day countdowns ("Release 5.0 · CW46 · 5w · 30d")
- **Sprints, Program Increments and quarters** ("Sprint 20 · day 5/9", "PI 4 · 5/5", "Q3 FY27")
- **Holidays and working days** for 10 countries, plus your company's own `.ics` holiday calendar
- A warning when ISO and US week numbers differ for the same week
- Runs on your Mac: no account, no tracking. The only network request is the optional Sparkle update check.

## Quick Finder syntax

| Type | Example | Result |
|---|---|---|
| Week | `46`, `CW46`, `KW 46`, `3 2027`, `2027-W3` | Dates, how far away, working days, holidays, milestones, sprint |
| Date | `Dec 12`, `12/12/2026`, `next friday` | Which CW it falls in |
| Offset | `+6w`, `-2w`, `+10d`, `in 6 weeks`, `3 weeks ago` | Target week or date |
| Range | `46-50`, `CW52–CW2`, `46 to 48` | Number of weeks and working days, holidays included |
| Countdown | `until CW52`, `to dec 24`, `today -> 46` | Weeks and working days left |
| Milestone | `release` | Matching milestones |
| Keywords | `today`, `next week`, `last week` | |

⏎ copies the selected result · ⌘⏎ shows it in the widget · ↑↓ selects · esc closes.
From week 40 onward, a week number of 12 or lower means next year.

## Widget
- Drag the top half to move it. Right-click for the menu; right-click a week row to add a milestone there.
- Hover a week to see its dates. Click it to pin it. Scroll over the calendar to change month.
- Holidays are shown in red, milestone weeks get a flag, and a line marks where each sprint starts.
- The menu bar shows `CW 41`. Its menu has a copy action and **Weeks in 2026/2027**, a list of every week with its dates, holidays and milestones.

## Build
```bash
./build.sh            # runs tests, builds build/Weekmark.app (universal, ad-hoc signed)
./build.sh install    # also installs to /Applications and relaunches
swift test            # core tests (week maths, parser, holidays, sprints)
```
Command-line flags: `--find "<query>"`, `--settings <general|widget|planning|holidays|milestones|about>`, `--onboarding`.

## Release
```bash
DEV_ID="Developer ID Application: Your Name (TEAMID)" ./scripts/release.sh
```
Signs the app (including Sparkle's helpers) with the hardened runtime, builds a DMG, notarizes and staples it,
writes the Sparkle feed to `docs/appcast.xml`, and prints the SHA-256 for `packaging/homebrew/weekmark.rb`.
Set `INSTALLER_ID` as well to build a signed `.pkg` for MDM/Intune. One-time setup is described at the top of the script.

Then upload the DMG to a GitHub release tagged `v<version>` and push. GitHub Pages (from `/docs`) serves both the
landing page and `appcast.xml`, and installed copies update themselves.

**Update signing key:** the EdDSA private key is stored in your login keychain under the account `weekmark`
(public key in `Resources/Info.plist`). Back it up with
`.build/artifacts/sparkle/Sparkle/bin/generate_keys --account weekmark -x weekmark-sparkle.key` and keep the file
out of git. If you lose it, existing installs can't verify future updates.

## Website
`docs/index.html` is the landing page, with a live CW lookup demo and a 53-week year strip. In the GitHub repo, enable
Pages from the `main` branch `/docs` folder to publish it at `https://rockykusuma.github.io/weekmark/`.

## Company rollout (IT)
`packaging/mdm/Weekmark-Company.mobileconfig` is an example profile that presets week numbering, holiday
regions, sprints, fiscal quarters, company milestones and company holidays for every Mac. Managed values override user settings.

## Code layout
- `Sources/CWCore`: platform-independent logic (week maths, `QueryEngine`, holidays and `.ics` import, sprints, quarters, milestones). Fully unit-tested.
- `Sources/Weekmark`: AppKit/SwiftUI app (widget panel, Quick Finder, global hotkey, settings, onboarding, Sparkle updater)
- `docs/`: landing page and Sparkle appcast (GitHub Pages)
- `release-notes/`: per-version notes shown in the update dialog
- `Tests/CWCoreTests`: 27 tests covering 53-week years, year boundaries, DST, locales, holiday rules and the parser
