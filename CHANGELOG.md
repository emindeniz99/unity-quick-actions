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
