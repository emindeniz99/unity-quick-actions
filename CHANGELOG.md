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

### Added

- **The built-in Android icons now ship an API 26+ adaptive variant.** On API 26+
  AOSP does not draw a legacy shortcut drawable as authored — it wraps it onto a
  white plate at 0.70 of the viewport, so the built-ins rendered as a small
  indigo disc inside a white ring, out of place beside their neighbours. Every
  Android build now also writes
  `res/drawable-anydpi-v26/ic_quickaction_builtin_<name>.xml`, an
  `<adaptive-icon>` under the **same resource name**, over two layers of its own
  (`ic_quickaction_builtin_<name>_background`, full-bleed indigo, and
  `…_foreground`, the same glyph scaled into the 66-of-108 safe zone so no
  launcher mask clips it). The plain vector stays for API 25 and the resource
  qualifier picks between them, so nothing that names an icon changes: the Java
  lookup, the static `android:icon="@drawable/ic_quickaction_builtin_<name>"`
  bake, the `ic_quickaction_*` keep rule, the settings opt-out and the define-off
  sweep all cover the new files unchanged. About 6 KB of XML for all four icons.
  Not yet seen on a launcher — neither variant.
- **The `android-smoke` legs now photograph the launcher's long-press sheet.**
  `dumpsys shortcut` proves each shortcut registered *with* an icon resource;
  nothing has ever seen that art drawn by a launcher. With `CAPTURE_LONGPRESS=1`,
  `tools~/device-smoke/android_device_smoke.sh` ends — after its `PASS:`
  verdict — by going home, opening the app drawer, locating the app's icon by
  its launcher label in a `uiautomator dump`, long-pressing it, and keeping
  `longpress.png` plus both hierarchy dumps; CI uploads them per leg as
  `longpress-<leg>`. It asserts nothing and can fail nothing: the gesture and
  the sheet belong to whatever launcher the system image ships, so the capture
  runs with `errexit` off, bounds every `adb` call with `timeout`, prints one
  grep-able `shortcut sheet visible: yes/no`, and leaves the verdict to the
  eight steps above it. Its first run (both images ship the Pixel launcher)
  found the API 30 swipe opening no drawer and the API 35 swipe-hold opening
  no sheet, so the capture now escalates through `KEYCODE_ALL_APPS`, the
  launcher's own "Apps list" handle and the `ALL_APPS` intent when the icon
  is not found, and presses with `input motionevent` DOWN / hold / UP, taking
  the screenshot and the hierarchy while the finger is still down. That
  second attempt (run 56) photographed the Pixel launcher's "App suggestions"
  sheet on API 35: the icon it pressed was the hotseat's *prediction* of the
  app, which never opens the shortcut popup, and on API 30 no opener moved
  the launcher off the home screen. A predicted-app match now counts as a
  miss, so the drawer escalation runs and tries to find, and then press, a
  real icon — an attempt whose first run is still ahead.
- **The settings page says what an `Icon` does on Android, next to the field.**
  A property drawer for `IconType` adds one line under the popup: "built-in
  drawable" for `Add` / `Compose` / `Favorite` / `Play`, or the exact
  `ic_quickaction_<name>` the project must ship for the other 25 — which used
  to surface first as a build-log warning. The four names come from a second
  generated file, `Editor/QuickActionsBuiltInIconSet.cs`, written by the same
  generator from the same list (the Android assembly holding the art is
  `UNITY_ANDROID`-only, so the always-present Editor assembly needed its own
  copy), held to the art by `--check` and by harness tests that also pin the
  drawer's name derivation to the Java `ICON_NAMES` table member by member,
  that the note stops promising a drawable when **Write built-in Android
  icons** is off, and that in the static list it says what the baker needs:
  a static shortcut bakes no icon for a non-built-in choice unless
  `AndroidDrawable` names one. The note wraps at narrow inspector widths.
- **`AGENTS.md`** — the shortest correct integration path for an AI coding
  agent (or a hurried human): install, define, asmdef reference, the guarded
  snippet, the checks that prove it worked, and the don'ts. The README gains a
  60-second quickstart with the same guarded snippet, a contents list, and an
  embedded-package (vendoring) install option.

- **CI runs the iOS native hooks under a mock coexistence host.** Two new
  legs, `ios-export-coex` and `ios-simulator-coex`, re-export Testbed2022 and
  Testbed6 with four sources from `Examples~/Coexistence/iOS/` copied into
  `Assets/Plugins/iOS`, so one app is at once an `IMPL_APP_CONTROLLER_SUBCLASS`
  host, a category `+load` swizzle of the two app-delegate selectors, and a
  GoogleUtilities-style isa proxy — the three shapes a project's own
  `UnityAppController` subclass, AppsFlyer / OneSignal-style categories and
  Firebase take. The Simulator launch must print every `QA-COEX: PASS` line by
  name plus the package's own install line; the category `+load` records
  whether it ran before or after the package's class `+load` and composes
  either way, because the first run showed both — category first on the
  2022.3.62f3 export, class first on 6000.3.21f1, every file in
  `UnityFramework` both times — where the README had asserted a class
  `+load` always runs first. On the scene testbed a second launch shadows
  `application:configurationForConnectingSceneSession:options:` without
  calling super and requires the install line to flip from "via
  configuration" to "via notification". Until now the `ios-simulator` leg
  only required the process to still exist, which an app with no hooks
  installed satisfies too. The lifecycle each leg exercises is asserted, not
  described. **First run (2026-09-02):** on Testbed6 (6000.3.21f1, scene
  manifest, iOS 26 Simulator) every check passed on both launches — the
  UIScene path's first exercise anywhere: hooks on `UnityScene` via the
  configuration wrapper, via the notification fallback when the host shadows
  the selector, the cold launch item queued once with `NO` returned, the
  warm tap once with one completion, the host's discarded `NO` not
  double-delivering. On Testbed2022 the mock host itself was wrong twice: its
  category `+load` demanded an order objc4 does not promise, and its
  scene-configuration override opted the manifest-less app into the scene
  lifecycle — UIKit calls that selector on any delegate that implements it,
  manifest or not, and the app then never became active. Both corrected: the
  order is recorded, and the override exists only under a manifest.
