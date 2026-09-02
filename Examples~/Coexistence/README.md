# iOS coexistence mock host (CI only)

`iOS/` holds four Objective-C++ sources that CI copies into a testbed's
`Assets/Plugins/iOS/` before the Unity iOS export, so the exported app is
simultaneously:

* a `UnityAppController` **subclass** installed with `IMPL_APP_CONTROLLER_SUBCLASS`
  (`QACoexAppController.mm`) — the AppsFlyer / Braze / Singular / Branch shape;
* a **category `+load` swizzle** of the two app-delegate selectors the package owns,
  saving and chaining each original (`QACoexCategory.mm`) — the AppsFlyer swizzle-mode
  and OneSignal / Firebase-C++ shape;
* a **GoogleUtilities-style isa proxy** applied to the live delegate during
  `didFinishLaunching` (`QACoexIsaProxy.mm`) — the Firebase shape;
* an assertion **probe** that drives synthetic taps once the app is up
  (`QACoexProbe.mm`).

Nothing here ships. `Examples~` ends in `~`, so Unity ignores the folder, and it sits
outside the package's `Runtime/`, `Editor/` and `Plugins/` trees; every file is also
wrapped in `#if QUICKACTIONS_ENABLED` so a define-off build of a project that copied
them anyway compiles them to nothing.

## What it proves

* **Install ordering, measured — not required.** The category `+load` records
  whether `application:performActionForShortcutItem:completionHandler:` — a selector
  Unity never implements — already existed on `UnityAppController` when it ran
  (`category-load-ran order=class-first|category-first`), and behaves like a real
  vendor swizzle either way: it wraps and chains what it finds, or adds its own
  handler for the package to wrap later. The first run saw both orders — category
  first on the 2022.3.62f3 export, class first on 6000.3.21f1 — with every file in
  `UnityFramework`, which is why the design rests on composing in either order, not
  on winning the race.
* **Chain integrity** through a subclass, a category swizzle and an isa proxy at the
  same time: the cold call reaches the package and its `NO` comes back up through both
  wrappers; a warm tap through the proxied delegate still lands in the package's queue.
* **The cold contract**: a marked item in `launchOptions` produces exactly one queue
  entry and a `NO` return, and the host discarding that `NO` does not double-deliver.
* **Exactly-once completion**: the category wrapper owns the completion handler and
  calls it once; if the package ever completed while wrapped, the counter would read 2.
* **Cold/warm dedup, driven**: a delegate that returns YES for a launch item is also
  handed that item through the warm selector, and this host returns YES. UIKit will not
  redeliver an item the host injected into `launchOptions` itself, so the subclass sends
  the same marked item through its own warm override right after `super` returns; the
  queue must hand the id back once (`cold-warm-dedup`) and the handler must run once
  (`cold-warm-dedup-completion-once`). Without that send, "once" would hold for any
  implementation, dedup or not.
* **GoogleUtilities' own gate**: its `class_getInstanceSize` equality condition holds
  for a proxy built over the class we hooked, so this leg stops where Firebase would
  stop rather than sailing past it.
* **Scene binding** (on a scene-manifest testbed): the connected scene's delegate is a
  real `UnityScene`, `session.configuration.delegateClass` is `UnityScene`, and the
  package's warm hook is on that class — checked both when the host subclass forwards
  `application:configurationForConnectingSceneSession:options:` to super and when it
  shadows it without calling super (`SIMCTL_CHILD_QA_COEX_SHADOW_SCENE_CONFIG=1`),
  which forces the `UISceneWillConnectNotification` fallback.

## What it does NOT prove

Every tap here is a direct message send, so this shows what the package does with a
payload — never that iOS would have routed one to it. A genuine SpringBoard
long-press, the `launchOptions` / `connectionOptions` UIKit fills in for a real cold
tap, physical-device behaviour, and any Unity version outside the two testbeds are all
still unobserved. A green leg is not device coverage.

## Output contract

Every check prints one line via `NSLog`:

```
QA-COEX: PASS <name>
QA-COEX: FAIL <name> <detail>
```

plus `QA-COEX: NOTE …` for context and a closing `QA-COEX: DONE`. The
`ios-simulator-coex` job in `.github/workflows/unity-ci.yml` requires every PASS name
it expects, requires `DONE`, requires the package's own
`[QuickActions] iOS hooks: …` install line, and fails on any `FAIL` anywhere in the
log.
