#!/bin/bash
# Rebuild docs/assets/og.png (the link-preview image for LinkedIn, Slack, X…) from scripts/og/og.html
# and the current widget/finder screenshots. Needs Google Chrome; fetches the web fonts.
set -euo pipefail
cd "$(dirname "$0")/.."
CHROME="/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
[ -x "$CHROME" ] || { echo "✗ Google Chrome not found"; exit 1; }
"$CHROME" --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=1 \
  --allow-file-access-from-files --virtual-time-budget=8000 --window-size=1200,627 \
  --screenshot="$PWD/docs/assets/og.png" "file://$PWD/scripts/og/og.html" 2>/dev/null
echo "✓ docs/assets/og.png"
