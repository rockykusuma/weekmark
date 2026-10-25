# Weekmark for Windows: test checklist

Thanks for trying it. Work through the list and note anything that looks wrong, even small things.

## How to report

Open a GitHub issue at https://github.com/rockykusuma/weekmark/issues (title it "Windows: ..."). Include:

- your Windows version (Settings, System, About) and whether the PC is **x64 or ARM64** ("System type")
- the numbers of the checklist items that failed or looked odd, and what you saw instead
- a screenshot if it is visual (blur, wrong position, wrong colours)
- if Weekmark crashed or closed by itself: the file `%LOCALAPPDATA%\Weekmark\crash.log` (paste that path into
  Explorer's address bar). It holds only the error details and versions, none of your settings.

Don't paste anything private (for example the contents of your company `.ics`).

## Checklist

1. **Install.** Pick the download that fits your PC: Settings → System → About → "System type".
   "x64-based processor" (almost all laptops) → the x64 download; "ARM-based processor" (Snapdragon / Copilot+ PCs)
   → ARM64. The wrong one says "This app can't run on your PC". A zip (the Actions download or the Portable zip):
   right-click it, **Extract all**, then run the `…-Setup.exe` (or `Weekmark.exe`) from the extracted folder, not
   from inside the zip view. If SmartScreen shows "Windows protected your PC", click More info, then Run anyway.
   Did it install without asking for admin rights?
2. **Widget appears** at the top right of your main screen, behind your windows.
3. **Drag and restart.** Drag the widget elsewhere, quit it from the tray menu, start it again. Is it where you left it?
4. **Two monitors** (if you have them). Move it to the second screen, restart, then unplug and re-plug the monitor.
   Does it stay on a visible screen and come back to its place?
5. **Scaling.** At 125 % and 150 % display scaling, is the text sharp (not blurry) and the widget the right size?
6. **On desktop.** Press Win+D (or click Show desktop) and bring the windows back. Does the widget stay visible
   on the desktop and behind your windows?
7. **Float above windows.** Switch it on in the tray menu or Settings. Does it stay above other windows? Switch it off again.
8. **Tray number.** Pin the Weekmark icon to the visible tray area. Does it show the current week number?
9. **Tray menu.** Try each item: copy, show/hide, position, size, settings, quit.
10. **Copy text.** Use the tray menu's copy item and paste somewhere. Expect the current week, e.g. `CW43 (19–25 Oct)`.
11. **Holidays.** In Settings, choose your country. Are its public holidays marked in red on the right days?
12. **Milestone.** Add a milestone for a date this month. Does the week show a flag, and does it appear in the list?
13. **Company calendar.** Import your company `.ics` file. Do its entries show up? Any errors?
14. **Overnight.** Leave the PC asleep overnight. Does the widget show the new day, and the new week after a Sunday night?
15. **Restart Windows.** Does Weekmark start by itself after you sign in? (Settings lets you turn this off.)
16. **Search field.** Click the field at the bottom of the widget (it works even though the widget sits behind your
    windows) and type `51`, then `Dec 12`, `+6w`, `46-50` and `until CW52`. Does each give a sensible answer? Enter
    should copy the result (paste to check); Esc or × should clear the field. Paste a week number with Ctrl+V, and again with right-click → Paste: does
    each give an answer? Click somewhere else: does the widget go
    back behind your windows? Does typing ever freeze the widget?
17. **Year at a Glance.** Open it from the calendar button in the widget's month header, and again from the tray menu
    (the second time it should bring the same window forward, not open another). Hover a week: does it show its
    details? Click a week to pin it, then Ctrl+C and paste: does it give the week, e.g. `CW43 (19–25 Oct)`? Do ←, →
    and This Year change the year? Does Esc close it? Resize the window: do the months reflow into more or fewer
    columns? Close it and open it again: does it keep its size and position? Move it to a second monitor, close it,
    unplug that monitor and open it again: does it open centred?
    With monitors at different scales (125% and 100%): move the window to the other one, close and reopen: same size and
    place? On a 1080p laptop at 125% or 150%, does the first open fit the screen, with the Copy button visible?
18. **Menus and holidays.** Right-click the widget while it sits on the Desktop layer: does the menu open above your
    windows? Right-click a week in the month grid and choose Add Milestone in CW…: does Settings open on that week,
    with the Name field focused? Open Weeks in YYYY from the tray menu and from the widget's menu on a 1080p laptop at
    125%: does the list scroll in both? Choose a week: does the widget come forward on that month? Right-click
    repeatedly: do menus stack up? In Settings → General click Show Welcome Screen Again: does it open, and does a
    second click bring the same window forward? In Settings → Holidays add a holiday with a name and date (and one
    without a name): do they appear under "Added by you" and in Coming up? Does Remove All clear them? With no
    country selected, does Coming up say "No holidays selected"? Pick a date in the holiday date picker and press Add:
    does the list show that date, not today? With the widget menu open, click another app's window: does the menu
    close? Does Esc close it, and do the arrow keys move through it? Right-click the widget while it sits on the
    desktop (behind windows), then press Esc: does it stay behind your windows, not on top?
19. **Uninstall** from Settings → Apps. Does Weekmark stop starting with Windows and leave nothing running?

Optional: run with `--demo-data` to see the widget with sample data, without touching your settings.
