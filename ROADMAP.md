# Quick Actions for Unity — Roadmap

Follow-ups discussed but not shipped in v0.1.0. Delete an entry in the same
commit that ships it.

- **Per-item rasterized icons** — accept a `Texture2D`/`Sprite` and emit a
  template `UIApplicationShortcutIcon` (iOS) and a generated drawable
  (Android), instead of only system `IconType` glyphs + named drawables. This
  would also let iOS OS read-back recover icons (on iOS reconciled items come
  back as `IconType.None`; Android already recovers icon identity via extras).
- **Pinned shortcuts** — `requestPinShortcut` on Android; no iOS analog.
- **Static shortcut custom icons (iOS file icons)** — `UIApplicationShortcutItemIconFile`
  with an asset-catalog template image (Android already supports a drawable via
  `AndroidDrawable`; iOS static currently supports only system `IconType` glyphs).
- **`.unitypackage` export in CI** — `tools/pack_unitypackage.py` already builds
  the classic artifact without Unity; remaining: run it in CI and attach the
  output to releases.
- **Automated device CI** — drive an iOS simulator / Android emulator to assert
  cold + warm delivery end-to-end.
- **Localization** — per-locale titles/subtitles.

## Validate in a real Unity Editor (license-gated; can't run here)

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
  it is off — verify
  the prod manifest has no `QuickActionsTrampolineActivity` (the `.java` dead
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
- **iOS scene lifecycle:** cold-launch dedup relies on the app-delegate model
  (returning `NO` from `didFinishLaunchingWithOptions` suppresses the duplicate
  `performActionForShortcutItem`). Unity's trampoline uses the app-delegate model
  by default; if a project adopts the `UIScene` lifecycle, add a
  `windowScene:performActionForShortcutItem:` hook and queue-level dedup.
- **iOS host-subclass cold double-delivery:** our `didFinishLaunchingWithOptions`
  swizzle returns `NO` when the app was cold-launched from one of our shortcuts,
  which is what tells iOS *not* to also call `performActionForShortcutItem` — so
  the cold tap is delivered exactly once. If a host ships its own
  `UnityAppController` subclass that overrides this selector, calls `super`, then
  returns `YES` unconditionally (discarding our `NO`), iOS delivers the cold tap
  twice. The fix is on the host side — such an override should return the value
  from `[super application:didFinishLaunchingWithOptions:]`. Documented as a known
  limitation in `Plugins/iOS/QuickActions.mm`; a future hardening could add
  queue-level dedup so a doubled cold delivery collapses to one regardless.
