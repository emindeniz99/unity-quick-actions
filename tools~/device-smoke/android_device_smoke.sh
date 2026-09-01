#!/usr/bin/env bash
# Android device smoke for the Quick Actions Unity package. Drives a real device
# or emulator over adb and asserts the two things the headless harness
# (tools~/verify.sh) structurally cannot: that shortcuts really reach
# ShortcutManager, and that a tap on one really comes back into the game as a
# Performed event — both while the app is running (warm resume) and after a
# force-stop, which is the path a launcher tap takes on a quit app.
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
# The subset of SHORTCUT_IDS that must carry an ICON RESOURCE once registered:
# new_game is a static item with Icon=Add (the baked @drawable reference), daily
# a runtime Add(...) with IconType.Favorite (the Java name lookup, which falls
# back to the package's own ic_quickaction_builtin_favorite). `continue` is the
# static item with no icon at all. This is the one observation that turns "the
# drawable is in the APK" into "the lookup resolved it on an Android runtime".
ICON_SHORTCUT_IDS="${ICON_SHORTCUT_IDS:-new_game daily}"
TAP_ID="new_game"
# The cold step taps a DIFFERENT registered id on purpose: its assertion reads
# the whole `logcat -d` buffer, and `logcat -c` is known to under-clear on
# emulators while still exiting 0 (issuetracker.google.com/issues/175488702),
# which the `|| fail` on the clear cannot detect. Reusing TAP_ID would let the
# warm tap's line satisfy the cold assertion on its first poll.
COLD_TAP_ID="continue"

# Every wait is bounded: a smoke run that hangs in CI tells you less than one
# that fails. Overridable for slow emulators/first boots.
POLL_INTERVAL="${POLL_INTERVAL:-1}"
BOOT_ATTEMPTS="${BOOT_ATTEMPTS:-120}"
SHORTCUT_ATTEMPTS="${SHORTCUT_ATTEMPTS:-45}"
LOG_ATTEMPTS="${LOG_ATTEMPTS:-30}"
# The cold tap gets its own, larger budget: the warm tap only has to survive a
# resume, while this one waits out a whole process start — zygote fork, IL2CPP
# load, Unity's first frames — before the game can report anything.
COLD_LOG_ATTEMPTS="${COLD_LOG_ATTEMPTS:-60}"

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

# When the app goes silent, the question that decides everything downstream is
# whether the process is still alive (slow boot — raise the budget) or dead
# (crash — read the crash buffer). The dumpsys section alone cannot answer it:
# the first live CI run of the unity6 leg printed three healthy static
# shortcuts and "Calls: 0", which is what BOTH failure modes look like from
# ShortcutManager's side, and nothing else was captured before the emulator was
# torn down. Every fail message about the app's behaviour appends this.
app_diagnostics() {
  local pid
  pid="$(adb_ shell pidof "$APP_ID" 2>/dev/null | tr -d '\r\n')" || pid=""
  if [ -n "$pid" ]; then
    echo "process state: ALIVE (pid $pid) — still booting, or running without publishing."
  else
    echo "process state: DEAD — the app process is gone; it crashed or was killed."
  fi
  echo "-- crash buffer (logcat -b crash), last 40 lines:"
  adb_ logcat -d -b crash 2>/dev/null | tr -d '\r' | tail -n 40 || true
  echo "-- engine/package logcat lines, last 60:"
  adb_ logcat -d 2>/dev/null | tr -d '\r' \
    | grep -iE 'unity|quickactions|FATAL|AndroidRuntime' | tail -n 60 || true
}

# The package's own log line (QuickActions.Dispatch), which the demo turns on via
# LoggingEnable in Awake. Asserting on it — not on the demo's echo — is what
# makes this a test of the package rather than of the sample. Takes the id as an
# argument because the warm and cold steps assert different ids (see COLD_TAP_ID).
performed_logged() {
  local log id="$1"
  log="$(adb_ logcat -d 2>/dev/null | tr -d '\r')" || return 1
  case "$log" in
    *"Performed quick action '$id'"*) return 0 ;;
  esac
  return 1
}

