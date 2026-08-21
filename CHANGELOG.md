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

## [Unreleased]

### Fixed

- **Launcher icons could ship blank in minified release builds.** Every icon
  drawable is reached only through `getIdentifier("ic_quickaction_" + name, …)`,
  so with `minifyEnabled` + `shrinkResources` nothing statically references it
  and the shrinker may dummy-replace the file — bytes swapped for a tiny
  placeholder while the resource-table entry survives, so `getIdentifier` still
  returns non-zero and the launcher draws an empty square in release builds
  only, looking exactly like the un-configured state. AGP's *safe* (default)
  mode does carry a prefix heuristic for this concatenation shape, so the
  catalog names were probably surviving there — but that is an implementation
  detail, it can never cover a custom `AndroidDrawable` name handed in from C#
  at runtime (never a constant in the compiled code), and a single library
  declaring `tools:shrinkMode="strict"` flips the *whole* app to strict mode,
  where nothing name-resolved survives and a package cannot opt out. The Android
  post-processor now writes `res/raw/quickactions_keep.xml`
  (`tools:keep="@drawable/ic_quickaction_*"`, uniquely named because keep files
  merge globally by name) on **every** enabled Android build — before launcher
  discovery and before the zero-static-shortcuts return, because a dynamic-only
  project bakes no static set yet resolves its icons by exactly the same lookup —
  and never changes the host app's shrink mode. The define-off stripper deletes
  it, so a prod project keeps carrying no package trace. 8 new headless tests
  pin the emission, the parsed `tools:keep` value, idempotence, and that the
  static-shortcut cleanup leaves the file alone; that the shrinker **honors** it
  is not yet confirmed on a real minified Gradle build.
- **The define-off stripper left this package's per-locale string files in the
  Gradle project.** Reviewing the keep-file cleanup exposed an older gap next to
  it: the ungated stripper deleted `res/xml/quickactions_shortcuts.xml` and the
  base `res/values/quickactions_strings.xml`, but never the
  `res/values-<qualifier>/quickactions_strings.xml` copies the baker writes for
  localized static shortcuts — the stripper's delete list predates localization
  and only the gated cleanup learned about the per-locale files. A reused or
  exported Gradle project rebuilt with the define off therefore still shipped
  this package's `qa_short_N`/`qa_long_N` labels inside a release APK,
  contradicting the documented "no package trace" guarantee (which was measured
  before localization existed). The stripper now sweeps `values-*` directories
  for its own file name in both modules — a host app's `values-fr/strings.xml`
  is never touched — verified by compiling the stripper define-off against the
  stub harness and running it over a populated Gradle tree.
- **The release guard added in 0.4.6 did not catch the mistake it was added
  for.** `verify.sh` check 6 exempted an `[Unreleased]` top heading
  unconditionally, so the exact release-PR slip it exists to stop — bump
  `package.json` to 0.4.7, forget to rename the section — passed verify green,
  merged, and then killed the release workflow on main with no release cut. The
  exemption is now conditional: an `[Unreleased]` heading is a legal
  mid-development state only while `package.json` still names the *last
  released* version. Verified against all four cases (bumped + `[Unreleased]`
  now fails; un-bumped + `[Unreleased]` still passes; matching dated section
  passes; mismatch fails).
- **A comment claimed a structural invariant the code did not have, and its
  test was a tautology.** `BuiltinNames` was described as "shared by
  `BuiltinValues` … so the two can never drift apart"; `BuiltinValues` in fact
  spells its own keys and never reads the array, and the test only asserted
  that the probe built *from* `BuiltinNames` contained `BuiltinNames`. The test
  now compares the two key sets per platform — a placeholder added to one side
  alone turns it red (mutation-checked) — and the comment says what is true.
- **Docs that stopped matching the repo.** README said the Unity CI lane had
  never produced a green run (it has produced two, both green by the licence
  gate exactly as designed); `PRODUCTION_READINESS.md` opened and signed off
  with "no physical-device validation has happened" while recording an Android
  handset run three sections later, and cited README line numbers that the
  0.4.6 growth had moved; `MAINTAINING.md`'s pre-1.0 Android checklist left
  three already-executed items unticked; `.verify/README.md` (and
  `SECURITY.md`) documented a `.devcontainer/Dockerfile` that has never existed
  in this repo, sending web sessions past the `setup.sh` they actually need;
  `RELEASE_RUNBOOK.md` still told the maintainer to push the tag by hand, which
  now collides with or races `release.yml`; and `release.yml`'s own header
  claimed "--clobber semantics" its publish step does not have (`gh release
  create` has no such flag).
