# Changelog

All notable changes to this package are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the package adheres
to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.0] - Unreleased

### Added

- **SF Symbols and template-image icons on iOS**: `QuickActionItem.IosSystemImage`
  (e.g. `"star.fill"`, iOS 13+) and `QuickActionItem.IosTemplateImage` (bundle
  image name). Icon priority is SF Symbol > template image > `IconType` glyph,
  for dynamic and static (Info.plist `IconSymbolName`/`IconFile`) shortcuts
  alike; identity survives the cold-start reconcile via the `userInfo` marker.
  iOS 12: a dynamic symbol-only item falls through to the next icon source at
  runtime; a static one renders iconless (the plist carries a single icon key —
  see the `IosSystemImage` doc).
- **Runtime bitmap icons on Android**: `QuickActionItem.AndroidBitmapFile`
  (absolute path to a PNG/JPEG — e.g. a `Texture2D` written with
  `EncodeToPNG()`), plus `AndroidBitmapAdaptive` for `createWithAdaptiveBitmap`
  masking (API 26+). Falls back down the icon chain when the file is missing;
  identity survives the reconcile via the extras marker.
- **Pinned shortcuts (Android 8.0+)**: `QuickActions.IsPinSupported` and
  `QuickActions.RequestPin(id)` → `requestPinShortcut`, ownership-gated so only
  this package's currently installed shortcuts can be pinned. Honest no-op
  (false) on iOS and in the Editor.
- **Per-item payload**: `QuickActionItem.Payload`, an app-defined string riding
  the shortcut (iOS `userInfo`, Android extras + launch-intent extra) and
  restored across cold starts — read it via `GetById(id)?.Payload` from the id
  `Performed` reports (null for static-shortcut taps and ids removed since —
  static items never join the runtime list).
- **`QuickActions.MaxShortcutCount`**: the OS shortcut budget
  (`getMaxShortcutCountPerActivity` on Android; 4 on iOS, the Home Screen
  display limit; 0 in-Editor).
- **In-place `Update(item)`**: replace the added action with the same `Id`
  without changing its list position (launcher rank preserved), one OS update;
  Android user-pinned copies refresh in place. Same honesty contract as `Add`
  (false on refused/dropped writes, previous item restored on failure).
- **`ReportUsed(id)`**: forward in-app feature usage to the launcher's ranking
  predictor (`reportShortcutUsed`), ownership-gated like `RequestPin`; false on
  iOS/Editor (no analog).
- **iOS template-image pipeline**: a texture list in Project Settings ▸ Quick
  Actions — each PNG/JPEG is copied into the generated Xcode project's app
  target at build time, so `IosTemplateImage` art ships straight from Unity
  assets (manifest-scoped Append-build cleanup; only files the package copied
  are ever touched).
- **Per-locale titles/subtitles (localization)**:
  `QuickActionItem.LocalizedTitles`/`LocalizedSubtitles` (`LocalizedText`
  pairs; exact locale > language prefix > base text, case-insensitive) and
  `QuickActions.Locale` (defaults to the device language; setting a different
  value re-pushes so labels re-render immediately). Base text and tables
  survive cold starts inside the ownership-marker payload, and a stale render
  after a device-language change is refreshed with one automatic push on the
  next launch. **Static** (baked) shortcuts localize on Android only, via
  generated `values-<qualifier>/` string resources written under the package's
  own file name so a host app's `strings.xml` is never touched. iOS static
  shortcuts render in their base language: iOS resolves Info.plist
  localizations through `<locale>.lproj/InfoPlist.strings`, a bundle path whose
  every component the platform dictates, so shipping one would collide with any
  host that localizes its own display name or usage strings — a build failure
  or a silent overwrite of output this package does not own. Dynamic shortcuts
  localize on both platforms.
- **iOS UIScene-lifecycle delivery**: when a host adopts the scene lifecycle
  (scene manifest + scene delegate), cold and warm taps now arrive through
  hooks the package installs on the scene-delegate class it learns from the
  host's `UISceneConfiguration`; a default Unity project's launch path is
  untouched. If the host overrides
  `application:configurationForConnectingSceneSession:options:` in a
  `UnityAppController` subclass, that override shadows the package's and the
  class is instead learned from the connecting scene — which covers warm taps
  and later connections, but may miss the first cold tap of that install
  (calling `[super ...]` from the override avoids the gap). Not yet
  device-validated — see ROADMAP. A consume-once cold-dedup marker guarantees one queue entry per
  tap — it also fixes the documented double delivery when a host
  `UnityAppController` subclass discards our `didFinishLaunchingWithOptions`
  return value.