- **One `NSLog` per launch names the branch each iOS hook took.**
  `[QuickActions] iOS hooks: didFinishLaunching=… performAction=…
  sceneConfig=… manifest=…` at the end of `+load`, and
  `[QuickActions] iOS scene hooks installed on <class> via configuration` (or
  `via notification`) when the scene hooks land — the only way an adopter, or
  CI, can tell a working install from an inert one.

- **`Samples~/AndroidIcons` — a working `.androidlib` icon example.** The
  custom-Android-icon recipe as an importable plug-in rather than five written
  steps: **Package Manager ▸ Samples ▸ Import** copies a bare
  `QuickActionIcons.androidlib` — `AndroidManifest.xml` and `res/` at its root,
  the shape CI measures on the 2022.3 testbed — with `ic_quickaction_search`
  and `ic_quickaction_home` drawn like the built-ins, under `Assets/`, where
  Unity picks it up on the next Android build. Its README says how a runtime
  `Add` and a static item each reach the drawable, how to confirm it in the
  APK with `aapt2`, the Unity 6 (AGP 8) `namespace` change, and that the
  `.unitypackage` install cannot carry an `.androidlib`. The folder ships with
  its Android-only `PluginImporter` `.meta`; `tools~/gen_meta.py` now treats a
  `*.androidlib` as one plug-in (a `.meta` for the folder, none for its
  contents) in both its generate and `--check` passes.
### Changed

