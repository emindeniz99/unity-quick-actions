# Changelog

All notable changes to this package are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the package adheres
to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

> **Note on the early history.** Versions 0.2.0 through 0.4.0 were developed in
> three waves before the package was ever published, and shipped together as the
> **first public release, `v0.4.0`**. Only `v0.4.0` and later are tagged — 0.1.0
> through 0.3.0 were never published and cannot be installed. Each wave is kept
> as its own section because each is a distinct, self-contained set of API
> additions; read them as the package's development log.

## [0.4.4] - 2026-08-07

### Added

- **Window ▸ Quick Actions ▸ Enable Quick Actions** — one click adds the
  `QUICKACTIONS_ENABLED` define for Standalone, Android and iOS. It lives in a
  new `Editor/Bootstrap` assembly that carries **no** define constraint and no
  platform constraint, so it is the one piece of the package that exists while
  the package is switched off. Editor-only; nothing reaches a player build.

  Unity's own Asset Store validator is what made the case: importing the package
  into a clean project raises *"Check Missing Components in Scenes"* against the
  Demo scene, because the component it references is compiled away without the
  define. Nothing is broken — that is the gate working — but a first-time user
  reasonably reads an inert package and a missing script as a defect. The menu
  item turns "read the docs, find the right Player Settings tab, do it per
  platform" into one click, and greys itself out once every target has it.

  The define check matches whole tokens rather than substrings, so an unrelated
  define that merely contains this one as a prefix cannot be mistaken for it.

### Changed

- The stub harness compiles **10** configurations, not 9: the new ungated
  assembly gets its own config with `QUICKACTIONS_ENABLED` absent, which is what
  proves it cannot accidentally depend on a gated type.

## [0.4.3] - 2026-08-07

Completes the 0.4.2 packaging fix and undoes one part of it that backfired.

### Fixed

- **The dev folders are now hidden from Unity on *every* install path.** 0.4.2's
  `files` allowlist is an npm concept: it governs the OpenUPM tarball and has no
  effect on `Add package from git URL`, which the README calls the recommended
  method. That path clones the whole repo into `Library/PackageCache`, where
  Unity imports anything not dot-prefixed or tilde-suffixed — so 48 files
  including all of `tools/` (shell scripts, the packer, the device-smoke
  harness), `plans/` and `docs/` still landed in the Project window of anyone
  following our own first instruction. Renamed to `tools~/`, `plans~/` and
  `docs~/`; their folder `.meta` files are gone. (Deleting the `.meta` alone
  would not have worked — Unity hides by the `~`/`.` prefix, not by absence of
  a meta.) The maintainer-facing Markdown at the repo root — `CLAUDE.md`,
  `RELEASE_RUNBOOK.md`, `STORE_CHECKLIST.md` and friends — is still visible on
  that path, and stays that way on purpose: renaming those would break every
  relative link on GitHub and on the OpenUPM package page.
- **`LICENSE.md` is back in the `.unitypackage`.** Removing it in 0.4.2 did not
  take the MIT grant out of the artifact — the shipped README still carries an
  MIT badge and calls the package "MIT-licensed" in its first paragraph — it
  only broke that badge's `./LICENSE.md` link for every customer. Dual
  distribution is the copyright holder's right; the honest handling is to
  disclose it in the Asset Store submission notes, which `STORE_CHECKLIST.md`
  now requires, rather than hide the file and leave the claim dangling.

## [0.4.2] - 2026-08-07

Packaging, guardrails and store-compliance. No public API change.

### Fixed

- **The published package no longer carries the development repository.** There
  was no `files` allowlist, so `npm pack` shipped everything tracked — 303 files,
  2.1 MB. Worse than the weight: `tools~/`, `plans~/` and `docs~/` are neither
  tilde- nor dot-prefixed **and** carry folder `.meta` files, so Unity *imported*
  them into every consuming project — a consumer's Project window contained our
  release runbook and publishing notes. Now 107 files, 465 KB, containing only
  `Runtime`, `Editor`, `Plugins`, `Tests`, `Samples~` and the user-facing docs.
- **The settings asset no longer defaults inside the install root.**
  `DefaultAssetPath` moved from `Assets/QuickActions/QuickActionsSettings.asset`
  to `Assets/Settings/QuickActionsSettings.asset`. Updating a `.unitypackage`
  install means deleting and re-importing `Assets/QuickActions/`, which would
  have taken the user's own configuration with it. Existing projects are
  unaffected — the asset is located by type, not by path.
- The `.unitypackage` no longer emits a folder entry for the bare `Assets` root
  (Asset Store rule 5.2.d), and no longer includes `LICENSE.md`: Store products
  are governed by Unity's EULA, and an MIT grant beside it presents a reviewer
  with two licences for one product. The source remains MIT and public.
- Marketing images regenerated to Unity's key-image text rules — `social.png`
  and `icon.png` now carry no text, `card.png` only the title and publisher,
  `cover.png` the title plus one tag line. The feature grid also claimed
  "Unity 2022 → 6" while the package supports 2021.3.
