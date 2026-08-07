# Verification harness (no Unity required)

This folder lets the package be **type-checked and compiled without installing
Unity**, so CI / web sessions can catch regressions fast. It is hidden from
Unity (the leading `.` means Unity ignores it) and never ships in a build.

Run everything:

```bash
tools/verify.sh        # gen_meta + C# compile (x9) + unit tests + Java compile + smoke
tools/setup.sh         # one-time: install dotnet + JDK if missing
```

## What it does

| Check | How |
|-------|-----|
| Stable `.meta` for every asset | `tools/gen_meta.py` (idempotent) |
| Runtime + Editor C# | `dotnet build` against the UnityEngine/UnityEditor **stubs** in `Stubs/`, nine configs: `Editor`, `EditoriOS`, `EditorAndroid`, `NativeGate`, `NativeGateiOS`, `iOS`, `Android`, `Sample`, `SampleAndroid` — each defines the matching `UNITY_*` symbols so every `#if` branch is exercised. The `EditoriOS`/`EditorAndroid`/`NativeGate`/`NativeGateiOS` configs compile each build post-processor in isolation (mirroring the gated/ungated asmdefs; `NativeGate` is the ungated Android trampoline stripper, `NativeGateiOS` the ungated iOS gate cleanup — both compile WITHOUT `QUICKACTIONS_ENABLED`). `Sample` compiles the Demo as the Editor sees it; `SampleAndroid` compiles it as a **device** build does (`UNITY_ANDROID` without `UNITY_EDITOR`), which is the only config that reaches the demo's device-only autotest hook — the same reason `Android` exists for the device-only runtime bridge. **Caveat:** the stubs stand in for the real `UnityEditor.iOS.Xcode` / `UnityEditor.Android` extension DLLs, so asmdef `precompiledReferences` resolution is only truly validated in a real Unity build. |
| Android plugin (Java) | `javac` against the Android SDK **stubs** in `JavaStubs/`, then the stateful smoke test in `JavaSmoke/` runs the compiled plugin against them (coexistence, budget, trampoline gate, null-vs-empty reads). |
| Android static-shortcut resources (C#) | `EditorTests/` — NUnit tests compiled into the `dotnet test` assembly only. They drive `QuickActionsBuildPostProcessorAndroid`'s per-locale resource generation, whose failures (duplicate `<string>` names, case-colliding locale qualifiers) are aapt2 build-breakers invisible until Gradle runs. They live here rather than in `Tests/` because that post-processor's asmdef is `defineConstraint`ed to `UNITY_ANDROID`, so a Unity test assembly cannot reference it on any other build target. |

## Why stubs

Unity ships no public reference assemblies for `UnityEngine`/`UnityEditor`, and
the Android SDK / Apple SDK are not present in a Linux CI container. The stubs
declare just the API surface this package touches (e.g. `MonoBehaviour`,
`AndroidJavaClass`, `PlistDocument`, `IPostGenerateGradleAndroidProject`) with
empty bodies, purely so the compiler can resolve and type-check our code. They
are **not** functional and are never referenced by a real Unity build — Unity
compiles against its own assemblies.

A green run proves the package's C# and Java **compile and type-check** and that
the Java layer's branch logic holds against AOSP-shaped stub semantics; it does
not exercise runtime behaviour on a device. For that, see the on-device
checklist in [`../MAINTAINING.md`](../MAINTAINING.md).

## Web sessions / CI

The repo's `.devcontainer/Dockerfile` bakes in `dotnet-sdk-10.0` and a headless
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
      "command": "sh -c 'f=\"${CLAUDE_PROJECT_DIR:-.}/tools/setup.sh\"; if [ -x \"$f\" ] && ! { command -v dotnet >/dev/null 2>&1 && command -v javac >/dev/null 2>&1; }; then nohup \"$f\" >/tmp/quick-actions-setup.log 2>&1 & fi; exit 0'"
    }
  ]
}
```

It is idempotent and backgrounds the install, so it never blocks session start.
