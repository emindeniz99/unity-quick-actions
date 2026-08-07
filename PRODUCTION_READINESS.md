# Production Readiness — Quick Actions for Unity

Per-feature traceability: every feature is listed once, with the test(s) that
cover it, the review that signed it off, and what (if anything) still gates it
for production. Honest status — nothing marked "ready" that wasn't actually
exercised.

The short version — which Unity lines are compile-verified, which are not, and
that no on-device validation has happened — is the **Status** section of the
[README](./README.md#status). This file is the per-feature breakdown behind it;
the two must not disagree.

**Legend — Verified by:**
`unit` = headless NUnit (`dotnet test`, 73 tests) · `unity-test` = Unity Test
Runner only (JsonUtility) · `static` = compiles in the stub harness (9 configs) ·
`review` = code review, several adversarial rounds (see git log) ·
`device` = **requires a real physical device — NOT done** ·
`editor-2022.3` = **executed in a real licensed Unity 2022.3.9f1 Editor
(2026-07-17)** — import 0 errors, Test Runner 35/35, real player builds
(incl. Android APKs). ·
`editor-6.x` = **executed in real licensed Unity 6000.3.20f1 (6.3 LTS) and
6000.0.79f1 (6.0 LTS) Editors (2026-07-17)** — each: import 0 errors, Test
Runner 35/35, both menus register, managed-gate Standalone builds (define ON →
dll present; OFF → zero trace).

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

## 3. Bridges / native (compiled; behavior is device-only)

| Feature | Verified by | Status / gate |
|---|---|---|
| iOS P/Invoke signatures + string malloc/free pairing | static + review + **real iOS SDK compile** | ✅ compiles & reviewed. `Plugins/iOS/QuickActions.mm` builds against the current iOS SDK (ARC, arm64, deployment target iOS 13) with **0 deprecation and 0 availability errors**. **device**: runtime behavior |
| iOS `UnityAppController` swizzle + cold/warm capture | review | ⏳ **device** — never run against UIKit |
| Android JNI bridge + `ShortcutManager` + manifest-collision guard + JNI-safe reads | static + review | ✅ Java compiles (SDK stubs) & reviewed; **device**: real `ShortcutManager` |
| Android trampoline (version-proof, foreground task) | review + **editor-2022.3** (declared in a real APK — see §4 injector row) | ⏳ **device** — tap behavior on Unity 2022 `UnityPlayerActivity` **and** Unity 6 `GameActivity` |
| Cold + warm delivery end-to-end | review | ⏳ **device** — both OSes, multiple buffered taps |

## 4. Editor / build-time features (Unity-only)

| Feature | Verified by | Status / gate |
|---|---|---|
| Static shortcuts → iOS `Info.plist` (`PBXProject`/`PlistDocument`) | static + review | ⏳ **Unity** — real extension DLLs + a build |
| Static shortcuts → Android `res/xml` + strings + meta-data (escaping, real `applicationId`) | static + review + **editor-2022.3 REAL APK** | ✅ **build-proven 2026-07-17**: 4 static shortcuts (with hostile escaping inputs — embedded `"`, `'`, `&`, `<>`, leading `@`/`?`, `\`, edge whitespace) baked into a real dev APK. `aapt dump`: `res/xml/quickactions_shortcuts.xml` present, each `<shortcut>` intent targets `QuickActionsTrampolineActivity` with action `…PERFORM.<id>` and `targetPackage` = the real `applicationId` from the launcher `build.gradle`; launcher activity has the `android.app.shortcuts` meta-data; every string round-tripped exactly and aapt2 accepted them (a wrong `EscapeResValue` would fail the resource compile). |
| Trampoline `<activity>` injector (`QuickActionsTrampolineInjectorAndroid`, gated) | static + review + **editor-2022.3 REAL APK** | ✅ **build-proven 2026-07-17**: real Gradle dev build (define ON) → `aapt dump xmltree` shows the trampoline `<activity>` with exported/translucent/taskAffinity=""/excludeFromRecents/noHistory. (The injector exists because a real build proved Unity does **not** merge a loose `AndroidManifest.xml` from inside a UPM package — the pre-fix dev APK had no trampoline at all.) |
| Project Settings ▸ Quick Actions UI (+ asset create, dup-id warning) | static + review | ⏳ **Unity** — Editor GUI |
| Editor Simulator (warm tap + Play-Mode cold-launch seed, play-session state reset) | unit + static + review + **editor-2022.3 + editor-6.x** (both `Window ▸ Quick Actions ▸ Simulator/About` menus execute on 2022.3, 6.0 and 6.3) | ⏳ interactive Play-Mode flow still manual |

## 5. Dev-only `QUICKACTIONS_ENABLED` gate (the headline guarantee)

| Aspect | Verified by | Status / gate |
|---|---|---|
| Managed: gated asmdefs, define-OFF compiles to nothing | static + review + **editor-2022.3 + editor-6.x REAL BUILDS** | ✅ **build-proven 2026-07-17 on 2022.3, 6.0 and 6.3**: define ON → `EminDeniz99.QuickActions.dll` in the built Standalone player; define OFF → zero package trace in the entire build output (`grep -ri quickactions` = 0 hits) |
| iOS: `.mm` `#if`-wrapped + macro injector (gated) + ungated cleanup (macro + plist strip when OFF) | static + review | ⏳ **device** — diff a prod Xcode project for `QUICKACTIONS_ENABLED` (expect none, incl. after an Append build via `QuickActionsGateCleanupiOS`) |
| Android: trampoline `<activity>` absent when OFF (never injected + stripper defense-in-depth) | review + **editor-2022.3 REAL APK** | ✅ **build-proven 2026-07-17**: real prod Gradle build (define OFF) → `aapt dump xmltree` shows **no** trampoline `<activity>`, **no** `EminDeniz99.QuickActions.dll`, **zero** `quickactions` files in the APK. Only the dead trampoline `.java` remains as ~4 unreachable strings in `classes.dex` (documented; needs package exclusion for literally-zero). |

## Sign-off

- **Headless gate (closable without a Unity Editor): GREEN.** `tools/verify.sh` → **VERIFY: PASS** —
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

  So `dotnet test` reports **73** (69 + 4) and a fresh Unity Test Runner run
  would report **74** (69 + 5). The `35/35` real-editor runs cited below predate
  every test added from 2026-07-17 onward (cap-reconcile, failed-read/failed-write
  contracts, empty-accepted, and the later waves) — the editor runs have not been
  repeated since, so `35/35` is a historical measurement, not the current count.
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
  Still open below: iOS target pass (macOS-only) and the other editor lines.
- **Unity 6 lines (6.0 and 6.3): CLOSED 2026-07-17** in real licensed
  Unity 6000.0.79f1 and 6000.3.20f1 Editors. For **each**: a fresh project + the package as a `file:` UPM
  dependency **imports with 0 compile errors** on Unity 6, **Unity Test Runner
  35/35**, both `Window ▸ Quick Actions` menus register, and the managed gate is
  proven in **real Standalone builds** (define ON → `EminDeniz99.QuickActions.dll`
  present; OFF → zero `quickactions` trace in the whole build tree). This
  confirms no Unity-6 API deprecation breaks the package. The 6.x **Android**
  Gradle pass (GameActivity era) has not been run and is covered by the device
  matrix.
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
  **6.0.79f1** and **6.3.20f1** — managed gate + tests + menus; **2021.3.45f2** —
  resolves/loads only, never compiled (see above). Remaining gaps: the iOS
  target-specific Unity build pass, the 6.x Android Gradle pass, a real 2021.3
  compile/build, and physical-device taps on both platforms — the true blockers
  to a `1.0.0` "production-ready" stamp.

### Exact remaining steps to close the device gate

Steps 1 and 2 below are **DONE** for `2021.3.45f2` and `6000.3.21f1`, and the
iOS runtime path is done on the Simulator (see the Editor-coverage list above).
What is left is physical hardware.

1. ~~Open the package in EACH claimed line; switch to iOS + Android targets →
   confirm 0 console errors and that the gated Editor asmdefs'
   `precompiledReferences` resolve.~~ Done on 2021.3 and 6.3; 2022.3 and 6.0
   were covered by the earlier Editor runs.
2. ~~Dev build (define ON) + prod build (define OFF) on each platform; diff the
   output for `QuickActionsTrampolineActivity`.~~ Done on both lines: present
   with the define, absent without it, in the built APK's manifest.

   Note for whoever repeats this: the define must be flipped in a **separate
   Editor invocation** from the build. The package refuses to build otherwise
   — the Editor assemblies would still carry the define and the player would
   quietly keep the dev-only pieces, which is exactly the false "no trace"
   result this check exists to prevent.
3. On a physical device: cold + warm taps, static + dynamic shortcuts, both
   OSes. The Android half is scripted in
   [`tools/device-smoke/`](./tools/device-smoke/README.md); iOS is manual (no
   adb analog).

Until step 3 passes, ship honestly as a **`0.x` pre-device-validation release**
(the first public one is `0.4.0`), not `1.0.0`.
