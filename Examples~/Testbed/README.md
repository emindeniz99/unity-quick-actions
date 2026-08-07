# Testbed — a Unity project that consumes this package

A minimal, ready-to-open Unity project wired to the package in this repository.
Clone the repo, open this folder in Unity, and everything is already
configured — including the one step people miss (the scripting define).

It lives under `Examples~/`, so Unity's asset importer ignores it entirely: it
never reaches a consumer who installs the package, and it costs nothing at
import time. It is only here to be read and opened directly.

## Open it

```bash
git clone https://github.com/emindeniz99/unity-quick-actions.git
```

Then in Unity Hub: **Add project from disk** → `unity-quick-actions/Examples~/Testbed`.
Unity 2021.3 LTS or newer (built and verified with Unity 6.3).

## What is already wired for you

| Thing | Where | Why it matters |
|---|---|---|
| The package itself | `Packages/manifest.json` → `"com.emindeniz99.quick-actions": "file:../../../.."` | A **relative** local path up to the repo root, so it resolves on any machine. This is the "local package" install method from the README. |
| `testables` | `Packages/manifest.json` | Without it the package's tests never appear in the Test Runner. Local packages need it; embedded ones do not. |
| `QUICKACTIONS_ENABLED` | `ProjectSettings/ProjectSettings.asset`, for Android / iPhone / Standalone | The package is deliberately inert without this define. Forgetting it is the #1 reason people think the package does nothing. |
| CLI build entry points | `Assets/Editor/TestbedBuilder.cs` | Unity has no built-in command-line build, so any CI needs a static method like these. |

## Try it

1. Package Manager → the package → **Samples** → import **Demo**.
2. Open the imported `QuickActionsDemo` scene and press Play, or build to a
   device and long-press the app icon.

## Run the tests

```bash
unity test . --mode EditMode --output test-results.xml
```

Uses [Unity's CLI](https://docs.unity.com/en-us/unity-cli/use-unity-cli). The
full suite is 74 tests.

## Build from the command line

```bash
unity build . --target Android --execute-method TestbedBuilder.BuildAndroid
unity build . --target iOS     --execute-method TestbedBuilder.BuildiOS
```

`TestbedBuilder` also carries `DisableDefine` / `EnableDefine`. Those exist
because the package **refuses** to build if the define is flipped inside the
same editor invocation as the build — the editor assemblies would still be
compiled with it, so the resulting player would quietly still contain the
dev-only pieces. Flip the define in one invocation, build in the next.
