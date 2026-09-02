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

# ---------------------------------------------------------------------------
# Opt-in tail (CAPTURE_LONGPRESS=1): photograph the launcher's long-press sheet.
#
# Everything above is an assertion. This is not one. `dumpsys shortcut` proves
# each icon resolved to a RESOURCE ID; it says nothing about what a launcher
# draws, and until PR #18 run 61 (API 30, 2026-09-02) nothing had ever seen the
# package's built-in art drawn by a launcher.
# So, once the verdict is printed, optionally drive the launcher — home, open
# the app drawer, long-press the app's icon, screencap — and keep the picture.
#
# It is best-effort by construction and can never change the verdict:
#   * the PASS line above is printed BEFORE any of this runs;
#   * the whole block runs in a subshell with errexit and pipefail OFF, its
#     status discarded, so the script's own status stays 0 (see the tail);
#   * every adb call is bounded by `timeout` where coreutils has it — a hung
#     `uiautomator dump` would otherwise eat the job's whole timeout, which is
#     the one way a decoration like this could turn a green smoke red.
# The drawer gesture and the sheet belong to whatever launcher the system image
# ships (Launcher3 on an AOSP `default` image; something else on a google_apis
# or OEM one — the capture logs which HOME activity it resolved, because that
# is the first thing you need when it misses). A run that finds nothing is an
# expected outcome: it prints what it saw instead of failing.
#
# Written into CAPTURE_DIR (default: a temp dir):
#   longpress.png     the screen after the long press — the artifact for eyes
#   ui-drawer.xml     the hierarchy the app icon was located in
#   ui-longpress.xml  the hierarchy after the press, i.e. the machine-readable
#                     half: "shortcut sheet visible: yes/no" comes from it
# ---------------------------------------------------------------------------

# The app's launcher label, which is what the drawer shows and what the icon is
# found by: Unity writes android:label from productName, `QuickActionsDemo` in
# all three Examples~ testbeds. For any other APK, read it out of
# `aapt2 dump badging <apk>` (application-label) and pass it in.
CAPTURE_LABEL="${CAPTURE_LABEL:-QuickActionsDemo}"
# The labels the sheet should list, as '|'-separated TITLE=SUBTITLE pairs (not
# ids; '|' because they contain spaces). Either half counts: Launcher3 draws the
# LONG label — the package's Subtitle — when it fits the popup and the short one
# otherwise, and the first sheet ever photographed here (API 30, run 61) read
# "Start a fresh run", not "New Game". These are the three the testbeds'
# QuickActionsSettings.asset bakes statically; the smoke's AUTOTEST=add3 re-adds
# new_game and continue under the same ids (dropped as duplicates) and a dynamic
# `daily`, which the sheet lists as a fourth row.
# (The default lives in its own variable: an apostrophe inside "${…:-…}" is a
# quote to bash's parser and swallows the rest of the file.)
default_titles="New Game=Start a fresh run|Continue=Resume your save|Daily Reward=Claim today's gift"
CAPTURE_TITLES="${CAPTURE_TITLES:-$default_titles}"
CAPTURE_TIMEOUT="${CAPTURE_TIMEOUT:-60}"
CAPTURE_PRESS_MS="${CAPTURE_PRESS_MS:-1500}"

# adb, bounded. `adb_` cannot be reused here: `timeout` runs a program, not a
# shell function, so the serial has to be spelled out again.
cap_adb() {
  if command -v timeout >/dev/null 2>&1; then
    if [ -n "$SERIAL" ]; then
      timeout "$CAPTURE_TIMEOUT" adb -s "$SERIAL" "$@"
    else
      timeout "$CAPTURE_TIMEOUT" adb "$@"
    fi
  else
    adb_ "$@"
  fi
}

# uiautomator writes on the device, so a dump is a dump plus a pull. It also
# refuses while the screen animates ("Could not get idle state"), which is why
# the sequence below goes HOME first and waits: a Unity player redrawing every
# frame is never idle.
cap_dump_ui() {
  local dest="$1"
  cap_adb shell uiautomator dump /sdcard/quickactions-ui.xml >/dev/null 2>&1 || return 1
  cap_adb pull /sdcard/quickactions-ui.xml "$dest" >/dev/null 2>&1 || return 1
  [ -s "$dest" ]
}

