# Examples — one consuming Unity project per supported editor line

Three ready-to-open Unity projects that consume the package in this repository.
Clone the repo, open the one matching your editor, and everything is already
wired — including the step people miss (the scripting define).

| Folder | Authored with | Open it with |
|---|---|---|
| [`Testbed2021/`](./Testbed2021) | 2021.3.45f2 | Unity 2021.3 LTS — the package's declared minimum |
| [`Testbed2022/`](./Testbed2022) | 2022.3.62f3 | Unity 2022.3 LTS |
| [`Testbed6/`](./Testbed6) | 6000.3.21f1 | Unity 6 |

**Why three and not one.** Unity migrates a project *forward* into a newer
editor and never backward, and each line's `Packages/manifest.json` pins package
versions that only exist on that line (`com.unity.multiplayer.center` and the
accessibility/adaptive-performance modules are Unity 6 only; `com.unity.ugui`
is 1.0.0 on 2021.3 and 2.0.0 on Unity 6). A single project would open cleanly on
exactly one line and fail package resolution on the others. Each of these three
is the project the package was actually verified in on that line — see
[`PRODUCTION_READINESS.md`](../PRODUCTION_READINESS.md).

Opening one in a *newer* editor works and is a fine way to check the upgrade
path; Unity will rewrite `ProjectVersion.txt` and some settings when it does, so
don't commit that churn back.

These live under `Examples~/`, so Unity's asset importer ignores the folder
entirely: it never reaches a consumer who installs the package, and it costs
nothing at import time. It exists to be read and opened directly.

## Open one

```bash
git clone https://github.com/emindeniz99/unity-quick-actions.git
```

Then in Unity Hub: **Add project from disk** → `unity-quick-actions/Examples~/Testbed2021`
(or `Testbed2022` / `Testbed6`).

## What is already wired for you

| Thing | Where | Why it matters |
|---|---|---|
| The package itself | `Packages/manifest.json` → `"com.emindeniz99.quick-actions": "file:../../.."` | A **relative** local path up to the repo root, so it resolves on any machine. Unity resolves a `file:` path relative to the project's `Packages` folder — from `Examples~/<Testbed>/Packages` that is exactly three levels. |
| `testables` | `Packages/manifest.json` | Without it the package's tests never appear in the Test Runner. Local packages need it; embedded ones do not. |
| `QUICKACTIONS_ENABLED` | `ProjectSettings/ProjectSettings.asset`, for Android / iPhone / Standalone | The package is deliberately inert without this define. Forgetting it is the #1 reason people think the package does nothing. |
| Three **static** shortcuts | `Assets/QuickActions/QuickActionsSettings.asset` | `new_game`, `continue`, `daily_reward` are baked into the build, so they appear on a long-press **before the app is ever opened**. Edit them under *Project Settings ▸ Quick Actions*. |
| CLI build entry points | `Assets/Editor/TestbedBuilder.cs` | Unity has no built-in command-line build, so any CI needs a static method like these. |

## Try it

1. Package Manager → the package → **Samples** → import **Demo**. (The demo
   lives once, in [`Samples~/Demo`](../Samples~/Demo) — it is not copied into
   these projects, so there is only ever one version of it to read.)
2. Open the imported `QuickActionsDemo` scene and press Play, or build to a
   device and long-press the app icon.

## Run the tests

```bash
unity test . --mode EditMode --output test-results.xml
```

Uses [Unity's CLI](https://docs.unity.com/en-us/unity-cli/unity-cli-reference)
(experimental, and its published docs lag the binary — `unity --help` is the
authoritative command list). The full suite is 74 tests, and it has been run
green on all three lines.

## Build from the command line

```bash
unity build . --target Android --execute-method TestbedBuilder.BuildAndroid
unity build . --target iOS     --execute-method TestbedBuilder.BuildiOS
```

`BuildAndroid` inherits the project default (Mono, `armeabi-v7a`), which is fine
for an emulator but will not install on the 64-bit-only SoCs shipping since
around 2023 — Mono has no ARM64 backend on Android, so reaching arm64 means
IL2CPP. Use `TestbedBuilder.BuildAndroidPhone` for a sideload build that carries
both ABIs in one APK and installs on any phone from 7.1 up.

`TestbedBuilder` also carries `DisableDefine` / `EnableDefine` (both mobile
targets at once) and the define-off builds CI's `gate-off` job runs after
flipping — `BuildAndroidPhoneNoDefine` (IL2CPP, both ABIs, so the APK differs
from `BuildAndroidPhone`'s in nothing but the define) and
`BuildiOSSimulatorNoDefine`. The flip is separate because the package
**refuses** to build if the define is flipped inside the
same editor invocation as the build — the editor assemblies would still be
compiled with it, so the resulting player would quietly still contain the
dev-only pieces. Flip the define in one invocation, build in the next. (This is
not a quirk of this package: Unity documents that scripting symbols set from an
Editor script in batch mode do not take effect, because a headless Editor has no
loop in which to recompile.)
