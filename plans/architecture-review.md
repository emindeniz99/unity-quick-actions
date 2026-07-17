# Architecture & system design review — Quick Actions for Unity

A high-altitude review of the package's structure, abstractions, control/data
flow, and the trade-offs behind them — distinct from the line-level bug review
(those fixes are already in). Dev-facing; excluded from the .unitypackage (visible in a UPM install).

> **Resolution status:** All seven weaknesses below have been **addressed**.
> W1 (OS read-back reconcile), W2 (single pull delivery channel), W3 (platform
> post-processors split into `defineConstraint`-gated asmdefs), W4 (shared
> bridge), W5 (documented). W6/W7 were accepted-by-design and documented. The
> analysis is kept as the rationale; the table in §8 marks what shipped.

## 1. System context & components

```
                         ┌──────────────────────────────────────────┐
   Game code  ──────────▶│  QuickActions  (public static facade)     │
   (Add/Remove/Performed)│  · owns _items (authoritative list)        │
                         │  · raises Performed event                  │
                         └───────────────┬───────────────────────────┘
                                         │ IQuickActionsBridge
                 ┌───────────────────────┼───────────────────────────┐
                 ▼                       ▼                            ▼
        IOSQuickActionsBridge   AndroidQuickActionsBridge     NullQuickActionsBridge
          (DllImport __Internal) (AndroidJavaClass/JNI)          (Editor / no-op)
                 │                       │
     ┌───────────▼──────────┐  ┌─────────▼──────────────────────────┐
     │ QuickActions.mm      │  │ QuickActionsBridge.java +           │
     │ · UnityAppController  │  │ QuickActionsTrampolineActivity.java │
     │   swizzle (+load)     │  │ · ShortcutManager dynamic shortcuts │
     │ · UIApplication       │  │ · trampoline records tapped id      │
     │   .shortcutItems      │  │ · <activity> injected at build time │
     └───────────────────────┘  └─────────────────────────────────────┘

   QuickActionsRuntime (MonoBehaviour, auto-created)  ← native tap notifications
     · drains bridge.ConsumePendingPerformed() one frame after startup (cold)
       and on OnApplicationFocus(true)/OnApplicationPause(false) (warm) — one channel

   Editor (build-time, separate assembly):
     QuickActionsSettings (ScriptableObject) ─▶ Project Settings provider
     ─▶ iOS post-processor  (Info.plist UIApplicationShortcutItems)
     ─▶ Android post-processor (res/xml/quickactions_shortcuts.xml + manifest meta-data)
```

## 2. Responsibilities (cohesion check)

| Component | Responsibility | Verdict |
|-----------|----------------|---------|
| `QuickActions` | Public API, authoritative list, event | Cohesive ✔ |
| `QuickActionItem` / `IconType` | Data model | Cohesive ✔ |
| `IQuickActionsBridge` | Platform seam (set/remove/last/pending) | Right seam ✔ |
| `*QuickActionsBridge` | One platform each | Cohesive ✔ |
| `QuickActionsBridgeFactory` | Platform selection | Cohesive ✔ |
| `QuickActionsRuntime` | Native→managed funnel + lifecycle timing | Mixed (delivery + timing) ⚠ |
| Editor settings/provider | Static config authoring | Cohesive ✔ |
| Build post-processors | Bake static shortcuts | Cohesive, but coupled to ext-DLLs ⚠ |

## 3. Control / data flow (three canonical paths)

**A. Register (runtime):** `QuickActions.Add` → validate → mutate `_items` →
`bridge.SetShortcuts(_items)` → native sets the full OS set. *Managed list is the
source of truth; native is a write-through projection.*

**B. Cold launch from a shortcut:** native captures the id pre-scripting
(iOS `didFinishLaunchingWithOptions`; Android trampoline `onCreate`) → buffers it
→ `QuickActionsRuntime` waits one frame (so subscribers exist) → drains
`ConsumePendingPerformed()` → raises `Performed`. `LastPerformed` also exposes it
(pull alternative).