- **The Unity 6000.6 canary retries an editor that died before the suite
  ran.** `tests (unity6-latest)` has twice (main run 41, PR #16 run 53) ended
  with exit 137 at "Begin MonoManager ReloadAssembly" — the editor killed
  during its own start-up, no test executed — and started fine on the runs
  between. The job now starts the editor once more when, and only when, the
  first attempt left no `editmode-results.xml`; an attempt that produced
  results is final, so a real test failure is never retried, and a verdict
  step turns the job red unless the last attempt passed.
- **The iOS status claims are scoped to the path that was actually run.** The
  Simulator run every doc cited was logged as "Unity 6.3" with no patch number
  and its exported `Info.plist` was never inspected, so which of the two iOS
  delivery paths carried that tap is unknown — 6000.3.8f1 is where Unity
  starts emitting `UIApplicationSceneManifest` and Apple stops calling the
  app-delegate selector. README Status, `PRODUCTION_READINESS.md` (the one
  iOS row becomes two, and the older Simulator run is credited to neither —
  what each row claims now comes from the coexistence leg), `CLAUDE.md`
  and `ROADMAP.md` now say so, and the ROADMAP's scene items start with
  "confirm the app still starts": the package adds
  `application:configurationForConnectingSceneSession:options:` where Unity
  has none, so a wrong return value there would mean no engine at all. The
  README gains **Coexisting with other native iOS plugins**: the five
  selectors the package touches, why it hooks the root class from a class
  `+load`, and what each surveyed SDK family does — Firebase / GoogleUtilities
  isa-swizzling inherits the package's IMPs because it adds no ivars, Firebase
  C++ / OneSignal / AppsFlyer category swizzles chain after it, the listener
  SDKs (Facebook, Helpshift, Unity IAP) cannot collide, Singular's scene
  swizzle composes, and Unity as a Library and the Swift project type are
  unsupported. It is a source audit, and says so.
- **The custom-asmdef integration shape is compiled by CI in both
  configurations.** README tells a project whose scripts live in their own
  assembly definition to reference `EminDeniz99.QuickActions`; with the define
  off that assembly is not compiled, so whether the referencing assembly still
  builds was an assertion about Unity, not a measurement. Testbed2022 now
  carries `Assets/Integration/Testbed.Integration.asmdef`, which references the
  package and holds the guarded quickstart component; the define-on legs and
  the define-off `gate-off` build compile it on every push, and the README
  also describes the gated-glue-asmdef alternative.
- **The `.androidlib` icon recipe is measured on a real APK — and the
  measurement corrected it.** README's recipe for a consumer's own Android
  shortcut icons rested on Unity's documentation and on reading Unity's
  `com.unity.mobile.notifications`, never on a build. Testbed2022 now carries
  an `.androidlib` under `Assets/` and one inside an embedded UPM package
  (`Packages/com.quickactions.testlib/`), and the 2022.3 leg of
  `android-build` requires their drawables in the APK resource table on every
  push. The first run proved the documented layout wrong: a bare `.androidlib`
  (no `build.gradle` of its own) takes `AndroidManifest.xml` and `res/` at its
  root — the `src/main/` layout the README prescribed, Unity's own package's
  layout only because that package ships a `build.gradle`, was silently
  ignored. The recipe, its first "trap" and the keep-file example now say so,
  and a decoy planted under `src/main/res/` must stay absent from the APK on
  every push, so the trap is asserted rather than assumed.
- **The Unity 6 Android leg now builds twice and asserts the second APK.** CI
  had only ever built a clean Gradle project on a fresh runner, so nothing knew
  whether the icons, `res/raw/quickactions_keep.xml`, the baked shortcut
  resources and the trampoline `<activity>` survive an INCREMENTAL build, where
  Unity reuses the project it staged under `Library/`. The leg runs
  `unity-builder` a second time over the same project directory
  (`TestbedBuilder.BuildAndroidPhoneSecond`, same configuration to
  `Builds/QuickActionsDemo-phone-2.apk`), uploads that APK as
  `quickactions-demo-apk-unity6-incremental`, and runs the *same* aapt2
  assertions against it — one function over the leg's APKs rather than a copy
  per APK. The job summary names which of the two was the incremental one.
- **The `.unitypackage` carries the docs the README links to.** The classic
  install now includes `GETTING_STARTED.md`, `SECURITY.md`,
  `PRODUCTION_READINESS.md` and `AGENTS.md` next to the four root docs it
  already shipped, so a reader of the packed README no longer hits dangling
  links. The README's usage example is `#if QUICKACTIONS_ENABLED`-guarded,
  says where the settings asset lives, and the API table states that a `null`
  item throws — the one way any call throws.
- **CI proves the gate and measures the footprint on every push.** A new
  `gate-off` job builds the 2022.3 testbed with `QUICKACTIONS_ENABLED` off —
  an IL2CPP APK in `android-build`'s exact configuration and an iOS Simulator
  export — and asserts the package left nothing behind: no trampoline
  `<activity>` or shortcuts meta-data in the manifest, no `quickactions_*` /
  `qa_*` / `ic_quickaction_builtin_*` resources, nothing of the package's
  assembly in the IL2CPP metadata, no `QUICKACTIONS_ENABLED` macro in the `.pbxproj`, no
  marked `UIApplicationShortcutItems`. Every negative is paired with a positive
  control — the define-on artifacts from the same run must trip the same
  probes, or the check declares itself blind instead of green. The two APKs
  are then diffed entry by entry, plus the whole-file size: that difference is the package's footprint,
  printed in the job summary and held under 1 MiB, so an asset or code path
  that starts shipping turns the job red. `PRODUCTION_READINESS.md`'s two gate
  rows move from "build-proven once, 2026-07-17" to CI. `TestbedBuilder` gains
  `BuildAndroidPhoneNoDefine` / `BuildiOSSimulatorNoDefine`, and its
  `DisableDefine` / `EnableDefine` now flip both mobile targets.

  The first measurement (run 51) read 1,077,821 bytes and failed the ceiling —
  and the entry-by-entry diff said why: `libunity.so` alone accounted for
  1.4 MiB uncompressed, because the define-off build had dropped the IMGUI,
  TextRendering, InputLegacy and both TextCore engine modules. Nothing in the
  package uses them (its runtime reaches AndroidJNI and JSONSerialize, present
  in both APKs); the **Demo sample** does, and the Demo wrapped its whole file
  in `#if QUICKACTIONS_ENABLED`, so with the define off its `MonoBehaviour`
  vanished from `Assembly-CSharp`, engine-code stripping removed the modules
  only it used, and their weight was billed to the package. The Demo now keeps
  its component and IMGUI shell compiled in both configurations and guards
  only the package calls — the README's own guidance, and what the
  `Testbed.Integration` component already did — so the same engine modules
  stay on both sides of the diff. A new harness config, `SampleOff`, compiles
  that `#else` branch the way a define-off device build does (no define, no
  Runtime), which no config did before. The Demo's own namespace,
  `EminDeniz99.QuickActions.DemoSample`, therefore stays in a define-off
  build's IL2CPP metadata — as any consumer namespace would — and run 52
  flagged exactly that as a leak, because the metadata probe grepped for the
  namespace prefix. It now names what only the assembly leaves behind: the
  image name `EminDeniz99.QuickActions.dll`, the `Internal` namespace and the
  root namespace's `namespace|Type` strings — 12 lines on the define-on APK,
  none on the define-off one — and a leak report prints every metadata line
  mentioning the namespace, probe-matching or not. With the Demo on both
  sides, run 52's APK pair diffed through the job's own script measures
  **144,061 bytes (140.7 KiB)** compressed: `libil2cpp.so` +149.7 KiB (arm64)
  and +122.0 KiB (armv7) uncompressed, `global-metadata.dat` +31.4 KiB,
  `libunity.so` +27.0 / +19.9 KiB, and about 10 KB of resources — the
  eighteen files the package writes.
- **The Demo sample survives a define-off build as a real component.** With
  `QUICKACTIONS_ENABLED` off, `Samples~/Demo/QuickActionsDemo.cs` used to
  compile to nothing, so a define-off build of its scene carried a "missing
  script". The component and its on-screen shell now compile either way; with
  the define off the buttons report that the package is not compiled instead
  of calling it. Define-on behaviour is unchanged — the same catalog, log lines
  and autotest hook the device smoke drives.

### Fixed

- **Both scene-discovery routes bind only to Unity's own scene delegate.** The
  `UISceneWillConnectNotification` fallback learned the `UnityScene`-ancestry
  rule in this release; the configuration wrapper still installed the scene
  hooks on whatever class the *first* connecting session declared. A manifest
  may declare more than one role — CarPlay, an external display — and when
  such a session connects before Unity's window, that install burned the
  process-wide one-shot on the host's class (Unity's scene never hooked) and
  added `windowScene:performActionForShortcutItem:completionHandler:` to a
  class that lacked it, with the owner still recorded as confirmed. One
  shared rule now sits in front of both routes: bind only to `UnityScene` or
  a subclass when this process has that class (a non-Unity class is logged and
  left alone, one-shot intact), and bind to the first declared class with the
  owner recorded as unconfirmed when it does not. Found by review; no CI leg
  drives a multi-role manifest yet.
