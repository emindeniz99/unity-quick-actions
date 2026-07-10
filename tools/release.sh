#!/usr/bin/env bash
# One command to produce every release artifact, no Unity required:
#   1. verify.sh   — .meta + C# compile + unit tests + Android plugin compile
#   2. store images — regenerate marketing PNGs at Asset Store sizes
#   3. unitypackage — build the drag-and-drop dist~/QuickActions.unitypackage
#
# After this, the only remaining steps need YOU: a licensed Unity (open/compile),
# a device pass, a real screenshot, then upload via the Publisher portal.
# See STORE_CHECKLIST.md.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"

echo "### 1/3  verify"
"$ROOT/tools/verify.sh"

echo
echo "### 2/3  store images"
python3 "$ROOT/tools/gen_store_images.py"

echo
echo "### 3/3  unitypackage"
python3 "$ROOT/tools/pack_unitypackage.py"

echo
echo "Artifacts ready:"
echo "  - dist~/QuickActions.unitypackage   (drag into the Unity Editor)"
echo "  - store~/*.png                       (Asset Store images)"
echo "  - store~/listing/*                   (paste-ready listing text)"
echo
echo "Reminder at release time: set the [0.1.0] date in CHANGELOG.md (currently"
echo "'Unreleased') and git-tag quick-actions/v<version> (see plans/openupm.md)."
