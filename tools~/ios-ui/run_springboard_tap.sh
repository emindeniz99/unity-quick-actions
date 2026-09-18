#!/usr/bin/env bash
# One pass of the iOS SpringBoard tap: point the XCUITest bundle at a booted
# simulator, tap ONE quick-action row, and turn the verdict the test writes into
# an exit status and a job-summary block. CI calls it three times per leg — a row
# Info.plist baked in, a row QuickActions.Add published at runtime, and one warm
# re-entry into an app that is already running — and a human can call it for any
# of them, which is why this lives in a script rather than inline in
# .github/workflows/unity-ci.yml.
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
#   QA_WARM      non-empty: warm pass. This script launches the app, waits for
#                its runtime to boot, records the pid, and after the tap checks
#                the pid is UNCHANGED — that comparison, not the test's own
#                app.state, is what proves the tap did not cold-start the app.
#                (XCUITest reported .runningForeground on both legs 15 s after
#                the Home press on run 109, so its state cannot answer this.)
#   QA_SETTLE_SECONDS  with QA_WARM, seconds to let the runtime boot before the
#                tap — a cold Unity launch on the Simulator spends 35-54 s
#                before its first frame                                    (75)
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
if [ -n "${QA_WARM:-}" ]; then
  echo "tapping '$ROW_TITLE' ($ACTION_ID) on the RUNNING app — marker: $MARKER"
else
  echo "tapping '$ROW_TITLE' ($ACTION_ID) — marker: $MARKER"
fi

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
export TEST_RUNNER_QA_WARM="${QA_WARM:-}"

# The warm pass owns its own warm-up, here rather than inside the test, because
# only simctl can see the process id — and "same pid across the tap" is the
# whole evidence that the app was re-entered rather than relaunched.
PID_BEFORE=""
if [ -n "${QA_WARM:-}" ]; then
  LAUNCH="$(xcrun simctl launch --terminate-running-process "$UDID" "$APP_ID")"
  echo "launch: $LAUNCH"
  PID_BEFORE="${LAUNCH##*: }"
  # simctl prints "<bundle id>: <pid>"; anything else means the pid is unknown,
  # and an unknown pid must read as "unproven", never as a match.
  case "$PID_BEFORE" in ''|*[!0-9]*) PID_BEFORE="" ;; esac
  SETTLE="${QA_SETTLE_SECONDS:-75}"
  echo "letting the runtime boot for ${SETTLE}s (pid $PID_BEFORE)"
  sleep "$SETTLE"
fi

app_pid() {
  xcrun simctl spawn "$UDID" launchctl list 2>/dev/null \
    | awk -v want="UIKitApplication:$APP_ID" 'index($3, want) == 1 { print $1; exit }'
}

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

# Warm pass only: same process before and after, or the tap relaunched the app
# and this pass measured the cold path a second time. A mismatch does not fail
# the run — it retracts the claim, the way the test retracts its own.
WARM_NOTE=""
if [ -n "${QA_WARM:-}" ]; then
  PID_AFTER="$(app_pid)"
  echo "pid before the tap: ${PID_BEFORE:-<unknown>} — after: ${PID_AFTER:-<unknown>}"
  printf 'pid before=%s after=%s\n' "${PID_BEFORE:-?}" "${PID_AFTER:-?}" > "$OUT/warm-pid.txt"
  if [ -n "$PID_BEFORE" ] && [ "$PID_BEFORE" = "$PID_AFTER" ]; then
    WARM_NOTE="pid $PID_BEFORE unchanged across the tap — the app was re-entered, not relaunched"
    echo "$WARM_NOTE"
  else
    WARM_NOTE="WARM UNPROVEN: pid went from ${PID_BEFORE:-<unknown>} to ${PID_AFTER:-<unknown>} — this tap may have cold-started the app"
    echo "::warning::$WARM_NOTE"
  fi
fi
if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
  {
    HEADING="### ios springboard tap"
    if [ -n "$LEG" ]; then HEADING="$HEADING ($LEG)"; fi
    echo "$HEADING — $PASS"
    echo
    echo '```'
    echo "${VERDICT:-no verdict written — see xcodebuild-test.log in the artifact}"
    if [ -n "$WARM_NOTE" ]; then echo "$WARM_NOTE"; fi
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
