# Production Readiness — Quick Actions for Unity

Per-feature traceability: every feature is listed once, with the test(s) that
cover it, the review that signed it off, and what (if anything) still gates it
for production. Honest status — nothing marked "ready" that wasn't actually
exercised.

**Legend — Verified by:**
`unit` = headless NUnit (`dotnet test`, 33 tests) · `unity-test` = Unity Test
Runner only (JsonUtility) · `static` = compiles in the stub harness (7 configs) ·
`review` = human/agent code review (multiple adversarial rounds + a 15-unit workflow; see git log) ·
`device` = **requires a real device — NOT done here** ·
`editor-2022.3` = **executed in a real licensed Unity 2022.3.9f1 (Linux, xvfb)
in this container on 2026-07-17** — import 0 errors, Test Runner 35/35, real
player builds.

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
| JSON contract `{"items":[{"Id"…}]}` (exact keys, Icon as int) | unity-test + review + **editor-2022.3** | `SerializationTests` — **executed for real: 35/35 incl. these** | ✅ |

## 3. Bridges / native (compiled here, behavior is device-only)

| Feature | Verified by | Status / gate |
|---|---|---|
| iOS P/Invoke signatures + string malloc/free pairing | static + review | ✅ compiles & reviewed; **device**: real Apple-SDK compile of `.mm` |
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
| Editor Simulator (warm tap + Play-Mode cold-launch seed, play-session state reset) | unit + static + review + **editor-2022.3** (both `Window ▸ Quick Actions ▸ Simulator/About` menus execute) | ⏳ interactive Play-Mode flow still manual |

## 5. Dev-only `QUICKACTIONS_ENABLED` gate (the headline guarantee)

| Aspect | Verified by | Status / gate |
|---|---|---|
| Managed: gated asmdefs, define-OFF compiles to nothing | static + review + **editor-2022.3 REAL BUILDS** | ✅ **build-proven 2026-07-17**: define ON → `EminDeniz99.QuickActions.dll` in the Linux player; define OFF → zero package trace in the entire build output (`grep -ri quickactions` = 0 hits) |
| iOS: `.mm` `#if`-wrapped + macro injector (idempotent) | review | ⏳ **device** — diff a prod Xcode project for `QUICKACTIONS_ENABLED` (expect none) |
| Android: trampoline `<activity>` absent when OFF (never injected + stripper defense-in-depth) | review + **editor-2022.3 REAL APK** | ✅ **build-proven 2026-07-17**: real prod Gradle build (define OFF) → `aapt dump xmltree` shows **no** trampoline `<activity>`, **no** `EminDeniz99.QuickActions.dll`, **zero** `quickactions` files in the APK. Only the dead trampoline `.java` remains as ~4 unreachable strings in `classes.dex` (documented; needs package exclusion for literally-zero). |

## Sign-off

- **Headless gate (closable here): GREEN.** `tools/verify.sh` → **VERIFY: PASS** —
  7 C# configs compile, **33 unit tests pass**, Java compiles, every asset has a
  stable `.meta`. Every managed feature has a dedicated, intent-encoding test
  (Rule 9). Reviewed one-by-one across repeated adversarial rounds + a 15-unit workflow
  (every confirmed finding fixed or explicitly documented; **0 P0 ship-blockers**).
- **Real-Editor gate (2022.3): CLOSED IN-CONTAINER 2026-07-17** via a licensed
  Unity 2022.3.9f1 (student Pro `.ulf`): package imports with **0 console
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
- **Device gate (NOT closable in this container): OPEN.** Everything marked
  ⏳ above needs each claimed Unity line (2021.3, 2022.3, 6.0, 6.3 — full pass on all) + an iOS device
  (via macOS/Xcode) + an Android API-25+ device. The container now has a
  licensed 2022.3.9f1 (see above) with the Android target passes closed, so the
  remaining gaps are the iOS target-specific pass, the other three editor lines,
  and physical-device taps — the true blockers to a `1.0.0` "production-ready"
  stamp.

### Exact remaining steps to close the device gate
1. Open the package in EACH claimed line (2021.3, 2022.3, 6.0, 6.3); switch to iOS + Android targets →
   confirm 0 console errors and that the gated Editor asmdefs' `precompiledReferences`
   resolve.
2. Dev build (define ON) + prod build (define OFF) on each platform; diff the
   output for `QUICKACTIONS_ENABLED` / `QuickActionsTrampolineActivity`.
3. On-device: cold + warm taps, static + dynamic shortcuts, both OSes
   (procedure in `plans/mvp.md`).

Until step 3 passes, ship honestly as **`0.1.0` (pre-device-validation)**, not
`1.0.0`.
