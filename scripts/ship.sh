#!/bin/bash
# Ship a Weekmark release end to end.
#
#   ./scripts/ship.sh 1.1            # asks before publishing
#   ./scripts/ship.sh 1.1 --yes      # no prompt
#   ./scripts/ship.sh 1.1 --dry-run  # checks only, changes nothing
#
# Steps: pre-flight checks → bump version → build/sign/notarize (release.sh) → GitHub release
#        → Homebrew tap → update feed (appcast) → verify the live download and feed.
# Safe to re-run with the same version if a step fails; finished steps are skipped.
# See RELEASING.md.
set -euo pipefail
cd "$(dirname "$0")/.."

REPO="rockykusuma/weekmark"
TAP_REPO="rockykusuma/homebrew-weekmark"
TAP_DIR="${TAP_DIR:-../homebrew-weekmark}"
PLIST="Resources/Info.plist"
PB=/usr/libexec/PlistBuddy

bold() { printf '\033[1m%s\033[0m\n' "$*"; }
step() { echo; bold "→ $*"; }
ok()   { printf '  ✓ %s\n' "$*"; }
fail() { printf '\n✗ %s\n' "$*" >&2; exit 1; }
usage() { sed -n '2,12p' "$0" | sed 's/^# \{0,1\}//'; }

# version_gt A B → true if A > B (numeric, dot-separated)
version_gt() {
  [ "$1" = "$2" ] && return 1
  local IFS=.; local a=($1) b=($2) i
  for i in 0 1 2; do
    local x=${a[$i]:-0} y=${b[$i]:-0}
    [ "$x" -gt "$y" ] && return 0
    [ "$x" -lt "$y" ] && return 1
  done
  return 1
}

VERSION="${1:-}"; [ $# -gt 0 ] && shift
YES=0; DRY=0
for arg in "$@"; do
  case "$arg" in
    -y|--yes) YES=1 ;;
    -n|--dry-run) DRY=1 ;;
    *) usage; fail "Unknown option: $arg" ;;
  esac
done
[[ "$VERSION" =~ ^[0-9]+\.[0-9]+(\.[0-9]+)?$ ]] || { usage; fail "Give a version like 1.1 or 1.1.2"; }

CUR=$($PB -c "Print :CFBundleShortVersionString" "$PLIST")
BUILD=$($PB -c "Print :CFBundleVersion" "$PLIST")
NOTES="release-notes/$VERSION.md"
DMG="dist/Weekmark-$VERSION.dmg"

# ── Pre-flight ───────────────────────────────────────────────────────────────
step "Pre-flight checks"
command -v gh >/dev/null || fail "GitHub CLI missing: brew install gh"
GH_USER=$(gh api user --jq .login 2>/dev/null) || fail "GitHub CLI not signed in: gh auth login"
[ "$GH_USER" = "${REPO%%/*}" ] || fail "GitHub CLI is using '$GH_USER'. Switch with: gh auth switch -u ${REPO%%/*}"
ok "GitHub CLI signed in as $GH_USER"
[ -f .env.local ] || fail ".env.local missing. Copy .env.local.example and fill it in (see RELEASING.md)"
ok ".env.local present"
[ "$(git rev-parse --abbrev-ref HEAD)" = "main" ] || fail "Switch to the main branch first"
[ -z "$(git status --porcelain)" ] || fail "Uncommitted changes. Commit or stash them first:
$(git status --short)"
git fetch -q origin main --tags
[ "$(git rev-parse HEAD)" = "$(git rev-parse origin/main)" ] || fail "main is not in sync with origin/main (pull or push first)"
ok "On main, clean, in sync with GitHub"
if gh release view "v$VERSION" -R "$REPO" >/dev/null 2>&1; then
  fail "Release v$VERSION already exists on GitHub"
fi
RESUME=0
if [ "$CUR" = "$VERSION" ]; then
  RESUME=1; ok "Info.plist is already $VERSION; resuming a previous run"
elif version_gt "$VERSION" "$CUR"; then
  ok "Version $CUR → $VERSION (build $BUILD → $((BUILD + 1)))"
else
  fail "$VERSION is not newer than the current version $CUR"
fi
if [ ! -f "$NOTES" ]; then
  LAST_TAG=$(git describe --tags --abbrev=0 2>/dev/null || true)
  if [ "$DRY" = 1 ]; then
    echo "  • $NOTES is missing; a real run would create a draft from commits since ${LAST_TAG:-the start}"
  else
    mkdir -p release-notes
    {
      echo "# Weekmark $VERSION"
      echo
      git log ${LAST_TAG:+$LAST_TAG..}HEAD --no-merges --pretty='- %s' | grep -viE '^- (release|appcast|weekmark [0-9])' || true
    } > "$NOTES"
    echo
    bold "Draft release notes written to $NOTES"
    echo "  Edit them (users see these in the update dialog), commit, then run this again:"
    echo "    git add $NOTES && git commit -m \"Release notes $VERSION\" && git push"
    echo "    ./scripts/ship.sh $VERSION"
    exit 1
  fi
