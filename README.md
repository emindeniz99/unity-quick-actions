# Home-Screen Quick Actions for iOS & Android (Unity)

[![openupm](https://img.shields.io/npm/v/com.emindeniz99.quick-actions?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.emindeniz99.quick-actions/)
[![license](https://img.shields.io/badge/license-MIT-blue)](./LICENSE.md)
[![Unity](https://img.shields.io/badge/Unity-2021.3%2B-black?logo=unity)](https://unity.com)

Home-screen **quick actions** for Unity games — the shortcuts revealed when a
user long-presses your app icon (iOS calls them *Home Screen quick actions*,
Android calls them *app shortcuts*). An MIT-licensed C# wrapper over the
platforms' own public APIs — Apple's `UIApplicationShortcutItem` and Android's
`ShortcutManager` — targeting **Unity 2021 LTS and newer** (including Unity 6).

| iOS | Android |
|:---:|:---:|
| ![Long-pressing the app icon on iOS shows New Game, Continue and Daily Reward with SF Symbol icons](https://raw.githubusercontent.com/emindeniz99/unity-quick-actions/main/store~/device-ios.jpg) | ![The same three shortcuts on an Android home screen](https://raw.githubusercontent.com/emindeniz99/unity-quick-actions/main/store~/device-android.jpg) |
| iPhone (iOS 26.5) | Moto G Play 2024 (Android 14) |

The demo's three shortcuts, from the same C# code on both platforms. Note the
platform difference the screenshots make obvious: **iOS renders `Title` and
`Subtitle` on two lines with an icon; Android's launcher shows a single label**
— the *long* one, which is our `Subtitle`. Worth knowing when you write labels.
The blank icons on the Android side are the pre-0.5.0 state: Android has no
system glyph catalog, so icons come from drawables in the app — and until 0.5.0
the package shipped none. It now ships the four the demo uses (`Add`, `Play`,
`Favorite`, `Compose`), written into every build automatically; the screenshot
has not been retaken since, so nobody has seen them on a launcher yet — see
[Android icons](#android-icons).

| Platform | Mechanism | Min OS |
|----------|-----------|--------|
| iOS | `UIApplicationShortcutItem` (dynamic) | iOS 9 |
| Android | `ShortcutManager` dynamic shortcuts | API 25 (Android 7.1) |

Below those minimums the package is a **safe no-op** — every native call is
guarded (`SDK_INT < 25` returns early; the package imposes no `minSdk`
on your game), so nothing crashes: `IsPlatformSupported` reports `false`, the OS
just never shows shortcuts. One iOS nuance: on iOS 9–12 the menu needed 3D Touch
hardware; iOS 13+ opens it with a plain long-press on every device.

- **Runtime (dynamic) API** — add/remove shortcuts from C#; the OS keeps them across launches.
- **Static shortcuts** — configure shortcuts in **Project Settings ▸ Quick
  Actions**; build post-processors bake them into `Info.plist` (iOS) and
  `shortcuts.xml` (Android) so they exist on first launch.
- **Tap callback** — `Performed` event + `LastPerformed` for cold launches —
  identical for static and dynamic shortcuts.
- **Zero native edits** — the iOS app delegate is hooked at load via the ObjC
  runtime; the Android trampoline activity is injected into the generated
  Gradle manifest by a build post-processor.
- **Version-proof Android** — a trampoline activity instead of subclassing
  Unity's activity, so it works on both `UnityPlayerActivity` (2021/2022) and
  `UnityPlayerGameActivity` (6+).

## 60-second quickstart

1. **Install** — Package Manager ▸ *Add package from git URL…*:
   `https://github.com/emindeniz99/unity-quick-actions.git#v0.5.0`
2. **Turn it on** — **Window ▸ Quick Actions ▸ Enable Quick Actions** adds the
   `QUICKACTIONS_ENABLED` define for Standalone, Android and iOS. The package is
   inert without it, by design.
3. **Wire it up** from a script in your first scene — guarded, so the project
   still compiles when the define is off:

   ```csharp
   #if QUICKACTIONS_ENABLED
   using EminDeniz99.QuickActions;
   #endif
   using UnityEngine;

   public class ShortcutSetup : MonoBehaviour
   {
   #if QUICKACTIONS_ENABLED
       // Subscribe early: the cold-launch tap arrives one frame after startup.
       void Awake() => QuickActions.Performed += OnShortcut;
       // Performed is static and process-wide: never leave a handler behind.
       void OnDestroy() => QuickActions.Performed -= OnShortcut;
       
       // Fires on every tap, including the cold launch that started the app.
       void OnShortcut(string id) => Debug.Log($"Tapped: {id}");

       void Start()
       {
           QuickActions.Add(new QuickActionItem(
               id: "new_game", title: "New Game",
               subtitle: "Start fresh", icon: IconType.Add));
       }
   #endif
   }
   ```

   If that script lives in its own assembly definition, add
   `EminDeniz99.QuickActions` to the asmdef's references; `Assembly-CSharp`
   sees the package with no setup. With the define off the package's
   assemblies are not compiled at all and Unity drops the reference rather
   than failing the build — the `gate-off` CI job builds a testbed assembly of
   exactly this shape (`Examples~/Testbed2022/Assets/Integration/`) with the
   define off — so the same `#if` guards keep your assembly compiling. To keep
   the package out of your gameplay assembly altogether, put the glue in a
   small asmdef with `"defineConstraints": ["QUICKACTIONS_ENABLED"]` and keep
   `MonoBehaviour`s out of it, so no scene component goes missing when that
   assembly is not compiled.
4. **See it** — **Window ▸ Quick Actions ▸ Simulator** fires taps in the
   Editor; build to a device (or the iOS Simulator) and long-press the app icon
   for the real menu.

Longer walk-through: [GETTING_STARTED](./GETTING_STARTED.md). Handing the
integration to an AI coding agent: [AGENTS.md](./AGENTS.md).

## Contents

- [Status](#status) · [Install](#install) · [Dev-only — excluding it from production](#dev-only--excluding-it-completely-from-production-builds)
- [Usage](#usage) · [API](#api) · [Android icons](#android-icons) · [Test in the Editor](#test-in-the-editor--no-device-needed) · [Static shortcuts](#static-shortcuts-baked-into-the-build) · [Build-time placeholders](#build-time-placeholders--app-info-on-long-press)
- [How it works](#how-it-works) · [The OS shortcut cap](#known-limits--the-os-shortcut-cap) · [Host coexistence](#host-coexistence--the-package-touches-only-its-own-shortcuts) · [Localization](#known-limits--localization) · [Android build variants](#known-limits--android-build-variants-and-static-shortcuts) · [Android minification](#known-limits--android-minification-r8proguard--resource-shrinking)
- [Security](#security-a-shortcut-tap-is-not-an-authenticated-action) · [Limitations / roadmap](#limitations--roadmap) · [Verification](#verification--running-the-checks-yourself) · [Notes / learnings](#notes--learnings)

## Status

This is **0.5.0**, a pre-1.0 release. Here is exactly what has been proven and
what has not — one place, no hedging. (Per-feature detail:
[PRODUCTION_READINESS.md](./PRODUCTION_READINESS.md).)

**Verified in a licensed Unity Editor — across the supported range.**
On **2021.3.45f2** (the declared minimum) and **Unity 6.3** the package imports
with 0 console errors and the **Unity Test Runner run is green: 74/74** on
each. On both lines a **real Android APK** carries the trampoline `<activity>`
— injected on the old `UnityPlayerActivity` path for 2021.3 and on the
`UnityPlayerGameActivity` path for 6.x — and the same build with the define
removed contains **no trace of it**. On both lines the generated Xcode project
compiles against the real iOS SDK with **no warnings from the plugin**.
**2022.3.62f3** matches them in the middle of the range: it imports with 0
console errors, the Test Runner is green at **74/74**, and a real Android APK
carries the trampoline on the `UnityPlayerActivity` path. **Unity 6.0 LTS** was
verified earlier (0 console errors, Test Runner green at 35/35 — a *historical*
number, from when the suite was that size).

**Verified on the iOS Simulator (Unity 6.3).** Long-pressing the app icon shows
the static shortcuts baked into `Info.plist` alongside one added at runtime
through the C# API; tapping one cold-launches the app, and the action id
arrives on the `Performed` event. The same run is not possible on 2021.3:
Unity ships an x86_64-only simulator runtime for that line, and Apple silicon
cannot run it (Unity added arm64 Simulator support in Unity 6 and stated it
will not be backported to 2021 LTS).

**Partly verified on a physical device (Android).** On a Moto G Play 2024
(Android 14), a sideloaded build from `Examples~/Testbed2021` showed the baked
static shortcuts on a long-press of a **cold, never-opened install**, runtime
`Add` published further shortcuts, and a dynamic item whose id collided with a
static one was dropped in favour of the manifest entry — exactly as documented.
**Still not verified on hardware:** a tap arriving as `Performed` (cold or
warm), and anything at all on a physical iPhone. Plan on validating the tap path
on your own device before you ship. The 0.4.6 build-time
[placeholders](#build-time-placeholders--app-info-on-long-press) are likewise
covered by headless tests only — no device or Simulator run has happened since
they landed, so what a resolved `v1.4.0 (37)` looks like on a real home screen
is still unconfirmed.

**Also true:** the suite is 122 headless tests (`dotnet test`) and 77 in Unity's
Test Runner (it adds 6 `JsonUtility` serialization tests; 51 of the headless ones
don't run there), plus an Android Java smoke of 111 checks, across 10 C# compile
configurations with 0 warnings. The last CI-measured Test Runner result was
76/76 (run 38, 2026-09-01), taken before the sixth serialization test landed.
The iOS `.mm` compiles cleanly against the current iOS SDK
(ARC, arm64, deployment target iOS 13) with no deprecation or availability
errors — a compile result, separate from the Simulator run above.

## Install

> **⚠️ After installing — one required step:** turn the package on. Easiest is
> **Window ▸ Quick Actions ▸ Enable Quick Actions**, which adds the
> `QUICKACTIONS_ENABLED` define for Standalone, Android and iOS in one click;
> that menu item is the one piece of the package that exists while it is off.
> By hand: **Project Settings ▸ Player ▸ Scripting Define Symbols**, per
> platform tab. The package is deliberately inert without the define (that's
> its dev-only safety design — see
> [Dev-only](#dev-only--excluding-it-completely-from-production-builds)).

> New here? **[GETTING_STARTED](./GETTING_STARTED.md)** walks the whole thing
> end to end: install into a fresh project → turn the define on → wire up a
> handler → run it on a device.

> **⚠️ Pick exactly one install method.** The UPM package and the drag-and-drop
> `.unitypackage` are the *same* assemblies with the *same* asset GUIDs, so a
> project holding both fails to compile with
> `Assembly with name 'EminDeniz99.QuickActions' already exists`. Unity raises
> that at compile time, which is *before* any code of ours could run — so the
> package cannot detect the clash and warn you itself. If you see that error,
> remove one copy: delete `Assets/QuickActions/`, or drop the package line from
> `Packages/manifest.json`.

Pick whichever fits — all install the same package. `package.json` sits at the
repo root, so the UPM methods point straight at the repository.

### 1. UPM via Git URL — recommended, works for everyone

No registry, no download. **Package Manager ▸ + ▸ Add package from git URL…** and
paste (or add the line to `Packages/manifest.json` under `dependencies`):

```
https://github.com/emindeniz99/unity-quick-actions.git
```

Pin a version by appending a tag, e.g.:

```
https://github.com/emindeniz99/unity-quick-actions.git#v0.5.0
```

(Without a tag you track the default branch. `v0.4.0` is the first tag, so
pinning works from that version on.)

This is the best fit for the **dev-only** workflow: the package lives read-only
under `Packages/`, and removing the one line removes it completely (see
[Dev-only](#dev-only--excluding-it-completely-from-production-builds)).

### 2. UPM via OpenUPM (scoped registry)

Published on OpenUPM:
[**openupm.com/packages/com.emindeniz99.quick-actions**](https://openupm.com/packages/com.emindeniz99.quick-actions/).
Install with its CLI — a Node tool, so install that first
(`npm install -g openupm-cli`):

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
    "com.emindeniz99.quick-actions": "0.5.0"
  }
}
```

OpenUPM gives version management and update notifications in Package Manager.

### 3. Drag-and-drop `.unitypackage` (classic)

The same package is also **submitted to the Unity Asset Store** (Free; in
review since 2026-08-07). Once approved, the listing lives at
<https://assetstore.unity.com/packages/slug/398736> (short:
<https://u3d.as/470o>) — until then that link 404s, and the download below is
identical to what the Store will ship.

Download `QuickActions.unitypackage` from the
[**Releases** page](https://github.com/emindeniz99/unity-quick-actions/releases)
— it's a build output attached to each release by CI, not a file committed to the
repo. Drag it into an open Editor (or *Assets ▸ Import Package ▸ Custom
Package…*). It installs under `Assets/QuickActions/`. Build it yourself any time
with `python3 tools~/pack_unitypackage.py` (no Unity needed); the result lands in
the gitignored `dist~/`. Note: it lands in `Assets/` (editable, not read-only),
so it's less clean to fully remove than UPM.

### 4. UPM from a local clone

**Package Manager ▸ Add package from disk…** ▸ select this folder's `package.json`,
or:

```json
"com.emindeniz99.quick-actions": "file:../path/to/unity-quick-actions"
```

### 5. Vendor the source (embedded package)

Copy the repository — a release tag's tree, ideally — into your project as
`Packages/com.emindeniz99.quick-actions/` (the folder name must be the package
name; `package.json` sits at its root). Unity treats a folder under `Packages/`
as an **embedded package**: no `manifest.json` entry, the source is editable
and versioned with your game, and the `~`-suffixed folders (`tools~/`,
`Examples~/`, `Samples~/`) are ignored by the importer exactly as in any other
install. Upgrade by replacing the folder. Pick this if your team keeps every
dependency in the repo or expects to patch the package — it is the same
assemblies as methods 1–4, so the one-copy rule above still applies.

---

After installing, import the **Demo** sample from the package page to try it on a
device. More on packaging/export: [`tools~/export-unitypackage.md`](https://github.com/emindeniz99/unity-quick-actions/blob/main/tools~/export-unitypackage.md).

### Or skip the setup: open a ready-made project

`Examples~/` holds one consuming Unity project per supported editor line —
[`Testbed2021`](https://github.com/emindeniz99/unity-quick-actions/tree/main/Examples~/Testbed2021), [`Testbed2022`](https://github.com/emindeniz99/unity-quick-actions/tree/main/Examples~/Testbed2022)
and [`Testbed6`](https://github.com/emindeniz99/unity-quick-actions/tree/main/Examples~/Testbed6) — each with the package linked, the
define set and three static shortcuts already configured. Clone the repo, open
the one matching your editor, and compare it against your own integration. See
[`Examples~/README.md`](https://github.com/emindeniz99/unity-quick-actions/blob/main/Examples~/README.md).

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
- **Android native** — a post-processor in the *gated* `Editor.Android`
  assembly (`QuickActionsTrampolineInjectorAndroid`) injects the trampoline
  `<activity>` into the generated Gradle manifest — Unity never merges a loose
  `AndroidManifest.xml` from inside a UPM package, so build-time injection is
  the mechanism, and it only runs when the define is set. With the define off
  nothing is injected, and an *ungated* post-processor
  (`Editor/NativeGate/QuickActionsTrampolineStripperAndroid`) additionally
  strips any pre-existing entry (defense in depth), so the trampoline can't be
  launched (the package is **inert**). One caveat: the two plugin `.java` files
  (the trampoline and the bridge, ~20 KB of bytecode together) still compile
  into the APK as dead, unreachable classes unless R8 minification removes
  them — Unity can't conditionally exclude a loose native source. The `gate-off`
  CI job's APK diff reports exactly what remains. For a *literally*-zero Android
  footprint, keep the package out of the prod project (see below). All these
  post-processors edit the **build output**, so they work for read-only UPM
  packages.

**To use it in your dev build:**

1. Add `QUICKACTIONS_ENABLED` to your **Scripting Define Symbols**. On **Unity 6**
   put it in a **dev Build Profile** (Build Profiles ▸ your dev profile ▸ Scripting
   Define Symbols) and keep it **out of the shared Player Settings**. Build Profile
   symbols are **additive** on top of Player Settings — they can *add* a symbol but
   [cannot *remove*](https://docs.unity3d.com/6000.1/Documentation/Manual/custom-scripting-symbols.html)
   one inherited from Player Settings — so defining it only in the dev profile means
   prod profiles (which don't add it) build **without** it. ⚠️ Do **not** put it in
   the shared Player Settings and expect a prod Build Profile that merely *omits* it
   to drop it: the symbol is inherited additively and stays on, leaving the gate
   active in the prod build. If it is in Player Settings you must **delete it there**
   before a prod build. On **2021/2022 LTS** (no Build Profiles) add/remove it in
   Player Settings ▸ Scripting Define Symbols before cutting the prod build.
   ⚠️ **CI scripts: `BuildPlayerOptions.extraScriptingDefines` is NOT enough** —
   Unity applies extra defines to the *player* compilation only, never to
   *editor* scripts, so the gated build post-processors (Android trampoline
   injector, iOS macro adder) would not compile and the build would contain
   runtime code with **no** trampoline / no `QUICKACTIONS_ENABLED=1` macro.
   Scripts must set the define in Player Settings or the active Build Profile
   in a **prior editor invocation** (so editor scripts recompile), then build.
   **Always verify** the built project has no
   `QUICKACTIONS_ENABLED` (see the grep checks below); that is the only guarantee.
2. Guard your own call sites so your game still compiles when the define is off
   and the `QuickActions` type doesn't exist:

   ```csharp
   #if QUICKACTIONS_ENABLED
   using EminDeniz99.QuickActions;
   ...
   QuickActions.Add(new QuickActionItem("new_game", "New Game"));
   #endif
   ```

For a **guaranteed-zero** prod (no dead class either), don't ship the package in
the prod project at all — e.g. install it as a UPM Git dependency only on your
dev branch/manifest. Prefer it **always-on** in your own fork (no define, active
on import)? Remove the `defineConstraints` from the asmdefs, drop the
`#if QUICKACTIONS_ENABLED` from the `.mm`, and delete the two gate post-processors
— the gate is this package's default and its point, but the conversion is three
mechanical steps if your project wants the opposite trade-off.

> The native gating edits the generated Xcode/Gradle project and can't be
> exercised by the stub harness. CI does the real-build check on every code
> push: the `gate-off` job in [`unity-ci.yml`](https://github.com/emindeniz99/unity-quick-actions/blob/main/.github/workflows/unity-ci.yml)
> builds the 2022.3 testbed with the define **off** — an IL2CPP APK and an iOS
> Simulator export — and requires the `.pbxproj` to contain **no**
> `QUICKACTIONS_ENABLED`, the merged Android manifest **no**
> `QuickActionsTrampolineActivity` and no shortcuts meta-data, the resource
> table nothing of the package's, and the IL2CPP metadata no
> `EminDeniz99.QuickActions` — each against the define-**on** build from the same
> run as a positive control. It then diffs the two APKs entry by entry (plus the whole-file size): that
> number, printed in the job summary, is the package's footprint (held under
> 1 MiB). To repeat it by hand, do the same in your own project.

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

No setup beyond the define — no prefab or `Init()` call; use the static `QuickActions`
API and the tap event pump self-initializes. Subscribe to `Performed` from a
script **in your first scene** (in `Awake`/`OnEnable` for safety): the
cold-launch tap is delivered one frame after startup — after the first scene's
`Awake`/`OnEnable`/`Start` have run — so an early subscriber catches it. Wire it
up later, or only in a scene loaded afterward, and you'll miss the cold-launch
tap (warm taps still arrive).

```csharp
#if QUICKACTIONS_ENABLED
using System.Collections.Generic;
using EminDeniz99.QuickActions;
#endif
using UnityEngine;

public class ShortcutRouter : MonoBehaviour
{
#if QUICKACTIONS_ENABLED
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
#endif
}
```

The `#if` guards keep the game compiling in a build where the define is off —
the recommended production setup (see [Dev-only](#dev-only--excluding-it-completely-from-production-builds)) —
and they wrap only the package-specific parts: the `MonoBehaviour` itself stays
compiled, so the component on your scene object is an inert router there, not a
missing script.
If the script lives in its own assembly definition, add
`EminDeniz99.QuickActions` to that asmdef's references (and
`EminDeniz99.QuickActions.Editor` to an Editor asmdef that uses the build-time
hooks); scripts in `Assembly-CSharp` see the package with no setup. That
reference resolves to nothing with the define off and Unity drops it rather
than failing the build — CI compiles a testbed assembly of that shape both
ways (see the [quickstart](#60-second-quickstart)); a gated glue asmdef
(`"defineConstraints": ["QUICKACTIONS_ENABLED"]`, no `MonoBehaviour`s in it)
is the alternative for a project that wants the package out of its gameplay
assembly entirely.

### API

| Member | Purpose |
|--------|---------|
| `bool IsPlatformSupported` | True on a supported device; false in-Editor **and on Android < 7.1 / API 25** (all calls are safe no-ops there — in-Editor, use the [Simulator](#test-in-the-editor--no-device-needed)). |
| `bool LoggingEnable` | Toggle `Debug.Log` tracing. |
| `event Action<string> Performed` | Tapped action id (main thread; includes cold launch). |
| `string LastPerformed` | Id the app was last launched/resumed from, or null. |
| `void ResetLastPerformed()` | Clear `LastPerformed`. |
| `bool Add(QuickActionItem)` | Add one; false if invalid, id already added, or the OS set couldn't be read / the OS rejected the write (transient — retry later). A `null` item throws `ArgumentNullException` — the only way any call here throws. |
| `void AddList(IList<QuickActionItem>)` | Add several in one OS update (same transient no-op cases as `Add`; a `null` list throws). |
| `List<QuickActionItem> GetAll()` | Snapshot of the currently installed dynamic actions (OS-reconciled). |
| `QuickActionItem GetById(string)` | Lookup by id. |
| `bool Update(QuickActionItem)` | `null` throws `ArgumentNullException`. Replace the added action with the same `Id` **in place** — list position (launcher rank) preserved, one OS update, Android user-pinned copies refresh too. False when not added (use `Add`), invalid, the OS set couldn't be read, or the OS refused the write (all leave the previous item in place) — or when the OS **dropped** the pushed item (budget shrank; the shortcut is then gone, re-`Add` when there's room). |
| `bool Remove(QuickActionItem)` / `RemoveById(string)` | Remove one. |
| `void RemoveAll()` | Remove every action. |
| `bool IsAdded(QuickActionItem)` / `IsAdded(string)` | Membership test. |
| `int MaxShortcutCount` | The OS shortcut budget: Android `getMaxShortcutCountPerActivity`; iOS 4 (display limit, no OS query). Shared with static shortcuts on **both** platforms (and with host-published dynamic ones on Android), so fewer slots may be free. 0 in-Editor. |
| `bool IsPinSupported` | True when the launcher can pin shortcuts (Android 8.0+; always false on iOS/Editor). |
| `bool RequestPin(string)` | Ask the launcher to pin an **added** action to the home screen. True = request *dispatched* (the user still confirms in launcher UI — the OS reports no outcome). |
| `bool ReportUsed(string)` | Tell the launcher the user reached this action's feature **in-app** (Android `reportShortcutUsed`, improves ranking predictions). Call on normal-UI usage, not on shortcut taps. False on iOS/Editor (no analog), for a not-added id, or when the native call failed. |
| `string Locale` | The locale labels resolve against (defaults to the device language via `Application.systemLanguage`). Set it from an in-app language picker — a **different** value re-pushes the current set so the launcher re-renders immediately. A device-language change while the app was closed is caught on next launch: the cold-start reconcile detects stale labels and refreshes them with one push. If the OS refuses that push (Android rate-limits writes while backgrounded), the managed list stays authoritative — only the on-screen labels are stale — and exactly **one** retry is attempted on the next list call; after that the labels are fixed by your next successful `Add`/`Update`/`Remove`, so read-only calls never turn into a stream of OS writes. |

`QuickActionItem` fields:

| Field | Purpose |
|-------|---------|
| `Id` (required, unique) / `Title` (required) / `Subtitle` | Labels. `Subtitle` renders under the title on iOS and as the Android long label. In **static** (baked) items both may embed build-time `{placeholders}` — see [Build-time placeholders](#build-time-placeholders--app-info-on-long-press). |
| `Icon` (`IconType`) | Built-in glyph catalog (29 entries). iOS uses Apple's system icons — nothing to ship. Android resolves a drawable by name — your `ic_quickaction_<name>` first, then the package's own `ic_quickaction_builtin_<name>`: **four ship built in** (`Add`, `Compose`, `Favorite`, `Play`), the other 25 need a drawable **you add**. Without one the launcher shows a blank square. See [Android icons](#android-icons). |
| `IosSystemImage` | SF Symbol name (`"star.fill"`, iOS 13+) — beats `IosTemplateImage` and `Icon`. Ignored on Android. |
| `IosTemplateImage` | Template-image name shipped in the Xcode bundle (single-color, ~35×35 pt) — beats `Icon`. Ignored on Android. |
| `AndroidBitmapFile` | Absolute path to a PNG/JPEG on device — runtime icons from a `Texture2D`: `File.WriteAllBytes(path, tex.EncodeToPNG())` under `Application.persistentDataPath` (keep the file alive; the launcher re-reads it). Beats `AndroidDrawable` and `Icon`. Ignored on iOS (no runtime-bitmap shortcut API). |
| `AndroidBitmapAdaptive` | Install `AndroidBitmapFile` as an adaptive icon (API 26+, launcher-masked; supply safe-zone padding). |
| `AndroidDrawable` | Drawable resource name overriding the `Icon` lookup. Ignored on iOS. A name outside the `ic_quickaction_*` catalog **used only from a runtime `Add(...)`** needs your own keep rule under minification (a **static** item's name is baked as a real `@drawable` reference the shrinker follows) — see [Known limits](#known-limits--android-minification-r8proguard--resource-shrinking). |
| `Payload` | App-defined string riding the shortcut (iOS `userInfo`, Android extras), restored across cold starts. Not pushed with the tap — read it via `GetById(id)?.Payload` from the id `Performed` reports (`GetById` is null for a **static**-shortcut tap or an id removed since: static items never join the runtime list and carry no payload). |
| `LocalizedTitles` / `LocalizedSubtitles` | Per-locale label replacements (`LocalizedText { Locale, Text }` pairs). Resolution: exact locale match > language prefix (`"pt-BR"` matches a `"pt"` entry) > base `Title`/`Subtitle`, case-insensitive. The tables survive cold starts (they ride the ownership-marker payload), so labels re-resolve after a device-language change. Static (baked) shortcuts localize on **Android only** (`values-<qualifier>/` string resources); iOS static shortcuts render in their base language — see "Known limits". |

### Android icons

`IconType` resolves differently per platform. **iOS** maps it to Apple's
built-in `UIApplicationShortcutIconType` catalog — the OS owns those glyphs,
nothing to ship. **Android has no system glyph catalog**, so the same
`IconType` resolves to a drawable *name* looked up in the app — yours first,
then the package's own:

```java
getIdentifier("ic_quickaction_add", "drawable", pkg)          // your drawable, if any
getIdentifier("ic_quickaction_builtin_add", "drawable", pkg)  // else the built-in
```

**Four catalog entries ship built in** — `Add`, `Compose`, `Favorite` and
`Play`, the ones the demo uses. On every Android build with the define on, the
package's post-processor writes them into the generated Gradle project
(`unityLibrary/src/main/res/`), next to the keep rule that carries them through
resource shrinking. Each is **one resource name in two variants**, and the
resource qualifier — not anything at build time — picks between them:

```
res/drawable/ic_quickaction_builtin_add.xml              API 25: white glyph on an indigo disc
res/drawable-anydpi-v26/ic_quickaction_builtin_add.xml   API 26+: <adaptive-icon>, two layers
res/drawable/ic_quickaction_builtin_add_background.xml     full-bleed indigo
res/drawable/ic_quickaction_builtin_add_foreground.xml     the glyph, inside the mask's safe zone
```

The API 25 file carries its own contrast because API 26+ launchers wrap a
*legacy* shortcut drawable onto a white plate, where a glyph alone would vanish
— but that same wrap also scales it to 0.70 of the plate, so from API 26 the
`-v26` variant takes over and the launcher masks a full-bleed icon instead.
All XML, density-independent, about 6 KB for all four icons and their layers.
A **static** item with one of those four as its `Icon` and no `AndroidDrawable`
bakes a reference to the built-in — the one name, so it gets the right variant
per device — and renders on a cold install too. Every CI build reads all four
(and their layers) back out of the APK with `aapt2`, the shrink experiment
holds them unchanged through `shrinkResources`, and the emulator smoke requires
the registered shortcuts to have resolved an icon resource; what nobody has
done yet is look at either variant on a launcher — the screenshot at the top of
this file predates them.

**The other 25 entries stay blank until you add a drawable**, and so does any
built-in one you would rather draw yourself. The settings page says which is
which next to every `Icon` field — "built-in drawable" for the four, or the
exact `ic_quickaction_<name>` a member needs — so you learn it while
configuring, not from a build-log warning. Create an **Android Library
plug-in** anywhere under `Assets/` (Unity's supported mechanism on 2021.3,
2022.3 and 6.x alike — the import instructions are identical across all three):

```
Assets/QuickActionIcons.androidlib/
  AndroidManifest.xml              <manifest package="com.yourcompany.qaicons"/>
  res/drawable-xhdpi/ic_quickaction_search.png
```

Then either name the drawable `ic_quickaction_<icontype>` so `Icon` finds it, or
point at any resource explicitly with `AndroidDrawable = "my_icon"`. Draw it
with its own background — a white glyph on transparent is invisible on the
white plate API 26+ launchers wrap it onto (`store~/example-shortcut-icons/`
shows the four built-ins as PNGs, in the style that works).

**Your drawable always wins — by name, not by luck.** The built-ins live under
their own prefix, `ic_quickaction_builtin_`, so the package never writes,
overwrites or even looks for a file under yours; the two coexist in the merged
resources and the runtime lookup asks for `ic_quickaction_<name>` first. That
holds however your drawable reaches the build — an `.androidlib`, an `.aar`, a
Maven dependency in `mainTemplate.gradle` — because nothing depends on the
package seeing it. One asymmetry to know: a **static** item cannot be resolved
at runtime, so with `Icon` alone it bakes the built-in; to bake yours instead,
set `AndroidDrawable = "ic_quickaction_<name>"` on that item (or any name you
like). Want no package art in your APK at all? Untick **Write built-in Android
icons** in *Project Settings ▸ Quick Actions*: nothing is written, a copy an
earlier build left behind is removed, and those four render blank unless you
ship your own. A define-off production build carries none of this either way.

Three traps worth knowing:

- Manifest and resources sit at the **root** of a bare `.androidlib` — one
  with no `build.gradle` of its own — because that is the module layout Unity
  generates for it. The Gradle-module layout, `src/main/res/`, is **silently
  ignored** there: green build, no warning, no icon. It works only when the
  `.androidlib` ships its own `build.gradle`, which is how Unity's
  `com.unity.mobile.notifications` gets away with it. The first version of
  this recipe said the opposite; the `android-build` job's 2022.3 leg measured
  it — a drawable under `src/main/res/` never reached the APK, one under `res/`
  did — and now plants a decoy under `src/main/res/` that must stay absent on
  every push.
- **Do not** use `Assets/Plugins/Android/res/`. Unity **removed** that path in
  2021.2, below this package's floor, and it now fails the build outright
  rather than being ignored.
- **Minified release builds** can strip a drawable's bytes while leaving its
  resource entry behind, so the icon goes blank in release only — the package
  ships a keep rule for `ic_quickaction_*` names, and a **static** shortcut's
  `AndroidDrawable` bakes a real `@drawable` reference the shrinker follows.
  Only a custom name used **only** from a runtime `Add(...)` needs your own; see
  [Known limits](#known-limits--android-minification-r8proguard--resource-shrinking).

### Test in the Editor — no device needed

Quick actions don't appear in the Editor (there's no home screen), but you don't
need a device to test your **tap handling**. Open **Window ▸ Quick Actions ▸
Simulator** and click any listed shortcut (or type a custom id) — it raises
`Performed` exactly as a real tap does, so your routing code runs
(`LastPerformed` updates too). Two modes, matching the device:

- **In Play Mode** → delivered immediately (a *warm* tap).
- **Not in Play Mode** → it **starts Play Mode and delivers at startup** through
  the runtime's real pending-queue drain, exactly like tapping the icon while the
  app is closed (a *cold launch*).

Use this fast loop while coding; build to a device — or, on iOS, the Simulator,
where the long-press menu works — only to verify the OS menu itself.
(Editor-only — it never ships in a build.)

### Static shortcuts (baked into the build)

For shortcuts that must exist on the **first** launch (before any runtime
`Add`), open **Project Settings ▸ Quick Actions**, click *Create settings
asset*, and add entries. The asset is created at
`Assets/Settings/QuickActionsSettings.asset` — commit it with the project. It
is found by type, not path, so it can live anywhere under `Assets/` (the
testbeds keep theirs under `Assets/QuickActions/`); deliberately not the
`.unitypackage` install folder, so a re-import never deletes it. At build time:

<img src="https://raw.githubusercontent.com/emindeniz99/unity-quick-actions/main/store~/device-android-dynamic.jpg" alt="Four shortcuts on Android after the demo added two at runtime" width="240" align="right">

Static and dynamic shortcuts coexist. The screenshot on the right is the same
Android device after the demo added shortcuts at runtime: the two dynamic
items whose ids collided with static ones were dropped in favour of the
manifest entries, the dynamic-only `daily` and `settings` took the remaining
slots, and static `daily_reward` fell off the end at the launcher's four-item
cap — see [Known limits](#known-limits--the-os-shortcut-cap) and
[Host coexistence](#host-coexistence--the-package-touches-only-its-own-shortcuts).


- **iOS** — written into the Xcode `Info.plist` as `UIApplicationShortcutItems`
  (`UIApplicationShortcutItemType` = your `Id`, plus title/subtitle and one icon
  key: SF Symbol > template image > system icon type). The settings page also
  takes a **template-image texture list**: each PNG/JPEG is copied into the
  generated Xcode project's app target, so template art ships for static *and*
  dynamic shortcuts with no manual Xcode step — a PNG resolves as
  `IosTemplateImage = "<file name without extension>"`, a JPEG must include its
  extension (bare-name bundle lookup is PNG-only; prefer PNG). Stale copies
  from a previous Append build are cleaned up via a manifest — only files this
  package copied are ever touched.
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

### Build-time placeholders — app info on long-press

Static titles and subtitles may embed `{placeholder}` tokens; the build
post-processors resolve them while baking `Info.plist` / `shortcuts.xml`, so
the resolved text exists from the first install, before the app ever runs. The
classic use is a build-info shortcut for development: **Project Settings ▸
Quick Actions** has an **"Add app info shortcut"** button that appends

```text
Id: app_info    Title: App info    Subtitle: v{version} ({build})
```

and the baked subtitle then reads e.g. `v1.4.0 (37)` — which build is on this
device, answerable from a long-press without launching the app. The version
belongs in the **subtitle** because that is the line long-press actually shows:
Android launchers render the long label (the subtitle), iOS shows title and
subtitle.

Built-in tokens (matched case-insensitively):

| Token | Bakes to |
|-------|----------|
| `{version}` | `PlayerSettings.bundleVersion` — what `Application.version` reports at runtime. |
| `{build}` | iOS: `PlayerSettings.iOS.buildNumber` (`CFBundleVersion`). Android: `PlayerSettings.Android.bundleVersionCode`. Left unresolved on any other target. |
| `{bundleId}` | iOS: `PlayerSettings.applicationIdentifier`. Android: the **Gradle-resolved** `applicationId` — the same id the static intent targets, so the label and the intent can never disagree. |
| `{productName}` | `PlayerSettings.productName`. |
| `{unityVersion}` | The Editor version building the player. |
| `{platform}` | `iOS` / `Android`. |

Rules: `{{` / `}}` produce a literal brace; an unknown token is left verbatim
(the settings page and the build log both warn); anything not token-shaped —
`{}`, `{a b}`, an unclosed `{` — passes through untouched. One upgrade caveat:
a pre-0.4.6 label that happens to contain a known token name (a literal
`{version}`) or doubled braces IS now interpolated/escaped — double the braces
(`{{version}}`) to keep such text literal. Localized titles/subtitles are
interpolated too (relevant on Android; iOS static items don't localize — see
[Known limits](#known-limits--localization)). Values are
frozen into that build and change only on the next one — for version info,
that's the point.

Two editor-script hooks extend this (both editor-only, in
`EminDeniz99.QuickActions.Editor`):

**Custom placeholders** — any value you can compute at build time (build date,
git hash, CI run number, an env var…):

```csharp
using UnityEditor;
using EminDeniz99.QuickActions.Editor;

[InitializeOnLoad]
static class MyBuildPlaceholders
{
    static MyBuildPlaceholders()
    {
        QuickActionsStaticBuild.RegisterPlaceholder("buildDate",
            () => System.DateTime.UtcNow.ToString("yyyy-MM-dd"));
        QuickActionsStaticBuild.RegisterPlaceholder("ci",
            () => System.Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER") ?? "local");
        // then e.g. Subtitle: "v{version} ({build}) · {buildDate} · #{ci}"
    }
}
```

Resolvers run once per build, at bake time. One that throws never fails the
build — the build log warns and the token falls back: it stays verbatim for a
new name, or keeps the built-in value when the resolver shadowed one. Custom
names win over built-ins (you can redefine `{version}`). A token that resolves
to an **empty title** would make the bakers skip that shortcut, so the build
log warns about that too.

**The `Customize` hook** — rewrite the baked set in code; e.g. ship the
app-info shortcut in development builds only:

```csharp
[InitializeOnLoad]
static class DevOnlyAppInfo
{
    static DevOnlyAppInfo()
    {
        QuickActionsStaticBuild.Customize += ctx =>
        {
            if (ctx.DevelopmentBuild)
                ctx.Shortcuts.Add(new QuickActionItem(
                    "app_info", "App info", "v{version} ({build})"));
        };
    }
}
```

`ctx.Shortcuts` is the exact list about to bake (copies — the settings asset is
never touched): add, remove, reorder or edit freely. Added items get
placeholder resolution too, and the hook also runs — with an empty list — in a
project that has no settings asset at all, so a whole static set can be defined
in code. A subscriber that throws fails the build, deliberately: baking a
half-customized set into a release would be worse.

This is a **static-shortcut** feature. Dynamic shortcuts don't need it — build
their strings at runtime with ordinary C# interpolation
(`new QuickActionItem("info", $"v{Application.version}")`); the platform build
number has no Unity runtime API, which is exactly why the static tokens resolve
in the Editor. The in-Editor [Simulator](#test-in-the-editor--no-device-needed)
previews built-in tokens using the active build target's Player Settings;
custom placeholders and `Customize` subscribers run only in real builds, so
their tokens show raw there.

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
(icon identity survives the reconcile on both platforms — the OS can't read
icons back, so it rides in the ownership-marker payload: `ShortcutInfo` extras
on Android, `userInfo` on iOS. A reconciled item with no subtitle reports
an empty subtitle on both platforms — Android leaves the OS long label unset
for it).

### Known limits — the OS shortcut cap

If you add more shortcuts than the OS shows (iOS caps at 4 total; Android at least
5, shared with any static shortcuts **and any dynamic shortcuts the host app itself
published** outside this package), the overflow is dropped on the device: iOS
lets the OS pick; Android keeps the **first** N you added (by insertion order) and
logs the rest (the log says how many slots host shortcuts took). Keep your most
important shortcuts first.

The **managed** list stays consistent with what the OS accepted: when Android trims
the overflow, those ids are pruned from the managed list in the same call, so
`GetAll()` / `IsAdded()` reflect what's actually on the device (they don't
over-report), while the icons you supplied are preserved for the shortcuts that were
kept. If the id you add is the one that doesn't fit, `Add` returns **`false`** — the
OS never installed it, so `GetAll()` / `IsAdded()` would immediately contradict a
`true`. `AddList` lands the ids that fit and logs each one it had to drop. Check
`QuickActions.MaxShortcutCount` and keep the set within it so nothing is
silently dropped (on Android the budget is shared, so fewer slots may be free).

### Host coexistence — the package touches only its own shortcuts

Every shortcut this package creates is stamped with an ownership marker (iOS:
a `UIApplicationShortcutItemUserInfo` key; Android: a `ShortcutInfo` extras
key — both `com.emindeniz99.quickactions.managed`). Writes, `RemoveAll()`, and
the cold-start reconcile operate **only on that marked subset**: quick actions
the host app published itself (its own `shortcutItems` / `ShortcutManager`
entries, or another plugin's) are never absorbed into `GetAll()`, never
republished with this package's intents, and never removed. Three consequences:

- **The cap is shared** (Android): host shortcuts take slots from the same
  per-activity budget, so an `Add` can be refused for lack of room even when this
  package holds fewer than the cap — it returns `false` and logs how many slots
  host items took.
- **Coexistence is one-sided on Android**: this package uses the subset APIs
  (`addDynamicShortcuts`/`removeDynamicShortcuts`), but a host that itself calls
  `setDynamicShortcuts(...)` replaces the **entire** dynamic set — including this
  package's items (they reappear on the next `Add`/push, with icons, but taps
  in between are lost). A coexisting host should use the additive APIs too.
- **Ordering**: this package ranks its items by your insertion order starting at
  0; launchers interleave host items by their own ranks, so the exact combined
  order across publishers is launcher-dependent.
- **Pinned copies on remove** (Android): if the user pinned one of this
  package's shortcuts, `RemoveById`/`RemoveAll` also **disable** the pinned
  copy (the launcher greys it out — Android never lets an app delete a user's
  pin outright). Re-adding the same id re-enables it. A host app's pinned
  shortcuts are never touched.
- **Same-id collisions**: on Android a colliding host id (dynamic **or pinned**)
  wins — the package drops its own item with a warning rather than update the
  host's entry in place. On iOS an unmarked same-id item is always preserved
  (the id then renders twice — the honest result of two publishers claiming
  one id); the package never removes anything it didn't mark.

### Known limits — localization

**Dynamic** shortcuts localize on both platforms: the label is resolved in C# at
push time and the per-locale tables ride along in the ownership-marker payload, so
they survive cold starts and re-render after a language change.

**Static** (baked) shortcuts localize on **Android only**, via generated
`values-<qualifier>/quickactions_strings.xml` files — the package's own file name
inside the shared resource folders, so a host app's `strings.xml` is never touched.
Two rows for one locale keep the first and warn (aapt2 rejects duplicate resource
names), and locale tags are normalised to one canonical qualifier ("zh-Hans" and
"zh-hans" are one directory).

On **iOS**, static shortcuts render in their **base language**. iOS localizes an
Info.plist value through `<locale>.lproj/InfoPlist.strings` in the bundle root — a
path where both components are fixed by the platform — so shipping one would collide
with any app that localizes its own display name or usage strings: a
"Multiple commands produce…" build failure, or a silent overwrite of files the
package doesn't own. Localize an iOS shortcut label by adding it at runtime with
`QuickActions.Add(...)` instead.

### Known limits — Android build variants and static shortcuts

The **static** Android shortcuts baked at build time encode an explicit intent
targeting the trampoline with your app's `applicationId` (read from the launcher
`build.gradle`). If you ship variants that change the final package name via
`applicationIdSuffix` or flavor-specific `applicationId`, the baked intent still
carries the **base** id (Android does not substitute the Gradle `${applicationId}`
placeholder inside `res/xml`), so a static shortcut in that variant's APK may target
the wrong package. For variant builds, prefer **runtime** shortcuts
(`QuickActions.Add(...)`) — they build the intent against the running app, so they're
always variant-correct. (Single-`applicationId` projects — the common case — are
unaffected.)

### Known limits — Android minification (R8/ProGuard + resource shrinking)

**Code (R8/ProGuard).** The C# runtime reaches the Java helper `com.emindeniz99.quickactions.QuickActionsBridge`
**by name** over JNI. If you build a **minified** dev/QA build (Player Settings ▸
Publishing Settings ▸ *Minify*), R8 can rename or strip that non-manifest class, and
the JNI lookup then fails so shortcuts silently don't get set. Add a keep rule to
`Assets/Plugins/Android/proguard-user.txt`:

```proguard
-keep class com.emindeniz99.quickactions.** { *; }
```

(The trampoline `<activity>` is kept automatically because it's declared in the
manifest — only the JNI-only bridge needs this. Most dev builds don't enable
minification, so this only matters if yours does.)

**Resources (`shrinkResources`) — icons.** Icon drawables are reached *only*
through `getIdentifier(name, "drawable", …)`, so with `minifyEnabled` +
`shrinkResources` nothing statically references them and the shrinker may
**dummy-replace** them: the file's bytes are swapped for a tiny placeholder
while the resource-table entry survives. `getIdentifier` therefore still returns
non-zero, `setIcon` is still called, and the launcher draws a **blank icon** — in
release builds only, looking exactly like the un-configured state.

The package handles the catalog names for you: on every Android build with the
define on it writes `res/raw/quickactions_keep.xml` carrying
`tools:keep="@drawable/ic_quickaction_*"` into the generated Gradle project, so
every `ic_quickaction_<name>` drawable survives even **strict** shrink mode —
which any *one* library in your app can switch the whole app into, with no way
for a package to opt out. No action needed for `ic_quickaction_*` names — the
built-in `ic_quickaction_builtin_*` ones included, same glob.

A **custom `AndroidDrawable`** name used only from a runtime `Add(...)` is
different: it has no static reference *and* never appears as a string constant in
the compiled code (it arrives from C# at runtime), so no shrinker heuristic can
retain it. Either:

- name your drawable with the `ic_quickaction_` prefix, and the shipped rule
  covers it; or
- ship your own keep rule inside your `.androidlib` (see
  [Android icons](#android-icons)) —
  `res/raw/myapp_keep.xml` (at the `.androidlib` root, next to its manifest):

  ```xml
  <?xml version="1.0" encoding="utf-8"?>
  <resources xmlns:tools="http://schemas.android.com/tools"
      tools:keep="@drawable/my_icon" />
  ```

  Give it a **unique file name** (`myapp_keep.xml`, not `keep.xml`): keep files
  merge globally by name, so a generic name collides with the host app's or
  another library's.

`AndroidBitmapFile` is unaffected — it is a path to a file on disk, not a
resource, so the shrinker never sees it.

The keep file's **emission** is covered by headless tests (it is written,
well-formed, namespaced, idempotent, and survives the static-shortcut cleanup),
and that a real shrinker **honors** it is no longer an assumption: CI's
`android-shrink-verify` job runs the AGP resource shrinker on a Unity 2022.3
export with `minifyEnabled` + `shrinkResources` and two planted drawables. On
2026-08-29 the keep-globbed probe came out byte-identical (990 → 990 bytes)
while the unreferenced control was replaced by AGP's 67-byte dummy — the
control proving the shrinker ran, the probe proving the rule held. It re-runs
on every code push.

## Security: a shortcut tap is not an authenticated action

> Found a vulnerability? Please report it privately — see
> [SECURITY.md](./SECURITY.md).

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

See [`ROADMAP.md`](./ROADMAP.md). Notable remaining: always-on device CI (the
shipped adb smoke runs on every code push and PR but covers Android alone — warm *and* cold taps, neither yet run on physical hardware; iOS has no
adb analog) and on-device
validation of the newest native paths (UIScene hooks — including the
subclass-shadowed fallback — and the Android localized static output). OS read-back can't recover icons natively; the package persists icon
identity in its ownership-marker payload — Android extras, iOS `userInfo` — so
reconciled items keep their icons on both platforms.

## Verification — running the checks yourself

*What has actually been proven is stated once, under [Status](#status) above,
and per feature in [PRODUCTION_READINESS.md](./PRODUCTION_READINESS.md). This
section is only how to re-run the checks.*

The package is type-checked and compiled without Unity via a stub-based harness:

```bash
tools~/setup.sh     # install dotnet + JDK (once)
tools~/verify.sh    # .meta + C# compile (11 configs) + unit tests + Android plugin + frozen strings + release coherence
```

`verify.sh` compiles the C# in **11 configurations** (0 warnings), runs the **122**
headless unit tests via `dotnet test`, and compiles and smoke-tests the Android
Java plugin (**111** checks). Those tests (bar 51 headless-only ones) plus 6
`JsonUtility` serialization tests run in Unity's **Test Runner** from
`Tests/Editor/` — **77** there. See [`.verify/README.md`](https://github.com/emindeniz99/unity-quick-actions/blob/main/.verify/README.md)
for how the stubs work.

Beyond the stubs, [`unity-ci.yml`](https://github.com/emindeniz99/unity-quick-actions/blob/main/.github/workflows/unity-ci.yml) runs the
**real editors** via [GameCI](https://game.ci). Every leg runs on every code
push and PR that touches code (docs-only changes trigger nothing): the
EditMode suite on all three [`Examples~`](https://github.com/emindeniz99/unity-quick-actions/tree/main/Examples~) testbeds, plus a
`unity6-latest` canary leg that resolves the newest Unity 6 editor with a
GameCI image at run time and upgrade-opens Testbed6 with it — so "the latest
editor broke the package" surfaces here before it surfaces in a user's
project, with no version pin for anyone to forget to bump — while the
build-heavy legs — a development Android APK per line fed into the adb device
smoke, and an iOS Simulator-SDK Xcode export per line, compiled unsigned and
cold-launched on a macOS-runner simulator for 2022.3 and Unity 6 (2021.3
exports only: its simulator support is x86_64-only) — run on the same events.
Nothing is held back for a manual step, and a weekly cron still runs it to
catch drift with no commit behind it. Runner minutes are free on a public repo;
the Unity-activating jobs are chained so at most `UNITY_MAX_PARALLEL`
(repository variable, default 2) editors are ever activated at once, which
costs wall clock — 29 minutes on the first chained run and 38 on the latest
measured one (the 2026-08-31 cron, shrink leg included), against 13
unchained — and nothing else. Each Android APK is also read back with
`aapt2`, which must
find the baked static shortcuts, the resource-shrinker keep file and the
trampoline `<activity>` inside it, a further 2022.3-only job
(`android-shrink-verify`) exports the Gradle project and runs the AGP resource
shrinker over it to test whether that keep file is honoured, and a last one
(`gate-off`) rebuilds the same testbed with `QUICKACTIONS_ENABLED` off, requires
nothing of the package in the APK or the Xcode export, and diffs the two APKs
to measure the package's footprint — the `aapt2`
read-back has been green on all three lines in every heavy run that carried it,
and the shrink job returned its first verdict on 2026-08-29: keep rule held,
control shrunk (below).
It needs the repo secrets `UNITY_LICENSE` (a Unity Hub
personal-licence `.ulf`'s contents), `UNITY_EMAIL` and `UNITY_PASSWORD` (Pro:
`UNITY_SERIAL` instead of the `.ulf`); without them every Unity job skips and
the run stays green — which is also what happens on a **fork** PR, where
GitHub withholds secrets from the run by design, so an outside contributor's
code never comes within reach of them. (Nothing here uses
`pull_request_target`, the trigger that *would* hand secrets to untrusted
code.) The workflow header documents the setup step by step. The first live
run with the secrets in place (2026-08-21) opened the real editors: all three
EditMode legs, all three Android APK builds, all three iOS exports and both
macOS simulator cold-launches were green on the first attempt. The emulator
smoke needed a second dispatch to explain itself: its failure paths now print
process-alive state, the crash buffer and the engine log tail, and that
output pinned the Unity 6 red as a boot crash (SIGSEGV in
`Profiler::Initialize` under the API 30 image's ARM translation — that leg
now runs the API 35 image), showed the 2021.3 leg passing warm but sitting
engine-silent after the cold-tap restart (a GPU-settle delay now precedes the
cold tap), and took the 2022.3 leg through all eight steps — the first
observation anywhere of a cold tap arriving as `Performed`. The next dispatch
cashed those fixes in: 2021.3 joined 2022.3 at all eight steps green, and on
the API 35 image the Unity 6 player boots and publishes — which let the same
diagnostics catch a real runtime bug, the GameActivity warm-tap delivery gap
(see CHANGELOG). The first weekly cron carrying that fix then took the
Unity 6 leg through all eight steps too, so **every supported line now
passes the full emulator smoke, warm and cold taps included**. The
`android-shrink-verify` job validated its loud-failure design run after run
and now supplies the toolchain the export actually needs — JDK 17 and NDK
r23b, with Gradle taken from the export's own wrapper pin; the earlier JDK 11 /
Gradle 7.2 guess was disproved by run 25 — and its probe/control verdict came
back green on 2026-08-29: probe 990 → 990 bytes, control 990 → 67.

For a real device/emulator, `tools~/device-smoke/` has an adb-driven Android
smoke (install a dev APK → assert the demo's shortcuts registered → simulate a
tap → assert delivery, once warm and once more after a force-stop — the cold
assertion has run green on all three emulator legs (2021.3, 2022.3, Unity 6),
never yet on a real device) and a manually-dispatched
emulator CI workflow — see its
README, including the honest iOS limitations. A green `verify.sh` proves
everything compiles and the logic tests pass; it says nothing about on-device
behaviour, which cannot run in the Editor.

## Notes / learnings

- Min Unity is **declared** as 2021.3 LTS because `NamedBuildTarget` — the
  newest Editor API this package uses — ships in 2021.2; that line is verified
  end to end (import, 74/74 Test Runner, an APK with the trampoline, and a clean
  Xcode compile — see [Status](#status)). The dynamic native hooks avoid editing generated
  `UnityAppController` / `UnityPlayerActivity`; only **static** shortcuts need
  build post-processors (Info.plist / shortcuts.xml).
- The iOS `.mm` compiles cleanly against the real iOS SDK (ARC, arm64,
  deployment target iOS 13) with no deprecation or availability errors; the
  cross-platform `tools~/verify.sh` harness can only brace/structure-check it,
  so that compile is a separate, macOS-only step. The C# and Android Java are
  fully compiled against stubs by `verify.sh` — and the Java is also *executed*
  against stateful stubs (`.verify/JavaSmoke`).
- **Unity 6 scripting defines are ADDITIVE across scopes** (csc.rsp + Player
  Settings + Build Profile). A profile can *add* a symbol but never *remove*
  one — so a dev-only gate must live in the **dev profile**, never in shared
  Player Settings. Getting this backwards silently ships the gate in prod.
- **Android `ShortcutManager` sharp edges** (verified against AOSP source):
  `addDynamicShortcuts` updates same-id **dynamic and pinned** entries in
  place (a same-id write can hijack another publisher's pinned shortcut), and
  a pinned leftover of a removed manifest shortcut is **immutable** — including
  its id throws `IllegalArgumentException` up front and takes the whole batch
  with it. Coexisting with a host means marker extras + the additive APIs
  (`add`/`removeDynamicShortcuts`), never full-set `setDynamicShortcuts`.
  Extras survive OS persistence and read-backs (icons don't — persist icon
  identity *in* the extras); removes are never rate-limited, adds are.
- **iOS `shortcutItems` is one shared array** — coexistence needs a `userInfo`
  marker and merge-writes. Swizzle chains must assume they can be wrapped
  *later* by another plugin: "nobody was before me" ≠ "I am the terminal
  handler" (check the installed IMP at call time before owning the
  completionHandler). Returning `NO` from `didFinishLaunchingWithOptions` is
  what dedupes a cold shortcut tap.
- **Compile-only stubs miss contract bugs.** The classes of defect that
  slipped past `javac` + the NUnit suite (in-place pinned updates, null-vs-empty
  reads, rate-limit windows) were caught only by *running* the plugin against
  stateful stubs and by adversarial review against the real AOSP source. Also:
  a test that stays green when the fix is deleted is a tautology — mutation-check
  new tests before trusting them.
- **Failed writes must be loud:** an optimistic mutation that the OS rejected
  has to roll back and return false (mirroring the failed-*read* contract), or
  callers record success for a shortcut that silently evaporates on the next
  reconcile. Distinguish "read failed" (null) from "genuinely empty" everywhere.
