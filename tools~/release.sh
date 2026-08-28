#!/usr/bin/env bash
# One command to produce every release artifact, no Unity required:
#   1. verify.sh   — .meta + C# compile + unit tests + Android plugin compile
#   2. store images — regenerate marketing PNGs at Asset Store sizes
#   3. unitypackage — build the drag-and-drop dist~/QuickActions.unitypackage
#
# The .unitypackage is a BUILD OUTPUT, not a source file: dist~/ is gitignored,
# CI rebuilds it reproducibly from the same script and attaches it to the GitHub
# Release. Nothing here commits or diffs it.
#
# What this script cannot do is the part that needs a licensed Unity and real
# hardware: the editor pass, the device pass, and a real screenshot.
# See MAINTAINING.md.
#
# Prereqs: dotnet SDK + a JDK (see tools~/setup.sh) for step 1, and Pillow
# (pip install Pillow) for step 2's image regen. The store~/ PNGs are already
# committed, so step 2 only matters when you actually change the art.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"

echo "### 1/3  verify"
"$ROOT/tools~/verify.sh"

echo
echo "### 2/3  store images"
python3 "$ROOT/tools~/gen_store_images.py"

echo
echo "### 3/3  unitypackage"
python3 "$ROOT/tools~/pack_unitypackage.py"

echo
echo "Artifacts ready:"
echo "  - dist~/QuickActions.unitypackage   (untracked build output; drag into the Editor)"
echo "  - store~/*.png                       (Asset Store images)"
echo "  - store~/listing/*                   (paste-ready listing text)"
echo
echo "Reminder at release time — the version lives in package.json:"
echo "  1. in the release PR, bump \"version\" in package.json AND rename the top"
echo "     CHANGELOG.md section to '## [<version>] - <date>' (verify.sh check 6"
echo "     fails the PR if the two disagree), and repoint the #v<version> install"
echo "     pins in the same commit"
echo "  2. merge that PR with a real merge commit (never squash)"
echo "  3. .github/workflows/release.yml then verifies, packs this .unitypackage,"
echo "     creates the v<version> tag and publishes the Release with your"
echo "     CHANGELOG section as its notes — do NOT push the tag by hand, and do"
echo "     not commit dist~/."
echo "See MAINTAINING.md § Cutting a release for the full checklist, including the"
echo "hand-pushed-tag fallback that ci.yml still covers."
