# Quick Actions for Unity — Roadmap

Follow-ups discussed but not shipped in v0.1.0. Delete an entry in the same
commit that ships it.

- **Per-item rasterized icons** — accept a `Texture2D`/`Sprite` and emit a
  template `UIApplicationShortcutIcon` (iOS) and a generated drawable
  (Android), instead of only system `IconType` glyphs + named drawables.
- **OS-backed `GetAll()`** — read the currently-installed shortcuts back from
  `UIApplication.shortcutItems` / `ShortcutManager.getDynamicShortcuts()` so the
  managed list is accurate after a cold restart without re-registering.
- **Pinned shortcuts** — `requestPinShortcut` on Android; no iOS analog.
- **Static (build-time) shortcuts** — optional Editor list baked into
  `Info.plist` `UIApplicationShortcutItems` / Android `shortcuts.xml` via build
  post-processors, for shortcuts that exist before first launch.
- **`.unitypackage` export automation** — a batch-mode Unity script so CI can
  emit the classic-format artifact (currently a documented manual step).
- **Automated device CI** — drive an iOS simulator / Android emulator to assert
  cold + warm delivery end-to-end.
- **Localization** — per-locale titles/subtitles.
