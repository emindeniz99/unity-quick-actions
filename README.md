# Quick Actions for iOS & Android (Unity)

Home-screen **quick actions** for Unity games — the shortcuts revealed when a
user long-presses your app icon. A clean-room, MIT-licensed equivalent of the
"Quick Actions for iOS and Android" Asset Store package, targeting **Unity 2022
LTS and newer** (including Unity 6).

| Platform | Mechanism | Min OS |
|----------|-----------|--------|
| iOS | `UIApplicationShortcutItem` (dynamic) | iOS 9 |
| Android | `ShortcutManager` dynamic shortcuts | API 25 (Android 7.1) |

- **Runtime (dynamic) API** — add/remove shortcuts from C#; the OS keeps them across launches.
- **Static shortcuts** — configure shortcuts in **Project Settings ▸ Quick
  Actions**; build post-processors bake them into `Info.plist` (iOS) and
  `shortcuts.xml` (Android) so they exist on first launch.
- **Tap callback** — `Performed` event + `LastPerformed` for cold launches —
  identical for static and dynamic shortcuts.
- **Zero native edits** — the iOS app delegate is hooked at load via the ObjC
  runtime; the Android trampoline activity is merged in from a plugin manifest.
- **Version-proof Android** — a trampoline activity instead of subclassing
  Unity's activity, so it works on both `UnityPlayerActivity` (2022) and
  `UnityPlayerGameActivity` (6+).

## Install

Pick whichever fits — all install the same package. The repo is a monorepo, so
the UPM methods point at the `projects/quick-actions-unity` subfolder.

### 1. UPM via Git URL — recommended, works for everyone

No registry, no download. **Package Manager ▸ + ▸ Add package from git URL…** and
paste (or add the line to `Packages/manifest.json` under `dependencies`):

```
https://github.com/emindeniz99/playground.git?path=projects/quick-actions-unity
```

Pin a version with a tag (recommended once tags exist), e.g.:

```
https://github.com/emindeniz99/playground.git?path=projects/quick-actions-unity#quick-actions/v0.1.0
```

(The tag is prefixed `quick-actions/v…` because this is a monorepo — see
[`plans/openupm.md`](./plans/openupm.md). No tags exist yet; the default-branch
URL above works in the meantime.)

