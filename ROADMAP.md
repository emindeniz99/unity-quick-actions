# Quick Actions for Unity — Roadmap

Follow-ups discussed but not shipped. Delete an entry in the same commit that
ships it.

- **`.androidlib` does not survive `.unitypackage` export/import** (reported
  against 2022.3.15, re-confirmed 2024, unfixed). This is why the built-in icons
  must be written by the build post-processor rather than shipped as a
  `.androidlib` inside the package: the Asset Store channel delivers a
  `.unitypackage`, so anything relying on a shipped `.androidlib` would work on
  OpenUPM/Git and silently vanish for Asset Store users. Re-check whether Unity
  has fixed this before choosing any design that depends on it.

- **Emulator screenshot of the built-in icons — photographed once, on API 30;
  the API 35 drawer is still the gap.** The smoke legs end with a best-effort
  long-press capture (`CAPTURE_LONGPRESS=1` in
  `tools~/device-smoke/android_device_smoke.sh`) and upload `longpress-<leg>`
  (screenshot plus two `uiautomator` dumps) on every push. Three attempts on
  2026-09-02 got the gesture right a piece at a time: a real press (`input
  motionevent` DOWN, hold, capture, UP) instead of a swipe that never moves;
  escalation through `KEYCODE_ALL_APPS`, the launcher's "Apps list" handle and
  the `ALL_APPS` intent when the swipe surfaces nothing; and counting the
  hotseat's *prediction* of the app (`Predicted app: …`, whose long press
  opens the launcher's "App suggestions" sheet) as a miss. On the run after
  that (PR #18 run 61) the API 30 swipe opened the drawer — the same swipe had
  left the launcher on its home screen on every earlier run — the press
  landed on the real icon, and `longpress-2022.3` holds the first photograph
  of the app's popup: the three static entries and the runtime-added `daily`,
  drawn with their long labels, with the built-in `add` and `favorite` art on
  two of the rows (white glyph on the blue background, masked round by the
  launcher — the `-v26` adaptive variant, which is what API 30 resolves). The
  script's own `shortcut sheet visible` line said `no` on that run because it
  matched the titles while the launcher drew the subtitles; it now accepts
  either. Still open: the API 35 Pixel launcher has never left its home screen
  for any opener (not yet tried: a swipe that starts above the
  gesture-navigation band, `KEYCODE_APP_SWITCH`, the launcher's all-apps
  activity by component name), and on API 30 the swipe has opened the drawer
  once in four runs, so the capture is a photograph when it lands, never a
  check. The verdict does not depend on it.
- **Port the Android post-processor to `AndroidProjectFilesModifier` (Unity 6).**
  Unity's own notifications package moved to it "for better compatibility with
  incremental build"; the built-ins' distinct-name design was chosen so that
  port is mechanical (every output declared up front, nothing inspected in the
  tree), but the port itself is not done. CI's unity6 leg now builds the
  Android player twice over one project directory and runs the same aapt2
  assertions on the second APK (first measured 2026-09-02: 54,951,386 vs
  54,951,670 bytes, every assertion green on both), so the current
  `IPostGenerateGradleAndroidProject` path is measured rather than assumed on
  every push — this is an API-alignment item, not a bug.

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
  smoke**. No *real device* has run the cold step yet. Remaining: asserting
  a shortcut **tap** on iOS (no
  `simctl` API reads `UIApplicationShortcutItems` or triggers a tap — it needs
  an XCUITest target driving SpringBoard; see the device-smoke README).
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
- **Settings-asset orphan when the define is off:** `QuickActionsSettings` (the
  static-shortcuts ScriptableObject) lives in the gated Editor assembly, so a
  project that has a `QuickActionsSettings.asset` and is opened with
  `QUICKACTIONS_ENABLED` off shows it as "missing script". Harmless and reversible
  (re-enable the define), documented in README. A future cleanup could move the SO
  *type* into an always-compiled editor assembly so the asset never orphans.
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
