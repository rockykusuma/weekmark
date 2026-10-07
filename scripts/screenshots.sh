#!/bin/bash
# Re-capture the website screenshots (docs/assets/widget.png, finder.png) from a build, using sample
# data dated relative to today. Your own Weekmark settings are backed up and restored afterwards.
#
#   ./scripts/screenshots.sh                 # uses build/Weekmark.app
#   ./scripts/screenshots.sh /path/to/Weekmark.app
#
# Needs Screen Recording permission for the app running this script (System Settings → Privacy &
# Security → Screen & System Audio Recording). Without it the captures come out blank and are skipped.
set -euo pipefail
cd "$(dirname "$0")/.."

APP="${1:-build/Weekmark.app}"
DOMAIN="com.rockykusuma.weekmark"
OUT="docs/assets"
[ -d "$APP" ] || { echo "✗ No app at $APP (run ./build.sh first)"; exit 1; }

TMP=$(mktemp -d)
WAS_RUNNING=0; pgrep -x Weekmark >/dev/null && WAS_RUNNING=1
HAD_PREFS=0; defaults export "$DOMAIN" "$TMP/prefs.plist" 2>/dev/null && HAD_PREFS=1

restore() {
  pkill -x Weekmark 2>/dev/null || true
  sleep 0.5
  defaults delete "$DOMAIN" >/dev/null 2>&1 || true
  [ "$HAD_PREFS" = 1 ] && defaults import "$DOMAIN" "$TMP/prefs.plist"
  [ "$WAS_RUNNING" = 1 ] && open -a Weekmark 2>/dev/null || true
  rm -rf "$TMP"
}
trap restore EXIT

pkill -x Weekmark 2>/dev/null || true
sleep 0.5

# Sample data: code freeze in 3 weeks, release in 5 weeks (ISO weeks).
w() { date -v+"$1"w +%G; date -v+"$1"w +%V; }
read -r Y1 W1 <<<"$(w 3 | xargs)"; read -r Y2 W2 <<<"$(w 5 | xargs)"
W1=$((10#$W1)); W2=$((10#$W2))
defaults write "$DOMAIN" onboardingDone -bool true
defaults write "$DOMAIN" size xlarge
defaults write "$DOMAIN" background solid
defaults write "$DOMAIN" layer floating
for k in showMonth showProgress showPlanning showMilestones; do defaults write "$DOMAIN" "$k" -bool true; done
defaults write "$DOMAIN" milestones -string "[{\"id\":\"00000000-0000-4000-8000-000000000001\",\"name\":\"Code freeze\",\"week\":{\"year\":$Y1,\"week\":$W1},\"color\":\"purple\"},{\"id\":\"00000000-0000-4000-8000-000000000002\",\"name\":\"Release 5.0\",\"week\":{\"year\":$Y2,\"week\":$W2},\"color\":\"orange\"}]"
defaults write "$DOMAIN" sprint -string '{"enabled":true,"lengthWeeks":2,"anchor":{"y":2026,"m":1,"d":5},"firstNumber":1,"name":"Sprint","sprintsPerPI":5,"firstPI":1}'

list_windows() {
  swift -e '
import CoreGraphics
let l = CGWindowListCopyWindowInfo([.optionOnScreenOnly], kCGNullWindowID) as! [[String: Any]]
for w in l where (w[kCGWindowOwnerName as String] as? String) == "Weekmark" {
  let b = w[kCGWindowBounds as String] as! [String: Any]
  let wd = Int(b["Width"] as! Double), ht = Int(b["Height"] as! Double)
  if ht > 100 { print(w[kCGWindowNumber as String]!, wd, ht) }
}' 2>/dev/null
}

# Launch with the Quick Finder open; wait until both windows are on screen (relaunch once if needed).
WINDOWS=""
for attempt in 1 2; do
  pkill -x Weekmark 2>/dev/null || true
  sleep 0.5
  open -n "$APP" --args --find ""
  for _ in 1 2 3 4 5 6; do
    sleep 1
    WINDOWS=$(list_windows)
    if echo "$WINDOWS" | grep -q ' 640 ' && [ "$(echo "$WINDOWS" | grep -c .)" -ge 2 ]; then break 2; fi
  done
done
sleep 0.5  # let animations settle

[ -n "${DEBUG:-}" ] && echo "windows: $(echo "$WINDOWS" | tr '\n' '|')"
captured=0
while read -r id width height; do
  [ -z "${id:-}" ] && continue
  if [ "$width" = 640 ]; then name=finder; else name=widget; fi
  screencapture -x -o -l "$id" "$TMP/$name.png"
  bytes=$(wc -c < "$TMP/$name.png" | tr -d ' ')
  if [ "$bytes" -gt 20000 ]; then
    cp "$TMP/$name.png" "$OUT/$name.png"
    echo "  ✓ $OUT/$name.png (${width}x${height})"
    captured=$((captured + 1))
  else
    echo "  • $name capture looks blank ($bytes bytes); kept the old image. Check Screen Recording permission."
  fi
done <<<"$WINDOWS"

[ "$captured" -gt 0 ] || { echo "✗ No screenshots captured"; exit 1; }
