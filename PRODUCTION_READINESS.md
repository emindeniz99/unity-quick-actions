# Production Readiness — Quick Actions for Unity

Per-feature traceability: every feature is listed once, with the test(s) that
cover it, the review that signed it off, and what (if anything) still gates it
for production. Honest status — nothing marked "ready" that wasn't actually
exercised.

**Legend — Verified by:**
`unit` = headless NUnit (`dotnet test`, 33 tests) · `unity-test` = Unity Test
Runner only (JsonUtility) · `static` = compiles in the stub harness (7 configs) ·
`review` = human/agent code review (multiple adversarial rounds + a 15-unit workflow; see git log) ·
`device` = **requires a real Unity Editor + device — NOT done here**.

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
| JSON contract `{"items":[{"Id"…}]}` (exact keys, Icon as int) | unity-test + review | `SerializationTests` (literal-key asserts) | ✅ (Unity Test Runner) |

## 3. Bridges / native (compiled here, behavior is device-only)

| Feature | Verified by | Status / gate |
|---|---|---|
| iOS P/Invoke signatures + string malloc/free pairing | static + review | ✅ compiles & reviewed; **device**: real Apple-SDK compile of `.mm` |
| iOS `UnityAppController` swizzle + cold/warm capture | review | ⏳ **device** — never run against UIKit |
| Android JNI bridge + `ShortcutManager` + manifest-collision guard + JNI-safe reads | static + review | ✅ Java compiles (SDK stubs) & reviewed; **device**: real `ShortcutManager` |
| Android trampoline (version-proof, foreground task) | review | ⏳ **device** — Unity 2022 `UnityPlayerActivity` **and** Unity 6 `GameActivity` |
| Cold + warm delivery end-to-end | review | ⏳ **device** — both OSes, multiple buffered taps |

## 4. Editor / build-time features (Unity-only)

| Feature | Verified by | Status / gate |
|---|---|---|
| Static shortcuts → iOS `Info.plist` (`PBXProject`/`PlistDocument`) | static + review | ⏳ **Unity** — real extension DLLs + a build |
| Static shortcuts → Android `res/xml` + strings + meta-data (escaping, real `applicationId`) | static + review | ⏳ **Unity** — real Gradle project |
| Project Settings ▸ Quick Actions UI (+ asset create, dup-id warning) | static + review | ⏳ **Unity** — Editor GUI |
| Editor Simulator (warm tap + Play-Mode cold-launch seed, play-session state reset) | unit (seam: `EditorSimulateTap…`, `OverrideBridgeForTesting_ClearsSimulatedTapState`) + static + review | ⏳ **Unity** — manual Editor check of the window/Play-Mode flow |

## 5. Dev-only `QUICKACTIONS_ENABLED` gate (the headline guarantee)

| Aspect | Verified by | Status / gate |
|---|---|---|
| Managed: gated asmdefs, define-OFF compiles to nothing | static (NativeGate define-OFF config) + review | ✅ structurally proven by the 7-config harness |
| iOS: `.mm` `#if`-wrapped + macro injector (idempotent) | review | ⏳ **device** — diff a prod Xcode project for `QUICKACTIONS_ENABLED` (expect none) |
| Android: trampoline `<activity>` stripped when OFF | review | ⏳ **device** — diff a prod manifest for `QuickActionsTrampolineActivity` (expect none) |

## Sign-off

- **Headless gate (closable here): GREEN.** `tools/verify.sh` → **VERIFY: PASS** —
  7 C# configs compile, **33 unit tests pass**, Java compiles, every asset has a
  stable `.meta`. Every managed feature has a dedicated, intent-encoding test
  (Rule 9). Reviewed one-by-one across repeated adversarial rounds + a 15-unit workflow
  (every confirmed finding fixed or explicitly documented; **0 P0 ship-blockers**).
- **Device gate (NOT closable in this container): OPEN.** Everything marked
  ⏳ above needs each claimed Unity line (2021.3, 2022.3, 6.0, 6.3 — full pass on all) + an iOS device
  (via macOS/Xcode) + an Android API-25+ device. This environment is headless
  Linux with no Unity license, so native/build/on-device behavior is reviewed
  and compiled but **not executed**. This is the one true blocker to a `1.0.0`
  "production-ready" stamp.

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