- **`ROADMAP.md` broke its own rule** ("Delete an entry in the same commit that
  ships it"). The device-CI entry still said there was no Unity licence in CI
  and listed the game-ci wiring as remaining work — 0.4.6 shipped it; the
  documentation-site entry cited a 590-line README that is now 832 lines, which
  is the very "keeps growing" trigger it defers on; and the real-Editor asmdef
  confirmation was listed as open although `PRODUCTION_READINESS.md` records
  the Editor passes that closed it.
- **The install pins pointed at the previous release.** `v0.4.6` is tagged and
  published, so README / `GETTING_STARTED.md` / `CONTRIBUTING.md` /
  `CLAUDE.md` no longer tell readers to pin `v0.4.5` — a version that does not
  contain the placeholder feature the same README documents.

### Changed

- **The settings page no longer rescans the whole project on every repaint.**
  `Project Settings ▸ Quick Actions` called `AssetDatabase.FindAssets` once per
  GUI event; on a project with two settings assets it also re-logged the
  "found N" warning just as often. The asset is cached for as long as the page
  is open and dropped when it closes.

## [0.4.6] - 2026-08-20

### Added

- **A real-Unity CI lane: the `unity` workflow (GameCI).** The stub harness
  proves the C# type-checks but cannot open an editor;
  `.github/workflows/unity-ci.yml` is configured to run the EditMode suite on
  all three `Examples~` testbeds for every code push and PR, and — on manual
  dispatch or the weekly cron, because an hour of IL2CPP per line is not what a
  one-line change needs — to build a development APK per line and feed it
  through the adb device smoke, and export the iOS Simulator Xcode project on
  every line, compiling it unsigned and cold-launching it on a macOS-runner
  simulator for 2022.3 and Unity 6 (2021.3 exports only — that line's simulator
  support is x86_64-only). Unity jobs skip cleanly when the
  `UNITY_LICENSE`/`UNITY_EMAIL`/`UNITY_PASSWORD` secrets are absent, which is
  exactly what a fork PR gets: GitHub withholds secrets from fork-triggered
  runs, so untrusted code never reaches them, and the run still ends green.
  Not yet run — it awaits the licence secrets and a merge to `main`.

- **Releases cut themselves: the `release` workflow.** Bump `version` in
  `package.json`, date the `CHANGELOG.md` section, merge — and
  `.github/workflows/release.yml` notices that main declares a version with no
  tag, runs `verify.sh`, packs the `.unitypackage`, creates the tag and
  publishes the GitHub Release with **your changelog section as its notes**. A
  merge that does not bump the version is a no-op. No tag to push, no artifact
  to upload, and the release notes stay the hand-written narrative this file
  already is — which is precisely why Release Please was not adopted: it owns
  `CHANGELOG.md` with no opt-out, and anything it does with the repo's own
  `GITHUB_TOKEN` cannot trigger the packaging job, so its releases would ship
  without the `.unitypackage` unless a long-lived PAT were added to a public
  repo. `MAINTAINING.md` records the full reasoning.
- **`verify.sh` gained a sixth check** (and its header stopped claiming four):
  `package.json` and the top `CHANGELOG.md` heading must agree on the version.
  That rule existed only as prose in `MAINTAINING.md`, yet breaking it means
  OpenUPM's **E811** and release notes describing the wrong version — now the
  PR goes red instead of main after the merge that would have cut the release.
  A section still called `[Unreleased]` passes, since that is a legal
  mid-development state. `CONTRIBUTING.md` said "four checks" and "nine build
  configurations"; both were already wrong and now read six and ten.

- **Static shortcut labels can now carry build-time `{placeholder}` tokens** —
  the answer to "which build is on this device?" from a long-press, before the
  app is ever launched. The platform bakers resolve them while writing
  `Info.plist` / `shortcuts.xml`: `{version}`, `{build}` (iOS build number /
  Android versionCode — each platform gets its own), `{bundleId}` (on Android
  the **Gradle-resolved** `applicationId`, the same value the static intent
  targets), `{productName}`, `{unityVersion}`, `{platform}`. Matching is
  case-insensitive, `{{` escapes a literal brace, localized rows are
  interpolated too, and an unknown token is left verbatim — warned about on the
  settings page and in the build log — rather than baked as a hole. The
  settings page also gained a one-click **"Add app info shortcut"** preset
  (`app_info` / `v{version} ({build})` in the subtitle, because the subtitle is
  the line long-press actually shows on Android), and the Simulator window
  previews built-in tokens for the active build target.
- **`QuickActionsStaticBuild` — the baked set is now programmable.** Rather
  than shipping an opinionated "development-only" list, the pipeline exposes
  the two primitives that make any policy a three-line editor script:
  `RegisterPlaceholder("buildDate", …)` for custom values (env vars, git, CI
  numbers; a throwing resolver never fails the build — its token falls back to
  verbatim, or to the built-in value it shadowed, with a warning) and the
  `Customize` event, which hands subscribers the exact item list about to bake
  (copies — the asset is untouched) plus the platform and the Development-build
  flag, so `if (ctx.DevelopmentBuild) ctx.Shortcuts.Add(…)` is the dev-only
  recipe. A throwing `Customize` subscriber fails the build on purpose — a
  half-customized release set would be worse. 17 new headless tests pin the
  engine's contracts; `dotnet test` now runs **90**.

### Changed

- **A pre-existing static label that happens to contain `{aKnownTokenName}` or
  doubled braces now changes output on upgrade.** Interpolation is
  unconditional, so a literal `{version}` typed before this release starts
  resolving and `{{`/`}}` collapse to one brace — silently, since only
  *unknown* tokens warn. Non-token-shaped brace text (`{}`, `{a b}`, an
  unclosed `{`) still bakes exactly as typed; double the braces to keep
  token-shaped text literal.

### Fixed

- **Four stale harness counts in the docs said "9 compile configurations".**
  `verify.sh` has built 10 since the Bootstrap config landed (the 9→10 change
  was even changelogged at the time); README's Verifying section,
  `PRODUCTION_READINESS.md`'s sign-off, `MAINTAINING.md`'s verify description
  and `.verify/README.md`'s config list now say 10 — and the latter now
  actually names `Bootstrap`.

## [0.4.5] - 2026-08-11

Documentation only. No code change.

### Fixed

- **The one instruction this package gave for adding an Android shortcut icon
  was wrong, and would have broken the reader's build.** `store~/README.md`
  told users to drop drawables into `Assets/.../Plugins/Android/res/drawable/`.
  Unity **removed** that path in 2021.2 — one minor version below this
  package's declared 2021.3 floor — and files placed there now fail the build
  outright (`OBSOLETE - Providing Android resources in Assets/Plugins/Android/res
  was removed`), rather than being quietly ignored. It was wrong on every Unity
  version the package has ever supported.
- **And no shipped file said where drawables go at all.** That instruction lived
  in `store~/`, which reaches no consumer on any of the three channels (excluded
  from the npm/OpenUPM tarball and the `.unitypackage`, tilde-hidden on the
  git-URL path). Every surface a user actually reads — the README field table,
  `IconType.cs`, the Java bridge comment, the Editor's build warning — named the
  `ic_quickaction_<name>` convention and never named a folder. The package
  required a step it documented nowhere.

### Added

- README gains **"Android icons need a drawable in your project"**: why the two
  platforms differ (iOS uses Apple's system glyph catalog; Android has none, so
  the name is resolved out of your project with `getIdentifier`), the correct
  `.androidlib` layout — identical on 2021.3, 2022.3 and 6.x — and two traps:
  resources must sit under `src/main/res/`, and a `res/` folder at the
  `.androidlib` root is silently dropped with no warning.
- The screenshot caption now explains the blank Android icons instead of
  leaving readers to conclude the Android half is broken. It is the
  un-configured state, and it is what every user sees until they add a drawable.

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