- **A hostile local app can no longer crash the game through the trampoline.**
  `QuickActionsTrampolineActivity` is exported (the launcher has to start it),
  so any app can start it with arbitrary extras — and reading the action id
  unparcels the whole bundle, so a `Parcelable` this app cannot load threw
  `BadParcelableException` out of `onCreate`: a process crash attributed to the
  game. The read is now contained — a bundle that cannot be read is logged and
  treated as no tap — and the launch-intent lookup sits inside the existing
  catch too. A new Java smoke scenario drives the hostile bundle (111 checks).
- **A failed native read is never mistaken for an empty shortcut set.**
  `QuickActionList.Parse` returns `null` for a payload the real `JsonUtility`
  rejects; it used to return an empty list, which the facade treats as
  authoritative and prunes against, so a serializer failure would have removed
  the user's real shortcuts on the next write. The Android bridge's
  `SetShortcuts` propagates that null as a failed write, and the iOS
  `QABuildShortcutsJson` returns `NULL` instead of `{"items":[]}` when
  `NSJSONSerialization` fails. A Unity-only test pins the `Parse` contract
  (the Test Runner suite is 77).
- **The Android SDK-level read can neither throw out of the facade nor pose
  as "API < 25".** The `Build.VERSION` JNI read is guarded like every other
  JNI path in the bridge, read once and cached; a read that fails is kept
  distinct from an unsupported device — the bridge's read and write members
  return their failed-read signal (null) for it, so the facade retries instead
  of adopting an empty set and pruning the user's real shortcuts against it.
- **The `UISceneWillConnectNotification` fallback binds only to Unity's own
  scene delegate.** It used to bind to the first scene that connected,
  whatever it was, and burn its one-shot doing so; on a host that owns the
  scene manifest — Unity as a Library, a SwiftUI `@main` app, a second iPad
  window, an external-display or CarPlay scene — the real Unity scene was
  never hooked, and because the package *adds*
  `windowScene:performActionForShortcutItem:completionHandler:` to a class
  that lacks it, it then looked terminal for the host's own quick actions:
  adopted into a queue nothing on their side drains, completed `YES`. The
  fallback now reads the declared class from the connecting session's
  `UISceneConfiguration` (never a `GUL_` / `NSKVONotifying_` proxy; set before
  the delegate exists) and binds only to `UnityScene` or a subclass when this
  process has one. With no `UnityScene` class — a host-authored manifest on an
  older trampoline — it keeps the first-connecting behaviour but marks the
  owner unconfirmed: there it never adopts an unmarked item and completes
  `NO`, which is what UIKit would have seen had the selector never been added.
  The configuration-wrapper path is unchanged.
- **The defensive `class_addMethod` type encoding is built from
  `@encode(BOOL)`** rather than the hardcoded armv7-era `c` (`BOOL` is `bool`,
  encoding `B`, on arm64).

## [0.5.0] - 2026-09-01

### Added

