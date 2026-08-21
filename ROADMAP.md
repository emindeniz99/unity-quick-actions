# Quick Actions for Unity — Roadmap

Follow-ups discussed but not shipped as of v0.4.0 (the first public release).
Delete an entry in the same commit that ships it.

- **Android built-in icons: ship them from the build post-processor (0.5.0).**
  The `IconType` catalog has 29 entries and *none* of them render on Android
  unless the consumer adds a drawable themselves — the launcher draws a blank
  square, which is what the Android screenshot in the README shows. 0.4.5
  documented the manual `.androidlib` procedure, which is the honest fix but
  still one manual step for every user. The real fix is to write the PNGs into
  the generated Gradle project at build time, so nobody does anything.

  *Precedent worth copying:* Unity's own `com.unity.mobile.notifications`
  solves exactly this problem the same way. Its Java calls
  `res.getIdentifier(name, "drawable", context.getPackageName())` — identical
  to `QuickActionsBridge.java:505` — and its editor code writes the icon bytes
  into the generated project. On 2021.3/2022.3 it uses
  `IPostGenerateGradleAndroidProject` writing to
  `<gradleProject>/<lib>.androidlib/src/main/res/…`; on Unity 6 it switched to
  `AndroidProjectFilesModifier` writing to `unityLibrary/src/main/res/…`. That
  version split is the main implementation cost — we support both lines.

  *Where our code already is:* `Editor/Android/QuickActionsBuildPostProcessorAndroid.cs`
  already runs at exactly this point and already writes `res/xml/` and
  `values-<qualifier>/` into the generated project. Adding `res/drawable-xhdpi/`
  is the same code path, not a new mechanism.

  *Plan:* move the four PNGs from `store~/example-shortcut-icons/` into
  `Editor/Android/BuiltInIcons/` (normal imported assets with committed `.meta`,
  so they survive `.unitypackage` export — `package.json`'s `files` already
  includes `Editor`, so no allowlist change). Emit only the icons actually
  referenced by the build's shortcut set, so an app using none pays nothing.
  Cost if we emitted all four unconditionally: ~1.5 KB of APK.

  *Blocking sub-bug — must ship in the same release.* With `minifyEnabled` +
  `shrinkResources` (Gradle's default safe mode), the drawables are marked
  unreachable and their bytes replaced with a 67-byte dummy **while their
  resource-table entries survive**. So `getIdentifier` returns non-zero,
  `setIcon` is called, and the launcher still draws an empty square — a
  release-only failure that looks exactly like the un-configured state. Cause is
  our own naming: `QuickActionsBridge.java:505` builds the name by
  concatenation (`"ic_quickaction_" + ICON_NAMES[i]`), so the string pool holds
  only the bare prefix and the shrinker matches nothing. Fix is to emit
  `res/raw/quickactions_keep.xml` carrying `tools:keep`. **This bug exists
  today**, independently of shipping icons: any consumer who adds their own
  drawable and builds a minified release hits it.

  *Note:* even after this, 25 of the 29 catalog entries stay blank on Android —
  we only own art for add/compose/favorite/play. Either commission the rest,
  or document the catalog as "iOS-complete, Android-partial".

- **Verify the Android drawable mechanism on a real build (blocks the above).**
  Everything in the entry above rests on Unity documentation and on reading
  Unity's first-party package — **not** on a build we ran. The APK proof was
  attempted and could not run: the Unity editors live on `/Volumes/T7Data`,
  which was unmounted. Before writing 0.5.0, mount it and confirm on 2022.3:
  (a) a `.androidlib` under `Assets/` lands its `src/main/res/drawable-*/`
  entries in the APK resource table (`aapt2 dump resources <apk> | grep -i
  quickaction`); (b) `res/` at the `.androidlib` root is silently dropped, as
  the docs imply; (c) the `shrinkResources` failure reproduces, and `tools:keep`
  fixes it; (d) a `.androidlib` shipped *inside a UPM package* is picked up —
  Unity's notifications package does this, but it is undocumented behaviour.

- **`.androidlib` does not survive `.unitypackage` export/import** (reported
  against 2022.3.15, re-confirmed 2024, unfixed). This is why the built-in icons
  must be written by the build post-processor rather than shipped as a
  `.androidlib` inside the package: the Asset Store channel delivers a
  `.unitypackage`, so anything relying on a shipped `.androidlib` would work on
  OpenUPM/Git and silently vanish for Asset Store users. Re-check whether Unity
  has fixed this before choosing any design that depends on it.

- **Teaching sample for custom Android icons (small, optional).** A
  `Samples~/AndroidIcons/` containing a ready-made `QuickActionIcons.androidlib`
  (correct `src/main/AndroidManifest.xml` + `src/main/res/drawable-xhdpi/`)
  would let a user import a *working* example rather than follow a five-step
  written recipe. Imports to `Assets/Samples/<pkg>/<version>/…`, which is under
  `Assets/`, so it would be picked up with no further action. Caveats: nobody
  ships this combination today (untested), `Samples~` content ships without
  `.meta` so the Android-only `PluginImporter` setting falls back to defaults,
  and it inherits the `.unitypackage` limitation above. Do this *after* the
  post-processor work, as documentation-by-example — not as the delivery path.

- **Automated device CI, remaining scope** — 0.4.6 closed the licence half:
  `.github/workflows/unity-ci.yml` (GameCI) builds the dev APK in CI and feeds
  it straight into the adb smoke, and exports + compiles the iOS Simulator
  project on a macOS runner, cold-launching the app there. Both still need the
  `UNITY_LICENSE`/`UNITY_EMAIL`/`UNITY_PASSWORD` secrets to be configured
  before a single Unity job has actually run. Remaining after that: asserting a
  shortcut **tap** on iOS (no `simctl` API reads `UIApplicationShortcutItems`
  or triggers a tap — it needs an XCUITest target driving SpringBoard; see the
  device-smoke README), and asserting the Android **COLD-launch** path (the
  smoke's tap is a warm resume).
- **Documentation site (considered, deliberately deferred — revisit trigger now
  met)** — the reference docs live in a single **832-line** [README](./README.md)
  (590 when this entry was written; 0.4.6's placeholder section added ~130 of
  them, and the deferral below says to revisit "if the README keeps growing")
  plus a task-oriented
  [GETTING_STARTED](./GETTING_STARTED.md); a GitHub Pages site (or a UPM
  `Documentation~/` folder, which the Package Manager surfaces as the package's
  "Documentation" link) would make that navigable, searchable and versioned per
  release. Deferred for v0.4.0: a single README is what the Package Manager and
  the Asset Store listing both render inline, and one file is cheaper to keep
  truthful than a site that can silently drift from the code. Revisit if the
  README keeps growing — the natural first split is the per-platform behavior
  tables and the icon/localization reference.

## Validate in a real Unity Editor (license-gated; not covered by the stub harness)

The stub harness compiles the C#/Java but can't confirm Unity-only wiring:

- ~~Confirm the gated post-processor asmdefs (`Editor/iOS`, `Editor/Android`,
  `defineConstraints` `UNITY_IOS` / `UNITY_ANDROID`, with the extension DLLs in
  `precompiledReferences`) compile when that target is active and are skipped
  cleanly otherwise.~~ **Done** — the 2021.3 / 2022.3 / 6.x Editor passes
  imported with 0 errors and produced real Android APKs and Xcode projects,
  which is exactly this seam resolving; see `PRODUCTION_READINESS.md`.
- On-device: verify the Android trampoline reliably foregrounds the Unity task
  and fires `OnApplicationFocus(true)` (warm resume), that iOS warm taps land via
  the focus poll (performAction precedes didBecomeActive), and that static
  shortcuts.xml taps round-trip the action-encoded id. Confirm iOS cold + warm
  on a device via Xcode.
- **v0.2.0 feature validation (on-device):** SF Symbol + template-image icons
  render on iOS (incl. the iOS 12 dynamic fall-through), `AndroidBitmapFile`
  icons render and survive a reconcile (file kept alive), the adaptive variant
  masks correctly per launcher, `RequestPin` shows the launcher confirm sheet
  and the pinned copy taps through, and `Payload` survives a cold-start
  reconcile on both OSes. Also v0.3.0: `Update(item)` refreshes a pinned copy's
  label/icon in place on a real launcher; `ReportUsed` influences ranking (long
  feedback loop — just confirm no crash/no-op); and the template-image pipeline:
  confirm a copied PNG lands in the built app bundle root (group-style PBX adds
  flatten) and `iconWithTemplateImageName:`/`IconFile` resolve it by file name
  on device. The pipeline (`SyncTemplateImages`) is COMPILE-CHECKED ONLY in the
  harness — its PBX stubs are no-ops — so its behavior (manifest cleanup
  ordering, package-path resolution, Append rebuilds) is real-Editor/Xcode
  validation, not covered by verify.sh. NOTE the iOS side of v0.2.0 (the .mm userInfo
  persistence of symbol/template/payload and its read-back) has NO automated
  coverage — the .verify harness is Java+C# only — so the iOS reconcile
  round-trip is device-validation-only; the equivalent Android extras path is
  smoke-tested. Also probe on device whether iOS 13+ prefers
  `UIApplicationShortcutItemIconSymbolName` when `IconFile`/`IconType` coexist
  in one plist entry — if it does, the static writer could emit fallback keys
  and restore iOS 12 static-icon parity (see the post-processor comment).
- **Host-coexistence release gate (on-device, Android):** publish a host
  shortcut with a bitmap icon outside the package, then exercise
  `Add`/`RemoveAll`/cold-restart reconcile and confirm the host item survives
  untouched (icon intact, deep link works). Reboot between publish and
  read-back to confirm the extras ownership marker persists on OEM
  `ShortcutManager` forks — if a fork dropped extras, our items would be
  orphaned (host items are still safe; the failure direction is host-safe).
- Trampoline spoof-hardening SHIPPED (the trampoline now validates the tapped
  id against the OS's registered shortcuts before recording it). Residual: an
  id belonging to a genuinely REGISTERED shortcut (e.g. a static/manifest id)
  can still be spoofed by another app — low impact, only triggers an in-app
  route for a shortcut the app really has; verify the validation path on
  device.
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
  it is off — the build-output half is CONFIRMED on 2021.3 and 6.3 (a player
  built with the define removed contains no trace of the trampoline); on device,
  verify the prod manifest has no `QuickActionsTrampolineActivity` (the `.java` dead
  class remains; literally-zero needs the package excluded from the prod project).
  Both ungated cleanups gate on compile-time `#if QUICKACTIONS_ENABLED` (the
  same truth as the gated injectors), with a stale-assembly coherence check
  that FAILS the build if the define was removed without a script recompile.
  On Unity 6, confirm a dev Build Profile carrying the define builds
  coherently (the check reads Player Settings plus the active profile).
- **Settings-asset orphan when the define is off:** `QuickActionsSettings` (the
  static-shortcuts ScriptableObject) lives in the gated Editor assembly, so a
  project that has a `QuickActionsSettings.asset` and is opened with
  `QUICKACTIONS_ENABLED` off shows it as "missing script". Harmless and reversible
  (re-enable the define), documented in README. A future cleanup could move the SO
  *type* into an always-compiled editor assembly so the asset never orphans.
- **iOS scene lifecycle + cold dedup (SHIPPED in v0.4.0 — device-validate):** the
  package now learns the scene-delegate class from the host's
  `UISceneConfiguration` and installs cold
  (`scene:willConnectToSession:options:`) + warm
  (`windowScene:performActionForShortcutItem:completionHandler:`) hooks, with a
  consume-once cold-dedup marker that also swallows the host-subclass
  double-delivery on the app-delegate path. NO ObjC compile harness exists; the
  first real validation was an Xcode build, and it has now happened — the
  generated project compiles against the real iOS device SDK on 2021.3 and 6.3
  with zero warnings from `QuickActions.mm`, and a 6.3 / iOS 26.5 Simulator run
  confirms a cold tap reaching `Performed`. On device, verify: a scene-manifest
  app delivers cold + warm taps exactly once each; a default (no-manifest) app
  behaves byte-identically to v0.3.0; a host UnityAppController subclass that
  discards our `NO` no longer double-delivers; multi-scene-delegate-class hosts
  get coverage only for the first class learned (documented in-code). Also
  verify the SUBCLASS-SHADOWED shape specifically: a host that overrides
  `application:configurationForConnectingSceneSession:options:` without calling
  super shadows our hook, and the `UISceneWillConnectNotification` fallback
  installs from the live scene's delegate instead. Confirm warm taps arrive in
  that shape, and measure whether the FIRST cold tap does — the notification may
  be posted after the delegate's own `willConnect`, in which case that one tap
  is lost by design (a `[super ...]` call on the host side closes it).
- **Localization (SHIPPED in v0.4.0 — device-validate):** dynamic per-locale
  titles resolve/refresh across cold starts (verify a device-language change
  re-renders on next launch, and the refresh push tolerates rate limiting);
  static output needs a real toolchain check — that aapt2 accepts the generated
  `values-<qualifier>/` directories (incl. `values-b+zh+Hans`) and resolves the
  labels on a device set to that locale. Static localization is ANDROID-ONLY on
  purpose: the iOS equivalent needs a `<locale>.lproj/InfoPlist.strings` in the
  bundle root, whose path the platform fixes, so it would collide with any host
  that localizes its own Info.plist. Adding it back requires MERGING into the
  host's existing lproj / variant group (marker-delimited, so cleanup stays
  scoped) and must be validated on a real Xcode build — the PBX stubs are
  no-ops, which is exactly how the collision went unnoticed the first time.
