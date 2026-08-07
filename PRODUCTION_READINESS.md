# Production Readiness — Quick Actions for Unity

Per-feature traceability: every feature is listed once, with the test(s) that
cover it, the review that signed it off, and what (if anything) still gates it
for production. Honest status — nothing marked "ready" that wasn't actually
exercised.

The short version — how far each Unity line is verified, and that no
physical-device validation has happened — is the **Status** section of the
[README](./README.md#status). This file is the per-feature breakdown behind it;
the two must not disagree.

**Legend — Verified by:**
`unit` = headless NUnit (`dotnet test`, 73 tests) · `unity-test` = Unity Test
Runner only (JsonUtility) · `static` = compiles in the stub harness (10 configs) ·
`review` = code review, several adversarial rounds (see git log) ·
`device` = **requires a real physical device** — Android partially done
(2026-08-07, Moto G Play 2024: static + dynamic shortcuts render, tap delivery
not yet captured); iOS not done. See "Exact remaining steps" below ·
`editor-2022.3` = **executed in a real licensed Unity 2022.3.9f1 Editor
(2026-07-17)** — import 0 errors, Test Runner 35/35 (historical count), real
player builds (incl. Android APKs); re-run on **2022.3.62f3** — import 0
errors, **Test Runner 74/74**, Android player build with the trampoline
injected on the `UnityPlayerActivity` path. ·
`editor-6.x` = **executed in real licensed Unity 6000.3.20f1 (6.3 LTS) and
6000.0.79f1 (6.0 LTS) Editors (2026-07-17)** — each: import 0 errors, Test
Runner 35/35 (historical count), both menus register, managed-gate Standalone
builds (define ON → dll present; OFF → zero trace). The 6.3 line was re-run on
the newer **6000.3.21f1** patch — import 0 errors, **Test Runner 74/74**,
Android build on the `UnityPlayerGameActivity` path, define-off build clean,
Xcode compile clean, plus a full **iOS Simulator runtime run**. Where this file
says "6.3" for work dated later than 2026-07-17, the Editor was `6000.3.21f1`.

## 1. Runtime API (managed) — fully testable, GREEN

| Feature | Verified by | Test(s) | Status |
|---|---|---|---|
| `Add` (valid / invalid / duplicate / null-throws) | unit + review | `Add_NewItem…`, `Add_InvalidItem…`, `Add_DuplicateId…`, `Add_Null_Throws` | ✅ |
| `AddList` (batch, single OS push, skips bad) | unit + review | `AddList_ValidItems_PushesToOsExactlyOnce`, `AddList_SkipsInvalidAndDuplicates`, `AddList_AllInvalid…`, `AddList_WithNullElement…` | ✅ |
| `Remove` / `RemoveById` | unit + review | `RemoveById_RemovesWhenPresent…`, `Remove_ByItem_UsesId` | ✅ |
| `RemoveAll` (OS-first ordering, throw-safe) | unit + review | `RemoveAll_ClearsEverything`, `RemoveAll_WhenBridgeThrows_LeavesInMemoryStateIntact`, `RemoveAll_ThenAdd_DoesNotResurrect…` | ✅ |
| `GetAll` (copy, insertion order) | unit + review | `GetAll_ReturnsCopy…`, `GetAll_PreservesInsertionOrder` | ✅ |
| `GetById` (match / missing / null / empty) | unit + review | `GetById_ReturnsMatch_NullForMissingOrNullOrEmpty` | ✅ |
| `IsAdded(item)` / `IsAdded(string)` | unit + review | covered across Add/Remove tests | ✅ |
| `Performed` event (fires once, ignores null/empty) | unit + review | `Dispatch_RaisesPerformedExactlyOnce`, `Dispatch_NullOrEmpty_DoesNotRaise` | ✅ |
| `LastPerformed` (pull-based) | unit + review | `LastPerformed_ReflectsBridgeValue` | ✅ |
| `ResetLastPerformed` | unit + review | `ResetLastPerformed_ClearsViaBridge` | ✅ |
| `IsPlatformSupported` | unit + review | `IsPlatformSupported_ReflectsBridge_FalseOnNoOpBridge` | ✅ |
| `LoggingEnable` | review | trivial property; exercised indirectly | ✅ |
| OS reconcile on first access (dedup, drop invalid) | unit + review | `FirstAccess_Reconciles…`, `Reconcile_DropsInvalidAndDuplicate…` | ✅ |
| Re-entrancy guard during load | unit + review | `EnsureLoaded_ReentrantBridge…` | ✅ |
| Pull-channel drain (ordered, once, idempotent) | unit + review | `Drain_DeliversBufferedIdsInOrderExactlyOnce` | ✅ |

## 2. Data types — fully testable, GREEN

| Feature | Verified by | Test(s) | Status |
|---|---|---|---|
| `QuickActionItem` ctor / `IsValid` / equality-by-id | unit + review | `Constructor_SetsFields`, `IsValid_RequiresIdAndTitle`, `Equality_IsByIdOnly` | ✅ |
| `IconType` pinned 0..29 (native contract) | unit + review | `IconType_EveryValueIsPinned`, `IconType_NoneIsZero…` | ✅ |
| JSON contract `{"items":[{"Id"…}]}` (exact keys, Icon as int) | unity-test + review + **editor-2022.3 + editor-6.x** | `SerializationTests` (5 tests) — **executed for real on 2022.3, 6.0 and 6.3** (the whole suite was 35/35 at the time; see Sign-off for today's counts) | ✅ |

## 3. Bridges / native (compiled; behavior is device-only, except the iOS path now proven on the Simulator)

| Feature | Verified by | Status / gate |
|---|---|---|
| iOS P/Invoke signatures + string malloc/free pairing | static + review + **real iOS SDK compile** | ✅ compiles & reviewed. `Plugins/iOS/QuickActions.mm` builds against the current iOS SDK (ARC, arm64, deployment target iOS 13) with **0 deprecation and 0 availability errors**. **device**: runtime behavior |
| iOS `UnityAppController` swizzle + cold/warm capture | review + **editor-6.x iOS Simulator run** | ✅ run against real UIKit on the iOS Simulator (6.3 / iOS 26.5): tapping a home-screen shortcut cold-launches the app and the action id arrives on the C# `Performed` event (seen in SpringBoard's log and the app console). ⏳ **device** — physical-hardware confirmation still open |
| Android JNI bridge + `ShortcutManager` + manifest-collision guard + JNI-safe reads | static + review | ✅ Java compiles (SDK stubs) & reviewed; **device**: real `ShortcutManager` |
| Android trampoline (version-proof, foreground task) | review + **editor-2022.3 + editor-6.x** (declared in real APKs on both activity paths — see §4 injector row) | ⏳ **device** — injection is build-proven on the `UnityPlayerActivity` path (2021.3, 2022.3) **and** the Unity 6 `UnityPlayerGameActivity` path (6.3); tap behavior itself is still device-only |
| Cold + warm delivery end-to-end | review + **editor-6.x iOS Simulator run** (cold half) | ⏳ **device** — the iOS **cold** path is proven on the Simulator (6.3); warm re-entry, Android, and multiple buffered taps are still device-only |

## 4. Editor / build-time features (Unity-only)

| Feature | Verified by | Status / gate |
|---|---|---|
| Static shortcuts → iOS `Info.plist` (`PBXProject`/`PlistDocument`) | static + review + **editor-6.x iOS Simulator run** | ✅ run for real on 6.3: the baker wrote the static shortcuts into the generated Xcode project's `Info.plist`, and they appear on the Simulator home screen with their SF Symbol icons (alongside one added at runtime through the C# API) |
| Static shortcuts → Android `res/xml` + strings + meta-data (escaping, real `applicationId`) | static + review + **editor-2022.3 REAL APK** | ✅ **build-proven 2026-07-17**: 4 static shortcuts (with hostile escaping inputs — embedded `"`, `'`, `&`, `<>`, leading `@`/`?`, `\`, edge whitespace) baked into a real dev APK. `aapt dump`: `res/xml/quickactions_shortcuts.xml` present, each `<shortcut>` intent targets `QuickActionsTrampolineActivity` with action `…PERFORM.<id>` and `targetPackage` = the real `applicationId` from the launcher `build.gradle`; launcher activity has the `android.app.shortcuts` meta-data; every string round-tripped exactly and aapt2 accepted them (a wrong `EscapeResValue` would fail the resource compile). |
| Trampoline `<activity>` injector (`QuickActionsTrampolineInjectorAndroid`, gated) | static + review + **editor-2022.3 REAL APK** + **editor-6.x REAL BUILD** | ✅ **build-proven 2026-07-17**: real Gradle dev build (define ON) → `aapt dump xmltree` shows the trampoline `<activity>` with exported/translucent/taskAffinity=""/excludeFromRecents/noHistory. Since re-proven on the `UnityPlayerActivity` path (2021.3.45f2, 2022.3.62f3) **and** on the Unity 6 `UnityPlayerGameActivity` path (6000.3.21f1). (The injector exists because a real build proved Unity does **not** merge a loose `AndroidManifest.xml` from inside a UPM package — the pre-fix dev APK had no trampoline at all.) |
| Project Settings ▸ Quick Actions UI (+ asset create, dup-id warning) | static + review | ⏳ **Unity** — Editor GUI |
| Editor Simulator (warm tap + Play-Mode cold-launch seed, play-session state reset) | unit + static + review + **editor-2022.3 + editor-6.x** (both `Window ▸ Quick Actions ▸ Simulator/About` menus execute on 2022.3, 6.0 and 6.3) | ⏳ interactive Play-Mode flow still manual |

## 5. Dev-only `QUICKACTIONS_ENABLED` gate (the headline guarantee)

| Aspect | Verified by | Status / gate |
|---|---|---|
| Managed: gated asmdefs, define-OFF compiles to nothing | static + review + **editor-2022.3 + editor-6.x REAL BUILDS** | ✅ **build-proven 2026-07-17 on 2022.3, 6.0 and 6.3**: define ON → `EminDeniz99.QuickActions.dll` in the built Standalone player; define OFF → zero package trace in the entire build output (`grep -ri quickactions` = 0 hits) |
| iOS: `.mm` `#if`-wrapped + macro injector (gated) + ungated cleanup (macro + plist strip when OFF) | static + review | ⏳ **device** — diff a prod Xcode project for `QUICKACTIONS_ENABLED` (expect none, incl. after an Append build via `QuickActionsGateCleanupiOS`) |
| Android: trampoline `<activity>` absent when OFF (never injected + stripper defense-in-depth) | review + **editor-2022.3 REAL APK** + **2021.3 / 6.3 define-off builds** | ✅ **build-proven 2026-07-17**: real prod Gradle build (define OFF) → `aapt dump xmltree` shows **no** trampoline `<activity>`, **no** `EminDeniz99.QuickActions.dll`, **zero** `quickactions` files in the APK. Re-proven on 2021.3.45f2 and 6000.3.21f1: the same build with the define removed carries no trace of the trampoline. Only the dead trampoline `.java` remains as ~4 unreachable strings in `classes.dex` (documented; needs package exclusion for literally-zero). |

## Sign-off

- **Headless gate (closable without a Unity Editor): GREEN.** `tools~/verify.sh` → **VERIFY: PASS** —
  9 C# configs compile with **0 warnings**, **73 unit tests pass** (`dotnet test`),
  the Android plugin compiles and its Java smoke test passes **103 checks, 0
  failed**, and every asset has a stable `.meta`. Every managed feature has a
  dedicated, intent-encoding test. Reviewed feature by feature across repeated
  adversarial rounds; every confirmed finding was fixed or explicitly
  documented, and **no ship-blocker remains open**.
- **Test inventory (where each number comes from).** 78 distinct C# tests:
  - **69 shared** — `Tests/Editor/QuickActionsApiTests.cs` (63) and
    `QuickActionItemTests.cs` (6). Run by BOTH `dotnet test` and the Unity Test
    Runner.
  - **5 Unity-only** — `Tests/Editor/SerializationTests.cs`. Needs real
    `JsonUtility`, so the headless harness excludes it (see the `Compile Include`
    list in `.verify/QuickActions.Tests.csproj`).
  - **4 headless-only** — `.verify/EditorTests/AndroidStaticLocalizationTests.cs`.
    Lives in the harness because the code under test sits behind a
    `UNITY_ANDROID` `defineConstraints` asmdef that a Unity test assembly cannot
    reference on other build targets.

  So `dotnet test` reports **73** (69 + 4) and a Unity Test Runner run reports
  **74** (69 + 5) — and **74/74 is the measured result** on 2021.3.45f2,
  2022.3.62f3 and 6000.3.21f1. The `35/35` real-editor runs cited below predate
  every test added from 2026-07-17 onward (cap-reconcile, failed-read/failed-write
  contracts, empty-accepted, and the later waves), so wherever `35/35` appears it
  is a historical measurement, not the current count. The only line not re-run
  at the current suite size is **6.0** (`6000.0.79f1`).
- **Real-Editor gate (2022.3): CLOSED 2026-07-17** in a licensed
  Unity 2022.3.9f1 Editor: package imports with **0 console
  errors**, **Unity Test Runner 35/35**, both menus registered, the managed
  QUICKACTIONS_ENABLED gate proven in **real player builds** (ON → assembly
  present; OFF → zero trace), and the **Android target end-to-end** — a real
  Gradle build produced a **dev APK** (define ON) whose merged manifest carries
  the trampoline `<activity>` and whose assets carry the managed dll, and a
  **prod APK** (define OFF) with **no** trampoline `<activity>`, **no** managed
  dll, and **zero** `quickactions` files (only the dead `.java` as ~4
  unreachable dex strings). This surfaced and fixed a real shipping bug: Unity
  does not merge a loose UPM-package `AndroidManifest.xml`, so the trampoline is
  now injected by a gated build post-processor. The **static-shortcuts baker**
  is also build-proven: a real APK carries `res/xml/quickactions_shortcuts.xml`,
  the launcher `android.app.shortcuts` meta-data, and correctly-escaped string
  resources (validated with deliberately hostile inputs that aapt2 accepted).
  (**35/35** is the historical suite size at that date; a later run on
  **2022.3.62f3** imported clean, passed the Test Runner **74/74**, and built an
  Android player with the trampoline injected.) The iOS target pass and the
  other editor lines, listed as open here on 2026-07-17, have since been done —
  see the Unity 6 and 2021.3 entries below.
- **Unity 6 lines (6.0 and 6.3): CLOSED 2026-07-17** in real licensed
  Unity 6000.0.79f1 and 6000.3.20f1 Editors. For **each**: a fresh project + the package as a `file:` UPM
  dependency **imports with 0 compile errors** on Unity 6, **Unity Test Runner
  35/35**, both `Window ▸ Quick Actions` menus register, and the managed gate is
  proven in **real Standalone builds** (define ON → `EminDeniz99.QuickActions.dll`
  present; OFF → zero `quickactions` trace in the whole build tree). This
  confirms no Unity-6 API deprecation breaks the package. (**35/35** is the
  historical suite size at that date; the suite is 74 in the Test Runner now.)
  The 6.x **Android** Gradle pass (GameActivity era) **has since been run**, on
  `6000.3.21f1`: the player builds with the trampoline `<activity>` injected on
  the `UnityPlayerGameActivity` path, and the define-off build carries no trace
  of it. The same 6.3 run also compiled the generated Xcode project cleanly and
  completed a full **iOS Simulator runtime run**: static shortcuts baked into
  `Info.plist` plus one added at runtime through the C# API both appear on the
  Simulator home screen with their SF Symbol icons, and tapping one
  cold-launches the app with the action id arriving on the C# `Performed` event
  (confirmed in SpringBoard's own log and the app console). What the device
  matrix still covers on this line is behavior on real hardware.
- **Unity 2021.3: VERIFIED (2021.3.45f2) — and it found a real bug.** The
  declared minimum is now proven in a real Editor: **74/74 tests pass**, an
  Android player builds with the trampoline `<activity>` injected on the old
  `UnityPlayerActivity` path, the same build with the define removed contains
  **no trace** of it, and the generated Xcode project compiles with
  `xcodebuild` against the device SDK with **zero warnings from
  `QuickActions.mm`**.

  The first attempt did not compile: `SystemLanguage.Hindi` (added in Unity
  2022.2) was used ungated in the localization mapping, so the package had
  never been compilable on its own declared minimum. It is now behind
  `UNITY_2022_2_OR_NEWER`.

  The reason nothing caught this earlier is worth keeping: the stub harness's
  `SystemLanguage` enum mirrored a NEWER Editor than the declared minimum
  (it included `Hindi = 42`), so all nine compile configs and every CI run went
  green against an API 2021.3 does not have. The stub now mirrors the
  **minimum**; anything newer belongs behind a `UNITY_x_OR_NEWER` gate and is
  verified in a real Editor, not in the harness.

  **The 2021.3 Simulator cannot be tested on Apple silicon — Unity's limit,
  not the package's.** Unity 2021.3's iOS support ships a simulator runtime for
  **x86_64 only** (`baselib-amd64.a`; every arm64 library in that install is a
  device build, and nothing in it is named `*sim*`). Apple silicon cannot run an
  x86_64 app on a modern iOS Simulator runtime, so the 2021.3 player is
  uninstallable there. For contrast, Unity 6.3 ships `libiPhone-lib-sim-arm64`,
  `-sim-x64` and `-sim-x64arm64`, which is why the Simulator run described above
  was possible on that line (Unity added arm64 Simulator support in Unity 6 and
  has said it will not backport it to 2021 LTS; 2022.3 does ship arm64 simulator
  libraries). The 2021.3 runtime path is therefore covered by the
  device gate, not by the Simulator; its build-time path (post-processor,
  Info.plist, Xcode compile of the plugin) IS verified above.

  **Licensing note for anyone reproducing this:** `2021.3.45f2` is the newest
  2021.3 build a Personal/Pro licence can run. Later patches (`.46f1` onward,
  through `.58f1`) are **Extended LTS** and require Industry or Enterprise —
  the Editor refuses to launch with `com.unity.editor.access.xlts` missing.
  Unity's public release API lists only `.45f1` and `.45f2` for this line,
  which is the cheapest way to tell before downloading ~10 GB.
- **Device gate: OPEN — no physical-device validation has been done on either
  platform.** Everything marked ⏳ above needs each claimed Unity line (2021.3,
  2022.3, 6.0, 6.3 — full pass on all) + an iOS device (via macOS/Xcode) + an
  Android API-25+ device. Editor coverage so far: **2022.3.9f1** — managed gate
  + real Android APKs (trampoline injection dev/prod + static-shortcuts baker);
  **2022.3.62f3** — import 0 errors, Test Runner **74/74**, Android player build
  with the trampoline injected (`UnityPlayerActivity` path); **6.0.79f1** and
  **6.3.20f1** — managed gate + tests + menus; **6000.3.21f1 (6.3)** — Test
  Runner 74/74, Android build on the `UnityPlayerGameActivity` path, define-off
  build clean, Xcode compile clean, **plus a full iOS Simulator runtime run**
  (static shortcuts from `Info.plist` and one added at runtime both on the
  Simulator home screen with their SF Symbol icons; tapping one cold-launches
  the app and the id arrives on `Performed`); **2021.3.45f2** — 74/74, Android
  trampoline injected / absent without the define, and a clean `xcodebuild`
  compile of the generated Xcode project (see above). The one remaining gap is
  **physical-device taps on both platforms** — the true blocker to a `1.0.0`
  "production-ready" stamp.

### Exact remaining steps to close the device gate

Steps 1 and 2 below are **DONE** for `2021.3.45f2` and `6000.3.21f1`, and the
iOS runtime path is done on the Simulator (see the Editor-coverage list above).
What is left is physical hardware.

1. ~~Open the package in EACH claimed line; switch to iOS + Android targets →
   confirm 0 console errors and that the gated Editor asmdefs'
   `precompiledReferences` resolve.~~ Done on 2021.3, 2022.3 (`.62f3`) and 6.3;
   6.0 was covered by the earlier Editor runs.
2. ~~Dev build (define ON) + prod build (define OFF) on each platform; diff the
   output for `QuickActionsTrampolineActivity`.~~ Done on 2021.3 and 6.3:
   present with the define, absent without it, in the built APK's manifest.
   (On 2022.3.62f3 only the define-ON half was rebuilt; the define-OFF half
   there is the earlier 2022.3.9f1 prod APK.)

   Note for whoever repeats this: the define must be flipped in a **separate
   Editor invocation** from the build. The package refuses to build otherwise
   — the Editor assemblies would still carry the define and the player would
   quietly keep the dev-only pieces, which is exactly the false "no trace"
   result this check exists to prevent.
3. On a physical device: cold + warm taps, static + dynamic shortcuts, both
   OSes. The Android half is scripted in
   [`tools~/device-smoke/`](./tools~/device-smoke/README.md); iOS is manual (no
   adb analog).

   **Android, partially closed 2026-08-07** — Moto G Play 2024 (Android 14,
   arm64), sideloaded APK built from `Examples~/Testbed2021` with 2021.3.45f2,
   IL2CPP, `arm64-v8a` + `armeabi-v7a`. Confirmed on hardware:
   - **Static shortcuts on a cold, never-opened install**: long-pressing the
     icon straight after installing showed all three baked shortcuts. This is
     the manifest-merge + `res/xml/quickactions_shortcuts.xml` path, which no
     simulator or stub run had ever exercised.
   - **Dynamic add**: "Add 3 shortcuts" and "Add 'settings'" both published.
   - **Same-id collision, exactly as documented** at `README.md:373`: `new_game`
     and `continue` exist in both the static and dynamic sets, and the launcher
     kept the *manifest* entries while the dynamic duplicates were dropped —
     the remaining slots went to the dynamic-only `daily` and `settings`, with
     static `daily_reward` pushed out by the launcher's four-item cap.
   - **Android renders the long label** (our `Subtitle`), not the short one, so
     the popup reads "Start a fresh run" rather than "New Game" — `README.md:322`
     is correct, and it is worth knowing when authoring labels for Android.

   Still open on Android: **tap delivery** — a shortcut tap arriving as
   `Performed` on a cold and on a warm launch. The device run above published
   and rendered shortcuts but did not capture a tap. That is a few minutes with
   `adb logcat -s Unity` or by reading the demo's on-screen log.

Until step 3 passes on both platforms, ship honestly as a **`0.x`
pre-device-validation release** (the first public one is `0.4.0`, tagged and
released 2026-08-07 with the `.unitypackage` attached), not `1.0.0`.
