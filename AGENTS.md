# AGENTS.md — integrating this package with an AI coding agent

For an agent (or a human in a hurry) told "add home-screen quick actions to
our Unity game" who landed here. It is the shortest correct path plus the
checks that prove it worked. The README says all of it in more words; when
the two disagree, the README is wrong and this file should be fixed.

## What this is

`com.emindeniz99.quick-actions`: one static C# API,
`EminDeniz99.QuickActions.QuickActions`, over iOS Home Screen quick actions
(`UIApplicationShortcutItem`) and Android app shortcuts (`ShortcutManager`).
MIT. Unity 2021.3 LTS through Unity 6. No native project edits — the iOS side
hooks the app delegate at load, the Android side injects a trampoline activity
into the generated Gradle manifest at build time. `package.json` is at the
repository root, so the repository **is** the package.

## Do this, in order

1. **Install** — add to `Packages/manifest.json`, or through Package Manager ▸
   *Add package from git URL…*:

   ```json
   "com.emindeniz99.quick-actions": "https://github.com/emindeniz99/unity-quick-actions.git#v0.5.0"
   ```

   Alternatives: `openupm add com.emindeniz99.quick-actions`; or vendor the
   tree as an embedded package at `Packages/com.emindeniz99.quick-actions/`.
   **One method only** — a second copy fails to compile with
   `Assembly with name 'EminDeniz99.QuickActions' already exists`.
2. **Turn it on.** The package is inert until the scripting define
   `QUICKACTIONS_ENABLED` is set for each platform that should have it. In the
   Editor: **Window ▸ Quick Actions ▸ Enable Quick Actions** sets it for
   Standalone, Android and iOS in one click. Headless / scripted: call
   `PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, ...)`
   (and `.iOS`, `.Standalone`) from your own Editor script, appending the
   define to whatever is already there. Do not set it for a platform whose
   production build must not contain the package (see README "Dev-only").
3. **Reference the assembly** if the calling script lives in an assembly
   definition: add `EminDeniz99.QuickActions` to that asmdef's `references`
   (and `EminDeniz99.QuickActions.Editor` to an Editor asmdef that uses the
   build-time hooks). Scripts in `Assembly-CSharp` need nothing.
4. **Subscribe in the first scene, then add** — guarded so the project still
   compiles with the define off:

   ```csharp
   #if QUICKACTIONS_ENABLED
   using EminDeniz99.QuickActions;
   #endif
   using UnityEngine;

   public class ShortcutSetup : MonoBehaviour
   {
   #if QUICKACTIONS_ENABLED
       void Awake()
       {
           // Subscribe in Awake/OnEnable of the FIRST scene: the cold-launch tap
           // is delivered one frame after startup and a later subscriber misses it.
           QuickActions.Performed += id => Debug.Log($"Tapped: {id}");
       }

       void Start()
       {
           QuickActions.Add(new QuickActionItem(
               id: "new_game", title: "New Game",
               subtitle: "Start fresh", icon: IconType.Add));
       }
   #endif
   }
   ```

5. **Static shortcuts** (present before the app ever ran) are configured in
   **Project Settings ▸ Quick Actions**, which creates
   `Assets/Settings/QuickActionsSettings.asset` — commit it. Build
   post-processors bake them into `Info.plist` and `res/xml/quickactions_shortcuts.xml`.

## Checks that prove it worked

- The project compiles with the define **on** and **off** (the `#if` guard).
- Editor: **Window ▸ Quick Actions ▸ Simulator** lists the added items;
  clicking one raises `Performed` with that id (Play Mode: immediately; outside
  Play Mode: it starts Play Mode and delivers on startup, the cold-launch path).
- Android APK: the merged manifest contains
  `com.emindeniz99.quickactions.QuickActionsTrampolineActivity`
  (`aapt2 dump xmltree --file AndroidManifest.xml app.apk`); after `Add` ran on
  the device, `adb shell dumpsys shortcut --package <applicationId>` lists the
  id; a long-press on the launcher icon shows the menu (API 25+).
- iOS: the generated Xcode project compiles; on the iOS Simulator a long-press
  on the app icon shows the menu and a tap cold-launches into `Performed`
  (verified on Unity 6.3 / iOS 26.5 — see README "Status").
- The package's own test suite runs in the consuming project's Test Runner
  only with `"testables": ["com.emindeniz99.quick-actions"]` in
  `Packages/manifest.json`.

## Don'ts

- Don't edit the generated `AndroidManifest.xml` or `Info.plist` for this —
  the post-processors do it, and they also clean up when the define is off.
- Don't hand-write `res/xml/shortcuts.xml`: the settings page bakes it, and
  a hand-written one competes for the same launcher slots.
- Don't treat the id from `Performed` / `LastPerformed` as authenticated: on
  Android any app can start the exported trampoline with any id. Route it to
  a screen, never straight to a destructive or privileged action
  (README "Security", `SECURITY.md`).
- Don't reuse a static shortcut's id in a runtime `Add`: iOS shows it twice,
  Android drops the dynamic one.
- Don't pass `null` to `Add` / `AddList` / `Update`: those throw
  `ArgumentNullException`; every other failure returns `false`.
- Don't ship the define in a production build that must carry zero footprint;
  gate it on a dev Build Profile instead (README "Dev-only").

## Contract in one screen

- Ids are unique and non-empty; `Title` is required. `Add` returns `false`
  for a duplicate id, an invalid item, or an OS read/write that failed
  (transient — retry later).
- `Performed` fires on the main thread for warm and cold taps alike;
  `LastPerformed` is sticky until `ResetLastPerformed()`.
- `MaxShortcutCount` is the OS budget (Android queries it; iOS is 4). Static
  shortcuts share it.
- Icons: iOS renders system glyphs (`IconType`) or SF Symbols
  (`IosSystemImage`); Android resolves a drawable named
  `ic_quickaction_<name>` from the project first, then the package's four
  built-ins (`Add`, `Compose`, `Favorite`, `Play`) — any other `IconType`
  needs a drawable you add, or the launcher shows a blank square.
- `Payload` rides the shortcut and comes back through `GetById(id)?.Payload`
  (null for a static-shortcut tap).
- Android < 7.1 / API 25: `IsPlatformSupported` is `false` and every call is
  a safe no-op. The package sets no `minSdk`.

## Working on the package itself

Rules for agents changing this repository are in [`CLAUDE.md`](./CLAUDE.md);
the human-facing detail is in [`CONTRIBUTING.md`](./CONTRIBUTING.md).
`tools~/verify.sh` must end with `VERIFY: PASS` before any push.
