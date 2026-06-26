# Verification harness (no Unity required)

This folder lets the package be **type-checked and compiled without installing
Unity**, so CI / web sessions can catch regressions fast. It is hidden from
Unity (the leading `.` means Unity ignores it) and never ships in a build.

Run everything:

```bash
tools/verify.sh        # gen_meta + C# compile (x4) + Java compile
tools/setup.sh         # one-time: install dotnet + JDK if missing
```

## What it does

| Check | How |
|-------|-----|
| Stable `.meta` for every asset | `tools/gen_meta.py` (idempotent) |
| Runtime + Editor C# | `dotnet build` against the UnityEngine/UnityEditor **stubs** in `Stubs/`, four configs: `Editor`, `iOS`, `Android`, `Sample` — each defines the matching `UNITY_*` symbols so every `#if` branch is exercised. |
| Android plugin (Java) | `javac` against the minimal Android SDK **stubs** in `JavaStubs/`. |

## Why stubs

Unity ships no public reference assemblies for `UnityEngine`/`UnityEditor`, and
the Android SDK / Apple SDK are not present in a Linux CI container. The stubs
declare just the API surface this package touches (e.g. `MonoBehaviour`,
`AndroidJavaClass`, `PlistDocument`, `IPostGenerateGradleAndroidProject`) with
empty bodies, purely so the compiler can resolve and type-check our code. They
are **not** functional and are never referenced by a real Unity build — Unity
compiles against its own assemblies.

A green run proves the package's C# and Java **compile and type-check**; it does
not exercise runtime behaviour on a device. For that, see the on-device
procedure in [`../plans/mvp.md`](../plans/mvp.md).

## Web sessions / CI

The repo's `.devcontainer/Dockerfile` bakes in `dotnet-sdk-8.0` and a headless
JDK, so Claude Code on the web can run `tools/verify.sh` with no setup. On a
plain machine, run `tools/setup.sh` once first.

### Optional: auto-prepare the toolchain on session start

A SessionStart hook can ensure the toolchain in any environment. It is **not**
installed automatically (editing agent startup config requires your explicit
opt-in). To enable it, add this entry to the `SessionStart` array in
`.claude/settings.json`:

```json
{
  "matcher": "",
  "hooks": [
    {
      "type": "command",
      "command": "sh -c 'f=\"${CLAUDE_PROJECT_DIR:-.}/projects/quick-actions-unity/tools/setup.sh\"; if [ -x \"$f\" ] && ! { command -v dotnet >/dev/null 2>&1 && command -v javac >/dev/null 2>&1; }; then nohup \"$f\" >/tmp/quick-actions-setup.log 2>&1 & fi; exit 0'"
    }
  ]
}
```

It is idempotent and backgrounds the install, so it never blocks session start.
