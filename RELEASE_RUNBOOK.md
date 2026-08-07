# Release Runbook — test-release week, step by step

The condensed, day-of sequence. Details live in
[`GETTING_STARTED.md`](./GETTING_STARTED.md) (referenced as **GS**) and
[`STORE_CHECKLIST.md`](./STORE_CHECKLIST.md) (**SC**). Decisions already made:
**price = FREE · gate = KEPT (dev-only)** — nothing to re-decide on the day.

## Phase 0 — before Unity (15 min, any OS)

- [ ] `git clone https://github.com/emindeniz99/unity-quick-actions.git`
- [ ] `cd unity-quick-actions && tools/setup.sh && tools/verify.sh`
      → expect **`VERIFY: PASS`** (if not, stop: the checkout is broken, nothing
      else will work — re-clone or ask for help).
- [ ] Install **Unity Hub** + the two editors on the ends of the supported
      range: **2021.3 LTS** and **Unity 6.3**, each with **Android Build Support**
      (+ iOS Build Support if on a Mac). *(GS §0)*
- [ ] Phone ready: Android 7.1+ with **USB debugging** enabled.

> **Matrix rule (current plan):** the two ends of the supported range get the
> FULL bar — repeat **Phases 1–3** (and Phase 4 on a Mac) in **2021.3 LTS** and
> in **Unity 6.3**. The lines in between (2022.3, 6.0) are **not claimed as
> tested**; they should work, but nothing here validates them, so don't write
> them up as verified. One testbed project per editor version (don't upgrade one
> project through versions — that tests migration, not fresh installs). Budget
> roughly half a day per line; start with 2021.3 (the minimum, most likely to
> surface an API gap) and finish with 6.3 (the longest-lived target).

## Phase 1 — Editor smoke test (30 min per editor)

- [ ] Unity Hub ▸ New 3D project `QuickActionsTestbed`; open it.
- [ ] Package Manager ▸ + ▸ *Add package from disk…* ▸ the `package.json` at the
      **root of your clone**.
- [ ] **THE step:** Project Settings ▸ Player ▸ Scripting Define Symbols → add
      `QUICKACTIONS_ENABLED` (Android tab; iOS tab too if building iOS). *(GS §A5)*
- [ ] Console shows **0 errors / 0 warnings** after recompile.
      ⚠️ First real-Unity compile — if an asmdef/DLL error appears here, copy the
      exact message and bring it back; this is the one known unproven seam.
- [ ] Package Manager ▸ the package ▸ Samples ▸ **Import Demo**.
- [ ] **Window ▸ Quick Actions ▸ Simulator** opens; **Window ▸ Quick Actions ▸
      About** opens (both menus must exist).
- [ ] Simulator, Play OFF → click a shortcut → Play Mode starts **and** the log
      prints `Performed('<id>')` at startup (cold launch through the real pipeline).
- [ ] In Play Mode → click another shortcut → delivered immediately (warm tap).

## Phase 2 — Android device pass (1–2 h) *(GS §B1)*

- [ ] Build Settings ▸ Android ▸ **Switch Platform**; demo scene in *Scenes In Build*.
- [ ] **Build And Run** onto the phone.
- [ ] Tap **"Add 3 shortcuts"** → home screen → **long-press the app icon** →
      New Game / Continue / Daily Reward appear.
- [ ] Tap one → app foregrounds, log shows `Performed '<id>'` (**warm**).
- [ ] Force-close app → long-press → tap a shortcut → app **cold-launches** and
      still logs the id.
- [ ] 📸 **Take the long-press screenshot now** → save it over
      `store~/screenshot-1.png`. ⚠️ Do **not** run `gen_store_images.py`
      afterwards — it regenerates placeholders (it now skips an existing
      screenshot-1 unless you pass `--force`, but don't tempt it).
- [ ] *(Optional but recommended)* Project Settings ▸ Quick Actions → add one
      **static** shortcut → rebuild → it exists on first launch before pressing
      anything.

## Phase 3 — prove the gate (30 min) *(GS §B3 — the headline promise)*

- [ ] Remove `QUICKACTIONS_ENABLED` from the Android define symbols; build again.
- [ ] Generated/merged `AndroidManifest.xml` contains **no**
      `QuickActionsTrampolineActivity`; long-press shows no package shortcuts.
- [ ] (iOS, if applicable) prod Xcode project greps clean for
      `QUICKACTIONS_ENABLED`.
- [ ] Re-add the define afterwards (your testbed stays a dev project).

## Phase 4 — iOS pass (only with a Mac) *(GS §B2)*

- [ ] Switch Platform ▸ iOS → Build → open in Xcode → set Signing Team → Run on
      iPhone → repeat Phase 2's checks 3–6.
- [ ] No Mac? Skip — submitting Android-validated first is fine; add iOS later.

## Phase 5 — submit (1 h) *(SC §§1–6)*

- [ ] publisher.unity.com → create the free Publisher profile, accept agreement.
- [ ] Portal ▸ Create new package draft.
- [ ] Paste from `store~/listing/`: title/metadata (`metadata.md`),
      `summary.txt`, `description.md` (keep the **"One switch to turn it on"**
      section — the gate is the product), `tags.txt`.
- [ ] Upload images from `store~/` (icon/card/cover/social + screenshots incl.
      your real one).
- [ ] Upload the `.unitypackage` via Asset Store Publishing Tools **from the
      2021.3 editor** (upload version = listed minimum). It is a build output,
      not a repo file: build it with `python3 tools/pack_unitypackage.py` (or
      `tools/release.sh`) → gitignored `dist~/QuickActions.unitypackage`, or take
      the one attached to the
      [GitHub Release](https://github.com/emindeniz99/unity-quick-actions/releases).
- [ ] Price: **Free**. Submit for review. (Review: days → ~2 weeks.)

## Phase 6 — stamp the release (10 min, back in the repo)

The **first public release is `v0.4.0`** — the version this repo already carries.
Tags are plain semver (`v0.4.0`), one tag per release.

- [ ] `package.json` `version` and the top `CHANGELOG.md` heading agree, and the
      heading has a real date (no `Unreleased` left).
- [ ] Stay in `0.x` until the matrix above has actually been walked on devices;
      a `1.0.0` is a claim of "validated on both ends of the supported range",
      not a mood. Bumping later is cheap.
- [ ] `git tag v<version> && git push origin v<version>` (e.g. `v0.4.0`).
- [ ] Check the resulting **GitHub Release** carries the built
      `QuickActions.unitypackage` asset (CI attaches it; if it's missing, build
      it with `tools/release.sh` and upload it to the release by hand) —
      docs point downloaders at
      <https://github.com/emindeniz99/unity-quick-actions/releases>.
- [ ] OpenUPM one-time submission: [`plans/openupm.md`](./plans/openupm.md).

## If something breaks

| Symptom | First move |
|---|---|
| Console errors on import / platform switch | Copy the exact error → bring it back to me (likely the asmdef extension-DLL seam — known unproven spot) |
| `QuickActions` type not found | The define (Phase 1, "THE step") — per platform tab |
| Shortcuts don't appear on long-press | Did you tap "Add 3 shortcuts" first? Android ≥ 7.1? |
| Tap opens app but no log line | Grab `adb logcat -s QuickActions Unity` output → bring it back |
| Store review declines | Read their reason; usual fixes: real screenshot, zero console warnings, clearer description |

**Everything in Phases 0–1 and 6 is reversible; nothing here can damage the repo.**
