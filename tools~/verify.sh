#!/usr/bin/env bash
# Full static verification for the Quick Actions package, runnable without a
# Unity install. Four checks:
#   1. gen_meta.py        -> every asset has a stable .meta
#   2. dotnet build x9    -> Runtime/Editor C# type-checks against UnityEngine/
#                            UnityEditor stubs (editor, iOS, Android, sample —
#                            the sample twice: in-Editor and as it compiles on device)
#   3. dotnet test        -> NUnit unit tests against the stub harness
#   4. javac              -> Android plugin compiles against Android SDK stubs
#
# Exit non-zero on any failure. See .verify/README.md for the rationale.
set -uo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERIFY="$ROOT/.verify"
fail=0

echo "== 1/5  .meta presence (must already be committed, not generated here) =="
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

echo
echo "== 2/5  C# compile (UnityEngine/UnityEditor stubs) =="
if command -v dotnet >/dev/null 2>&1; then
  export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
  for proj in Editor EditoriOS EditorAndroid NativeGate NativeGateiOS Bootstrap iOS Android Sample SampleAndroid; do
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
echo "== 3/5  C# unit tests (dotnet test) =="
if command -v dotnet >/dev/null 2>&1; then
  out="$(dotnet test "$VERIFY/QuickActions.Tests.csproj" -v q --nologo 2>&1)"; rc=$?
  echo "$out" | grep -E 'Passed!|Failed!|error|Passed:|Failed:' || true
  if [ "$rc" -ne 0 ]; then fail=1; fi
else
  echo "!! dotnet not found — run tools~/setup.sh first"; fail=1
fi

echo
echo "== 4/5  Java compile + smoke test (Android SDK stubs) =="
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
echo "== 5/5  Frozen device-facing strings =="
# These literals are persisted by the OS on end-user devices (pinned shortcut
# intents, PersistableBundle extras, UIApplicationShortcutItemUserInfo, and the
# res/xml baked into every shipped APK). Renaming one is silent: the app still
# launches and Performed simply never fires. Each is duplicated across 2-4 files
# in three languages, so a C# unit test cannot cover them.
python3 "$ROOT/tools~/check_frozen_strings.py" || fail=1

if [ "$fail" = "0" ]; then echo "VERIFY: PASS"; else echo "VERIFY: FAIL"; fi
exit $fail
