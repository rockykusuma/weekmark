#!/bin/bash
# Create (or refresh) the Developer ID provisioning profile that lets the Mac app use iCloud key-value
# storage, so milestones/sprints/holidays sync with the iPhone and Watch apps.
#
# Uses Xcode's automatic signing on a throwaway project with the Mac app's bundle id, which registers the
# App ID with iCloud and generates the profile. Requires Xcode → Settings → Accounts to be signed in with
# an Apple ID in team Y87BZN47C5, and the Developer ID Application certificate in your keychain.
#
# Output: Resources/Weekmark.provisionprofile (release.sh embeds it automatically).
set -euo pipefail
cd "$(dirname "$0")/.."
TEAM="${TEAM_ID:-Y87BZN47C5}"
BUNDLE="com.rockykusuma.weekmark"
TMP=$(mktemp -d)
mkdir -p "$TMP/Stub"
trap 'rm -rf "$TMP"' EXIT

cat > "$TMP/Stub/main.swift" <<'SWIFT'
import Foundation
print("Weekmark profile stub")
SWIFT
cat > "$TMP/project.yml" <<YML
name: ProfileStub
options:
  deploymentTarget: { macOS: "14.0" }
targets:
  Weekmark:
    type: application
    platform: macOS
    sources: [Stub]
    info:
      path: Stub/Info.plist
      properties: { CFBundleDisplayName: Weekmark }
    entitlements:
      path: Stub/Weekmark.entitlements
      properties:
        com.apple.developer.ubiquity-kvstore-identifier: "\$(TeamIdentifierPrefix)$BUNDLE"
    settings:
      base:
        PRODUCT_BUNDLE_IDENTIFIER: $BUNDLE
        DEVELOPMENT_TEAM: $TEAM
        CODE_SIGN_STYLE: Automatic
        ENABLE_HARDENED_RUNTIME: YES
YML
cat > "$TMP/export.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>method</key><string>developer-id</string>
  <key>signingStyle</key><string>automatic</string>
  <key>teamID</key><string>$TEAM</string>
</dict></plist>
PLIST

echo "→ Generating stub project"
(cd "$TMP" && xcodegen -q)
echo "→ Archiving (registers App ID $BUNDLE with iCloud)"
xcodebuild -project "$TMP/ProfileStub.xcodeproj" -scheme Weekmark -configuration Release \
  -archivePath "$TMP/stub.xcarchive" -allowProvisioningUpdates archive -quiet
echo "→ Exporting for Developer ID (creates the provisioning profile)"
xcodebuild -exportArchive -archivePath "$TMP/stub.xcarchive" -exportOptionsPlist "$TMP/export.plist" \
  -exportPath "$TMP/out" -allowProvisioningUpdates -quiet

PROFILE="$TMP/out/Weekmark.app/Contents/embedded.provisionprofile"
[ -f "$PROFILE" ] || { echo "✗ Export produced no provisioning profile"; exit 1; }
if ! security cms -D -i "$PROFILE" 2>/dev/null | grep -q "ubiquity-kvstore-identifier"; then
  echo "✗ Profile lacks iCloud key-value storage"; exit 1
fi
cp "$PROFILE" Resources/Weekmark.provisionprofile
EXP=$(security cms -D -i Resources/Weekmark.provisionprofile 2>/dev/null | plutil -extract ExpirationDate raw -o - - 2>/dev/null || echo "?")
echo "✓ Resources/Weekmark.provisionprofile (iCloud key-value storage, expires $EXP)"