**C. Warm tap (single pull channel, as shipped):** iOS → `performActionForShortcutItem`
enqueues; Android → trampoline records + foregrounds Unity. Both → the app
regains focus → `OnApplicationFocus(true)`/`OnApplicationPause(false)` → poll
drains the queue → `Performed`. (The original push design is analysed as W2.)

## 4. Key design decisions & rationale

1. **Facade + bridge interface.** Tiny stable API; platform churn isolated. ✔
2. **Managed list as source of truth, write-through to OS.** Simple mental model;
   avoids two-way sync. ✔ (but see Weakness W1).
3. **Trampoline activity over subclassing Unity's activity.** The standout
   decision: immune to `UnityPlayerActivity`→`UnityPlayerGameActivity`. ✔✔
4. **Swizzle chaining the original IMP.** Non-invasive, composes with other
   plugins (AppsFlyer/Firebase pattern). ✔
5. **Hybrid push/pull delivery + one-frame deferral.** Solves "native fires before
   subscribers exist." Pragmatic. ✔ (but see W2).
6. **Null Object bridge for Editor.** No null checks, safe play-mode. ✔

## 5. Strengths

- Clear dependency direction (facade → interface → impls); no cycles.
- Platform code fully isolated behind `#if` + interface; adding a platform = one
  new bridge + one factory branch.
- Single, narrow extension seam (`IQuickActionsBridge`) for new capabilities.
- Main-thread-only delivery; no background-thread event hazards.
- Managed logic is unit-testable without Unity (the full NUnit suite runs headless via stubs; two serialization tests are Unity-only).
- Native memory ownership is explicit (malloc ↔ native free).

## 6. Weaknesses / risks (severity · recommendation)

**W1 — Cross-session state divergence (MEDIUM, design smell).** The OS persists
dynamic shortcuts across launches, but `_items` resets each process. So
`GetAll()`/`IsAdded()` are inaccurate after a cold restart until the app
re-registers. The "authoritative" managed list is only authoritative within a
session — the OS is the real store. *Recommend:* add `bridge.GetShortcuts()`
read-back to reconcile on first use, **or** rename semantics to "session
shortcuts" and document the re-register-on-launch pattern prominently. (Already
in ROADMAP as "OS-backed GetAll".)

**W2 — Asymmetric delivery channels (MEDIUM, simplicity/altitude).** iOS warm uses
push (`UnitySendMessage`); cold + Android warm use poll. Correctness relies on the
invariant "iOS warm does not enqueue, so the focus-poll can't double-deliver." It
works, but a reader must hold several invariants. *Recommend:* collapse to a
**single pull channel** — have iOS warm also enqueue and drop `UnitySendMessage`;
the app becoming active fires `OnApplicationFocus(true)` → poll drains it. One
mechanism, no dedup reasoning, no `OnPerformed` sink. Generalize rather than
special-case. (Needs device validation of focus-vs-performAction ordering; the
queue makes a late poll safe.)

**W3 — Editor assembly couples runtime-config UI to build-time platform DLLs
(MEDIUM, robustness).** The post-processors (`UnityEditor.iOS.Xcode`,
`UnityEditor.Android` extensions) live in the same Editor asmdef as the settings
UI, pulled in via `overrideReferences` + `precompiledReferences` and `#if`
guards. If a target's extension DLL is absent, behaviour depends on Unity not
hard-failing the reference. *Recommend (de-risk):* move each post-processor into
its own asmdef gated by `defineConstraints: [UNITY_IOS]` / `[UNITY_ANDROID]`, so
the main Editor assembly never references those DLLs. Trade-off: two more
asmdefs. (Tracked in ROADMAP under "validate in real Unity".)

