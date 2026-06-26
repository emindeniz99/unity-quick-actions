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
- **`.unitypackage` export automation** — a batch-mode Unity script so CI can
  emit the classic-format artifact (currently a documented manual step).
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
