#!/usr/bin/env bash
# Full static verification for quick-actions-unity, runnable without a Unity
# install. Three checks:
#   1. gen_meta.py        -> every asset has a stable .meta
#   2. dotnet build x4    -> Runtime/Editor C# type-checks against UnityEngine/
#                            UnityEditor stubs (editor, iOS, Android, sample)
#   3. javac              -> Android plugin compiles against Android SDK stubs
#
# Exit non-zero on any failure. See .verify/README.md for the rationale.
set -uo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERIFY="$ROOT/.verify"
fail=0

echo "== 1/3  .meta generation =="
python3 "$ROOT/tools/gen_meta.py" || fail=1

echo
echo "== 2/3  C# compile (UnityEngine/UnityEditor stubs) =="
if command -v dotnet >/dev/null 2>&1; then
  export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
  for proj in Editor iOS Android Sample; do
    echo "-- QuickActions.$proj.csproj"
    if ! dotnet build "$VERIFY/QuickActions.$proj.csproj" -v q -nologo \
         | grep -Ev '^\s*$|Determining|Restored '; then fail=1; fi
  done
else
  echo "!! dotnet not found — run tools/setup.sh first"; fail=1
fi

echo
echo "== 3/3  Java compile (Android SDK stubs) =="
if command -v javac >/dev/null 2>&1; then
  TMP="$(mktemp -d)"
  mkdir -p "$TMP/out"
  if javac -d "$TMP/out" $(find "$VERIFY/JavaStubs" -name '*.java') 2>"$TMP/stub.err"; then
    if javac -d "$TMP/out" -cp "$TMP/out" "$ROOT"/Plugins/Android/*.java 2>"$TMP/plugin.err"; then
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
