# Changelog

All notable changes to this package are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the package adheres
to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-06-25

### Added

- Initial release. Runtime quick-actions API (`QuickActions.Add/AddList/Remove/
  RemoveById/RemoveAll/GetAll/GetById/IsAdded`) with `Performed` event and
  `LastPerformed` for cold launches.
- iOS implementation via `UIApplicationShortcutItem` with `UnityAppController`
  swizzling (no AppDelegate edits).
- Android implementation via `ShortcutManager` dynamic shortcuts plus a
  trampoline activity that works on both `UnityPlayerActivity` and
  `UnityPlayerGameActivity`.
- `IconType` system-icon enum, Editor *About* window, and a Demo sample.
- Static shortcuts: a **Project Settings ▸ Quick Actions** asset plus build
  post-processors that bake shortcuts into iOS `Info.plist`
  (`UIApplicationShortcutItems`) and Android `res/xml/quickactions_shortcuts.xml` +
  launcher-activity meta-data. Static intents reuse the trampoline via an
  action-encoded id.
- Unity-free verification harness (`.verify/`, `tools/verify.sh`): compiles the
  C# against UnityEngine/UnityEditor stubs and the Android plugin against Android
  SDK stubs. Toolchain baked into the devcontainer image.
- Unit tests (`Tests/Editor/`): 25 NUnit tests for the runtime API (list
  management, validity, equality, full IconType pinning, single-shot event
  dispatch, OS reconcile, RemoveAll OS-first ordering) runnable in the Unity Test
  Runner and via `dotnet test`; plus JsonUtility serialization tests.
- Store collateral: marketing images at Asset Store sizes (`store/`,
  `tools/gen_store_images.py`), `STORE_CHECKLIST.md`, and `plans/release.md`.
- **Opt-in `QUICKACTIONS_ENABLED` gate:** managed asmdefs use
  `defineConstraints: [QUICKACTIONS_ENABLED]`. Native plugins (which Unity won't
  gate via define constraints) are gated at the build-output level: the iOS
  `.mm` is wrapped in `#if QUICKACTIONS_ENABLED` and a gated post-processor adds
  that macro to the Xcode project only when enabled (so it compiles to nothing in
  prod); an ungated post-processor strips the trampoline `<activity>` from the
  Android manifest when disabled. With the define off there is zero C# and no iOS
  swizzle (the trampoline `.java` remains a dead, unreachable class). Add the
  define to a dev Build Profile to use it. See README "Dev-only".
