# Getting Started — run it locally, test on a device, publish to the Asset Store

A step-by-step guide for someone who has **not pulled the repo yet**. Three
parts: **A) get it running locally**, **B) test on a real device**, **C) publish
to the Unity Asset Store**.

> Key fact up front: this repo is a **Unity package** (its `package.json` sits at
> the repo root), not a runnable Unity project. You test it by creating a small
> empty project and importing it.

---

## 0. What you need

| For | Install |
|---|---|
| Everything | [Unity Hub](https://unity.com/download) + a **Unity 2021.3 LTS** editor (2022.3 and Unity 6.x also supported) |
| Android testing | The **Android Build Support** module (add it in Unity Hub ▸ the editor ▸ ⚙ Add Modules). Works on Windows/macOS/Linux. |
| iOS testing | A **Mac** with **Xcode**, plus the **iOS Build Support** module. (iOS cannot be built from Windows/Linux.) |
| A test device | Android phone (Android 7.1 / API 25+) **or** iPhone. Quick actions do **not** appear in the Editor or on a plain simulator — you need a real long-press on a device. |

Don't have a Mac? Test on **Android first** — it exercises the whole flow and
needs no Apple hardware.

---

## A. Get it running locally

### A1. Pull the code to your PC
```bash
git clone https://github.com/emindeniz99/unity-quick-actions.git
cd unity-quick-actions
```
(Everything for this asset is in that one repo, and every path below is relative
to its root.)

### A2. (Optional, no Unity needed) Sanity-check it compiles
```bash
tools/setup.sh    # one-time: installs dotnet + JDK if missing
tools/verify.sh   # → VERIFY: PASS  (compiles C#, runs the unit tests, compiles Java)
```
This proves the code is healthy before you even open Unity.

### A3. Make a throwaway test project
In **Unity Hub ▸ New project ▸ 3D (URP or Built-in, doesn't matter)**. Name it
`QuickActionsTestbed`. Open it.

### A4. Install the package into the test project
**Window ▸ Package Manager ▸ + ▸ Add package from disk…** and pick the
`package.json` at the **root of your clone**.
(Alternatives: **+ ▸ Add package from git URL…** with
`https://github.com/emindeniz99/unity-quick-actions.git` — append `#v0.4.0` to
pin a version; or drag a `QuickActions.unitypackage` into the Project window —
it lands under `Assets/QuickActions/`. That `.unitypackage` is a build output,
not a committed file: grab it from the
[Releases page](https://github.com/emindeniz99/unity-quick-actions/releases) or
build it yourself with `python3 tools/pack_unitypackage.py`.)

### A5. ⚠️ Turn the package ON (the #1 gotcha)
The package is **opt-in**: with no define, the `QuickActions` API doesn't even
exist and nothing happens. Enable it:

**Project Settings ▸ Player ▸ Other Settings ▸ Scripting Define Symbols** → add:
```
QUICKACTIONS_ENABLED
```
Do this for **each platform tab** you'll build (Android / iOS). Press Enter, then
**Apply**. Wait for the recompile.

> If you skip this, the package compiles to nothing: the Demo is inert (its code
> is `#if`-guarded away) and any unguarded `using EminDeniz99.QuickActions;` in
> YOUR scripts errors — that's expected, just add the define.

### A6. Import the Demo
Package Manager ▸ select **Home-Screen Quick Actions** ▸ **Samples** tab ▸
**Import** next to "Demo". It copies into `Assets/Samples/…/Demo`.
(If you imported a `.unitypackage` instead, the demo is at
`Assets/QuickActions/Example`.)

Open the demo scene (`QuickActionsDemo.unity`) — or just drop the
`QuickActionsDemo` component onto an empty GameObject in any scene. It's IMGUI,
so no Canvas/EventSystem needed.

You can press Play in the Editor to confirm it compiles and the buttons work, but
**the actual shortcuts only show on a device** (next part).

### A7. Fastest loop: the in-Editor Simulator (no device)
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

This is the loop to use while writing your routing code; only do the real
device pass (Part B) when you want to verify the OS-level long-press menu itself.

---

## B. Test on a real device

### B1. Android (easiest — no Mac)
1. **File ▸ Build Settings ▸ Android ▸ Switch Platform** (wait for the reimport).
2. Make sure `QUICKACTIONS_ENABLED` is in the **Android** Scripting Define Symbols (A5).
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

(Full device procedure also in [`plans/mvp.md`](./plans/mvp.md); readiness matrix
in [`PRODUCTION_READINESS.md`](./PRODUCTION_READINESS.md).)

---

## C. Publish to the Unity Asset Store

> Detailed checklist with paste-ready text/images: [`STORE_CHECKLIST.md`](./STORE_CHECKLIST.md).
> Publishing is **free** (you keep 70%, Unity takes 30%; price free or ≥ $4.99).

### C0. ✅ Gate decision — already made (2026-07-10): the gate STAYS
This package ships to the store **as a dev-only gated tool** — that's its
differentiator (guaranteed-zero production footprint), and the listing sells it
that way: the description leads with the **"One switch to turn it on"** section
and the README leads Install with the define. Nothing to decide on release day;
just **don't remove that section** from the listing.

> *Fork note:* if you (or a fork) ever want an always-on build instead, the
> conversion recipe lives in README ▸
> ["Dev-only"](./README.md#dev-only--excluding-it-completely-from-production-builds)
> (remove the asmdef `defineConstraints`, drop the `.mm` `#if`, delete the two
> gate post-processors).

### C1. Create a publisher account
Go to <https://publisher.unity.com>, sign in with your Unity ID, create a
**Publisher profile**, fill payout (PayPal/bank), and accept the Provider Agreement.

### C2. Validate the package in a real Unity (the device gate)
Before uploading, do the FULL pass (Part B, device included) on **both ends of
the supported range: 2021.3 LTS (the minimum) and Unity 6.3** — the versions in
between are not claimed as tested — with **zero console errors/warnings**. This
is the one thing that must pass and that could not be done in the build
environment (no Unity, no devices). Step-by-step order in
[`RELEASE_RUNBOOK.md`](./RELEASE_RUNBOOK.md).

### C3. Build the upload package
- Easiest: install **Asset Store Publishing Tools** from the Asset Store (search
  it in the Package Manager / Asset Store), which adds an uploader window in Unity.
- Or upload a `QuickActions.unitypackage`. It is a **build output** — not
  committed to the repo — so either download it from the
  [Releases page](https://github.com/emindeniz99/unity-quick-actions/releases) or
  build it locally with `python3 tools/pack_unitypackage.py` (or `tools/release.sh`,
  which also refreshes the store images); it lands in the gitignored `dist~/`.

### C4. Fill the listing (everything is pre-written)
In the publisher portal, create a draft package and paste from
[`store~/listing/`](./store~/listing/): `metadata.md` (title/category/version),
`summary.txt`, `description.md`, `tags.txt`. Upload the images from
[`store~/`](./store~/) (icon 160×160, card 420×280, cover 1950×1300, screenshots).
**Replace at least one screenshot with a real on-device long-press capture** —
the current ones are mockups and the store prefers in-context shots.

### C5. Submit
Set price/availability, then **Submit for review**. Review is manual — typically
a few business days, up to ~2 weeks. You'll get an approval or a decline with
specific feedback to fix and resubmit.

---

## Quick reference

| Symptom | Cause / fix |
|---|---|
| `QuickActions` type not found in your scripts / Demo inert | `QUICKACTIONS_ENABLED` define missing (step A5) — add it for that platform |
| Nothing happens in the Editor Play mode | Expected — the OS menu only exists on a device (Part B). Use the Simulator (step A7) to test tap handling in-Editor |
| Long-press shows no shortcuts | Did you press "Add 3 shortcuts" first? Android < 7.1 (API 25) isn't supported |
| Buyer says "imported it and nothing works" | Expected for the dev-only design — point them to the define step (the listing and README lead with it) |
| iOS build but can't run | Needs a Mac + Xcode + a signing team (step B2) |

Other docs: [`README.md`](./README.md) (API + install) ·
[`STORE_CHECKLIST.md`](./STORE_CHECKLIST.md) (submission) ·
[`PRODUCTION_READINESS.md`](./PRODUCTION_READINESS.md) (what's tested) ·
[`plans/openupm.md`](./plans/openupm.md) (free OpenUPM distribution).
