#!/bin/bash
# Build a signed + notarized release: dist/Weekmark-<version>.dmg (and optionally a .pkg for MDM/Intune).
#
# One-time setup:
#   1. A "Developer ID Application" certificate in your keychain (Apple Developer Program, $99/yr).
#      Optional: a "Developer ID Installer" certificate for the .pkg.
#   2. Store notarization credentials (app-specific password from appleid.apple.com):
#        xcrun notarytool store-credentials CW_NOTARY --apple-id you@example.com --team-id TEAMID --password xxxx-xxxx-xxxx-xxxx
#
# Usage:
#   DEV_ID="Developer ID Application: Your Name (TEAMID)" ./scripts/release.sh
#   DEV_ID=… INSTALLER_ID="Developer ID Installer: Your Name (TEAMID)" ./scripts/release.sh   # also builds .pkg
set -euo pipefail
cd "$(dirname "$0")/.."
: "${DEV_ID:?Set DEV_ID to your 'Developer ID Application: …' identity (see: security find-identity -v -p codesigning)}"
PROFILE=${NOTARY_PROFILE:-CW_NOTARY}
VERSION=$(/usr/libexec/PlistBuddy -c "Print :CFBundleShortVersionString" Resources/Info.plist)
APP="build/Weekmark.app"

./build.sh

echo "→ Signing with hardened runtime…"
SP="$APP/Contents/Frameworks/Sparkle.framework/Versions/B"
sign() { codesign --force --options runtime --timestamp --sign "$DEV_ID" "$@"; }
sign "$SP/XPCServices/Installer.xpc"
sign --preserve-metadata=entitlements "$SP/XPCServices/Downloader.xpc"
sign "$SP/Autoupdate"
sign "$SP/Updater.app"
sign "$APP/Contents/Frameworks/Sparkle.framework"
sign "$APP"
codesign --verify --strict --verbose=2 "$APP"

mkdir -p dist
DMG="dist/Weekmark-$VERSION.dmg"
STAGE=$(mktemp -d)
cp -R "$APP" "$STAGE/"
ln -s /Applications "$STAGE/Applications"
rm -f "$DMG"
hdiutil create -volname "Weekmark" -srcfolder "$STAGE" -ov -format UDZO "$DMG" >/dev/null
rm -rf "$STAGE"
codesign --force --timestamp --sign "$DEV_ID" "$DMG"

echo "→ Notarizing DMG (a few minutes)…"
xcrun notarytool submit "$DMG" --keychain-profile "$PROFILE" --wait
xcrun stapler staple "$DMG"
spctl -a -t open --context context:primary-signature -v "$DMG"

if [ -n "${INSTALLER_ID:-}" ]; then
  PKG="dist/Weekmark-$VERSION.pkg"
  echo "→ Building installer package for MDM…"
  pkgbuild --component "$APP" --install-location /Applications --identifier com.rockykusuma.weekmark \
           --version "$VERSION" --sign "$INSTALLER_ID" --timestamp "$PKG"
  xcrun notarytool submit "$PKG" --keychain-profile "$PROFILE" --wait
  xcrun stapler staple "$PKG"
fi

echo "→ Generating Sparkle appcast (docs/appcast.xml)…"
# Feed lists only the latest build; Sparkle just needs the newest version + its EdDSA signature.
FEED_SRC=$(mktemp -d)
cp "$DMG" "$FEED_SRC/"
[ -f "release-notes/$VERSION.md" ] && cp "release-notes/$VERSION.md" "$FEED_SRC/Weekmark-$VERSION.md"
mkdir -p docs
.build/artifacts/sparkle/Sparkle/bin/generate_appcast --account weekmark \
  --download-url-prefix "https://github.com/rockykusuma/weekmark/releases/download/v$VERSION/" \
  -o docs/appcast.xml "$FEED_SRC"
rm -rf "$FEED_SRC"

echo
echo "✓ Release ready:"
ls -lh dist/
echo "SHA-256 (for the Homebrew cask):"
shasum -a 256 "$DMG"
echo
echo "Next: create GitHub release v$VERSION, upload $DMG, then commit + push docs/appcast.xml (GitHub Pages serves it)."
