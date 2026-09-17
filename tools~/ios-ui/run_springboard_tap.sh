#!/usr/bin/env bash
# One pass of the iOS SpringBoard tap: point the XCUITest bundle at a booted
# simulator, tap ONE quick-action row, and turn the verdict the test writes into
# an exit status and a job-summary block. CI calls it twice per leg — once for a
# row Info.plist baked in, once for a row QuickActions.Add published at runtime —
# and a human can call it for either, which is why this lives in a script rather
# than inline in .github/workflows/unity-ci.yml.
#
# Usage: run_springboard_tap.sh <udid> <out-dir> <action-id> <row-title>
#
# Required environment:
#   QA_PROJECT   the SpringBoardTap.xcodeproj to run. Pass a copy OUTSIDE the
#                repo: derived data must never point back into a `~` folder.
#   QA_MARKER    host path of the testbed's performed-marker file — the app is
#                never launched from here, so it is the only delivery evidence.
# Optional:
#   QA_APP_ID    bundle id under test              (com.quickactions.testbed)
#   QA_APP_NAME  the home-screen icon's label      (QuickActionsDemo)
#   QA_DERIVED   derived data for the test bundle  (<out-dir>/DerivedData-ui)
#   QA_PASS      what this pass is called in the summary            (the id)
#   QA_EXPECT_LABEL  substring one button of the open menu must carry, or FAIL
#   QA_LEG       the CI leg's name, for the summary heading
#
# Exit 0 on PASS and on SKIPPED — the automation's own misses never fail a run —
# non-zero on FAIL and when the test wrote no verdict at all.
set -euo pipefail

UDID="${1:?usage: run_springboard_tap.sh <udid> <out-dir> <action-id> <row-title>}"
OUT="${2:?missing <out-dir>}"
ACTION_ID="${3:?missing <action-id>}"
ROW_TITLE="${4:?missing <row-title>}"

PROJECT="${QA_PROJECT:?QA_PROJECT must point at a SpringBoardTap.xcodeproj outside the repo}"
MARKER="${QA_MARKER:?QA_MARKER must point at the performed-marker file the testbed writes}"
APP_ID="${QA_APP_ID:-com.quickactions.testbed}"
APP_NAME="${QA_APP_NAME:-QuickActionsDemo}"
DERIVED="${QA_DERIVED:-$OUT/DerivedData-ui}"
PASS="${QA_PASS:-$ACTION_ID}"
LEG="${QA_LEG:-}"

mkdir -p "$OUT"
echo "tapping '$ROW_TITLE' ($ACTION_ID) — marker: $MARKER"

# xcodebuild forwards TEST_RUNNER_* from its own environment into the test
# runner's, prefix stripped — that is how the test is configured.
export TEST_RUNNER_QA_OUT="$OUT"
export TEST_RUNNER_QA_APP_ID="$APP_ID"
export TEST_RUNNER_QA_APP_NAME="$APP_NAME"
export TEST_RUNNER_QA_ROW_TITLE="$ROW_TITLE"
export TEST_RUNNER_QA_ACTION_ID="$ACTION_ID"
export TEST_RUNNER_QA_MARKER="$MARKER"
# Forwarded even when empty: xcodebuild only hands the runner TEST_RUNNER_* vars
# it is itself given, and the test treats an empty value as "not checked".
export TEST_RUNNER_QA_EXPECT_LABEL="${QA_EXPECT_LABEL:-}"

set +e
xcodebuild test \
  -project "$PROJECT" \
  -scheme SpringBoardTap \
  -destination "id=$UDID" \
  -derivedDataPath "$DERIVED" \
  -resultBundlePath "$OUT/SpringBoardTap.xcresult" \
  CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO CODE_SIGN_IDENTITY="" \
  2>&1 | tee "$OUT/xcodebuild-test.log"
rc="${PIPESTATUS[0]}"
set -e
echo "xcodebuild test exit: $rc"

# Context only, never asserted on: the unified log is per device and spans
# launches (same caveat as the coex leg).
xcrun simctl spawn "$UDID" log show --style compact --last 10m \
  --predicate "processImagePath CONTAINS \"$APP_NAME\"" \
  >"$OUT/unified-log.txt" 2>/dev/null || true
if [ -f "$MARKER" ]; then cp "$MARKER" "$OUT/marker.txt"; fi

VERDICT="$(cat "$OUT/launcher-tap.txt" 2>/dev/null || true)"
echo "verdict: ${VERDICT:-<none written>}"
if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
  {
    HEADING="### ios springboard tap"
    if [ -n "$LEG" ]; then HEADING="$HEADING ($LEG)"; fi
    echo "$HEADING — $PASS"
    echo
    echo '```'
    echo "${VERDICT:-no verdict written — see xcodebuild-test.log in the artifact}"
    echo '```'
  } >> "$GITHUB_STEP_SUMMARY"
fi

case "$VERDICT" in
  PASS*) echo "SpringBoard tap delivered: $VERDICT" ;;
  SKIPPED*) echo "::warning::SpringBoard tap not asserted — $VERDICT" ;;
  FAIL*) echo "::error::SpringBoard tap — $VERDICT"; exit 1 ;;
  *)
    echo "::error::the test wrote no verdict (xcodebuild exit $rc) — read xcodebuild-test.log in the artifact"
    exit 1 ;;
esac

# The verdict is authoritative, but a non-zero xcodebuild behind a PASS or
# SKIPPED means the runner tripped AFTER writing it.
if [ "$rc" -ne 0 ]; then
  echo "::error::xcodebuild test exited $rc despite the verdict above"
  exit 1
fi
