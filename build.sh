#!/bin/bash
# Build "Weekmark.app" (universal, ad-hoc signed).
#   ./build.sh             → build/Weekmark.app
#   ./build.sh install     → also copy to /Applications and relaunch
#   SKIP_TESTS=1 ./build.sh
set -euo pipefail
cd "$(dirname "$0")"
APP="build/Weekmark.app"
VERSION=$(/usr/libexec/PlistBuddy -c "Print :CFBundleShortVersionString" Resources/Info.plist)

if [ "${SKIP_TESTS:-0}" != "1" ]; then
  echo "→ Running tests…"
  swift build --target CWCoreTests -q
  xcrun xctest .build/debug/CWCoreTests.xctest 2>&1 | grep -E "Executed .* tests" | tail -1
  xcrun xctest .build/debug/CWCoreTests.xctest >/dev/null 2>&1 || { echo "✗ Tests failed"; xcrun xctest .build/debug/CWCoreTests.xctest 2>&1 | grep error; exit 1; }
fi

echo "→ Compiling v$VERSION (universal)…"
swift build -c release --arch arm64 --arch x86_64 -q
BINDIR=$(swift build -c release --arch arm64 --arch x86_64 --show-bin-path)

rm -rf build
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources" "$APP/Contents/Frameworks"
cp "$BINDIR/Weekmark" "$APP/Contents/MacOS/Weekmark"
ditto "$BINDIR/Sparkle.framework" "$APP/Contents/Frameworks/Sparkle.framework"
if ! otool -l "$APP/Contents/MacOS/Weekmark" | grep -q "@executable_path/../Frameworks"; then
  install_name_tool -add_rpath "@executable_path/../Frameworks" "$APP/Contents/MacOS/Weekmark"
fi
cp Resources/Info.plist "$APP/Contents/Info.plist"

if [ ! -f Resources/AppIcon.icns ]; then
  echo "→ Generating icon…"
  swift scripts/make_icon.swift build/icon.png
  ICONSET=build/AppIcon.iconset; mkdir -p "$ICONSET"
  for s in 16 32 128 256 512; do
    sips -z $s $s build/icon.png --out "$ICONSET/icon_${s}x${s}.png" >/dev/null
    sips -z $((s*2)) $((s*2)) build/icon.png --out "$ICONSET/icon_${s}x${s}@2x.png" >/dev/null
  done
  iconutil -c icns "$ICONSET" -o Resources/AppIcon.icns
  rm -rf "$ICONSET" build/icon.png
fi
cp Resources/AppIcon.icns "$APP/Contents/Resources/AppIcon.icns"

codesign --force --deep --sign - "$APP"
echo "✓ Built $APP"

if [ "${1:-}" = "install" ]; then
  pkill -x Weekmark 2>/dev/null || true
  pkill -x CalendarWeek 2>/dev/null || true   # pre-rename build
  sleep 0.5
  rm -rf "/Applications/Weekmark.app"
  cp -R "$APP" /Applications/
  open "/Applications/Weekmark.app"
  echo "✓ Installed to /Applications and launched"
fi
