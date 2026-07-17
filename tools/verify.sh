#!/usr/bin/env bash
# Full static verification for quick-actions-unity, runnable without a Unity
# install. Four checks:
#   1. gen_meta.py        -> every asset has a stable .meta
#   2. dotnet build x8    -> Runtime/Editor C# type-checks against UnityEngine/
#                            UnityEditor stubs (editor, iOS, Android, sample)
#   3. dotnet test        -> NUnit unit tests against the stub harness
#   4. javac              -> Android plugin compiles against Android SDK stubs
#
# Exit non-zero on any failure. See .verify/README.md for the rationale.
set -uo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERIFY="$ROOT/.verify"
fail=0

echo "== 1/4  .meta generation =="
python3 "$ROOT/tools/gen_meta.py" || fail=1

echo
echo "== 2/4  C# compile (UnityEngine/UnityEditor stubs) =="
if command -v dotnet >/dev/null 2>&1; then
  export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
  for proj in Editor EditoriOS EditorAndroid NativeGate NativeGateiOS iOS Android Sample; do
    echo "-- QuickActions.$proj.csproj"
    # Decide pass/fail on dotnet's exit code, not on whether grep matched output
    # (a quieter dotnet could otherwise mark a clean build FAIL).
    out="$(dotnet build "$VERIFY/QuickActions.$proj.csproj" -v q -nologo 2>&1)"; rc=$?
    echo "$out" | grep -Ev '^\s*$|Determining|Restored ' || true
    if [ "$rc" -ne 0 ]; then fail=1; fi
  done
else
  echo "!! dotnet not found — run tools/setup.sh first"; fail=1
fi

echo
echo "== 3/4  C# unit tests (dotnet test) =="
if command -v dotnet >/dev/null 2>&1; then
  out="$(dotnet test "$VERIFY/QuickActions.Tests.csproj" -v q --nologo 2>&1)"; rc=$?
  echo "$out" | grep -E 'Passed!|Failed!|error|Passed:|Failed:' || true
  if [ "$rc" -ne 0 ]; then fail=1; fi
else
  echo "!! dotnet not found — run tools/setup.sh first"; fail=1
fi

echo
echo "== 4/4  Java compile (Android SDK stubs) =="
if command -v javac >/dev/null 2>&1; then
  TMP="$(mktemp -d)"
  mkdir -p "$TMP/out"
  if javac --release 11 -d "$TMP/out" $(find "$VERIFY/JavaStubs" -name '*.java') 2>"$TMP/stub.err"; then
    if javac --release 11 -d "$TMP/out" -cp "$TMP/out" "$ROOT"/Plugins/Android/*.java 2>"$TMP/plugin.err"; then
      echo "Android plugin compiles OK"
    else
      echo "!! Android plugin failed:"; cat "$TMP/plugin.err"; fail=1
    fi
  else
    echo "!! Java stubs failed:"; cat "$TMP/stub.err"; fail=1
  fi
  rm -rf "$TMP"
else
  echo "!! javac not found — run tools/setup.sh first"; fail=1
fi

echo
if [ "$fail" = "0" ]; then echo "VERIFY: PASS"; else echo "VERIFY: FAIL"; fi
exit $fail
