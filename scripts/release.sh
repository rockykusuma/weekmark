#!/bin/bash
# Build a signed + notarized release: dist/Weekmark-<version>.dmg (and optionally a .pkg for MDM/Intune),
# then write the Sparkle update feed to docs/appcast.xml.
#
# Credentials: copy .env.local.example to .env.local and fill it in (see the comments there).
# Any variable can also be passed on the command line, which wins over .env.local:
#   DEV_ID="Developer ID Application: …" ./scripts/release.sh
set -euo pipefail
cd "$(dirname "$0")/.."

# Load .env.local without overriding variables already set in the environment.
if [ -f .env.local ]; then
  while IFS='=' read -r key value; do
    [[ -z "$key" || "$key" =~ ^[[:space:]]*# ]] && continue
    key=$(echo "$key" | xargs)
    value=$(echo "$value" | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*#.*$//' -e 's/^"\(.*\)"$/\1/' -e "s/^'\(.*\)'$/\1/")
    [ -z "${!key:-}" ] && export "$key=$value"
  done < .env.local
fi

: "${DEV_ID:?Set DEV_ID in .env.local (see .env.local.example)}"
NOTARY_PROFILE=${NOTARY_PROFILE:-}
SPARKLE_ACCOUNT=${SPARKLE_ACCOUNT:-weekmark}
GITHUB_REPO=${GITHUB_REPO:-rockykusuma/weekmark}

notarize() {
  if [ -n "$NOTARY_PROFILE" ]; then
    xcrun notarytool submit "$1" --keychain-profile "$NOTARY_PROFILE" --wait
  else
    : "${APPLE_ID:?Set NOTARY_PROFILE, or APPLE_ID + APP_SPECIFIC_PASSWORD + TEAM_ID, in .env.local}"
    : "${APP_SPECIFIC_PASSWORD:?Set APP_SPECIFIC_PASSWORD in .env.local}"
    : "${TEAM_ID:?Set TEAM_ID in .env.local}"
    xcrun notarytool submit "$1" --apple-id "$APPLE_ID" --password "$APP_SPECIFIC_PASSWORD" --team-id "$TEAM_ID" --wait
  fi
}

# Fail early if the signing identity isn't in the keychain.
security find-identity -v -p codesigning | grep -qF "$DEV_ID" \
  || { echo "✗ \"$DEV_ID\" not found in your keychain. Run: security find-identity -v -p codesigning"; exit 1; }
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
notarize "$DMG"
xcrun stapler staple "$DMG"
spctl -a -t open --context context:primary-signature -v "$DMG"

if [ -n "${INSTALLER_ID:-}" ]; then
  PKG="dist/Weekmark-$VERSION.pkg"
  echo "→ Building installer package for MDM…"
  pkgbuild --component "$APP" --install-location /Applications --identifier com.rockykusuma.weekmark \
           --version "$VERSION" --sign "$INSTALLER_ID" --timestamp "$PKG"
  notarize "$PKG"
  xcrun stapler staple "$PKG"
fi

echo "→ Generating Sparkle appcast (docs/appcast.xml)…"
# Feed lists only the latest build; Sparkle just needs the newest version + its EdDSA signature.
FEED_SRC=$(mktemp -d)
cp "$DMG" "$FEED_SRC/"
[ -f "release-notes/$VERSION.md" ] && cp "release-notes/$VERSION.md" "$FEED_SRC/Weekmark-$VERSION.md"
mkdir -p docs
if [ -n "${SPARKLE_KEY_FILE:-}" ]; then KEYARG=(--ed-key-file "$SPARKLE_KEY_FILE"); else KEYARG=(--account "$SPARKLE_ACCOUNT"); fi
.build/artifacts/sparkle/Sparkle/bin/generate_appcast "${KEYARG[@]}" \
  --download-url-prefix "https://github.com/$GITHUB_REPO/releases/download/v$VERSION/" \
  -o docs/appcast.xml "$FEED_SRC"
rm -rf "$FEED_SRC"

echo
echo "✓ Release ready:"
ls -lh dist/
echo "SHA-256 (for the Homebrew cask):"
shasum -a 256 "$DMG"
echo
[ -z "${SHIP:-}" ] && echo "Tip: ./scripts/ship.sh $VERSION does the GitHub release, Homebrew tap and feed for you."
