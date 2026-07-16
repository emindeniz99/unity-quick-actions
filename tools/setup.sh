#!/usr/bin/env bash
# Idempotent toolchain setup for the quick-actions-unity verification harness.
# Installs the .NET SDK (for C# stub compilation) and a JDK (for the Android
# plugin compilation) if they are not already present. Fast no-op when both
# are available, so it is safe to call from a SessionStart hook.
set -e

need_dotnet=0; need_jdk=0
# Capability check, not just presence: the harness targets net10.0, so an old
# dotnet without a 10.x SDK must trigger an install.
{ command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; } || need_dotnet=1
command -v javac  >/dev/null 2>&1 || need_jdk=1

if [ "$need_dotnet" = "0" ] && [ "$need_jdk" = "0" ]; then
  echo "setup: dotnet + javac already present; nothing to do."
  exit 0
fi

SUDO=""
[ "$(id -u)" != "0" ] && command -v sudo >/dev/null 2>&1 && SUDO="sudo"

echo "setup: refreshing apt index..."
$SUDO apt-get update -y

pkgs=""
[ "$need_dotnet" = "1" ] && pkgs="$pkgs dotnet-sdk-10.0"
[ "$need_jdk" = "1" ] && pkgs="$pkgs default-jdk-headless"

echo "setup: installing$pkgs"
$SUDO apt-get install -y --no-install-recommends $pkgs

echo "setup: done. dotnet=$(command -v dotnet || echo missing) javac=$(command -v javac || echo missing)"
