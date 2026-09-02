# Release Runbook — test-release week, step by step

The condensed, day-of sequence. Details live in
[`GETTING_STARTED.md`](./GETTING_STARTED.md) (referenced as **GS**) and
[`STORE_CHECKLIST.md`](./STORE_CHECKLIST.md) (**SC**). Decisions already made:
**price = FREE · gate = KEPT (dev-only)** — nothing to re-decide on the day.

## Phase 0 — before Unity (15 min, any OS)

- [ ] `git clone https://github.com/emindeniz99/unity-quick-actions.git`
- [ ] `cd unity-quick-actions && tools~/setup.sh && tools~/verify.sh`
      → expect **`VERIFY: PASS`** (if not, stop: the checkout is broken, nothing
      else will work — re-clone before going further).
- [ ] Install **Unity Hub** + the two editors on the ends of the supported
      range: **2021.3 LTS** and **Unity 6.3**, each with **Android Build Support**
      (+ iOS Build Support if on a Mac). *(GS §0)*
- [ ] Phone ready: Android 7.1+ with **USB debugging** enabled.

> **Matrix rule (current plan):** the two ends of the supported range get the
> FULL bar — repeat **Phases 1–3** (and Phase 4 on a Mac) in **2021.3 LTS** and
> in **Unity 6.3**. The lines in between (2022.3, 6.0) have since had their own
> Editor pass too (import + Test Runner green; 2022.3 also builds the Android
> player) — see `PRODUCTION_READINESS.md` for exactly how far each line got.
> One testbed project per editor version (don't upgrade one
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
      If an asmdef/DLL error appears here, copy the exact message — it belongs
      in an issue. (This seam has since compiled cleanly on 2021.3, 2022.3, 6.0
      and 6.3, so an error here would be news.)
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
- [ ] 📸 **Take the long-press screenshot now**, then composite it to listing
      size — do not save the raw capture over the listing image, a portrait
      phone screen becomes a sliver in a landscape thumbnail:
      `python3 tools~/make_store_screenshot.py <capture.png> store~/screenshot-1.jpg`
      (JPEG, because a device capture is photographic). An iOS Simulator
      version is already committed; this replaces it with an Android one if you
      want that platform shown. `gen_store_images.py` skips screenshot-1 when
      either extension exists, so it will not clobber your capture.
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

> **SUBMITTED 2026-08-07** — done, as [`STORE_CHECKLIST.md`](./STORE_CHECKLIST.md)
> records (package 0.4.4, Free, auto-publish ON). The boxes below are ticked as
> the record of that submission; do not re-submit — a later version goes
> through the portal's *update* flow for the existing package.

- [x] publisher.unity.com → create the free Publisher profile, accept agreement.
- [x] Portal ▸ Create new package draft.
- [x] Paste from `store~/listing/`: title/metadata (`metadata.md`),
      `summary.txt`, `description.md` (keep the **"One switch to turn it on"**
      section — the gate is the product), `tags.txt`.
- [x] Upload images from `store~/` (icon/card/cover/social + screenshots incl.
      your real one).
- [x] Upload the `.unitypackage` via Asset Store Publishing Tools **from a
      2022.3+ editor** — store rule 1.3.a bans uploading from older editors, so
      the listing floor reads 2022.3 while OpenUPM keeps serving 2021.3. It is a build output,
      not a repo file: build it with `python3 tools~/pack_unitypackage.py` (or
      `tools~/release.sh`) → gitignored `dist~/QuickActions.unitypackage`, or take
      the one attached to the
      [GitHub Release](https://github.com/emindeniz99/unity-quick-actions/releases)
      — `v0.4.0` is the first release and carries it.
- [x] Price: **Free**. Submit for review. (Review: days → ~2 weeks.)

## Phase 6 — stamp the release (10 min, back in the repo)

Releases are cut by **merging**, not by tagging.
[`.github/workflows/release.yml`](./.github/workflows/release.yml) notices that
main declares a version with no tag, runs `verify.sh`, packs the
`.unitypackage`, creates `v<version>` at the merge commit and publishes the
Release with your `CHANGELOG.md` section as its notes. Full procedure:
[`MAINTAINING.md` § Cutting a release](./MAINTAINING.md#cutting-a-release).
Tags are plain semver, one per release; `v0.4.0`, cut 2026-08-07, was the first.

- [ ] **In the release PR itself, not afterwards:** `package.json` `version` and
      the top `CHANGELOG.md` heading agree, and the heading has a real date (no
      `Unreleased` left). `verify.sh` check 6 fails the PR otherwise — the
      release is cut from those two files, so the bump must be in the commit
      that gets tagged. The same commit moves the `#v<version>` install pins
      (README, GETTING_STARTED, CONTRIBUTING, CLAUDE.md), the README's
      OpenUPM version snippet and its Status line (`This is **<version>**`) —
      check 6 fails the PR when any of them still names the previous version.
- [ ] Stay in `0.x` until the matrix above has actually been walked on devices;
      a `1.0.0` is a claim of "validated on both ends of the supported range",
      not a mood. Bumping later is cheap.
- [ ] Merge the PR with a real merge commit (never squash), then watch the
      **release** workflow. Do **not** push the tag by hand: the workflow
      creates it, and a hand-pushed tag either collides or races the run.
- [ ] Check the resulting **GitHub Release** carries the built
      `QuickActions.unitypackage` asset (the workflow attaches it; if it's
      missing, build it with `tools~/release.sh` and upload it by hand) —
      docs point downloaders at
      <https://github.com/emindeniz99/unity-quick-actions/releases>.
- [x] OpenUPM one-time submission — **done**: `com.emindeniz99.quick-actions`
      0.4.0 through 0.4.9 are listed on OpenUPM (checked 2026-09-01) and every
      later tag is picked up automatically. Procedure, should it ever need
      redoing: [`docs~/publishing-to-openupm.md`](./docs~/publishing-to-openupm.md).

## If something breaks

| Symptom | First move |
|---|---|
| Console errors on import / platform switch | Copy the exact error and open an issue (first suspect is the asmdef extension-DLL seam, though it now compiles clean on all four claimed lines) |
| `QuickActions` type not found | The define (Phase 1, "THE step") — per platform tab |
| Shortcuts don't appear on long-press | Did you tap "Add 3 shortcuts" first? Android ≥ 7.1? |
| Tap opens app but no log line | Grab `adb logcat -s QuickActions Unity` output and attach it to the issue |
| Store review declines | Read their reason; usual fixes: real screenshot, zero console warnings, clearer description |

**Everything in Phases 0–1 and 6 is reversible; nothing here can damage the repo.**
