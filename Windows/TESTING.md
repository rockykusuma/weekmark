# Weekmark for Windows: test checklist

Thanks for trying it. Work through the list and note anything that looks wrong, even small things.

## How to report

Open a GitHub issue at https://github.com/rockykusuma/weekmark/issues (title it "Windows: ..."). Include:

- your Windows version (Settings, System, About) and whether the PC is **x64 or ARM64** ("System type")
- the numbers of the checklist items that failed or looked odd, and what you saw instead
- a screenshot if it is visual (blur, wrong position, wrong colours)

Don't paste anything private (for example the contents of your company `.ics`).

## Checklist

1. **Install.** Run the Setup (or unzip the Portable zip and run `Weekmark.exe`). If SmartScreen shows "Windows
   protected your PC", click More info, then Run anyway. Did it install without asking for admin rights?
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
10. **Copy text.** Use the tray menu's copy item and paste somewhere. Expect `CW43 (19–25 Oct)` for the current week.
11. **Holidays.** In Settings, choose your country. Are its public holidays marked in red on the right days?
12. **Milestone.** Add a milestone for a date this month. Does the week show a flag, and does it appear in the list?
13. **Company calendar.** Import your company `.ics` file. Do its entries show up? Any errors?
14. **Overnight.** Leave the PC asleep overnight. Does the widget show the new day, and the new week after a Sunday night?
15. **Restart Windows.** Does Weekmark start by itself after you sign in? (Settings lets you turn this off.)

Optional: run with `--demo-data` to see the widget with sample data, without touching your settings.