# The precondition of the cold step, and the only thing separating it from the
# warm one. `am force-stop` can be accepted by AMS and still leave the process
# briefly alive (or, on a refusal, indefinitely alive), and its exit status
# says nothing either way — only an empty pidof does.
app_stopped() {
  [ -z "$(adb_ shell pidof "$APP_ID" 2>/dev/null | tr -d '\r\n')" ]
}

command -v adb >/dev/null 2>&1 || fail "adb is not on PATH (install Android platform-tools)."
[ -f "$APK" ] || fail "APK not found: $APK"

step "1/8 wait for the device"
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

step "2/8 install the APK"
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

step "3/8 reset app state"
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

step "4/8 launch with the autotest extra"
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

step "5/8 wait for the demo's shortcuts to reach ShortcutManager"
if ! poll "$SHORTCUT_ATTEMPTS" shortcuts_registered; then
  fail "after $((SHORTCUT_ATTEMPTS * POLL_INTERVAL))s, 'dumpsys shortcut' does not list all of: $SHORTCUT_IDS
This means the app did not publish them — the autotest hook did not run (not a
QUICKACTIONS_ENABLED dev build of the Demo sample?), the write was rejected, or
the app never got that far.
$(app_diagnostics)
dumpsys section for $APP_ID:
$(our_shortcut_dump || true)"
fi
echo "registered: $SHORTCUT_IDS"

# Same step, second assertion: the registered entries that should carry an icon
# do. `dumpsys shortcut` prints each entry as one `ShortcutInfo {id=…, flags=0x…
# […], …, iconRes=<id>[<name>], …}` line; a resolved resource icon shows as a
# non-zero iconRes and an `Ic-r` flag, an absent one as `iconRes=0[null]`. Both
# spellings are accepted, and a dump that carries neither token is reported as
# such rather than read as a pass — the format is the platform's, not ours.
icons_dump="$(our_shortcut_dump || true)"
for id in $ICON_SHORTCUT_IDS; do
  rest="${icons_dump#*"id=$id,"}"
  block="${rest%%ShortcutInfo \{*}"
  case "$block" in
    *"iconRes=0["*|*"iconRes=0,"*)
      fail "shortcut '$id' registered WITHOUT an icon resource (iconRes=0). Its icon
comes from a drawable resolved by name — a static @drawable reference for a baked
item, getIdentifier(\"ic_quickaction_<name>\") then the ic_quickaction_builtin_
fallback for a runtime one — so the drawable is missing from the APK, misnamed,
or the lookup did not run.
dumpsys entry:
$block" ;;
    *"iconRes="[1-9]*|*"Ic-r"*)
      echo "icon resource resolved for '$id'" ;;
    *)
      fail "cannot tell whether shortcut '$id' has an icon: its dumpsys entry carries
neither an iconRes= field nor an Ic-r flag, so the format this script expects has
changed. Read it and adjust the check; do not call this a pass.
dumpsys entry:
$block" ;;
  esac
done

step "6/8 simulate a WARM tap on '$TAP_ID' (app already running)"
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

step "7/8 assert the warm tap arrived as Performed"
if ! poll "$LOG_ATTEMPTS" performed_logged "$TAP_ID"; then
  fail "no \"Performed quick action '$TAP_ID'\" in logcat within $((LOG_ATTEMPTS * POLL_INTERVAL))s.
The shortcut is registered, so the tap either never reached the trampoline, was
dropped by its ownership gate, or never surfaced in the game.
$(app_diagnostics)
QuickActions lines seen since the tap:
$(adb_ logcat -d 2>/dev/null | tr -d '\r' | grep -i quickactions | tail -n 20 || true)"
fi
echo "warm tap on '$TAP_ID' came back as Performed"