- **Android device smoke + emulator CI (experimental)**: `tools/device-smoke/`
  installs a dev APK, drives the demo's autotest hook, asserts the shortcuts
  registered via `dumpsys shortcut` (scoped to the app id) and that a
  simulated trampoline tap delivers `Performed`; a manually-dispatched
  workflow runs it on a GitHub-hosted emulator. iOS automation is documented
  as not shipped (no adb analog) with manual steps instead.
- CI now packs `dist~/QuickActions.unitypackage` and uploads it as a workflow
  artifact.

## [0.1.0] - 2026-07-29

### Added

- Initial release. Runtime quick-actions API (`QuickActions.Add/AddList/Remove/
  RemoveById/RemoveAll/GetAll/GetById/IsAdded`) with `Performed` event and
  `LastPerformed` for cold launches.
- iOS implementation via `UIApplicationShortcutItem` with `UnityAppController`
  swizzling (no AppDelegate edits).
- Android implementation via `ShortcutManager` dynamic shortcuts plus a
  trampoline activity that works on both `UnityPlayerActivity` and
  `UnityPlayerGameActivity`. The trampoline `<activity>` is injected into the
  generated Gradle manifest by a gated build post-processor (Unity never merges
  a loose `AndroidManifest.xml` from inside a UPM package).
- Host coexistence on both platforms: every shortcut the package creates is
  stamped with an ownership marker (iOS `userInfo`, Android `ShortcutInfo`
  extras), and all writes/removes/read-backs are scoped to that subset — a host
  app's own quick actions are never absorbed, republished, or removed. Android
  uses the additive `addDynamicShortcuts`/`removeDynamicShortcuts` APIs instead
  of full-set replacement.
- `IconType` system-icon enum, Editor *About* window, and a Demo sample.
- **Editor Simulator** (*Window ▸ Quick Actions ▸ Simulator*): lists the runtime
  and static shortcuts and fires a tap (raises `Performed`, updates `LastPerformed`)
  through the real path so routing code can be tested without a device. In Play
  Mode it's a warm tap; outside Play Mode it **starts Play Mode and seeds the id
  into the runtime's pending queue before the first scene loads**, so the normal
  one-frame `QuickActionsRuntime` drain delivers it — a real cold launch through
  the real pipeline, as if the app was opened by that shortcut while closed.
- Static shortcuts: a **Project Settings ▸ Quick Actions** asset plus build
  post-processors that bake shortcuts into iOS `Info.plist`
  (`UIApplicationShortcutItems`) and Android `res/xml/quickactions_shortcuts.xml` +
  launcher-activity meta-data. Static intents reuse the trampoline via an
  action-encoded id.
- Unity-free verification harness (`.verify/`, `tools/verify.sh`): compiles the
  C# against UnityEngine/UnityEditor stubs and the Android plugin against Android
  SDK stubs. Toolchain baked into the devcontainer image.
- Unit tests (`Tests/Editor/`): an NUnit suite for the runtime API (list
  management, validity, equality, full IconType pinning, single-shot event
  dispatch, OS reconcile, RemoveAll OS-first ordering, ordered drain,
  LastPerformed) runnable in the Unity Test Runner and via `dotnet test`; plus
  JsonUtility serialization tests.
- Store collateral: marketing images at Asset Store sizes (`store~/`,
  `tools/gen_store_images.py`), `STORE_CHECKLIST.md`, and `plans/release.md`.
- **Opt-in `QUICKACTIONS_ENABLED` gate:** managed asmdefs use
  `defineConstraints: [QUICKACTIONS_ENABLED]`. Native plugins (which Unity won't
  gate via define constraints) are gated at the build-output level: the iOS
  `.mm` is wrapped in `#if QUICKACTIONS_ENABLED` and a gated post-processor adds
  that macro to the Xcode project only when enabled (so it compiles to nothing in
  prod); the Android trampoline `<activity>` is only injected by a gated
  post-processor when enabled, and an ungated post-processor additionally strips
  any stale entry when disabled. With the define off there is zero C# and no iOS
  swizzle (the trampoline `.java` remains a dead, unreachable class). Add the
  define to a dev Build Profile to use it. See README "Dev-only".
