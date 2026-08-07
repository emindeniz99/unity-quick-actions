# Unity Asset Store — submission checklist

Everything needed to publish **Home-Screen Quick Actions (iOS & Android)**. Sources:
[Submission Guidelines](https://assetstore.unity.com/publishing/submission-guidelines),
[Start publishing](https://assetstore.unity.com/publishing/publish-and-sell-assets),
[Marketing image sizes](https://support.unity.com/hc/en-us/articles/210122403).

## Status — done vs needs-you

✅ **Done in repo (run `tools~/release.sh` to build/regenerate all):**
listing texts (`store~/listing/`), marketing images (`store~/`), compile + unit
tests green, full source + docs. The drag-and-drop `.unitypackage` is a **build
output** — it is not committed; `tools~/release.sh` writes it to the gitignored
`dist~/`, and CI attaches the same artifact to each
[GitHub Release](https://github.com/emindeniz99/unity-quick-actions/releases),
so you can either build it or download it — `v0.4.0` is the first release and
carries the artifact.

⏳ **Needs your account / hardware (can't be automated):**
1. Create the free publisher account + accept the agreement.
2. ~~Open the package in **each** licensed Unity line (2021.3, 2022.3, 6.0, 6.3) and confirm a
   clean import; switch to iOS/Android targets.~~ — done on all four lines (see §5).
3. **Device test** (Android device; iOS via macOS/Xcode) + one real screenshot.
4. Set the price to **Free** (decided 2026-07-10 — see §2), upload the
   `.unitypackage`, submit for review.

---

## 0. ⚠️ Gate decision — do this FIRST, before building the upload

The package is currently **dev-only gated** behind `QUICKACTIONS_ENABLED`; a
buyer who imports it without the define gets an inert package. Decide (see
GETTING_STARTED §C0 and README "Dev-only"):

- [x] **DECIDED (2026-07-10): keep the gate** — sold explicitly as a dev-only
      tool. The listing states the define requirement prominently
      (`store~/listing/description.md` "One switch to turn it on"), README leads
      Install with it, and GETTING_STARTED calls it the #1 gotcha. Do NOT remove
      that section from the listing.
- ~~Either produce a de-gated (always-on) build for the store~~ (rejected — the
  zero-prod-footprint gate is the product's differentiator).

## 0b. Cost & account (the "is it free?" answer)

- **Publishing is free.** No fee to create a publisher account or list assets.
- **Revenue split: you keep 70%, Unity takes 30%** (standard, non-negotiable).
- **Price:** free, or paid with a **$4.99 minimum** (Unity's floor for paid
  assets). Launching free to gather reviews and switching later is allowed.
- **Account needed:** a Unity ID + a **Publisher account** at
  <https://publisher.unity.com> (one publisher profile per seller).
- **Payouts:** monthly via PayPal or quarterly via bank transfer.
- A verified Unity ID and accepting the **Provider Agreement** are required.

## 1. Publisher account setup

- [ ] Create/verify Unity ID, then a Publisher profile at publisher.unity.com.
- [ ] Fill publisher name, description, logo, and payout (PayPal/bank) details.
- [ ] Accept the Asset Store Provider Agreement.

## 2. Package metadata — ✅ written, paste-ready in `store~/listing/`

All copy is prepared in [`store~/listing/`](./store~/listing/) — paste verbatim:
- [x] **Title / Category / Version / compatibility:** [`metadata.md`](./store~/listing/metadata.md)
- [x] **Summary:** [`summary.txt`](./store~/listing/summary.txt)
- [x] **Description:** [`description.md`](./store~/listing/description.md)
- [x] **Tags / keywords:** [`tags.txt`](./store~/listing/tags.txt)
- [x] **Price:** **FREE** (decided 2026-07-10).

## 3. Marketing images (generated → `store~/`)

All sizes are ✅ pre-built by `python3 tools~/gen_store_images.py`. See
[`store~/README.md`](./store~/README.md).

- [x] **Icon** 160×160, no text — `store~/icon.png`
- [x] **Card** 420×280 — `store~/card.png`
- [x] **Cover** 1950×1300 — `store~/cover.png`
- [x] **Social** 1200×630 — `store~/social.png`
- [x] **Screenshots** ≥1200w (2400×1600 here), ≥1 — `store~/screenshot-*.png`
- [x] `screenshot-1.jpg` is a **real capture**, not a mockup (2026-08-07): the
      iOS Simulator long-press menu, composited onto the 2400×1600 canvas with
      `python3 tools~/make_store_screenshot.py <capture.png> store~/screenshot-1.jpg`.
      A raw phone capture is the wrong shape for a landscape listing thumbnail;
      the tool scales and centres it on the same gradient the generated art uses.
      Re-run it with an Android capture if you want that platform shown too.
- [x] Key-image text rules (checked 2026-08-07, images regenerated to match):
      icon and **social** carry no text at all; **card** carries only the asset
      title and publisher name; **cover** carries the title plus one tag line.
- [ ] No watermarks; not blurry/stretched; not bare Editor shots.

## 4. Package contents — what to ship (and what to strip)

A clean `.unitypackage` is **built on demand** — `python3 tools~/pack_unitypackage.py`
(no Unity needed) writes `dist~/QuickActions.unitypackage`, which is gitignored
because it's a build output; the identical artifact is attached to every
[GitHub Release](https://github.com/emindeniz99/unity-quick-actions/releases)
(first one: `v0.4.0`). It:

- [x] Includes only package content, remapped under `Assets/QuickActions/`
      (Runtime, Editor, Plugins, Example, README/CHANGELOG/LICENSE/ROADMAP).
- [x] Excludes dev/publishing collateral (`.verify/`, `tools~/`, `plans~/`,
      `store~/`, `dist~/`, `Tests/`, `STORE_CHECKLIST.md`, `package.json`).
- [x] Carries every asset's committed `.meta` GUID (scene→script refs intact).
- [x] Ships **full source** for the native plugins (`.mm`, `.java`).
- [ ] Import it once in a licensed Editor to confirm before uploading.

## 5. Technical review gates (do before upload)

- [x] Imports into a fresh **2021.3 LTS** project (the claimed minimum) with **zero console
      errors/warnings**; repeat the FULL check on **2022.3, 6.0 LTS and 6.3 LTS** (all claimed lines get the same bar).
      (2021.3, 2022.3, 6.0 and 6.3 have all passed import + Test Runner in a
      licensed editor — see [`PRODUCTION_READINESS.md`](./PRODUCTION_READINESS.md);
      2021.3 is in fact the most thoroughly verified line, down to an Android
      player build and an Xcode compile of the generated iOS project.)
- [ ] `tools~/verify.sh` is green (compile + unit tests + Android plugin).
- [ ] Switch build target to **iOS** and **Android** → Editor still compiles
      (confirms the asmdef extension-DLL references resolve — see ROADMAP).
- [ ] Build to an **iOS device** (needs macOS/Xcode) and an **Android device**:
      long-press the icon shows the demo's shortcuts; tapping routes correctly
      (cold + warm). This is the one step that needs real hardware.
- [ ] No use of another publisher's assets; all art (`store~/`) is original.
- [ ] Unique root namespace (`EminDeniz99.QuickActions`) and asmdef names.
- [ ] Demo lives under `Samples~/` and imports cleanly via the package page.

## 5b. Asset Store Validator — the two warnings you will see

Run with `QUICKACTIONS_ENABLED` set, or you get a third (below).

- **"Check Missing Components in Scenes"** — appears only when the define is
  ABSENT: `Samples~/Demo/QuickActionsDemo.cs` is wrapped in
  `#if QUICKACTIONS_ENABLED`, so the component the demo scene references is
  compiled away and Unity calls it missing. Set the define (Window ▸ Quick
  Actions ▸ Enable Quick Actions) and it clears. Nothing to fix in the package.
- **"Check Static Variables"** — a heuristic that fires on any script with a
  static field; the validator itself says it "does not definitively identify
  problematic code areas". Already handled here: `QuickActions.cs`'s
  `EditorResetForPlaySession` (`[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`)
  clears every static for the Fast-Enter-Play-Mode case, and static events are
  cleared on play EXIT via `EditorClearPerformedSubscribers` — deliberately on
  exit, since wiping subscribers during an entry phase could race a legitimate
  same-phase subscription. `QuickActionsRuntime._instance` is not in that reset
  and does not need to be: its GameObject is destroyed on play exit, and Unity's
  overloaded `!=` reports destroyed objects as null, so the Bootstrap guard
  recreates it. No action.

## 6. Submit

- [ ] Make a **fresh, empty 3D project in 2022.3** to upload from. ⚠️ Do **not**
      use `Examples~/Testbed2022`: it already links the package through UPM, so
      importing the `.unitypackage` on top collides — same assembly names, same
      asset GUIDs, `Assembly with name 'EminDeniz99.QuickActions' already
      exists`, and the project stops compiling.
- [ ] Install **Asset Store Publishing tools** (Publisher portal → "Create new
      package" → use the Unity Editor uploader / Asset Store Tools package),
      then import the `.unitypackage` so `Assets/QuickActions/` exists. The
      uploader may want a *folder* inside `Assets/` rather than a pre-exported
      file; importing first covers both, and uploads identical content either
      way.
- [ ] Create the draft package in the portal, attach metadata + images.
- [ ] Upload the clean `.unitypackage` **from a 2022.3 (or newer) editor**. Rule
      1.3.a: *"New assets and updates to already published assets use Unity
      version 2022.3 or newer versions."* The upload editor sets the listing's
      minimum, so the Store will show 2022.3 — that is unavoidable and does **not**
      mean dropping 2021.3 support: `package.json` keeps `"unity": "2021.3"`, and
      2021.3 users install from OpenUPM or the Git URL.
- [ ] **Upload from 2022.3 specifically — not 6.x.** Two rules push the same
      way. 1.3.a bars uploading from anything older than 2022.3, so 2022.3 is
      the oldest editor allowed and therefore the widest listed range you can
      get. And 1.3.c: *"New assets and updates ... submitted using Unity Editor
      6.5 or newer must support Universal Render Pipeline (URP) or
      High-Definition Render Pipeline (HDRP)."* This package does no rendering
      and is pipeline-agnostic, so it would likely satisfy that trivially —
      but uploading from 2022.3 means never having to argue it.
      One upload is enough: 1.3.b's multi-version upload exists for packages
      whose code differs per editor line, and ours compiles unchanged on all
      four.
- [ ] **Say the 2021.3 support in the description**, since the compatibility
      field cannot carry it. 1.3.b explicitly allows explaining version
      compatibility in description text. Suggested wording: "Also compatible
      with Unity 2021.3 LTS — the Asset Store lists 2022.3 as the minimum
      because submissions must be uploaded from 2022.3 or newer. For 2021.3,
      install from OpenUPM or the Git URL."
- [ ] **Disclose the dual licensing in the submission notes**: the source is
      MIT on GitHub/OpenUPM, this Store copy is additionally distributed under
      Unity's EULA by the same author. The shipped README says "MIT-licensed",
      so a reviewer will see it — better stated up front than queried.
- [ ] Set price/availability and **Submit for review**.
- [ ] Review is manual (typically several business days to ~2 weeks). Expect a
      pass or a decline with specific feedback to address and resubmit.

## 7. Pre-flight one-liner

```bash
tools~/release.sh   # verify (compile + tests) + fresh images + dist~/QuickActions.unitypackage
```

Then upload `dist~/QuickActions.unitypackage` from a licensed Editor.