else
  ok "Release notes: $NOTES"
fi
security find-identity -v -p codesigning | grep -q "Developer ID Application" || fail "No Developer ID Application certificate in your keychain"
ok "Developer ID certificate found"
if [ -d "$TAP_DIR/.git" ]; then ok "Homebrew tap checkout: $TAP_DIR"; else ok "Homebrew tap will be cloned from $TAP_REPO"; fi

echo
bold "Ready to publish Weekmark $VERSION"
echo "  • bump Info.plist, build, sign, notarize ($DMG)"
echo "  • GitHub release v$VERSION on $REPO"
echo "  • Homebrew tap $TAP_REPO → $VERSION"
echo "  • update feed docs/appcast.xml → installed copies get the update"
[ "$DRY" = 1 ] && { echo; ok "Dry run: nothing changed"; exit 0; }
if [ "$YES" != 1 ]; then
  read -r -p "Publish? [y/N] " answer
  [[ "$answer" =~ ^[Yy]$ ]] || fail "Cancelled"
fi

# ── Bump ─────────────────────────────────────────────────────────────────────
if [ "$RESUME" = 0 ]; then
  step "Bumping version"
  $PB -c "Set :CFBundleShortVersionString $VERSION" "$PLIST"
  $PB -c "Set :CFBundleVersion $((BUILD + 1))" "$PLIST"
  git add "$PLIST"
  git commit -q -m "Release $VERSION"
  ok "Info.plist → $VERSION (build $((BUILD + 1)))"
fi

# ── Build, sign, notarize ────────────────────────────────────────────────────
step "Building, signing and notarizing (a few minutes)"
SHIP=1 ./scripts/release.sh
[ -f "$DMG" ] || fail "release.sh did not produce $DMG"
SHA=$(shasum -a 256 "$DMG" | cut -d' ' -f1)
ok "$DMG  sha256 $SHA"

# ── GitHub release ───────────────────────────────────────────────────────────
step "Publishing GitHub release"
git push -q origin main
gh release create "v$VERSION" "$DMG" -R "$REPO" --title "Weekmark $VERSION" --notes-file "$NOTES" --target main >/dev/null
git fetch -q origin --tags
ok "https://github.com/$REPO/releases/tag/v$VERSION"

# ── Homebrew tap ─────────────────────────────────────────────────────────────
step "Updating Homebrew tap"
if [ -d "$TAP_DIR/.git" ]; then
  TAP="$TAP_DIR"
  git -C "$TAP" pull -q --ff-only
else
  TAP=$(mktemp -d)/homebrew-weekmark
  gh repo clone "$TAP_REPO" "$TAP" -- -q
fi
CASK="$TAP/Casks/weekmark.rb"
sed -i '' -e "s/^  version \".*\"/  version \"$VERSION\"/" -e "s/^  sha256 \".*\"/  sha256 \"$SHA\"/" "$CASK"
git -C "$TAP" add Casks/weekmark.rb
git -C "$TAP" commit -q -m "weekmark $VERSION"
git -C "$TAP" push -q
ok "brew install --cask rockykusuma/weekmark/weekmark now installs $VERSION"
{
  echo "# Source of truth: $TAP_REPO (Casks/weekmark.rb). Keep in sync on each release."
  echo "# Install: brew install --cask rockykusuma/weekmark/weekmark"
  echo
  cat "$CASK"
} > packaging/homebrew/weekmark.rb

# ── Update feed (last, so the download exists before apps are told about it) ──
step "Publishing update feed"
git add docs/appcast.xml packaging/homebrew/weekmark.rb
git commit -q -m "Appcast for $VERSION"
git push -q origin main
ok "docs/appcast.xml pushed (GitHub Pages deploys it in about a minute)"

# ── Verify ───────────────────────────────────────────────────────────────────
step "Verifying"
TMP=$(mktemp -d)
curl -sfL -o "$TMP/dl.dmg" "https://github.com/$REPO/releases/download/v$VERSION/Weekmark-$VERSION.dmg" || fail "Download URL not reachable yet"
[ "$(shasum -a 256 "$TMP/dl.dmg" | cut -d' ' -f1)" = "$SHA" ] || fail "Downloaded DMG checksum differs from the local build"
spctl -a -t open --context context:primary-signature "$TMP/dl.dmg" 2>/dev/null && ok "Download is reachable, matches, and passes Gatekeeper"
rm -rf "$TMP"
FEED="https://rockykusuma.github.io/weekmark/appcast.xml"
for i in $(seq 1 18); do
  if curl -sf "$FEED" | grep -q "<sparkle:shortVersionString>$VERSION<"; then ok "Update feed lists $VERSION"; FEED_OK=1; break; fi
  sleep 10
done
[ "${FEED_OK:-0}" = 1 ] || echo "  • Feed not updated after 3 minutes; check the Pages deploy: gh run list -R $REPO"

echo
bold "✓ Weekmark $VERSION shipped"
