# Quick Actions for Unity — Roadmap

Follow-ups discussed but not shipped as of v0.4.0 (the first public release).
Delete an entry in the same commit that ships it.

- **Automated device CI, remaining scope** — v0.4.0 ships an adb-driven Android
  smoke (`tools/device-smoke/`) and a manually-dispatched emulator workflow
  (needs a Unity-built dev APK — no Unity license in CI). Remaining: iOS
  simulator automation (no adb analog for shortcut taps — see the device-smoke
  README), asserting the COLD-launch path (the smoke's tap is a warm resume),
  and wiring the smoke into always-on CI once a Unity license (game-ci) exists.
- **Documentation site (considered, deliberately deferred)** — the reference
  docs live in a single 590-line [README](./README.md) plus a task-oriented
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

- Confirm the gated post-processor asmdefs (`Editor/iOS`, `Editor/Android`,
  `defineConstraints` `UNITY_IOS` / `UNITY_ANDROID`, with the extension DLLs in
  `precompiledReferences`) compile when that target is active and are skipped
  cleanly otherwise.
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