- **Four built-in Android icons ship with the package** — `IconType.Add`,
  `Compose`, `Favorite` and `Play`, the ones the demo uses. Until now the
  29-entry catalog rendered as a blank square on every Android launcher unless
  the consumer hand-added an `.androidlib` (0.4.5's documented recipe), because
  the Java bridge resolves an `IconType` by drawable *name* and the package
  shipped no drawable. On every Android build with the define on, the
  post-processor now writes `ic_quickaction_builtin_<name>.xml` for each into
  the generated Gradle project (`unityLibrary/src/main/res/drawable/`), next to
  the keep rule that carries them through `shrinkResources`. All four,
  unconditionally: a runtime `Add(...)` cannot be known at build time, and
  "only the ones the static set references" would have left the most common —
  dynamic-only — project blank. VectorDrawables, so one density-independent
  file each and nothing for a launcher to upsample; about 2 KB of APK for all
  four. A **static** item whose `Icon` is one of the four and whose
  `AndroidDrawable` is empty now bakes
  `android:icon="@drawable/ic_quickaction_builtin_<name>"` — before, such an
  item baked no icon at all and the build warned it would be blank, which
  would have made the headline claim false on exactly the surface a cold
  install shows. The warning stays for the other 25.

  The XML is generated by `tools~/gen_builtin_icons.py` (pure stdlib; the same
  glyph geometry as the store images, on a coloured disc so the icon carries
  its own contrast — API 26+ launchers wrap a legacy shortcut icon onto a
  white plate, where a white glyph alone is invisible) and embedded in
  `Editor/Android/QuickActionsBuiltInIcons.cs` rather than shipped as assets,
  so a Git, OpenUPM or `.unitypackage` install delivers identical bytes with no
  package-path resolution; `verify.sh` gains a seventh check that fails when
  the file and its generator disagree.

  **Your own drawable wins — by name.** The built-ins carry their own prefix,
  and `QuickActionsBridge` asks for the project's `ic_quickaction_<name>`
  first, falling back to `ic_quickaction_builtin_<name>`. So the package never
  writes, overwrites or looks for a file under the project's prefix, and the
  precedence holds for a drawable delivered any way at all — an `.androidlib`,
  an `.aar`, a Maven dependency — because nothing depends on the package
  seeing it. (An earlier draft of this feature wrote under the project's
  prefix and scanned the export for a clash; review showed the scan could not
  see an `.aar` and would have silently replaced that art, and that the shape
  cannot be expressed on Unity 6's `AndroidProjectFilesModifier`, where every
  output must be declared before the tree exists. The prefix answers both.)
  A new **Write built-in Android icons** checkbox in *Project Settings ▸ Quick
  Actions* (default on) is the escape hatch for a project that wants no
  package art in its APK: off, nothing is written and a copy an earlier build
  left is removed. The define-off stripper sweeps `ic_quickaction_builtin_*`
  out of `unityLibrary` — only that module, only that prefix, any extension —
  so a production build carries none of this art and a project's own
  `ic_quickaction_*` is never within its reach.

  Held in place by twelve harness tests (the verbatim write, well-formed
  vectors, the dynamic-only and no-launcher cases, idempotence, the
  never-touch-the-project's-file rule in every location, the static reference
  for each of the four and its absence for the rest, `AndroidDrawable`
  precedence, the opt-out in both directions, the stripper's exact reach, and
  each name pinned against both its `IconType` member and the Java
  `ICON_NAMES` table read from the source file); a Java smoke check that the
  lookup prefers the project's name and falls back to the built-in; the
  `android-build` job's `aapt2` read-back (all four in every APK on all three
  lines, with the demo's `new_game` now referencing one, so the reference has
  to link); the `android-shrink-verify` job (the shipped icons must come
  through the shrinker at their linked size); and the emulator smoke, which
  now requires the registered shortcuts to have resolved an icon resource in
  `dumpsys shortcut` — the by-name lookup observed on an Android runtime. What
  no check covers is the launcher itself: nobody has seen these on a home
  screen yet, and the README's Android screenshot predates them.

  The remaining 25 catalog entries still need a drawable in the consuming
  project — the catalog is iOS-complete and Android-partial, and the README
  says so in those words. `store~/example-shortcut-icons/` is regenerated from
  the same generator (`gen_store_images.py` now delegates to it), so the
  example art a consumer copies is the style that is visible on a launcher,
  not the white-on-transparent one that was there.

### Fixed

- **The `unity6-latest` canary made its first real catch — and it was the
  harness, not the package.** On 2026-09-01 it resolved the freshly published
  6000.6.0f1 image, whose editor aborts at start-up inside a container:
  "Requested 1073741824 bytes, but only 67108864 bytes available … run the
  container with --shm-size=1025M" (its asset database's UDS client). Docker's
  default `/dev/shm` is 64 MB and GameCI's actions pass no `--shm-size`, so
  every job in `unity-ci.yml` that runs an editor in a container now raises the
  Docker daemon's `default-shm-size` to 2 GB first (merged into the runner's
  existing `daemon.json`, then a daemon restart). The three pinned legs stay on
  editors that do not need it, and will the day a testbed moves to 6.6+.
- **Docs and workflow comments that had drifted from the repo** — the sweep
  an agent-run audit produced (30 findings, each checked against the file
  before it was touched; the ones the evidence contradicted were dropped, e.g.
  "the weekly cron did not fire" — it did, run 35 on 2026-08-31). README's
  Status line still said 0.4.8: the 0.4.9 cut moved every install pin but that
  one because nothing matched it, so `tools~/release_notes.py` now pins the
  `This is **x.y.z**` line too, and MAINTAINING / the runbook list it. README
  also called the shrink verdict "still pending" fifty lines after reporting
  it, described a JDK 11 / Gradle 7.2 toolchain that run 25 had disproved
  (the job supplies JDK 17 and takes Gradle from the export's own wrapper
  pin), and kept the retired "split by cost" gating rationale; CONTRIBUTING,
  MAINTAINING, CLAUDE.md, `.verify/README.md` and `ci.yml` described
  `verify.sh` as four or six checks when it runs seven; `unity-ci.yml`'s
  header still said the shrink job assembles a release build (it runs
  `:launcher:shrinkReleaseRes`) and that a run without secrets "stays green"
  (only a fork or Dependabot PR does; any other event fails the gate);
  `device-ci.yml` still claimed there was no Unity licence in CI;
  `RELEASE_RUNBOOK.md` Phase 5 told the maintainer to submit a package that
  has been in Asset Store review since 2026-08-07; `ROADMAP.md` dated itself
  "as of v0.4.0"; and `store~/listing/metadata.md`'s 0.4.4 now says it is the
  submitted version, not the current one.

### Changed

- **CI: three gaps the same audit found.** A change to `Samples~/Demo` — the
  scene every Android APK, aapt2 read-back and emulator smoke is built from —
  did not trigger `unity-ci.yml` at all; it does now. `ci.yml` gains a
  `lint-workflows` job that runs actionlint (shellcheck + pyflakes over every
  `run:` block — two comments in `unity-ci.yml` had claimed this was enforced,
  and nothing ran it) and asserts the hand-duplicated push / pull_request
  `paths:` lists stay identical. `android-shrink-verify`'s 120-minute ceiling,
  sized for the assembleRelease it no longer runs, is 45 — the job takes about
  four minutes with a warm cache.

## [0.4.9] - 2026-08-29

### Verified

