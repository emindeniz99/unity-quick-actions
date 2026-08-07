#!/usr/bin/env bash
# Android device smoke for the Quick Actions Unity package. Drives a real device
# or emulator over adb and asserts the two things the headless harness
# (tools~/verify.sh) structurally cannot: that shortcuts really reach
# ShortcutManager, and that a tap on one really comes back into the game as a
# Performed event.
#
# The APK must be a DEVELOPMENT build of the Demo sample with QUICKACTIONS_ENABLED
# defined: a production build ships neither the trampoline <activity> (the gate
# strips it) nor the sample's autotest hook, so this script has nothing to drive.
# See README.md next to this script.
#
# Usage: android_device_smoke.sh <apk> <application-id> [adb-serial]
set -euo pipefail

APK="${1:-}"
APP_ID="${2:-}"
SERIAL="${3:-}"

if [ -z "$APK" ] || [ -z "$APP_ID" ]; then
  echo "usage: $(basename "$0") <apk> <application-id> [adb-serial]" >&2
  exit 2
fi

# Names below must match the package byte-for-byte — a typo here would make this
# script pass or fail for reasons that have nothing to do with the package:
#   Plugins/Android/QuickActionsBridge.java   EXTRA_ACTION_ID
#   Editor/Android/QuickActionsTrampolineInjectorAndroid.cs   TrampolineClass
#   Samples~/Demo/QuickActionsDemo.cs         AutotestExtra + the Catalog ids
TRAMPOLINE="com.emindeniz99.quickactions.QuickActionsTrampolineActivity"
EXTRA_ACTION_ID="com.emindeniz99.quickactions.ACTION_ID"
AUTOTEST_EXTRA="com.emindeniz99.quickactions.AUTOTEST"
SHORTCUT_IDS="new_game continue daily"
TAP_ID="new_game"

# Every wait is bounded: a smoke run that hangs in CI tells you less than one
# that fails. Overridable for slow emulators/first boots.
POLL_INTERVAL="${POLL_INTERVAL:-1}"
BOOT_ATTEMPTS="${BOOT_ATTEMPTS:-120}"
SHORTCUT_ATTEMPTS="${SHORTCUT_ATTEMPTS:-45}"
LOG_ATTEMPTS="${LOG_ATTEMPTS:-30}"

STEP="startup"

step() {
  STEP="$1"
  printf '\n== %s ==\n' "$1"
}

# Every failure path names the step that failed and exits non-zero, so a red run
# points at one thing instead of "the smoke failed".
fail() {
  printf '\nFAIL [%s]\n%s\n' "$STEP" "$1" >&2
  exit 1
}

# Serial is optional, so route every call through here rather than building an
# argument list that would need unquoted expansion to stay empty.
adb_() {
  if [ -n "$SERIAL" ]; then
    adb -s "$SERIAL" "$@"
  else
    adb "$@"
  fi
}

# Run the predicate until it succeeds or the attempt budget runs out.
poll() {
  local attempts="$1" n=1
  shift
  while [ "$n" -le "$attempts" ]; do
    if "$@"; then return 0; fi
    sleep "$POLL_INTERVAL"
    n=$((n + 1))
  done
  return 1
}

boot_completed() {
  [ "$(adb_ shell getprop sys.boot_completed 2>/dev/null | tr -d '\r\n')" = "1" ]
}

# The `dumpsys shortcut` section belonging to OUR package only. Scoping matters:
# another app's shortcut that happens to share an id must never satisfy the
# assertion below — the package's whole coexistence contract is about not
# confusing another publisher's shortcuts with ours.
our_shortcut_dump() {
  adb_ shell dumpsys shortcut 2>/dev/null | tr -d '\r' | awk -v pkg="$APP_ID" '
    $1 == "Package:" { inpkg = ($2 == pkg); next }
    inpkg { print }
  '
}

# String matching is done with `case`, not `grep -q`: `grep -q` exits at the
# first match and the resulting SIGPIPE upstream would be turned into a spurious
# failure by `set -o pipefail`.
shortcuts_registered() {
  local dump id
  dump="$(our_shortcut_dump)" || return 1
  for id in $SHORTCUT_IDS; do
    case "$dump" in
      *"id=$id,"*) ;;
      *) return 1 ;;
    esac
  done
  return 0
}

# The package's own log line (QuickActions.Dispatch), which the demo turns on via
# LoggingEnable in Awake. Asserting on it — not on the demo's echo — is what
# makes this a test of the package rather than of the sample.
performed_logged() {
  local log
  log="$(adb_ logcat -d 2>/dev/null | tr -d '\r')" || return 1
  case "$log" in
    *"Performed quick action '$TAP_ID'"*) return 0 ;;
  esac
  return 1
}

command -v adb >/dev/null 2>&1 || fail "adb is not on PATH (install Android platform-tools)."
[ -f "$APK" ] || fail "APK not found: $APK"

step "1/7 wait for the device"
poll "$BOOT_ATTEMPTS" boot_completed \
  || fail "no booted device after $((BOOT_ATTEMPTS * POLL_INTERVAL))s (serial='${SERIAL:-<default>}'). \`adb devices\` shows:
$(adb devices || true)"
# Guarded like every other adb call: bare, `set -e` would kill the script here with
# adb's raw status and none of the FAIL[step] context the header promises. The window
# is real — the poll above only proves the device answered a moment ago, and a
# cold-booted emulator can go offline between the two commands.
API_LEVEL="$(adb_ shell getprop ro.build.version.sdk | tr -d '\r\n')" \
  || fail "could not read ro.build.version.sdk — adb lost the device right after it