# Locate the app's icon by its launcher label in a fresh hierarchy dump. Sets
# `xy` ("<x> <y>", or empty). Retried, not because the launcher is slow to
# answer but because a dump taken mid-animation comes back as the previous
# screen. Reads the caller's `dir` and `py`.
cap_find_icon() {
  local tries="$1" attempt rc
  xy=""
  for attempt in $(seq 1 "$tries"); do
    if cap_dump_ui "$dir/ui-drawer.xml"; then
      xy="$(python3 "$py" icon "$CAPTURE_LABEL" <"$dir/ui-drawer.xml")"
      rc=$?
      [ "$rc" -eq 0 ] && return 0
      xy=""
      # Exit 5: the label is on screen only as the launcher's PREDICTION of the
      # app. That screen will not change by waiting, and pressing there opens the
      # launcher's suggestions sheet, not the app's popup — so no retry: the
      # caller escalates to the drawer at once.
      if [ "$rc" -eq 5 ]; then
        echo "capture: '$CAPTURE_LABEL' is on screen only as a launcher prediction — escalating to the drawer"
        return 1
      fi
    fi
    xy=""
    echo "capture: attempt $attempt — no '$CAPTURE_LABEL' icon in the hierarchy yet"
    sleep 3
  done
  return 1
}

capture_longpress() {
  local dir tmp py wh w h x y_from y_to xy ix iy out rc handle hxy hx hy hold_s pressed
  local titles=()

  printf '\n== capture (best effort, NOT part of the verdict): the long-press sheet ==\n'
  command -v python3 >/dev/null 2>&1 || {
    echo "capture: python3 is not on PATH — nothing to parse the UI dumps with; skipped."
    return 0
  }
  dir="${CAPTURE_DIR:-${TMPDIR:-/tmp}/quickactions-longpress}"
  mkdir -p "$dir" || {
    echo "capture: could not create '$dir'; skipped."
    return 0
  }
  # Scaffolding, so NOT in CAPTURE_DIR: that directory is uploaded whole as a
  # CI artifact and a helper script is not evidence of anything.
  tmp="$(mktemp -d 2>/dev/null)" || tmp="$dir"
  py="$tmp/parse_ui.py"
  # Heredoc body and terminator sit at column 0 — `<<` (not `<<-`) strips
  # nothing, so indenting them would indent the Python with them.
cat >"$py" <<'PY'
"""Read `wm size` output or a uiautomator dump on stdin; print what the capture
needs, so the shell never has to parse XML. Three modes:

  size               -> "<width> <height>"
  icon <label>       -> "<centre-x> <centre-y>" of the smallest node whose
                        text/content-desc is that app label; exit 5 when the
                        only match is a launcher prediction of the app
  labels <title[=subtitle]>...
                     -> one "<title>: yes|no" line each, either form counting
                        (the launcher draws whichever fits); exit 0 if all were
                        found, 4 if some, 3 if none
"""
import re
import sys
import xml.etree.ElementTree as ET

BOUNDS = re.compile(r"\[(-?\d+),(-?\d+)\]\[(-?\d+),(-?\d+)\]")
CONTROL = re.compile(r"[\x00-\x08\x0b\x0c\x0e-\x1f]")


def read_nodes(data):
    """Every <node> of a uiautomator dump, as attribute dicts."""
    try:
        root = ET.fromstring(data)
    except ET.ParseError:
        # An app may put control characters in a label; uiautomator serializes
        # them and ElementTree then rejects the whole dump. Strip them, retry.
        root = ET.fromstring(CONTROL.sub("", data))
    return [element.attrib for element in root.iter("node")]


def labels_of(node):
    """Both places a launcher can carry a visible name."""
    return [t for t in (node.get("text", ""), node.get("content-desc", "")) if t]


def centre(node):
    """(x, y, area) of bounds="[x1,y1][x2,y2]", or None if it has no extent."""
    match = BOUNDS.match(node.get("bounds", ""))
    if not match:
        return None
    x1, y1, x2, y2 = (int(v) for v in match.groups())
    if x2 <= x1 or y2 <= y1:
        return None
    return (x1 + x2) // 2, (y1 + y2) // 2, (x2 - x1) * (y2 - y1)


def is_label(text, label):
    if text == label:
        return True
    # Launchers truncate a long label ("QuickActionsD…"), so accept a prefix —
    # but a substantial one, or "Q" would match half the drawer.
    trimmed = text.rstrip(". …")
    return len(trimmed) >= 6 and label.startswith(trimmed)


def main(argv):
    mode = argv[1] if len(argv) > 1 else ""
    data = sys.stdin.read()

    if mode == "size":
        # `wm size` prints "Physical size: WxH", plus an "Override size:" line
        # when the display is scaled — input coordinates follow the override.
        match = (re.search(r"Override size:\s*(\d+)x(\d+)", data)
                 or re.search(r"Physical size:\s*(\d+)x(\d+)", data))
        if not match:
            print("parse_ui: no WxH in the `wm size` output", file=sys.stderr)
            return 1
        print(match.group(1), match.group(2))
        return 0

    if mode == "icon":
        label = argv[2]
        seen, best, predicted = [], None, None
        for node in read_nodes(data):
            texts = labels_of(node)
            seen.extend(texts)
            if not any(is_label(t, label) for t in texts):
                continue
            point = centre(node)
            if not point:
                continue
            # A launcher PREDICTION of the app (the Pixel launcher's hotseat
            # suggestions carry content-desc "Predicted app: <label>") is not
            # the icon we want: a long press there opens the launcher's
            # "App suggestions" sheet, never the app's shortcut popup — the
            # second capture attempt photographed exactly that. Remember it,
            # but only as a last resort the caller can recognise.
            if any(t.startswith("Predicted app:") for t in texts):
                if predicted is None or point[2] < predicted[2]:
                    predicted = point
                continue
            # Smallest match wins: the icon itself, never a container that
            # happens to describe itself with the same name.
            if best is None or point[2] < best[2]:
                best = point
        if best is None and predicted is not None:
            print("parse_ui: %r is on screen only as a launcher prediction at %d,%d — "
                  "not a real icon; open the drawer" % (label, predicted[0], predicted[1]),
                  file=sys.stderr)
            return 5
        if best is None:
            print("parse_ui: nothing labelled %r among %d visible labels; saw: %s"
                  % (label, len(seen), ", ".join(sorted(set(seen))[:15])),
                  file=sys.stderr)
            return 3
        print(best[0], best[1])
        return 0

    if mode == "labels":
        # Each entry is "Title=Subtitle" (more '='-separated forms are fine).
        # Launcher3 draws the LONG label — the package's Subtitle — when it
        # fits the popup and the short one otherwise: the first sheet ever
        # photographed here (API 30, run 61) read "Start a fresh run", not
        # "New Game", and this mode called it a miss. Either form counts.
        wanted = argv[2:]
        seen = [t for node in read_nodes(data) for t in labels_of(node)]
        found = 0
        for entry in wanted:
            forms = [f for f in entry.split("=") if f] or [entry]
            shown = next((f for f in forms
                          if any(f == t or f in t for t in seen)), None)
            if shown is None:
                print("%s: no" % forms[0])
            elif shown == forms[0]:
                print("%s: yes" % forms[0])
            else:
                print('%s: yes (shown as "%s")' % (forms[0], shown))
            found += shown is not None
        if found == len(wanted):
            return 0
        return 4 if found else 3

    print("parse_ui: unknown mode %r" % mode, file=sys.stderr)
    return 1


if __name__ == "__main__":
    try:
        sys.exit(main(sys.argv))
    except Exception as exc:  # best-effort: a traceback here helps nobody
        print("parse_ui: %s" % exc, file=sys.stderr)
        sys.exit(1)
PY

  IFS='|' read -r -a titles <<<"$CAPTURE_TITLES"

  # 1. Home. The launcher has to be in front for uiautomator to reach an idle
  #    state at all, and step 8 left the app itself in the foreground. Name the
  #    launcher while we are here: everything below is ITS behaviour, and which
  #    one a given system image ships is exactly what a failed gesture leaves
  #    you guessing about.
  echo "capture: home activity: $(cap_adb shell cmd package resolve-activity --brief \
    -a android.intent.action.MAIN -c android.intent.category.HOME 2>/dev/null \
    | tr -d '\r' | tail -n 1)"
  cap_adb shell input keyevent KEYCODE_HOME >/dev/null 2>&1 || true
  sleep 2

  # 2. Open the all-apps drawer, then find the app's icon in it. First a swipe
  #    up from NEAR the bottom (not the very edge — on a gesture-navigation
  #    device that band belongs to the system and the swipe would only go home
  #    again). The first CI run showed the Pixel launcher on the API 30 image
  #    ignoring that swipe outright, so when the icon is not found afterwards
  #    the capture escalates through the launcher-independent ways of asking
  #    for the drawer, each followed by a fresh search: the ALL_APPS key
  #    (API 28+), a tap on the launcher's own drawer handle if the hierarchy
  #    shows one ("Apps list" on that image), and finally the ALL_APPS intent.
  #    On the API 35 image the icon sits in the predicted-apps row of the home
  #    screen; the second attempt found and pressed it there, and the launcher
  #    answered with its "App suggestions added to empty space" sheet — a
  #    prediction is not a real icon, so parse_ui now reports such a match as
  #    exit 5 and the search goes on to the drawer like any other miss.
  if ! wh="$(cap_adb shell wm size 2>/dev/null | tr -d '\r' | python3 "$py" size)"; then
    echo "capture: could not read the display size from 'wm size'; skipped."
    return 0
  fi
  w="${wh%% *}"
  h="${wh##* }"
  x=$((w / 2))
  # Start on the WORKSPACE first, not the bottom edge. The first four attempts
  # began at 90% of the height, and the dumps of both images put that point on
  # a search box: the Pixel launcher's hotseat search bar on API 35 (y 535–598
  # of 640) and the collapsed all-apps search box on API 30 (574–630). A drag
  # that begins on a search widget is the widget's to keep, and API 30 opened
  # its drawer on one run in five while API 35 never moved. 65% is above the
  # hotseat and the page indicator on both dumps (API 35: 441–465 / 465+;
  # API 30: 475–499 / 499+) and below the smartspace card at the top.
  # The 65% start opened the API 35 drawer on its first run (PR #19 run 64) and
  # the API 30 one not at all, while the 90% start had opened API 30's once —
  # the drag that starts on the collapsed all-apps box is the one that image
  # answers. So both, in that order: the workspace first, the bottom edge
  # second, each followed by a fresh search.
  y_from=$((h * 13 / 20))
  y_to=$((h / 5))
  echo "capture: display ${w}x${h} — swiping up at x=$x, y $y_from -> $y_to to open the app drawer"
  cap_adb shell input swipe "$x" "$y_from" "$x" "$y_to" 300 >/dev/null 2>&1 || true
  sleep 3
  cap_find_icon 3 || true
  if [ -z "$xy" ]; then
    y_from=$((h * 9 / 10))
    echo "capture: the workspace swipe surfaced no icon — swiping again from the bottom edge, y $y_from -> $y_to"
    cap_adb shell input swipe "$x" "$y_from" "$x" "$y_to" 300 >/dev/null 2>&1 || true
    sleep 3
    cap_find_icon 2 || true
  fi
  if [ -z "$xy" ]; then
    echo "capture: the swipe surfaced no icon — sending KEYCODE_ALL_APPS"
    cap_adb shell input keyevent KEYCODE_ALL_APPS >/dev/null 2>&1 || true
    sleep 3
    cap_find_icon 2 || true
  fi
  if [ -z "$xy" ] && [ -s "$dir/ui-drawer.xml" ]; then
    for handle in "Apps list" "All apps"; do
      hxy="$(python3 "$py" icon "$handle" <"$dir/ui-drawer.xml" 2>/dev/null)" || continue
      hx="${hxy%% *}"
      hy="${hxy##* }"
      echo "capture: tapping the launcher's '$handle' handle at $hx,$hy"
      cap_adb shell input tap "$hx" "$hy" >/dev/null 2>&1 || true
      sleep 3
      cap_find_icon 2 || true
      break
    done
  fi
  if [ -z "$xy" ]; then
    echo "capture: still no icon — starting the ALL_APPS intent"
    cap_adb shell am start -a android.intent.action.ALL_APPS >/dev/null 2>&1 || true
    sleep 3
    cap_find_icon 2 || true
  fi

  # 3. Long-press it: a real press — DOWN, hold, UP as separate events. The
  #    first run's swipe-that-never-moves (`input swipe x y x y ms`) reached
  #    the API 35 Pixel launcher as a gesture and opened nothing, and the
  #    hierarchy after it was identical to the one before. The screenshot and
  #    the hierarchy below are taken WHILE the finger is still down, so a sheet
  #    that dismisses on release is captured all the same. `input motionevent`
  #    needs API 28+; where the shell rejects it, the swipe form is the
  #    fallback, and either way the log says which was used.
  pressed=0
  if [ -n "$xy" ]; then
    ix="${xy%% *}"
    iy="${xy##* }"
    hold_s=$(((CAPTURE_PRESS_MS + 999) / 1000))
    echo "capture: app icon '$CAPTURE_LABEL' at $ix,$iy — pressing: DOWN, hold ${hold_s}s, then UP after the capture"
    out="$(cap_adb shell input motionevent DOWN "$ix" "$iy" 2>&1)"
    rc=$?
    case "$out" in *[Uu]sage*|*[Ee]rror*|*[Uu]nknown*) rc=1 ;; esac
    if [ "$rc" -eq 0 ]; then
      pressed=1
      sleep "$hold_s"
    else
      echo "capture: 'input motionevent' is not usable here ($(printf '%s' "$out" | head -n 1)) — swipe-hold for ${CAPTURE_PRESS_MS}ms instead"
      cap_adb shell input swipe "$ix" "$iy" "$ix" "$iy" "$CAPTURE_PRESS_MS" >/dev/null 2>&1 || true
      sleep 3
    fi
  else
    echo "capture: no icon labelled '$CAPTURE_LABEL' was found — screenshotting whatever is on"
    echo "         screen anyway; ui-drawer.xml says what the launcher was showing."
  fi

  # 4. The screenshot, taken even when the press never happened: a picture of
  #    the wrong screen is how the next person fixes the gesture.
  if cap_adb shell screencap -p /sdcard/quickactions-longpress.png >/dev/null 2>&1 \
    && cap_adb pull /sdcard/quickactions-longpress.png "$dir/longpress.png" >/dev/null 2>&1; then
    echo "capture: screenshot -> $dir/longpress.png"
  else
    echo "capture: screencap or pull failed — no screenshot this run."
  fi

  # 5. The half a machine can read: are the shortcut LABELS on screen — title
  #    or subtitle, whichever the launcher drew? This is the only line of the
  #    capture worth grepping for, and it is evidence about the LAUNCHER —
  #    step 5 of the smoke already proved the icons resolved.
  if [ -z "$xy" ]; then
    echo "shortcut sheet visible: no (nothing was pressed — no icon was found)"
  elif cap_dump_ui "$dir/ui-longpress.xml"; then
    out="$(python3 "$py" labels "${titles[@]}" <"$dir/ui-longpress.xml")"
    rc=$?
    case "$rc" in
      0) echo "shortcut sheet visible: yes" ;;
      4) echo "shortcut sheet visible: partial" ;;
      3) echo "shortcut sheet visible: no" ;;
      *) echo "shortcut sheet visible: unknown (the hierarchy could not be parsed)" ;;
    esac
    printf '%s\n' "$out" | sed 's/^/  /'
  else
    echo "shortcut sheet visible: no (no hierarchy could be read after the press)"
  fi

  # 6. Let go. Only now: everything worth keeping was taken with the finger
  #    still down.
  if [ "$pressed" = 1 ]; then
    cap_adb shell input motionevent UP "$ix" "$iy" >/dev/null 2>&1 || true
  fi

  rm -f "$py" || true
  echo "capture: artifacts in $dir"
}

# The last thing this script does, and its exit status either way is 0 — the
# `|| true` discards the subshell's, and an `if` whose condition is false is
# itself a success. That is what makes the capture unable to speak for the run.
# (A closing `exit 0` would say the same thing louder, but it cuts shellcheck's
# reachability graph: every function above that only `poll` ever calls then
# reads as dead code, thirteen false SC2317s deep.)
if [ "${CAPTURE_LONGPRESS:-0}" = "1" ]; then
  (
    set +e +o pipefail
    capture_longpress
  ) || true
fi
