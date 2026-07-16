# Release Runbook — test-release week, step by step

The condensed, day-of sequence. Details live in
[`GETTING_STARTED.md`](./GETTING_STARTED.md) (referenced as **GS**) and
[`STORE_CHECKLIST.md`](./STORE_CHECKLIST.md) (**SC**). Decisions already made:
**price = FREE · gate = KEPT (dev-only)** — nothing to re-decide on the day.

## Phase 0 — before Unity (15 min, any OS)

- [ ] `git clone https://github.com/emindeniz99/playground.git`
- [ ] `cd playground/projects/quick-actions-unity && tools/setup.sh && tools/verify.sh`
      → expect **`VERIFY: PASS`** (if not, stop: the checkout is broken, nothing
      else will work — re-clone or ask for help).
- [ ] Install **Unity Hub** + **Unity 2021.3 LTS** (min) and **6.3 LTS** with **Android Build Support** (add 2022.3 + 6.0 for the smoke pass)
      (+ iOS Build Support if on a Mac). *(GS §0)*
- [ ] Phone ready: Android 7.1+ with **USB debugging** enabled.

## Phase 1 — Editor smoke test (30 min)

- [ ] Unity Hub ▸ New 3D project `QuickActionsTestbed`; open it.
- [ ] Package Manager ▸ + ▸ *Add package from disk…* ▸ this folder's `package.json`.
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
- [ ] Upload `dist~/QuickActions.unitypackage` via Asset Store Publishing Tools **from the 2021.3 editor** (upload version = listed minimum).
- [ ] Price: **Free**. Submit for review. (Review: days → ~2 weeks.)

## Phase 6 — stamp the release (10 min, back in the repo)

- [ ] `CHANGELOG.md`: replace `Unreleased` with today's date.
- [ ] If device-validated and you're confident: bump `0.1.0` → `1.0.0` in
      `package.json` + CHANGELOG heading (else stay 0.1.0 — honest pre-1.0).
- [ ] `git tag quick-actions/v<version> && git push origin quick-actions/v<version>`
- [ ] (When repo goes public) OpenUPM one-time submission: `plans/openupm.md`.

## If something breaks

| Symptom | First move |
|---|---|
| Console errors on import / platform switch | Copy the exact error → bring it back to me (likely the asmdef extension-DLL seam — known unproven spot) |
| `QuickActions` type not found | The define (Phase 1, "THE step") — per platform tab |
| Shortcuts don't appear on long-press | Did you tap "Add 3 shortcuts" first? Android ≥ 7.1? |
| Tap opens app but no log line | Grab `adb logcat -s QuickActions Unity` output → bring it back |
| Store review declines | Read their reason; usual fixes: real screenshot, zero console warnings, clearer description |

**Everything in Phases 0–1 and 6 is reversible; nothing here can damage the repo.**
