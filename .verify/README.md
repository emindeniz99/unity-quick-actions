# Verification harness (no Unity required)

This folder lets the package be **type-checked and compiled without installing
Unity**, so CI / web sessions can catch regressions fast. It is hidden from
Unity (the leading `.` means Unity ignores it) and never ships in a build.

Run everything:

```bash
tools~/verify.sh        # gen_meta + C# compile (x10) + unit tests + Java compile + smoke
                        # + frozen strings + release-notes coherence + built-in icon bytes
tools~/setup.sh         # one-time: install dotnet + JDK if missing
```

## What it does

| Check | How |
|-------|-----|
| Stable `.meta` for every asset | `tools~/gen_meta.py` (idempotent), then `--check`: no orphaned `.meta`, and each one routes to the importer its path implies (the `guid:` line is exempt — some assets legitimately keep a GUID Unity assigned before they moved) |
| Runtime + Editor C# | `dotnet build` against the UnityEngine/UnityEditor **stubs** in `Stubs/`, ten configs: `Editor`, `EditoriOS`, `EditorAndroid`, `NativeGate`, `NativeGateiOS`, `Bootstrap` (the ungated enable-menu assembly, compiled WITHOUT `QUICKACTIONS_ENABLED`), `iOS`, `Android`, `Sample`, `SampleAndroid` — each defines the matching `UNITY_*` symbols so every `#if` branch is exercised. The `EditoriOS`/`EditorAndroid`/`NativeGate`/`NativeGateiOS` configs compile each build post-processor in isolation (mirroring the gated/ungated asmdefs; `NativeGate` is the ungated Android trampoline stripper, `NativeGateiOS` the ungated iOS gate cleanup — both compile WITHOUT `QUICKACTIONS_ENABLED`). `Sample` compiles the Demo as the Editor sees it; `SampleAndroid` compiles it as a **device** build does (`UNITY_ANDROID` without `UNITY_EDITOR`), which is the only config that reaches the demo's device-only autotest hook — the same reason `Android` exists for the device-only runtime bridge. **Caveat:** the stubs stand in for the real `UnityEditor.iOS.Xcode` / `UnityEditor.Android` extension DLLs, so asmdef `precompiledReferences` resolution is only truly validated in a real Unity build. |
| Android plugin (Java) | `javac` against the Android SDK **stubs** in `JavaStubs/`, then the stateful smoke test in `JavaSmoke/` runs the compiled plugin against them (coexistence, budget, trampoline gate, null-vs-empty reads). |
| Android static-shortcut resources (C#) | `EditorTests/` — NUnit tests compiled into the `dotnet test` assembly only. They drive `QuickActionsBuildPostProcessorAndroid`'s per-locale resource generation, whose failures (duplicate `<string>` names, case-colliding locale qualifiers) are aapt2 build-breakers invisible until Gradle runs, **and** its resource-shrinker keep rule (`res/raw/quickactions_keep.xml`), whose absence is invisible until a *minified release* build draws blank icons. They live here rather than in `Tests/` because that post-processor's asmdef is `defineConstraint`ed to `UNITY_ANDROID`, so a Unity test assembly cannot reference it on any other build target. |
| Static-shortcut build pipeline (C#) | `EditorTests/StaticBuildPlaceholdersTests.cs` — same mechanism: `QuickActionsStaticBuild` (the `{placeholder}` engine + `Customize` hook both bakers consume) lives in the Editor assembly the runtime-referencing Unity test asmdef can't see, so its escaping/precedence/per-locale contracts are pinned here, where a bug would otherwise only surface inside a shipped `Info.plist` / `res/xml`. |
| Frozen device strings | `tools~/check_frozen_strings.py` — the literals that persist on a device (intent actions, extras keys, the ownership marker, the `ic_quickaction_` drawable prefix) are pinned across C#, Java and Objective-C++; a rename would orphan shortcuts an older build already installed. |
| Release-notes coherence | `tools~/release_notes.py --check` — `package.json`, the top `CHANGELOG.md` heading and every install pin in the docs name the same version, because `release.yml` cuts the tag from the merge commit and publishes that CHANGELOG section as the notes. |
| Built-in Android icon bytes | `tools~/gen_builtin_icons.py --check` — the base64 in `Editor/Android/QuickActionsBuiltInIcons.cs` is regenerated in memory and must match byte for byte, so the generated file cannot drift from the generator that documents it. |

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

Nothing in this repo provisions the toolchain — there is no devcontainer image
(an earlier version of this paragraph claimed one, which never existed here).
Run `tools~/setup.sh` once per machine or container; it is a fast no-op when a
10.x `dotnet` SDK and `javac` are already on PATH. CI installs the same two
prerequisites with `setup-dotnet` / `setup-java` (see `.github/workflows/ci.yml`).

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
      "command": "sh -c 'f=\"${CLAUDE_PROJECT_DIR:-.}/tools~/setup.sh\"; if [ -x \"$f\" ] && ! { command -v dotnet >/dev/null 2>&1 && command -v javac >/dev/null 2>&1; }; then nohup \"$f\" >/tmp/quick-actions-setup.log 2>&1 & fi; exit 0'"
    }
  ]
}
```

It is idempotent and backgrounds the install, so it never blocks session start.