reported boot_completed (serial='${SERIAL:-<default>}'). \`adb devices\` shows:
$(adb devices || true)"
echo "device ready (API $API_LEVEL)"
# ShortcutManager itself only exists from API 25, so below that there is nothing
# to smoke — say so instead of failing on an empty dumpsys three steps later.
[ "$API_LEVEL" -ge 25 ] 2>/dev/null \
  || fail "this device reports API '$API_LEVEL'; dynamic shortcuts need API 25+."

step "2/7 install the APK"
if ! out="$(adb_ install -r "$APK" 2>&1)"; then
  fail "adb install failed:
$out"
fi
case "$out" in
  *Success*) ;;
  # adb has reported a zero exit for a failed install before; require the word.
  *) fail "adb install did not report Success:
$out" ;;
esac
echo "installed $APK"

step "3/7 reset app state"
# The autotest hook runs in Start, i.e. only in a FRESH process, and a previous
# run's shortcuts persist in ShortcutManager. Clearing data drops both; the
# force-stop is the fallback that still guarantees the cold start if the clear
# is refused. A failed clear is reported, not fatal: the tap assertion below
# still proves this run's behaviour on its own.
if ! adb_ shell pm clear "$APP_ID" >/dev/null 2>&1; then
  echo "warning: 'pm clear $APP_ID' failed — a previous run's shortcuts may survive," >&2
  echo "         so step 5 would then only prove the shortcuts exist, not that this run added them." >&2
fi
adb_ shell am force-stop "$APP_ID" >/dev/null 2>&1 || true

step "4/7 launch with the autotest extra"
# Resolve the launcher component instead of hard-coding it: the Unity entry point
# differs by version (UnityPlayerActivity vs UnityPlayerGameActivity).
# Same guard as step 1, and needed more here: the 2>/dev/null that keeps a clean
# "could not resolve" message also swallows adb's own error, so unguarded this line
# dies with literally no output — and the `case` below, written to catch a bad
# resolve, never runs.
COMPONENT="$(adb_ shell cmd package resolve-activity --brief "$APP_ID" 2>/dev/null | tr -d '\r' | tail -n 1)" \
  || fail "adb could not query the launcher activity for '$APP_ID' (device offline?). \`adb devices\` shows:
$(adb devices || true)"
case "$COMPONENT" in
  "$APP_ID"/*) ;;
  *) fail "could not resolve a launcher activity for '$APP_ID' (got '${COMPONENT:-<empty>}'). Is that the APK's application id?" ;;
esac
adb_ logcat -c >/dev/null 2>&1 || fail "could not clear logcat before the launch."
if ! out="$(adb_ shell am start -W -n "$COMPONENT" \
    -a android.intent.action.MAIN -c android.intent.category.LAUNCHER \
    --es "$AUTOTEST_EXTRA" add3 2>&1 | tr -d '\r')"; then
  fail "adb could not start $COMPONENT:
$out"
fi
# `am start` reports "Error: ..." on stdout and still exits 0 — check the text.
case "$out" in
  *Error*|*Exception*) fail "am start reported an error for $COMPONENT:
$out" ;;
esac
echo "launched $COMPONENT with $AUTOTEST_EXTRA=add3"

step "5/7 wait for the demo's shortcuts to reach ShortcutManager"
if ! poll "$SHORTCUT_ATTEMPTS" shortcuts_registered; then
  fail "after $((SHORTCUT_ATTEMPTS * POLL_INTERVAL))s, 'dumpsys shortcut' does not list all of: $SHORTCUT_IDS
This means the app did not publish them — the autotest hook did not run (not a
QUICKACTIONS_ENABLED dev build of the Demo sample?), or the write was rejected.
dumpsys section for $APP_ID:
$(our_shortcut_dump || true)"
fi
echo "registered: $SHORTCUT_IDS"

step "6/7 simulate a tap on '$TAP_ID'"
# A launcher tap is an intent at the exported trampoline, so starting it directly
# is the same thing the launcher does. It also exercises the REGISTERED-id path
# that the trampoline's spoof gate deliberately allows: an id that is not a live
# shortcut of ours is dropped there on purpose, so a green run here means the
# gate let a genuine tap through rather than that the gate is absent.
adb_ logcat -c >/dev/null 2>&1 || fail "could not clear logcat before the tap."
if ! out="$(adb_ shell am start -n "$APP_ID/$TRAMPOLINE" \
    -a android.intent.action.VIEW \
    --es "$EXTRA_ACTION_ID" "$TAP_ID" 2>&1 | tr -d '\r')"; then
  fail "adb could not start the trampoline $APP_ID/$TRAMPOLINE:
$out"
fi
case "$out" in
  *Error*|*Exception*) fail "am start reported an error for the trampoline (is the <activity> in the manifest? it is stripped without QUICKACTIONS_ENABLED):
$out" ;;
esac
echo "started $APP_ID/$TRAMPOLINE with $EXTRA_ACTION_ID=$TAP_ID"

step "7/7 assert the tap arrived as Performed"
if ! poll "$LOG_ATTEMPTS" performed_logged; then
  fail "no \"Performed quick action '$TAP_ID'\" in logcat within $((LOG_ATTEMPTS * POLL_INTERVAL))s.
The shortcut is registered, so the tap either never reached the trampoline, was
dropped by its ownership gate, or never surfaced in the game. QuickActions lines
seen since the tap:
$(adb_ logcat -d 2>/dev/null | tr -d '\r' | grep -i quickactions | tail -n 20 || true)"
fi

printf '\nPASS: %s on API %s — %s registered with ShortcutManager, and a tap on '\''%s'\'' came back as Performed.\n' \
  "$APP_ID" "$API_LEVEL" "$SHORTCUT_IDS" "$TAP_ID"