- **The resource shrinker honours the icon keep rule — measured, not assumed.**
  0.4.7 shipped `res/raw/quickactions_keep.xml` so that drawables reached only
  through `getIdentifier("ic_quickaction_" + name)` survive `shrinkResources`,
  and every doc since has said the shrinker's side of that was unconfirmed.
  It is now confirmed. On 2026-08-29 the `android-shrink-verify` job completed
  for the first time, against a Unity 2022.3 export with `minifyEnabled` and
  `shrinkResources` on:

  | planted drawable | before | after | meaning |
  | --- | --- | --- | --- |
  | `ic_quickaction_probe` (matches the keep glob) | 990 B | **990 B** | untouched — the keep rule held |
  | `zz_shrink_control` (matches nothing) | 990 B | **67 B** | replaced by AGP's dummy — the shrinker ran |

  Both halves are required: without the control shrinking, an intact probe
  would only mean the shrinker never ran. The job re-runs on every code push,
  so this is a standing check rather than a one-off observation. ROADMAP
  verification item (c) is retired.

### Changed

- **Every CI leg now runs on every code push and PR, not just the light ones.**
  The build-heavy legs — Android IL2CPP, the emulator smoke, the iOS export and
  its macOS compile, the shrink experiment — were gated to manual dispatch and
  the weekly cron on a "split by cost" rationale that measurement disproved:
  the full matrix is 17 jobs in **13.4 minutes of wall clock**, and GitHub
  billed **0 ms** for all of it, macOS included, because runner minutes on a
  public repo are free. What the gate actually bought was a manual step between
  writing a change and learning whether it works — which is how a test that
  passes headlessly and fails in a real Editor reached main. Docs-only changes
  still trigger nothing (paths filter), the weekly cron stays for drift with no
  commit behind it, and `tools~/device-smoke/**` joins the paths list so
  editing the smoke script actually runs the smoke.

- **At most `UNITY_MAX_PARALLEL` Unity editors are activated at once** (repository
  variable, default 2). `max-parallel` is a matrix-only key and cannot span job
  definitions, so a cap alone would have left five concurrent activations — one
  per Unity job. The five are therefore chained with `needs:` (tests →
  android-build → ios-export → tests-unity6-latest → android-shrink-verify) so
  exactly one is eligible, and `max-parallel` caps that one's legs. The chain
  gates on `!cancelled()` rather than the implicit `success()`, so a red tests
  leg still lets the Android build run, and the no-secrets path still
  propagates as skipped. Cost, measured on the first chained run rather than
  estimated: 29 minutes for every leg but the shrink experiment, against 13.4
  unchained. Runner minutes stay free, so the price is wall clock only.

  This is a deliberate margin, not a fix for an observed failure, and the
  workflow header now says so: across 30 runs no `game-ci` step has ever failed
  — every red is downstream of a successful activation — and run 25 activated
  all eleven legs at once with every one logging a returned ULF licence. The
  header's previous claim that parallel Linux jobs share one seat *"explicitly
  … for free licences"* also cited the wrong page: GameCI's FAQ does not
  contain the word "seat". The statement lives on the Docker-images page, is
  written per build platform rather than per job, and puts no number on how
  many parallel containers one activation covers.

- **The shrink experiment builds the resource shrinker's task, not a whole APK.**
  `assembleRelease` has to package the app, so it pulls in the release IL2CPP
  native build — fully optimised, unlike the development APK the `android-build`
  job produces in four minutes. Runs 30 and 31 both hit the job's two-hour
  ceiling still running `il2cpp`/`bee_backend`, so the probe/control verdict has
  never actually been reached. `shrinkReleaseRes` *is* the shrinker: it reads the
  linked resources and R8's output and writes a stripped resource archive, and
  the native build is a sibling that only packaging joins. The assertion now
  reads that archive (an `.ap_` is a zip with `res/` and `resources.arsc`,
  which is all it ever looked at), located by shape rather than a hardcoded
  path. The Gradle step also gains a 30-minute ceiling, so a runaway build
  fails by name instead of reporting `cancelled` with no failing step.

  With the build failing in 34 seconds instead of 120 minutes, the next cause
  was legible: AGP's own error-rewriter had been NPE-ing while formatting the
  message, hiding an aapt2 link failure. The step stripped the export's
  `android.aapt2FromMavenOverride` as one more dead `/opt/unity` path, which
  left AGP resolving its own AGP-7-era aapt2 from Maven — and that binary
  cannot read the SDK 36 `android.jar` the export compiles against. It is now
  re-pointed at the runner's newest build-tools aapt2 rather than dropped.

### Fixed

- **The 0.4.8 CI additions were themselves red on their first real run, in two
  ways this repo's headless harness structurally cannot see.** The new
  throwing-subscriber test failed all four Unity test legs: it asserts that
  `Dispatch` *contains* the exception, containment means the exception is
  **logged**, and Unity's Test Runner fails a test on any log it was not told
  to expect — while the stub harness no-ops `Debug`, so it was green locally.
  The log is the contract, so the test now declares it with `LogAssert.Expect`
  (stubbed for the headless build); reverting the fix still turns the test red,
  so the expectation did not blunt it.

  And `android-shrink-verify` reached its Gradle build for the first time, where
  AGP rejected the toolchain outright: *"Minimum supported Gradle version is
  7.5. Current version is 7.2."* The JDK 11 / Gradle 7.2 pin — described in the
  0.4.8 notes above as "the combo Unity 2022.3 itself bundles" — was an
  assumption this export does not match: the job's own artifact shows it
  compiling against **SDK 36**, i.e. a far newer AGP. Rather than guess again,
  the step now asks the export which Gradle it needs (wrapper properties first,
  the AGP coordinate second, a modern default last), prints which it chose,
  and verifies the download against Gradle's published sha256 — a discovered
  version cannot carry a pinned one. JDK moves to 17, which modern AGP needs
  and which `sdkmanager` already wanted.

  That run also confirmed the rest of the 0.4.8 CI work on real infrastructure:
  the `ndk.dir` collision is gone, `minifyEnabled`/`shrinkResources`/
  `crunchPngs false` and the keep-all ProGuard file all land in the release
  buildType, and the new iOS `Info.plist` and dex assertions passed on all
  three lines, as did the emulator smoke. The shrinker's probe/control verdict
  is still the one open question.