This is the best fit for the **dev-only** workflow: the package lives read-only
under `Packages/`, and removing the one line removes it completely (see
[Dev-only](#dev-only--excluding-it-completely-from-production-builds)).

### 2. UPM via OpenUPM (scoped registry)

Once published to [OpenUPM](https://openupm.com), install with the CLI:

```bash
openupm add com.emindeniz99.quick-actions
```

…or add the scoped registry manually to `Packages/manifest.json`, then add the
package under `dependencies`:

```json
{
  "scopedRegistries": [
    {
      "name": "package.openupm.com",
      "url": "https://package.openupm.com",
      "scopes": [ "com.emindeniz99" ]
    }
  ],
  "dependencies": {
    "com.emindeniz99.quick-actions": "0.1.0"
  }
}
```

OpenUPM gives version management and update notifications in Package Manager.
(Publishing it there is a one-time setup — see [`plans/openupm.md`](./plans/openupm.md).)

### 3. Drag-and-drop `.unitypackage` (classic)

Drag [`dist/QuickActions.unitypackage`](./dist/QuickActions.unitypackage) into an
open Editor (or *Assets ▸ Import Package ▸ Custom Package…*). It installs under
`Assets/QuickActions/`. Rebuild any time with `python3 tools/pack_unitypackage.py`
(no Unity needed). This is also what Asset Store buyers get. Note: it lands in
`Assets/` (editable, not read-only), so it's less clean to fully remove than UPM.

### 4. UPM from a local clone

**Package Manager ▸ Add package from disk…** ▸ select this folder's `package.json`,
or:

```json
"com.emindeniz99.quick-actions": "file:../path/to/projects/quick-actions-unity"
```

---

After installing, import the **Demo** sample from the package page to try it on a
device. More on packaging/export: [`tools/export-unitypackage.md`](./tools/export-unitypackage.md).

## Dev-only — excluding it completely from production builds

The package is **opt-in via the `QUICKACTIONS_ENABLED` scripting define**. Without
it (the default), production stays clean — fail-safe: if you forget the define,
nothing activates.

How it's wired (managed and native need different mechanisms — Unity's Define
Constraints only work for managed code, **not** native plugins):

- **Managed C#** — the gated asmdefs carry `defineConstraints: [QUICKACTIONS_ENABLED]`,
  so the assemblies aren't compiled unless the define is set. With the define off
  there is **zero** C#.
- **iOS native** — `Plugins/iOS/QuickActions.mm` is wrapped in `#if QUICKACTIONS_ENABLED`.
  A build post-processor in the *gated* `Editor.iOS` assembly
  (`QuickActionsEnableMacroiOS`) adds the `QUICKACTIONS_ENABLED=1` macro to the
  generated Xcode project — but only runs when the define is set. With the define
  off, the macro is never added and the `.mm` compiles to **nothing**: no `+load`
  swizzle, no symbols. **Zero** iOS behaviour.
- **Android native** — an *ungated* post-processor
  (`Editor/NativeGate/QuickActionsTrampolineStripperAndroid`) removes the
  trampoline `<activity>` from the generated manifest when the define is off, so
  the trampoline can't be launched (the package is **inert**). One caveat: the
  trampoline `.java` still compiles into the APK as a small dead, unreachable
  class — Unity can't conditionally exclude a loose native source. For a
  *literally*-zero Android footprint, keep the package out of the prod project
  (see below). Both post-processors edit the **build output**, so they work for
  read-only UPM packages.

**To use it in your dev build:**

1. Add `QUICKACTIONS_ENABLED` to your **Scripting Define Symbols**. Recommended
   setup: keep it **on in the Editor** (Project Settings ▸ Player) and rely on a
   prod **Build Profile** that omits it — Build Profile defines override the
   Player setting at build time, so prod stays clean while the Editor compiles
   the package (this also avoids the "missing script" note on the settings asset,
   see below). Per-profile defines are a Unity 2022.3/6 Build Profiles feature.
2. Guard your own call sites so your game still compiles when the define is off
   and the `QuickActions` type doesn't exist:

   ```csharp
   #if QUICKACTIONS_ENABLED
   using Playground.QuickActions;
   ...
   QuickActions.Add(new QuickActionItem("new_game", "New Game"));
   #endif
   ```

For a **guaranteed-zero** prod (no dead class either), don't ship the package in
the prod project at all — e.g. install it as a UPM Git dependency only on your
dev branch/manifest. Want it **always-on** (e.g. an Asset Store release)? Remove
the `defineConstraints` from the asmdefs (or flip `tools/gen_meta.py`), drop the
`#if QUICKACTIONS_ENABLED` from the `.mm`, and delete the two gate post-processors.

> The native gating edits the generated Xcode/Gradle project and can't be
> exercised by the stub harness — verify it once in a real build. Concretely, in a
> **prod build (define off)**: the generated Xcode project should contain **no**
> `QUICKACTIONS_ENABLED` (grep the `.pbxproj`) and the merged Android manifest
> should contain **no** `QuickActionsTrampolineActivity` (it's stripped). In a
> **dev build (define on)** both are present.

> **Static-shortcuts caveat when toggling the define.** If you configured static
> shortcuts (Project Settings ▸ Quick Actions), the `QuickActionsSettings.asset`
> references an Editor script that lives in the gated assembly. Open the project
> with the define **off** and Unity reports that asset as "missing script" (the
> class isn't compiled) — harmless, and it resolves the moment you re-enable the
> define. If you don't use static shortcuts this never appears. To avoid it
> entirely, delete the settings asset before disabling the define, or keep the
> define on in the Editor and gate only your prod **Build Profile** (the
> recommended setup).

## Usage

No setup, prefab, or `Init()` call is needed — call the static `QuickActions`
API and the tap event pump self-initializes. Subscribe to `Performed` from a
script **in your first scene** (in `Awake`/`OnEnable` for safety): the
cold-launch tap is delivered one frame after startup — after the first scene's
`Awake`/`OnEnable`/`Start` have run — so an early subscriber catches it. Wire it
up later, or only in a scene loaded afterward, and you'll miss the cold-launch
tap (warm taps still arrive).

```csharp
using System.Collections.Generic;
using Playground.QuickActions;
using UnityEngine;

public class ShortcutRouter : MonoBehaviour
{
    void Awake()
    {
        // Subscribe early so the cold-launch tap (delivered next frame) isn't missed.
        QuickActions.Performed += OnShortcut;
        QuickActions.LoggingEnable = true;        // optional Debug.Log tracing

        QuickActions.Add(new QuickActionItem(
            id: "new_game", title: "New Game",
            subtitle: "Start fresh", icon: IconType.Add));

        QuickActions.AddList(new List<QuickActionItem>
        {
            new QuickActionItem("continue", "Continue", "Resume last save", IconType.Play),
            new QuickActionItem("daily",    "Daily Reward", "Claim today",   IconType.Favorite),
        });
    }

    void OnDestroy() => QuickActions.Performed -= OnShortcut;

    // Fires on every tap, including the cold launch that started the app.
    void OnShortcut(string id) => Route(id);
}
```

### API

| Member | Purpose |
|--------|---------|
| `bool IsPlatformSupported` | True on a supported device; false in-Editor (calls are safe no-ops there — test on a device). |
| `bool LoggingEnable` | Toggle `Debug.Log` tracing. |
| `event Action<string> Performed` | Tapped action id (main thread; includes cold launch). |
| `string LastPerformed` | Id the app was last launched/resumed from, or null. |
| `void ResetLastPerformed()` | Clear `LastPerformed`. |
| `bool Add(QuickActionItem)` | Add one; false if invalid or id already added. |
| `void AddList(IList<QuickActionItem>)` | Add several in one OS update. |
| `List<QuickActionItem> GetAll()` | Snapshot of the currently installed dynamic actions (OS-reconciled). |
| `QuickActionItem GetById(string)` | Lookup by id. |
| `bool Remove(QuickActionItem)` / `RemoveById(string)` | Remove one. |
| `void RemoveAll()` | Remove every action. |
| `bool IsAdded(QuickActionItem)` / `IsAdded(string)` | Membership test. |

`QuickActionItem`: `Id` (required, unique), `Title` (required), `Subtitle`,
`Icon` (`IconType`, iOS system glyph), `AndroidDrawable` (optional drawable name).

### Static shortcuts (baked into the build)

For shortcuts that must exist on the **first** launch (before any runtime
`Add`), open **Project Settings ▸ Quick Actions**, click *Create settings
asset*, and add entries. At build time:

- **iOS** — written into the Xcode `Info.plist` as `UIApplicationShortcutItems`
  (`UIApplicationShortcutItemType` = your `Id`, plus title/subtitle/system icon).
- **Android** — written to `res/xml/quickactions_shortcuts.xml` (with generated
  string resources), and the `android.app.shortcuts` meta-data is injected into
  the launcher activity. Each static intent targets the trampoline and encodes
  its `Id` in the intent action (XML shortcuts can't carry extras).

Taps are delivered through the same `Performed` / `LastPerformed` path as dynamic
shortcuts. Static and dynamic shortcuts coexist; iOS shows up to four total
(extra dynamic items beyond the cap are silently not shown by iOS). Note: static
shortcuts are **not** surfaced by the runtime query API — `GetAll()`/`IsAdded()`
see only dynamic shortcuts, so avoid reusing a static shortcut's `Id` in a
runtime `Add()`: on iOS it shows twice, and on Android the colliding dynamic item
is dropped (the rest of the set is unaffected).

## How it works

- **iOS** — `Plugins/iOS/QuickActions.mm` swizzles `UnityAppController` at
  `+load`: both cold launches (`didFinishLaunchingWithOptions:`) and warm taps
  (an injected `application:performActionForShortcutItem:completionHandler:`)
  enqueue the id. Dynamic shortcut items are set on `UIApplication.shortcutItems`.
- **Android** — `Plugins/Android/QuickActionsBridge.java` builds `ShortcutInfo`s
  whose intents target `QuickActionsTrampolineActivity`. The trampoline records
  the tapped id and brings the Unity activity forward.

Delivery is a **single pull channel**: C# drains the native queue one frame after
startup (cold) and on regained focus (warm) — no `UnitySendMessage`. The C# layer
owns the authoritative list and pushes the full set to the OS on every change;
on first access it **reconciles** that list with the shortcuts the OS already has
(from a previous session), so `GetAll()`/`IsAdded()` are accurate across launches
(icons excepted; on Android a reconciled item with no subtitle reports its title
as the subtitle, since the OS stores only the long label).

If you add more shortcuts than the OS shows (iOS caps at 4 total), the overflow is
dropped: iOS lets the OS pick; Android keeps the **first** N you added (by insertion
order) and logs the rest. Keep your most important shortcuts first.

## Security: a shortcut tap is not an authenticated action

Treat the id from `Performed` / `LastPerformed` as a **navigation hint, not an
authorization**. On Android the trampoline activity must be `exported` for the
launcher to start it, so another app on the device could fire the same intent and
spoof a tap; on either platform the id is just a string the OS hands you. So:

- **Don't wire a shortcut id directly to a destructive or privileged action**
  (delete account, spend currency, switch user) without your normal in-app
  confirmation/auth. Route the id to a screen, not to an irreversible side effect.
- `LastPerformed` is **sticky** for the session (cleared only by
  `ResetLastPerformed()`), so don't re-read it on every `OnApplicationFocus` — a
  resume that wasn't a shortcut tap (e.g. returning from a call) would replay the
  old id. Use the `Performed` event for taps, or reset after consuming.

## Limitations / roadmap

See [`ROADMAP.md`](./ROADMAP.md). Notable: per-item rasterized icons from
`Texture2D`, pinned shortcuts, and automated device CI are not implemented.
(OS read-back recovers ids/titles but not icons — reconciled items report
`IconType.None`.)

## Verification

The package is type-checked and compiled without Unity via a stub-based harness:

```bash
tools/setup.sh     # install dotnet + JDK (once; pre-baked in the devcontainer)
tools/verify.sh    # .meta gen + C# compile (7 configs) + 31 unit tests + Android plugin
```

`verify.sh` runs the unit tests via `dotnet test`; the same tests (plus
JsonUtility serialization tests) run in Unity's **Test Runner** from
`Tests/Editor/`. See [`.verify/README.md`](./.verify/README.md). A green run
proves everything compiles and the logic tests pass; on-device behaviour is
validated with the procedure in [`plans/mvp.md`](./plans/mvp.md) (iOS/Android
quick actions can't run in the Editor or on Linux).

## Notes / learnings

- Min Unity is 2022.3 LTS. The dynamic native hooks avoid editing generated
  `UnityAppController` / `UnityPlayerActivity`; only **static** shortcuts need
  build post-processors (Info.plist / shortcuts.xml).
- The iOS `.mm` is compiled by Unity against the real SDK; here it's reviewed
  and brace/structure-checked only (no Apple SDK on Linux). The C# and Android
  Java are fully compiled against stubs by `tools/verify.sh`.
