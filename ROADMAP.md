# Quick Actions for Unity — Roadmap

Follow-ups discussed but not shipped. Delete an entry in the same commit that
ships it.

- **`.androidlib` does not survive `.unitypackage` export/import** — reported
  by other users, not by this project: a Unity Discussions thread opened
  against 2022.3.15
  (<https://discussions.unity.com/t/export-and-import-androidlib-file/935469>)
  and a Unity Issue Tracker entry on bundled plugins not exporting into a
  package
  (<https://issuetracker.unity3d.com/issues/console-error-error-while-exporting-package-no-assets-to-export-only-folders-did-you-mean-to-use-exportpackageoptions-dot-recurse-when-trying-to-export-a-bundle-file-as-a-package>);
  no fix version is recorded here. This is why the built-in icons
  must be written by the build post-processor rather than shipped as a
  `.androidlib` inside the package: the Asset Store channel delivers a
  `.unitypackage`, so anything relying on a shipped `.androidlib` would work on
  OpenUPM/Git and silently vanish for Asset Store users. Re-check whether Unity
  has fixed this before choosing any design that depends on it.

- **Port the Android post-processor to `AndroidProjectFilesModifier` (Unity 6)
  — decided against, 2026-09-10.** Unity's own notifications package moved to
  the new API "for better compatibility with incremental build", and the
  built-ins' distinct-name design was chosen so that a port would be mechanical.
  It is still not worth doing, for four measured reasons:
  (a) `IPostGenerateGradleAndroidProject` is **not deprecated** — Unity 6.2's
  scripting reference lists it as supported, with no obsolete marker on the
  interface or on `OnPostGenerateGradleAndroidProject`;
  (b) `AndroidProjectFilesModifier` exists only on 6000.0+, and Unity's own
  manual says it **cannot modify files in the default `unityLibrary` and
  `launcher` modules** — which is where every one of this package's outputs goes
  (the trampoline `<activity>`, `res/xml`, `res/values`, `res/raw`, the icons);
  (c) 2021.3 and 2022.3 would stay on the old interface regardless, so the port
  buys two implementations behind `#if UNITY_6000_0_OR_NEWER` and twice the test
  surface for one shared set of outputs;
  (d) the incremental-build worry it would answer is already measured: CI's
  unity6 leg builds the Android player twice over one project directory and runs
  the same aapt2 assertions on the second APK (2026-09-02: 54,951,386 vs
  54,951,670 bytes, every assertion green on both).
  Re-open when Unity marks the interface obsolete, or when the new API can write
  into `unityLibrary`.

- **Automated device CI, remaining scope** — 0.4.6 closed the licence half:
  `.github/workflows/unity-ci.yml` (GameCI) builds the dev APK in CI and feeds
  it straight into the adb smoke, and exports + compiles the iOS Simulator
  project on a macOS runner, cold-launching the app there — both live since the
  licence secrets landed (2026-08-21). The Android **COLD-launch** path is no
  longer on this list: `android_device_smoke.sh` force-stops the app and taps
  the trampoline again as its last step, and that step's first emulator run
  went green on the 2022.3 leg — the first observation anywhere of a cold tap
  arriving as `Performed` — then on 2021.3, and finally on Unity 6 (API 35
  image; its earlier warm-tap red exposed the GameActivity delivery gap
  fixed in the runtime, see CHANGELOG): **all three lines now pass the full
  smoke**. No *real device* has run the cold step yet. The iOS tap now has
  its harness too: `tools~/ios-ui` is an XCUITest bundle that drives
  SpringBoard on the simulator CI boots (long-press the icon, tap `Daily
  Reward`, read the id back from the testbed's marker file), run by the
  `ios-springboard` job. **Run 76 (2026-09-15): `SKIPPED` on both legs** —
  icon found on home-screen page 2, not reached (zero frame read as
  on-screen); fixed in the same PR. **Run 77: delivered on both legs** —
  SpringBoard's own context-menu tap cold-started the 2022.3.62f3 / iOS 18.6
  and the 6000.3.21f1 / iOS 26.5 exports and `daily_reward` reached
  `Performed` 35–43 s and 41–54 s after the touch, past the test's 30 s
  window, so both went red on a verdict that quoted the id; the window is
  120 s now, and run 78 was green on both legs (`PASS`, 41 s and 46 s after
  the tap). Every one of those taps landed on a **static** shortcut, because
  an app that was installed and never launched has nothing else in its menu;
  the leg now runs the long press a **second** time per leg on a row
  `QuickActions.Add` published while the app ran — the testbed seeds one when
  CI asks — so the runtime path gets the same real-menu treatment the Moto G
  gave it on Android. **Run 94 (2026-09-16): `PASS` on both supported legs** —
  `runtime_add` delivered 5 s after the tap on 2022.3 / iOS 26.2 and on
  6000.3.21f1 / iOS 26.5, and the menu SpringBoard opened held all four rows at
  once, three from `Info.plist` and one from `QuickActions.Add`. The
  `unity6-xcode27` canary tapped the same row on iOS 27.0 and the id never
  arrived; the app reached the foreground 0 s after the tap there (5 s on the
  green legs), so it did not cold-start and what failed is not established —
  iOS 27 is a preview toolchain no Unity line supports yet. Still the
  Simulator, still no iPhone.
- **Parity with Flutter's `quick_actions` — compared 2026-09-15, two real
  gaps.** Four of the five top open feature requests on Flutter's tracker
  (bitmap icons, SF Symbols, a max-count accessor, managing existing
  shortcuts) are things this package already ships, and their plugin replaces
  the whole dynamic-shortcut set where we merge behind an ownership marker.
  Both gaps are now **closed**: `QuickActions.SetList` is the one-call "make the
  set exactly this" mutator, and the trampoline fires Android
  `reportShortcutUsed` on a **dynamic or pinned** tap. One residual, found while
  closing that second one: `reportShortcutUsed`'s ownership gate scans
  `getDynamicShortcuts()` and `getPinnedShortcuts()` only, so a tap on a
  **static** (manifest-baked) shortcut records `Performed` as before and reports
  nothing. An earlier version of this entry claimed the trampoline fix "would
  cover static shortcuts too"; it does not. Closing that needs the gate to
  recognise manifest ids the way the trampoline already does — through the baked
  intent action, since static shortcuts cannot carry the extras marker. Smaller: `AddList`/`RemoveAll`
  return `void`, a build warning when R8 is on without the keep rule, an earlier
  `Info.plist`-driven scene-hook install, coex-probe assertions over the iOS
  marshalling layer, and the full 29-row `IconType` table in the README.
  Sources and the five refuted claims are in
  [`docs~/flutter-quick-actions-parity-2026-09.md`](https://github.com/emindeniz99/unity-quick-actions/blob/main/docs~/flutter-quick-actions-parity-2026-09.md).
- **CI wall clock (40–53 min per run) — the cache is not the lever.** The
  Library cache hits its primary key on every Unity leg; what costs the
  minutes is the self-imposed `needs:` chain plus `max-parallel: 2`, the
  per-job GameCI image pull and activation, and the iOS simulator's first
  boot behind GitHub's five-concurrent-macOS cap. **Un-chaining landed
  2026-09-17 and did not move the wall clock**: runs 103 and 104 took 36m01s
  and 46m37s, inside the chained 29–38 range. It fixed the Linux half, which
  now finishes at 16 minutes with ten jobs running three minutes in, but the
  macOS half owns the critical path — and that measurement found the real
  lever: six xcode-27 macOS jobs (four canaries plus both `ios-crossrun` legs,
  which are xcode-27-bound too) held slots on every PR while gating nothing,
  one of them for 23 minutes. **They moved to the cron 2026-09-18.** Still to
  do: ARM64 simulator export (the testbeds still export x86_64 by omission),
  and fold `ios-springboard` into `ios-simulator` with a pre-booted
  simulator — that job is what the critical path reduces to next. Sources, numbers and the things ruled out (image cache,
  DerivedData, AVD snapshots, larger runners) are in
  [`docs~/ci-cost-and-caching-research-2026-09.md`](https://github.com/emindeniz99/unity-quick-actions/blob/main/docs~/ci-cost-and-caching-research-2026-09.md).
- **Xcode 16.4 legs (`macos-15`) — keep, retarget or drop: decision deferred
  to October 2026.** Since 2026-04-28 App Store Connect accepts only builds
  made with Xcode 26+ and the iOS 26 SDK, Xcode 27 went GA on 2026-09-14, and
  the iOS 27 SDK becomes mandatory in April 2027 — with the scene lifecycle
  mandatory for apps built with it. CI keeps the existing legs unchanged and
  adds `xcode-27` canaries that never gate. The measurements, the sources and
  the options are in
  <https://github.com/emindeniz99/unity-quick-actions/blob/main/docs~/ios-toolchain-and-ui-test-research-2026-09.md>;
  re-read the canaries' results and that record before deciding.
- **Documentation site (considered, deliberately deferred — revisit trigger now
  met)** — the reference docs live in a single [README](./README.md) that has
  roughly tripled since this entry was written (590 lines then, well past 900
  now), which is precisely this entry's own "if the README keeps growing"
  trigger — a rounded phrasing on purpose, since the exact count went stale
  twice and a doc that lies about itself is worse than one that rounds —
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
  it is off — the build-output half is now proven on every code push by the
  `gate-off` CI job (define-off APK: no trampoline `<activity>`, no shortcuts
  meta-data, no package resources, nothing of the package's assembly in the
  IL2CPP metadata; define-off Xcode export: no macro, no marked plist items — each
  with the define-on build as the positive control), and was hand-confirmed
  earlier on 2021.3 and 6.3. What remains is the device: install that prod
  build and confirm no shortcut menu appears and the app is untouched (the
  `.java` dead class remains; literally-zero needs the package excluded from
  the prod project).
  Both ungated cleanups gate on compile-time `#if QUICKACTIONS_ENABLED` (the
  same truth as the gated injectors), with a stale-assembly coherence check
  that FAILS the build if the define was removed without a script recompile.
  On Unity 6, confirm a dev Build Profile carrying the define builds
  coherently (the check reads Player Settings plus the active profile).
- **Settings-asset orphan when the define is off — decided against, 2026-09-10.**
  `QuickActionsSettings` (the static-shortcuts ScriptableObject) lives in the
  gated Editor assembly, so a project that has a `QuickActionsSettings.asset`
  and is opened with `QUICKACTIONS_ENABLED` off shows it as "missing script".
  Harmless and reversible (re-enable the define), documented in README next to
  the define-off instructions, and **absent from the setup that README actually
  recommends** — keep the define on in the Editor and gate only the production
  Build Profile, and the asset is never orphaned.
  The fix would be to move the SO type into an always-compiled editor assembly.
  Cost is not size: an ungated *Editor* assembly reaches no player at all, and
  the one that already exists proves it — `EminDeniz99.QuickActions.Editor.Bootstrap`
  and `QuickActionsSettings` each appear **0 times** in the shipped APK's IL2CPP
  metadata, on both the development and the release build of PR #19 run 64.
  The cost is permanent maintenance: `QuickActionItem` must stay gated (it is a
  *runtime* type — ungating it would put it in every player), so the ungated SO
  needs a parallel DTO mirroring its 12 serialized fields, held by a parity test
  or it drifts silently and loses user data on the next bake; plus a migration
  test over a fixture asset (the `.meta` GUID must survive the move, or every
  existing user's asset breaks permanently) and a rework of the `IconType`
  property drawer, whose enum lives on the gated side. That is a standing tax on
  every future field, paid for a cosmetic wrinkle in a non-recommended setup.
  Re-open if a user hits it on the recommended setup, or if `QuickActionItem`
  stops being gated for another reason.
- **iOS scene lifecycle + cold dedup (SHIPPED in v0.4.0 — Simulator-measured
  since 2026-09-02, device still open):** the package learns the scene-delegate
  class from the connecting session's `UISceneConfiguration` and installs cold
  (`scene:willConnectToSession:options:`) + warm
  (`windowScene:performActionForShortcutItem:completionHandler:`) hooks, with a
  consume-once cold-dedup marker that also swallows the host-subclass
  double-delivery on the app-delegate path. CI's `ios-simulator-coex` leg now
  measures, on Testbed6 (6000.3.21f1) under a mock host: the app STARTS with the
  package's `configurationForConnectingSceneSession` in place (the connected
  scene's delegate is a `UnityScene`, `session.configuration.delegateClass` is
  `UnityScene`); the cold launch item is queued exactly once and a warm tap
  through the scene selector exactly once; a host subclass that discards our
  `NO` does not double-deliver; and the SUBCLASS-SHADOWED shape (the host
  overrides the configuration selector without calling super) is recovered by
  the `UISceneWillConnectNotification` fallback, warm taps included. All of it
  through synthetic sends. Still open: a cold tap arriving through
  `connectionOptions` (only UIKit fills that — a real SpringBoard tap, which
  needs an XCUITest target); whether the FIRST cold tap survives the shadowed
  shape (the notification may be posted after the delegate's own `willConnect`,
  in which case that one tap is lost by design — a `[super ...]` call on the
  host side closes it); multi-scene-delegate-class hosts (coverage only for the
  first class learned, documented in-code); and any physical device. Also
  measured, as a shape to avoid: an app delegate that implements
  `application:configurationForConnectingSceneSession:options:` opts the app
  into the scene lifecycle even WITHOUT `UIApplicationSceneManifest` (UIKit
  called the mock host's override on the manifest-less 2022.3 export) — the
  package gates every scene hook on the manifest, so such a host gets none;
  declare the manifest or do not implement the selector.
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
