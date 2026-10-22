#!/bin/bash
# Re-capture the iPhone and Apple Watch screenshots on the website (docs/assets/iphone-week.png,
# iphone-widgets.png, watch.png) from the simulators, with sample data dated relative to today.
#
#   ./scripts/mobile-screenshots.sh
#
# Uses the first available "iPhone 17 Pro" and "Apple Watch Series 10 (46mm)" simulators (override with IPHONE_SIM /
# WATCH_SIM set to a device name or UDID). Builds into /tmp/weekmark-mobile-dd so ./build.sh's
# `rm -rf build` can't take the results with it.
set -euo pipefail
cd "$(dirname "$0")/.."

PROJ="Mobile/WeekmarkMobile.xcodeproj"
DD="/tmp/weekmark-mobile-dd"
SHOTS="$DD/shots"; OUT="docs/assets"
mkdir -p "$SHOTS"

sim() {  # sim <name-or-udid> → udid of an available device
  xcrun simctl list devices available | grep -F "$1" | head -1 | grep -oE '[0-9A-F-]{36}'
}
IPH=$(sim "${IPHONE_SIM:-iPhone 17 Pro (}") || { echo "✗ No iPhone simulator"; exit 1; }
WAT=$(sim "${WATCH_SIM:-Apple Watch Series 10 (46mm)}") || { echo "✗ No Apple Watch simulator"; exit 1; }
echo "iPhone $IPH · Watch $WAT"

for d in "$IPH" "$WAT"; do xcrun simctl boot "$d" 2>/dev/null || true; done
xcrun simctl bootstatus "$IPH" -b >/dev/null
xcrun simctl status_bar "$IPH" override --time 9:41 --batteryState charged --batteryLevel 100 \
  --cellularBars 4 --wifiBars 3 2>/dev/null || true
xcrun simctl status_bar "$WAT" override --time 9:41 2>/dev/null || true

echo "→ Building iPhone app"
xcodebuild -quiet -project "$PROJ" -scheme Weekmark -configuration Debug \
  -destination "id=$IPH" -derivedDataPath "$DD" build
echo "→ Building Watch app"
xcodebuild -quiet -project "$PROJ" -target WeekmarkWatch -configuration Debug \
  -sdk watchsimulator SYMROOT="$DD/watch" build

IAPP=$(find "$DD/Build/Products/Debug-iphonesimulator" -maxdepth 1 -name '*.app' | head -1)
WAPP=$(find "$DD/watch/Debug-watchsimulator" -maxdepth 1 -name '*.app' | head -1)
IBID=com.rockykusuma.weekmark.mobile
WBID=com.rockykusuma.weekmark.mobile.watchkitapp

xcrun simctl install "$IPH" "$IAPP"
xcrun simctl install "$WAT" "$WAPP"

shoot() {  # shoot <udid> <bundle> <out> [args…]
  local d=$1 b=$2 o=$3; shift 3
  xcrun simctl terminate "$d" "$b" 2>/dev/null || true
  xcrun simctl launch "$d" "$b" "$@" >/dev/null
  sleep 4
  xcrun simctl io "$d" screenshot --type=png "$SHOTS/$o.png" >/dev/null 2>&1
  echo "  ✓ $o"
}
shoot "$IPH" "$IBID" week -demoData -tab 0
shoot "$IPH" "$IBID" find -demoData -tab 1
shoot "$IPH" "$IBID" widgets -demoData -widgetGallery
shoot "$WAT" "$WBID" watch

sips -Z 1100 "$SHOTS/week.png"    --out "$OUT/iphone-week.png"    >/dev/null
sips -Z 1100 "$SHOTS/widgets.png" --out "$OUT/iphone-widgets.png" >/dev/null
cp "$SHOTS/watch.png" "$OUT/watch.png"
echo "✓ Updated $OUT/iphone-week.png, iphone-widgets.png, watch.png (raw shots in $SHOTS)"