- `tools~/gen_store_images.py` now finds a real font on macOS instead of silently
  falling back to Pillow's bitmap default, which produced art too coarse to
  upload; when nothing is found it says so loudly.

### Added

- **`tools~/check_frozen_strings.py`**, run by `tools~/verify.sh`, pins the 13
  identifier strings the OS persists on end-user devices — the ownership marker,
  the trampoline class name, the intent action prefix and extra, and the
  icon/payload/l10n keys — across all 24 copies in Java, Objective-C and C#.
  These cannot be covered by a C# test (the Editor constants live in assemblies
  the test asmdef does not reference, and two of the three languages are not C#),
  and renaming one fails **silently**: the app launches and `Performed` never
  fires. A reverse scan also rejects any new or misspelled variant.
- README now documents the dual-install collision: the UPM package and the
  `.unitypackage` share assembly names and asset GUIDs, so a project holding both
  fails to compile. Unity raises that at compile time — before any of our code
  could run — so the package cannot detect it and warn.

## [0.4.1] - 2026-08-07

Documentation and examples only — no runtime, editor or native code changed
from `0.4.0`. Published because `0.4.0` reached the OpenUPM registry carrying a
pre-release README and a broken example project, and a registry version is
immutable once built.

### Fixed

- **The example project could not be opened by anyone.** Its package path was
  `file:../../../..`; Unity resolves a `file:` path relative to the project's
  `Packages` folder, so that pointed one level *above* the repository root and
  failed with `The file [.../package.json] cannot be found` on every editor
  version. Corrected to three levels.
- **The example targeted only Unity 6** (`ProjectVersion.txt` 6000.3.21f1) while
  claiming "Unity 2021.3 LTS or newer", and pinned Unity-6-only packages. Unity
  migrates projects forward only, so 2021.3 and 2022.3 users hit package
  resolution errors.
- Documentation that still described the package as unreleased: install
  instructions telling readers no tag existed and to build the
  `.unitypackage` locally, and an OpenUPM submission recipe whose metadata
  template omitted seven required fields and described `gitTagPrefix` and the
  submission flow incorrectly.

### Added

- `Examples~/Testbed2021`, `Examples~/Testbed2022` and `Examples~/Testbed6` —
  one consuming project per supported editor line, each carrying that line's own
  manifest, the `QUICKACTIONS_ENABLED` define, and three static shortcuts so a
  long-press shows shortcuts before the app is first opened. Each verified to
  open in its own editor with no resolution or compile errors.
- `TestbedBuilder.BuildAndroidPhone` — an IL2CPP build carrying both `arm64-v8a`
  and `armeabi-v7a`. The previous default (Mono, `armeabi-v7a`) will not install
  on the 64-bit-only SoCs shipping since around 2023.
- First physical-device confirmation, recorded in `PRODUCTION_READINESS.md`
  (Moto G Play 2024, Android 14): static shortcuts render on a cold,
  never-opened install, and the static/dynamic same-id collision behaves exactly
  as documented.

## [0.4.0] - 2026-08-07

First public release.

### Added

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
- **Android device smoke + emulator CI (experimental)**: `tools~/device-smoke/`
  installs a dev APK, drives the demo's autotest hook, asserts the shortcuts
  registered via `dumpsys shortcut` (scoped to the app id) and that a
  simulated trampoline tap delivers `Performed`; a manually-dispatched
  workflow runs it on a GitHub-hosted emulator. iOS automation is documented
  as not shipped (no adb analog) with manual steps instead.

### Fixed

- **Compiles on Unity 2021.3, the version `package.json` declares as the
  minimum.** The localization mapping named `SystemLanguage.Hindi`, which Unity
  added in 2022.2, so the package could not compile on its own declared floor.
  The case now sits behind `UNITY_2022_2_OR_NEWER`; Hindi still maps on the
  versions that have it. The stub-compile harness could not catch this — its
  `SystemLanguage` enum mirrored a newer Editor than the minimum — so that stub
  now mirrors the minimum instead.

## [0.3.0] - 2026-07-29

### Added

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

## [0.2.0] - 2026-07-29

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
- Unity-free verification harness (`.verify/`, `tools~/verify.sh`): compiles the
  C# against UnityEngine/UnityEditor stubs and the Android plugin against Android
  SDK stubs. Toolchain baked into the devcontainer image.
- Unit tests (`Tests/Editor/`): an NUnit suite for the runtime API (list
  management, validity, equality, full IconType pinning, single-shot event
  dispatch, OS reconcile, RemoveAll OS-first ordering, ordered drain,
  LastPerformed) runnable in the Unity Test Runner and via `dotnet test`; plus
  JsonUtility serialization tests.
- Store collateral: marketing images at Asset Store sizes (`store~/`,
  `tools~/gen_store_images.py`).
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