**W4 — Bridge instance vs reality (LOW, clarity).** `QuickActions` keeps a
`_bridge` field while `QuickActionsRuntime` news a throwaway bridge each poll.
Both work only because native state is process-global static — i.e. the bridges
are effectively stateless. *Recommend:* make the platform bridge a stateless
static (or a single shared instance) to stop implying per-instance state.

**W5 — Singleton-by-design, global native state (LOW, document).** `gQAPending`,
`gQALastPerformed`, `sPending`, `sLastPerformed` are process globals. The whole
feature is inherently a singleton (one shortcut set per app) — which matches the
platform APIs — but this constraint is implicit. *Recommend:* state it explicitly
in the architecture docs.

**W6 — Icon abstraction leaks platform model (LOW).** `IconType` is iOS-system-icon
shaped; Android has no such catalog, so `QuickActionItem` also carries
`AndroidDrawable`. One model object straddles two icon systems. Acceptable and
documented; a fully platform-agnostic icon type would be over-engineering for the
payoff.

**W7 — Fail-quiet error philosophy (LOW, trade-off).** Invalid items are dropped
and native exceptions are caught+logged (gated by `LoggingEnable`). This favours
host-game stability over discoverability, which is right for a store asset, but
conflicts with a strict "fail loud" rule. Keep, but ensure every silent drop logs
under `LoggingEnable` (it does).

## 7. Cross-cutting assessment

- **Threading:** all event delivery on the main thread; `SetShortcuts` dispatched
  to main on iOS. No data races in managed code. ✔
- **Performance:** O(n) list ops on a ≤4-item set; full-set push per change (fine
  at this scale). Bridge construction is cheap. No hot-path concerns. ✔
- **Memory:** one persistent GameObject; native strings freed correctly; no
  closures capturing large scopes. ✔
- **Security:** the Android trampoline is `exported` (required so the launcher can
  start it) → another app could fire a `PERFORM.<id>` action and spoof an in-app
  shortcut tap. Low impact (only triggers an in-app route). Noted in ROADMAP. ⚠
- **Extensibility:** new capability → extend `IQuickActionsBridge` + impls +
  facade; new platform → new bridge + factory branch. The seam is correct. ✔
- **Testability:** managed logic fully testable headless; native + post-processors
  are integration-tested only (inherent to the domain). ✔

## 8. Recommendations — status

| # | Change | Benefit | Status |
|---|--------|---------|--------|
| 1 | OS read-back to reconcile `_items` (W1) | Correct `GetAll`/`IsAdded` across launches | ✅ shipped (`EnsureLoaded` + `GetShortcuts` on each bridge; covered by a unit test) |
| 2 | Single pull delivery channel (W2) | Removes the asymmetry + dedup reasoning | ✅ shipped (iOS warm now enqueues; `UnitySendMessage`/`OnPerformed` removed) |
| 3 | Split post-processors into gated asmdefs (W3) | De-risks ext-DLL coupling | ✅ shipped (`Editor/iOS` + `Editor/Android` asmdefs, `defineConstraints`; main Editor asmdef no longer references the DLLs) |
| 4 | Stateless/shared bridge (W4) | Matches reality, clearer | ✅ shipped |
| 5 | Document singleton + first-frame-subscribe contract (W5) | Fewer surprises | ✅ shipped |

Remaining validation (not design issues): confirm W2's iOS focus-vs-performAction
ordering and W3's asmdef DLL resolution **on a real licensed Unity + device** —
tracked in `ROADMAP.md`.

## 9. Verdict

The architecture is **sound and well-factored for its scope**, and the seven
review items are now resolved. The standout decisions — the Android trampoline
and chained iOS swizzling — correctly solve the hardest cross-version problems;
the facade/bridge split keeps the public surface tiny and the platform code
isolated and testable; and the delivery model is now a single, uniform pull
channel with OS-reconciled state. No rewrite was needed — these were refinements
on a solid base. The only open items are device/real-Unity **validation**, not
construction.