## [0.4.8] - 2026-08-28

### Added

- **A `unity6-latest` canary in CI.** The EditMode suite now also runs on the
  newest Unity 6 editor that has a GameCI image, resolved at run time from
  the `unityci/editor` Docker Hub tags — no pinned version for anyone to
  forget to bump when Unity's quarterly updates land (6.5 today, 6.6/6.7
  next). The canary upgrade-opens Testbed6, which stays serialized at 6.3
  LTS, so the pinned legs keep proving the supported floor while this leg
  proves the moving edge; its Library cache is keyed by the resolved version
  because the upgrade rewrites it, and the resolver fails loudly rather than
  silently falling back to an older editor.

- **Every Android CI build now proves the package's bake reached the APK.**
  That the static shortcuts, the strings behind them and the trampoline
  `<activity>` really survive into a shipped APK was established once, by hand,
  with a single `aapt2` session on one machine — a fact about one build, not a
  property of the package. The `android-build` job asserts it on all three lines
  on every run: `aapt2 dump resources` must show `xml/quickactions_shortcuts`,
  `raw/quickactions_keep` and `string/qa_short_0`, and `aapt2 dump xmltree` must
  show `QuickActionsTrampolineActivity` and the `android.app.shortcuts`
  meta-data. A miss prints the dump it could not match rather than exiting on a
  bare code, so a surprise arrives as evidence. Green on all three lines in
  every heavy run since these steps landed — first the 2026-08-21 dispatch,
  most recently the 2026-08-24 cron. That the bake reaches a shipped APK is now
  a per-run fact; that a launcher renders it still needs human eyes.

