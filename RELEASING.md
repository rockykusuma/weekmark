# Releasing Weekmark

## Every release: two steps

```bash
./scripts/ship.sh 1.1          # first run writes a draft of release-notes/1.1.md and stops
# edit release-notes/1.1.md, then:
git add release-notes/1.1.md && git commit -m "Release notes 1.1" && git push
./scripts/ship.sh 1.1          # checks everything, asks once, then publishes
```

Add `--dry-run` to see the checks and the plan without changing anything.

### What `ship.sh` does

1. **Checks** that you're on a clean `main` in sync with GitHub, `gh` is signed in, `.env.local` and the
   Developer ID certificate exist, the version is newer, and release notes exist.
2. **Bumps** `CFBundleShortVersionString` to the new version and `CFBundleVersion` by one, then commits.
3. **Builds, signs, notarizes and staples** the DMG with `scripts/release.sh`, which also runs the tests and
   writes the Sparkle feed `docs/appcast.xml`.
4. **Publishes** the GitHub release `v<version>` with the DMG and your notes.
5. **Updates the Homebrew tap** ([rockykusuma/homebrew-weekmark](https://github.com/rockykusuma/homebrew-weekmark)):
   version and sha256 in `Casks/weekmark.rb`.
6. **Pushes the update feed** last, so the download already exists when installed apps are told about it.
7. **Verifies** the live download (checksum and Gatekeeper) and waits for the feed to list the new version.

If a step fails (notarization timeout, network), fix the cause and run the same command again. Finished steps are
detected and skipped.

### Versions

- `1.1`: new features. `1.0.1`: fixes only. Release notes are what users read in the update dialog, so write
  them for users: what changed for them, not commit messages.

## One-time setup on a new Mac

| What | How |
|---|---|
| Xcode | App Store; then `xcode-select -p` should print a path |
| GitHub CLI | `brew install gh && gh auth login` |
| Developer ID certificate | Xcode → Settings → Accounts → team Y87BZN47C5 → Manage Certificates. Check with `security find-identity -v -p codesigning` |
| `.env.local` | `cp .env.local.example .env.local` and fill it in. The comments explain every value |
| Notarization profile | `xcrun notarytool store-credentials weekmark-notary --apple-id <id> --team-id Y87BZN47C5 --password <app-specific password>` |
| Sparkle signing key | Restore from your backup: `swift package resolve`, then `.build/artifacts/sparkle/Sparkle/bin/generate_keys --account weekmark -f weekmark-sparkle.key` |
| Homebrew tap checkout (optional) | `git clone git@github.com:rockykusuma/homebrew-weekmark ../homebrew-weekmark`. Without it, `ship.sh` clones a temporary copy |

## Troubleshooting

- **Notarization: "HTTP 403 … agreement is missing or has expired"**: the Apple account holder must accept the
  latest Program License Agreement at developer.apple.com/account. It can take a while to apply.
- **Notarization rejected**: `xcrun notarytool log <submission-id> --keychain-profile weekmark-notary` shows why.
- **Feed not updating**: `gh run list -R rockykusuma/weekmark` and check the "pages build and deployment" run.
- **Lost the Sparkle key**: existing installs can't verify updates signed with a new key. Users would need to
  download the next version manually once. Keep the backup safe.
