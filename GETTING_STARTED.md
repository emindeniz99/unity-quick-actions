# Getting Started — install it, enable it, see a shortcut on your device

A step-by-step guide for someone who has **not installed the package yet**. Two
parts: **A) get it running in a Unity project**, **B) see the shortcuts on a real
device**.

> Key fact up front: this repo is a **Unity package** (its `package.json` sits at
> the repo root), not a runnable Unity project. You use it by adding it to a
> Unity project of your own.

---

## 0. What you need

| For | Install |
|---|---|
| Everything | [Unity Hub](https://unity.com/download) + a Unity editor — **2022.3 LTS, 6.0 LTS or 6.3** are the compile-verified lines (see below) |
| Android testing | The **Android Build Support** module (add it in Unity Hub ▸ the editor ▸ ⚙ Add Modules). Works on Windows/macOS/Linux. |
| iOS testing | A **Mac** with **Xcode**, plus the **iOS Build Support** module. (iOS cannot be built from Windows/Linux.) |
| A test device | Android phone (Android 7.1 / API 25+) **or** iPhone. Quick actions do **not** appear in the Editor or on a plain simulator — you need a real long-press on a device. |

Don't have a Mac? Start with **Android** — it exercises the whole flow and needs
no Apple hardware.

### Which Unity versions are actually verified

- **Unity 2022.3, 6.0 and 6.3 — compile-verified in real licensed editors**
  (2022.3.9f1, 6000.0.79f1, 6000.3.20f1): the package imports with **0 console
  errors**, the Unity **Test Runner passes 35/35**, and on 2022.3 real Android
  APKs proved both the trampoline injection and the "zero trace when the define
  is off" gate. Details per row in
  [`PRODUCTION_READINESS.md`](./PRODUCTION_READINESS.md).
- **Unity 2021.3 is the declared minimum in `package.json`, but has never been
  compiled.** Nothing in the code is known to need a newer editor — 2021.3 is
  the same architectural line as the fully-proven 2022.3 — but treat it as
  unverified until someone runs it. If you try it, an issue report either way is
  welcome.
- **No physical-device validation has happened yet**, on either platform. The
  on-device behaviour described in Part B is what the package is built to do and
  what the build artifacts show; it has not been confirmed by a human tapping a
  real home-screen icon.

---

## A. Get it running in a Unity project

### A1. Make a project to try it in
In **Unity Hub ▸ New project ▸ 3D (URP or Built-in, doesn't matter)**. Name it
`QuickActionsTestbed`. Open it. (An existing project works too — the package
adds nothing to a build unless you turn it on in step A3.)

### A2. Install the package
**Window ▸ Package Manager ▸ + ▸ Add package from git URL…** and paste:
```
https://github.com/emindeniz99/unity-quick-actions.git
```
Append `#v0.4.0` to pin a version.

Alternatives:
- **+ ▸ Add package from disk…** pointing at the `package.json` at the root of a
  local clone (`git clone https://github.com/emindeniz99/unity-quick-actions.git`)
  — use this if you want to edit the package source.
- Drag a `QuickActions.unitypackage` into the Project window; it lands under
  `Assets/QuickActions/`. That file is a build output, not a committed file —
  grab it from the
  [Releases page](https://github.com/emindeniz99/unity-quick-actions/releases).

### A3. ⚠️ Turn the package ON (the #1 gotcha)
The package is **opt-in**: with no define, the `QuickActions` API doesn't even
exist and nothing happens. Enable it:

**Project Settings ▸ Player ▸ Other Settings ▸ Scripting Define Symbols** → add:
```
QUICKACTIONS_ENABLED
```
Do this for **each platform tab** you'll build (Android / iOS). Press Enter, then
**Apply**. Wait for the recompile.

> If you skip this, the package compiles to nothing: the sample is inert (its
> code is `#if`-guarded away) and any unguarded
> `using EminDeniz99.QuickActions;` in your scripts errors — that's expected,
> just add the define.

### A4. Import the Demo sample
Package Manager ▸ select **Home-Screen Quick Actions** ▸ **Samples** tab ▸
**Import** next to "Demo". It copies into `Assets/Samples/…/Demo`.
(If you imported a `.unitypackage` instead, the demo is at
`Assets/QuickActions/Example`.)

Open the demo scene (`QuickActionsDemo.unity`) — or just drop the
`QuickActionsDemo` component onto an empty GameObject in any scene. It's IMGUI,
so no Canvas/EventSystem needed.

You can press Play in the Editor to confirm it compiles and the buttons work, but
**the actual shortcuts only show on a device** (Part B).

### A5. Fastest loop: the in-Editor Simulator (no device)
The home-screen menu doesn't exist in the Editor, but you can still test your
**tap-handling code** without building anything:

1. **Window ▸ Quick Actions ▸ Simulator.**
2. It lists your **runtime** shortcuts (whatever your game added via
   `QuickActions.Add`) and any **static** shortcuts. Click one → it raises
   `QuickActions.Performed` with that id **exactly as a real tap does**, so your
   routing/handler code runs (`LastPerformed` updates too).
3. Two modes, just like a device:
   - **In Play Mode** → delivered immediately (a *warm* tap).
   - **Not in Play Mode** → clicking **starts Play Mode and fires the tap at
     startup**, exactly like tapping the icon while the app is closed (*cold
     launch*) — the realistic iOS/Android cold-start path.
4. The **Custom id** field fires any id you type — handy to cold-launch from a
   static shortcut's id.

This is the loop to use while writing your routing code; do the real device pass
(Part B) when you want to verify the OS-level long-press menu itself.

---

## B. See the shortcuts on a real device

### B1. Android (easiest — no Mac)
1. **File ▸ Build Settings ▸ Android ▸ Switch Platform** (wait for the reimport).
2. Make sure `QUICKACTIONS_ENABLED` is in the **Android** Scripting Define Symbols (A3).
3. Add the demo scene to **Scenes In Build**.
4. On the phone: enable **Developer Options ▸ USB debugging**, plug it in, accept the prompt.
5. **Build And Run** (Unity installs and launches the app).
6. Press the on-screen **"Add 3 shortcuts"** button.
7. Go to the home screen, **long-press the app icon** → you should see New Game / Continue / Daily Reward.
8. Tap one → the app opens and the on-screen log shows `Performed '<id>'`.
9. Force-close the app, tap a shortcut from the long-press menu → it should **cold-launch** and still log the id.

### B2. iOS (needs a Mac + Xcode)
1. **File ▸ Build Settings ▸ iOS ▸ Switch Platform**.
2. Confirm `QUICKACTIONS_ENABLED` is in the **iOS** define symbols.
3. **Build** → choose a folder → Unity generates an Xcode project.
4. Open the `.xcodeproj`/`.xcworkspace` in Xcode, set your **Signing Team**, plug in an iPhone, **Run**.
5. Same checks as Android steps 6–9 (add shortcuts, long-press the icon, tap, cold-launch).

### B3. Prove the dev-only gate (the "zero in production" promise)
Make a **production** build with the define **removed**:
1. **First clear the dev build's shortcuts** — on Android, dynamic shortcuts
   added with `QuickActions.Add` are persisted by the OS *per install*, and a
   gate-off build has no managed code left to remove them (the stripper also
   removes the trampoline they target, so they'd survive as visible-but-dead
   entries). Either call `QuickActions.RemoveAll()` in the dev build first, or
   **uninstall the dev build** (or clear the app's data) before installing the
   prod build over it. A fresh install never has this problem.
2. Remove `QUICKACTIONS_ENABLED` from Scripting Define Symbols (that platform).
3. Build again.
4. Android: open the generated `…/unityLibrary/src/main/AndroidManifest.xml` (or the merged manifest) → there should be **no** `QuickActionsTrampolineActivity`.
5. iOS: search the generated Xcode project for `QUICKACTIONS_ENABLED` → **none**; the `.mm` compiles to nothing.
6. With step 1 done, the shortcuts no longer appear — confirming the package
   is inert in prod. (iOS persists dynamic `shortcutItems` across installs
   too, so step 1 applies there as well; the failure mode is milder — a stale
   iOS shortcut still just opens the app, while a stale Android one targets
   the stripped trampoline and dies.)

---

## Quick reference

| Symptom | Cause / fix |
|---|---|
| `QuickActions` type not found in your scripts / sample inert | `QUICKACTIONS_ENABLED` define missing (step A3) — add it for that platform |
| Imported it and nothing happens | Same thing: the package is opt-in by design. Add the define (step A3). |
| Nothing happens in the Editor Play mode | Expected — the OS menu only exists on a device (Part B). Use the Simulator (step A5) to test tap handling in-Editor |
| Long-press shows no shortcuts | Did you press "Add 3 shortcuts" first? Android < 7.1 (API 25) isn't supported |
| The package's tests don't show in the Test Runner | Add `"testables": ["com.emindeniz99.quick-actions"]` to your `Packages/manifest.json` |
| iOS build but can't run | Needs a Mac + Xcode + a signing team (step B2) |

Other docs: [`README.md`](./README.md) (API + install) ·
[`PRODUCTION_READINESS.md`](./PRODUCTION_READINESS.md) (what's tested, and what
isn't) · [`CONTRIBUTING.md`](./CONTRIBUTING.md) (building the package from
source) · [`MAINTAINING.md`](./MAINTAINING.md) (maintainer-only: releasing and
distribution).
