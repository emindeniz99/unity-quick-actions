# Quick Actions for Unity — Roadmap

Follow-ups discussed but not shipped in v0.1.0. Delete an entry in the same
commit that ships it.

- **Per-item rasterized icons** — accept a `Texture2D`/`Sprite` and emit a
  template `UIApplicationShortcutIcon` (iOS) and a generated drawable
  (Android), instead of only system `IconType` glyphs + named drawables. This
  would also let OS read-back recover icons (currently reconciled items come
  back as `IconType.None`).
- **Pinned shortcuts** — `requestPinShortcut` on Android; no iOS analog.
- **Static shortcut custom icons (iOS file icons)** — `UIApplicationShortcutItemIconFile`
  with an asset-catalog template image (Android already supports a drawable via
  `AndroidDrawable`; iOS static currently supports only system `IconType` glyphs).
- **`.unitypackage` export in CI** — `tools/pack_unitypackage.py` already builds
  the classic artifact without Unity; remaining: run it in CI and attach the
  output to releases.
- **Automated device CI** — drive an iOS simulator / Android emulator to assert
  cold + warm delivery end-to-end.
- **Localization** — per-locale titles/subtitles.

## Validate in a real Unity Editor (license-gated; can't run here)

The stub harness compiles the C#/Java but can't confirm Unity-only wiring:

- Confirm the gated post-processor asmdefs (`Editor/iOS`, `Editor/Android`,
  `defineConstraints` `UNITY_IOS` / `UNITY_ANDROID`, with the extension DLLs in
  `precompiledReferences`) compile when that target is active and are skipped
  cleanly otherwise.
- On-device: verify the Android trampoline reliably foregrounds the Unity task
  and fires `OnApplicationFocus(true)` (warm resume), that iOS warm taps land via
  the focus poll (performAction precedes didBecomeActive), and that static
  shortcuts.xml taps round-trip the action-encoded id. Confirm iOS cold + warm
  on a device via Xcode.
- Consider hardening the exported trampoline (it must be `exported` for the
  launcher to start it, but another app could fire a `PERFORM.<id>` action —
  low impact, only spoofs an in-app shortcut id).
- **CI limitation:** the stub harness compiles the post-processors (incl. an
  isolated per-platform pass) but cannot validate the asmdef `precompiledReferences`
  resolving to the real extension DLLs — that is Unity-only. Confirm in a real
  Editor on iOS/Android targets.
- **Native gate (dev-only):** confirm the build-output gating works on device.
  iOS: `QuickActionsEnableMacroiOS` (gated) adds `QUICKACTIONS_ENABLED=1` to the
  Xcode `UnityFramework` target only when enabled, and `QuickActions.mm` is
  wrapped in `#if QUICKACTIONS_ENABLED` — verify a prod build's Xcode project has
  no `QuickActions` symbols. Android: `QuickActionsTrampolineInjectorAndroid`
  (gated) injects the trampoline `<activity>` only when the define is on, and
  `QuickActionsTrampolineStripperAndroid` (ungated) strips any stale entry when
  it is off — verify
  the prod manifest has no `QuickActionsTrampolineActivity` (the `.java` dead
  class remains; literally-zero needs the package excluded from the prod project).
  Detection uses `PlayerSettings.GetScriptingDefineSymbols`; confirm it reflects
  per-Build-Profile defines on Unity 6.
- **Settings-asset orphan when the define is off:** `QuickActionsSettings` (the
  static-shortcuts ScriptableObject) lives in the gated Editor assembly, so a
  project that has a `QuickActionsSettings.asset` and is opened with
  `QUICKACTIONS_ENABLED` off shows it as "missing script". Harmless and reversible
  (re-enable the define), documented in README. A future cleanup could move the SO
  *type* into an always-compiled editor assembly so the asset never orphans.
- **iOS scene lifecycle:** cold-launch dedup relies on the app-delegate model
  (returning `NO` from `didFinishLaunchingWithOptions` suppresses the duplicate
  `performActionForShortcutItem`). Unity's trampoline uses the app-delegate model
  by default; if a project adopts the `UIScene` lifecycle, add a
  `windowScene:performActionForShortcutItem:` hook and queue-level dedup.
