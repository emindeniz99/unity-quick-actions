#!/usr/bin/env bash
# Full static verification for the Quick Actions package, runnable without a
# Unity install. Seven checks (the header used to say four and list four; it has
# run more than that since the frozen-string scan landed):
#   1. gen_meta.py        -> every asset has a stable .meta
#   2. dotnet build x10   -> Runtime/Editor C# type-checks against UnityEngine/
#                            UnityEditor stubs (editor, iOS, Android, sample —
#                            the sample twice: in-Editor and as it compiles on device)
#   3. dotnet test        -> NUnit unit tests against the stub harness
#   4. javac              -> Android plugin compiles against Android SDK stubs
#   5. check_frozen_strings.py -> device-persisted literals are unchanged
#   6. release_notes.py   -> package.json and the top CHANGELOG heading agree
#   7. gen_builtin_icons.py --check -> the embedded Android icon drawables match
#                            their generator (the .cs is generated, never edited)
#
# Exit non-zero on any failure. See .verify/README.md for the rationale.
set -uo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERIFY="$ROOT/.verify"
fail=0

echo "== 1/7  .meta completeness (must already be committed, not generated here) =="
# gen_meta only CREATES missing metas and always exits 0, so running it can't
# catch a missing/uncommitted meta. Fail if it had to create any — a committed
# repo/UPM must ship every .meta (a fresh GUID assigned on the user's machine
# breaks prefab/scene references that stable metas exist to protect).
meta_out="$(python3 "$ROOT/tools~/gen_meta.py")" || fail=1
echo "$meta_out"
if echo "$meta_out" | grep -qE 'created [1-9]'; then
  echo "!! .meta files were missing and had to be generated — commit them (git add)."
  fail=1
fi
# Presence alone was the whole gate, which passes a .meta whose asset is gone
# (an orphan ships in the package) and a .meta routing to the wrong importer —
# e.g. an iOS plugin with the Android platform flag set, which builds a broken
# player rather than failing. --check compares each meta against what its path
# routes to, ignoring the guid line: several committed metas legitimately carry
# a GUID Unity assigned before the asset moved, and rewriting those would break
# every reference to them.
python3 "$ROOT/tools~/gen_meta.py" --check || fail=1

echo
echo "== 2/7  C# compile (UnityEngine/UnityEditor stubs) =="
if command -v dotnet >/dev/null 2>&1; then
  export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
  for proj in Editor EditoriOS EditorAndroid NativeGate NativeGateiOS Bootstrap iOS Android Sample SampleAndroid SampleOff; do
    echo "-- QuickActions.$proj.csproj"
    # Decide pass/fail on dotnet's exit code, not on whether grep matched output
    # (a quieter dotnet could otherwise mark a clean build FAIL).
    out="$(dotnet build "$VERIFY/QuickActions.$proj.csproj" -v q -nologo 2>&1)"; rc=$?
    echo "$out" | grep -Ev '^\s*$|Determining|Restored ' || true
    if [ "$rc" -ne 0 ]; then fail=1; fi
  done
else
  echo "!! dotnet not found — run tools~/setup.sh first"; fail=1
fi

echo
echo "== 3/7  C# unit tests (dotnet test) =="
if command -v dotnet >/dev/null 2>&1; then
  out="$(dotnet test "$VERIFY/QuickActions.Tests.csproj" -v q --nologo 2>&1)"; rc=$?
  echo "$out" | grep -E 'Passed!|Failed!|error|Passed:|Failed:' || true
  if [ "$rc" -ne 0 ]; then fail=1; fi
else
  echo "!! dotnet not found — run tools~/setup.sh first"; fail=1
fi

echo
echo "== 4/7  Java compile + smoke test (Android SDK stubs) =="
if command -v javac >/dev/null 2>&1; then
  TMP="$(mktemp -d)"
  mkdir -p "$TMP/out"
  if javac --release 11 -d "$TMP/out" $(find "$VERIFY/JavaStubs" -name '*.java') 2>"$TMP/stub.err"; then
    if javac --release 11 -d "$TMP/out" -cp "$TMP/out" "$ROOT"/Plugins/Android/*.java 2>"$TMP/plugin.err"; then
      # Stateful smoke test: exercises the coexistence/budget/trampoline branches
      # of the compiled plugin against the stateful stubs (see .verify/JavaSmoke).
      if javac --release 11 -d "$TMP/out" -cp "$TMP/out" "$VERIFY"/JavaSmoke/*.java 2>"$TMP/smoke.err" \
         && java -cp "$TMP/out" com.emindeniz99.quickactions.QuickActionsBridgeSmokeTest >"$TMP/smoke.out" 2>&1; then
        echo "Android plugin compiles OK — $(tail -1 "$TMP/smoke.out")"
      else
        echo "!! Android smoke test failed:"
        cat "$TMP/smoke.err" 2>/dev/null
        cat "$TMP/smoke.out" 2>/dev/null
        fail=1
      fi
    else
      echo "!! Android plugin failed:"; cat "$TMP/plugin.err"; fail=1
    fi
  else
    echo "!! Java stubs failed:"; cat "$TMP/stub.err"; fail=1
  fi
  rm -rf "$TMP"
else
  echo "!! javac not found — run tools~/setup.sh first"; fail=1
fi

echo
echo
echo "== 5/7  Frozen device-facing strings =="
# These literals are persisted by the OS on end-user devices (pinned shortcut
# intents, PersistableBundle extras, UIApplicationShortcutItemUserInfo, and the
# res/xml baked into every shipped APK). Renaming one is silent: the app still
# launches and Performed simply never fires. Each is duplicated across 2-4 files
# in three languages, so a C# unit test cannot cover them.
python3 "$ROOT/tools~/check_frozen_strings.py" || fail=1

echo
echo "== 6/7  Release-notes coherence =="
# The version in package.json and the version in the top CHANGELOG heading must
# agree: OpenUPM rejects a tag/package.json mismatch with E811, and the release
# workflow quotes that section as the release notes. Catching it here means the
# PR goes red, not main after the merge that would have cut the release. A top
# section still called [Unreleased] is a legal mid-development state and passes.
python3 "$ROOT/tools~/release_notes.py" --check || fail=1

echo
echo "== 7/7  Built-in Android icons =="
# Editor/Android/QuickActionsBuiltInIcons.cs is GENERATED from the glyph
# geometry in tools~/gen_builtin_icons.py (pure stdlib, so this runs anywhere
# dotnet does). An edit to either side without regenerating would ship icons
# that disagree with their source of truth; --check regenerates in memory and
# fails on any byte of difference.
python3 "$ROOT/tools~/gen_builtin_icons.py" --check || fail=1

if [ "$fail" = "0" ]; then echo "VERIFY: PASS"; else echo "VERIFY: FAIL"; fi
exit $fail