step "8/8 simulate a COLD tap on '$COLD_TAP_ID' (app force-stopped first)"
# The other half of delivery. Step 6 tapped into a LIVE process, so the id only
# had to survive a resume (OnApplicationPause/Focus). A launcher tap on a quit
# app instead STARTS the process: the id rides the launch intent and has to
# still be there once the runtime is up and the game has subscribed. Nothing
# above can fail if that path is broken, which is why this step exists.
#
# force-stop only, deliberately no `pm clear`: the shortcuts published in step 4
# must stay registered, because the trampoline drops an id that is not a live
# shortcut of ours. Clearing data would turn a delivery bug into a gate
# rejection and this step would blame the wrong thing.
adb_ shell am force-stop "$APP_ID" >/dev/null 2>&1 || true
# The `|| true` above is right — force-stop's exit status proves nothing either
# way — but the PRECONDITION it exists to establish must be proven, or this
# step silently degrades into a second warm tap and passes while testing
# nothing new. Only an empty pidof says the process is really gone.
poll "$LOG_ATTEMPTS" app_stopped \
  || fail "'am force-stop $APP_ID' did not stop the app within $((LOG_ATTEMPTS * POLL_INTERVAL))s: it is still running (pid $(adb_ shell pidof "$APP_ID" 2>/dev/null | tr -d '\r\n')).
Without a dead process this step would send its intent into a LIVE app — i.e.
repeat step 6's warm tap — and say nothing about the cold-launch path."
# A dead app process is not yet a settled emulator: gfxstream tears the dead
# process's Vulkan objects down asynchronously, and the first live CI run
# showed "Destroyed VkDevice" logged AFTER the cold start had already been
# issued — with the restarted 2021.3 player then sitting engine-silent past
# the whole budget. Give the GPU side a moment before booting the next
# process into it. Harmless on a real device.
sleep "${COLD_SETTLE:-5}"
# Same fresh-log mechanism as steps 4 and 6 — clear, then read the whole buffer
# back with `logcat -d`. The assertion also taps a DIFFERENT id than step 6
# (see COLD_TAP_ID at the top): `logcat -c` alone is not trusted to isolate the
# two, because it can under-clear on emulators while exiting 0.
adb_ logcat -c >/dev/null 2>&1 || fail "could not clear logcat before the cold tap."
if ! out="$(adb_ shell am start -n "$APP_ID/$TRAMPOLINE" \
    -a android.intent.action.VIEW \
    --es "$EXTRA_ACTION_ID" "$COLD_TAP_ID" 2>&1 | tr -d '\r')"; then
  fail "adb could not start the trampoline $APP_ID/$TRAMPOLINE for the cold tap:
$out"
fi
case "$out" in
  *Error*|*Exception*) fail "am start reported an error for the cold trampoline tap:
$out" ;;
esac
echo "cold-started $APP_ID/$TRAMPOLINE with $EXTRA_ACTION_ID=$COLD_TAP_ID"
if ! poll "$COLD_LOG_ATTEMPTS" performed_logged "$COLD_TAP_ID"; then
  fail "no \"Performed quick action '$COLD_TAP_ID'\" in logcat within $((COLD_LOG_ATTEMPTS * POLL_INTERVAL))s of the COLD tap.
The equivalent tap passed in step 7 against a running app, so the trampoline and
its ownership gate are fine: what failed is the launch path — the id did not
survive the process start, or it was consumed before the game subscribed. (If
the device is simply slow, raise COLD_LOG_ATTEMPTS.)
$(app_diagnostics)
QuickActions lines seen since the cold tap:
$(adb_ logcat -d 2>/dev/null | tr -d '\r' | grep -i quickactions | tail -n 20 || true)"
fi

printf '\nPASS: %s on API %s — %s registered with ShortcutManager; a WARM tap on '\''%s'\'' and a COLD (force-stopped) tap on '\''%s'\'' both came back as Performed.\n' \
  "$APP_ID" "$API_LEVEL" "$SHORTCUT_IDS" "$TAP_ID" "$COLD_TAP_ID"