- **`android-shrink-verify`: a CI job that finally puts the resource-shrinker
  keep rule on trial.** 0.4.7 ships `res/raw/quickactions_keep.xml` so the icon
  drawables — reachable only through `getIdentifier("ic_quickaction_" + name)` —
  survive `shrinkResources`, and eight headless tests pin what the file
  contains, but nothing has ever built a minified release APK to see whether AGP
  *honours* it (ROADMAP verification item (c)). The new job exports the 2022.3
  Gradle project (`TestbedBuilder.ExportAndroidGradle`, added to all three
  testbeds), plants two unreferenced 1x1 PNGs — one matching the shipped keep
  glob, one matching nothing — flips `minifyEnabled` + `shrinkResources` on the
  *exported* release build, and compares what comes out: the control must be
  shrunk, or the shrinker never ran and the run is declared inconclusive rather
  than green; the keep-globbed probe must return byte-identical. Flipping those
  flags inside the testbed instead would have shipped experiment-only build
  configuration to everyone reading the example. The job is written to fail
  loudly and print the file, tree or dump it could not parse rather than to
  skip, and its first run (2026-08-21) validated exactly that design: export,
  ownership hand-off, probe-planting and the minify flip all worked, and the
  build then died on the toolchain — the export ships no gradlew, and the
  runner's system Gradle (9.x) cannot load the AGP 7.1.2 the export pins. The
  job now supplies the combo Unity 2022.3 itself bundles (JDK 11, a
  sha256-pinned Gradle 7.2, NDK r23b) and re-points the docker-image paths
  hardcoded in the export (`android.aapt2FromMavenOverride`, `ndkPath`). The
  next two runs each got exactly one step further: first the pin step died
  on `sdkmanager` not being on PATH for plain run steps (now called by full
  path), then on `sdkmanager` being compiled for Java 17 while the job's
  JAVA_HOME is deliberately 11 for Gradle 7.2/AGP 7.1.2 (that one install
  now runs on the runner's own 17). The probe/control verdict is still
  pending its first complete run.
  Note also what a green run would and would not say: it would show the icons
  surviving a minified release build, but not that the keep rule is what saved
  them, since AGP's default safe mode also carries a string-prefix heuristic
  that can retain `ic_quickaction_*` on its own.

- **The adb smoke asserts the COLD-launch tap, not just the warm one.** Its tap
  went to the trampoline while the app was already running, so it only ever
  proved the resume path — a launcher tap on a quit app, which is how a shortcut
  is usually used, starts the process and the id has to survive that start. The
  script now repeats the tap after an `am force-stop`, first proving the
  process is really gone (an empty `pidof` — force-stop's exit status says
  nothing, and a still-alive app would silently turn the step into a second
  warm tap), and asserts the `Performed quick action` line for a *different*
  registered id than the warm tap used, because `logcat -c` can under-clear on
  emulators and a leftover warm line must never satisfy the cold assertion.
  Its own longer timeout (`COLD_LOG_ATTEMPTS`) covers the whole Unity boot a
  cold start has in front of it. The step's first emulator run (2026-08-21)
  split three ways: the 2022.3 leg passed all eight steps — the first
  observation anywhere of a cold tap arriving as `Performed`, and live proof
  of the different-id defence, since the log showed `logcat -c` really had
  under-cleared — while the restarted 2021.3 player sat engine-silent past
  the whole budget (the emulator was still tearing down the dead process's
  Vulkan objects when the new one launched, so a GPU-settle delay now
  precedes the cold tap) and Unity 6 never reached the shortcut publish at
  all (below). The settle delay proved out one dispatch later: 2021.3 passed
  all eight steps, so both classic-activity lines now clear the full smoke,
  cold tap included. No real device has run the step yet.

### Changed

- **`verify.sh` gains two guards that check what was previously remembered.**
  Check 1 proved only that a `.meta` *exists*, so an orphaned one (asset gone,
  meta shipped) or a wrong importer — an iOS plugin carrying the Android
  platform flag, which builds a broken player instead of failing — passed
  clean; `gen_meta.py --check` now compares every meta against what its path
  routes to, exempting the `guid:` line because some assets legitimately keep a
  GUID Unity assigned before they moved. Check 6 additionally fails when an
  install pin disagrees with `package.json`: `release.yml` tags the merge
  commit, so a `#v<version>` moved one commit later is wrong for everyone who
  reads main in between — precisely what the 0.4.6 cycle shipped.

- **`game-ci/unity-builder` v4.8.1 → v5.0.0.** The major version extracts the
  CloudRunner inputs into a separate orchestrator action and moves the runtime
  from node20 to node24; every input this workflow passes (`projectPath`,
  `targetPlatform`, `buildMethod`, `versioning`, `allowDirtyBuild`) and the
  licence env contract (`UNITY_LICENSE`/`UNITY_EMAIL`/`UNITY_PASSWORD`) are
  unchanged, verified against the v5.0.0 `action.yml` and compiled dist. The
  companion pins were checked the same way and stay: `unity-test-runner` v4.3.1
  and `android-emulator-runner` v2.38.0 are the latest stable releases.

### Fixed

- **A throwing `Performed` subscriber could stop every later delivery.**
  `Dispatch` raised the event with a plain `Performed?.Invoke`, so an exception
  from any one handler — a null dereference in game code, a `MonoBehaviour`
  destroyed by a scene load that never unsubscribed from this process-wide
  static event — skipped the remaining subscribers and propagated back into the
  caller. With the polling beat below that is not a logged nuisance but a
  permanent stop: Unity ends a coroutine whose `MoveNext` throws and never
  resumes it, and on Unity 6's GameActivity that coroutine is the *only*
  delivery path, so one bad handler would silently disable every subsequent
  quick action for the rest of the session. `Dispatch` now walks the invocation
  list and contains each handler separately, and the coroutine guards its own
  drain as belt-and-braces. Two headless tests pin both halves; each was
  mutation-checked (reverting either fix turns its test red).

- **Unity 6 (GameActivity): a warm shortcut tap could sit undelivered until
  the next real focus change.** The runtime drained its pending-id queue only
  from `OnApplicationFocus(true)` and `OnApplicationPause(false)` — and CI's
  third heavy run showed Unity 6's `UnityPlayerGameActivity` completing a
  trampoline round-trip with **neither** callback reaching scripting: the
  trampoline recorded the id (no gate warning), the player logged
  `onNewIntent`/`onResume` and handled the native `APP_CMD_PAUSE`/`RESUME`
  pair a millisecond apart, and no dispatch followed for 30 s. The classic
  `UnityPlayerActivity` (2021.3/2022.3) fires the callbacks and was never
  affected. The runtime's hidden singleton now also polls the queue on a slow
  0.25 s unscaled-time beat — delivery no longer depends on which lifecycle
  events an activity implementation emits, and an empty-queue tick is a single
  query into the native queue (an `isEmpty` on Android, a count check on iOS),
  with the Android class handle now cached for the process so the tick costs no
  JNI `FindClass` or global-ref churn. Found, diagnosed to the exact missing
  callback, and
  proven by the same harness two days later: the first cron run carrying the
  fix took the Unity 6 leg through all eight smoke steps, so every supported
  line now passes the full smoke, warm and cold taps included.

- **The emulator smoke's failure output could not say WHY an app went silent.**
  The workflow's first live run (2026-08-21) proved the point: the unity6 leg
  timed out after 45s with the three static shortcuts healthy in `dumpsys` and
  `Calls: 0` — a picture identical for "Unity 6 boots slower than 45s under ARM
  translation on a software GPU" and "the player crashed on launch", and
  nothing else was captured before the emulator was torn down. The script's
  app-behaviour failure paths now print whether the process is alive, the
  logcat crash buffer, and the engine/package log tail; the CI legs raise
  `SHORTCUT_ATTEMPTS` to 240 and `COLD_LOG_ATTEMPTS` to 180, which stretches
  only the failure case since the poll returns the moment its condition holds.
  The diagnostics' own first exercise (the next dispatch, same day) settled
  the unity6 question in one log: process DEAD, crash buffer showing a
  SIGSEGV null dereference in `libunity.so` `profiling::Profiler::Initialize`
  — the Unity 6 *development* player dies during engine init under the API 30
  image's ARM-to-x86_64 translation, while 2021.3/2022.3 boot fine there. The
  unity6 smoke leg therefore now runs the API 35 system image, whose far
  newer translator is the one variable that crash implicates — and on it the
  player boots and publishes its shortcuts, which is what let the same
  diagnostics catch the GameActivity delivery bug fixed above.

## [0.4.7] - 2026-08-21

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
