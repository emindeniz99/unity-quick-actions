# Unity Asset Store — submission checklist

Everything needed to publish **Quick Actions for iOS & Android**. Sources:
[Submission Guidelines](https://assetstore.unity.com/publishing/submission-guidelines),
[Start publishing](https://assetstore.unity.com/publishing/publish-and-sell-assets),
[Marketing image sizes](https://support.unity.com/hc/en-us/articles/210122403).

## Status — done vs needs-you

✅ **Done in repo (run `tools/release.sh` to regenerate all):**
listing texts (`store/listing/`), marketing images (`store/`), prebuilt
drag-and-drop package (`dist/QuickActions.unitypackage`), compile + 15 unit
tests green, full source + docs.

⏳ **Needs your account / hardware (can't be automated):**
1. Create the free publisher account + accept the agreement.
2. Open the package in a **licensed** Unity (2022.3 LTS + Unity 6) and confirm a
   clean import; switch to iOS/Android targets.
3. **Device test** (Android device; iOS via macOS/Xcode) + one real screenshot.
4. Pick a price, upload `dist/QuickActions.unitypackage`, submit for review.

---

## 0. Cost & account (the "is it free?" answer)

- **Publishing is free.** No fee to create a publisher account or list assets.
- **Revenue split: you keep 70%, Unity takes 30%** (standard, non-negotiable).
- **Price:** free, or paid with a **$4.99 minimum**. (The reference asset sells
  for $7.) You can launch free to gather reviews, then switch to paid.
- **Account needed:** a Unity ID + a **Publisher account** at
  <https://publisher.unity.com> (one publisher profile per seller).
- **Payouts:** monthly via PayPal or quarterly via bank transfer.
- A verified Unity ID and accepting the **Provider Agreement** are required.

## 1. Publisher account setup

- [ ] Create/verify Unity ID, then a Publisher profile at publisher.unity.com.
- [ ] Fill publisher name, description, logo, and payout (PayPal/bank) details.
- [ ] Accept the Asset Store Provider Agreement.

## 2. Package metadata — ✅ written, paste-ready in `store/listing/`

All copy is prepared in [`store/listing/`](./store/listing/) — paste verbatim:
- [x] **Title / Category / Version / compatibility:** [`metadata.md`](./store/listing/metadata.md)
- [x] **Summary:** [`summary.txt`](./store/listing/summary.txt)
- [x] **Description:** [`description.md`](./store/listing/description.md)
- [x] **Tags / keywords:** [`tags.txt`](./store/listing/tags.txt)
- [ ] **Price:** decide free vs paid (≥ $4.99) — your call.

## 3. Marketing images (generated → `store/`)

All sizes are ✅ pre-built by `python3 tools/gen_store_images.py`. See
[`store/README.md`](./store/README.md).

- [x] **Icon** 160×160, no text — `store/icon.png`
- [x] **Card** 420×280 — `store/card.png`
- [x] **Cover** 1950×1300 — `store/cover.png`
- [x] **Social** 1200×630 — `store/social.png`
- [x] **Screenshots** ≥1200w (2400×1600 here), ≥1 — `store/screenshot-*.png`
- [ ] ⚠️ Replace `screenshot-1` with a **real on-device** long-press capture
      before submitting (store prefers in-context shots over mockups). Optional
      but improves approval odds & conversion.
- [ ] No watermarks; minimal text; not blurry/stretched; not bare Editor shots.

## 4. Package contents — what to ship (and what to strip)

✅ A clean `.unitypackage` is **prebuilt** at `dist/QuickActions.unitypackage`
(`tools/pack_unitypackage.py`, no Unity needed). It already:

- [x] Includes only package content, remapped under `Assets/QuickActions/`
      (Runtime, Editor, Plugins, Example, README/CHANGELOG/LICENSE/ROADMAP).
- [x] Excludes dev/publishing collateral (`.verify/`, `tools/`, `plans/`,
      `store/`, `dist/`, `Tests/`, `STORE_CHECKLIST.md`, `package.json`).
- [x] Carries every asset's committed `.meta` GUID (scene→script refs intact).
- [x] Ships **full source** for the native plugins (`.mm`, `.java`).
- [ ] Import it once in a licensed Editor to confirm before uploading.

## 5. Technical review gates (do before upload)

- [ ] Imports into a fresh **2022.3 LTS** project with **zero console
      errors/warnings**; repeat on **Unity 6**.
- [ ] `tools/verify.sh` is green (compile + 15 unit tests + Android plugin).
- [ ] Switch build target to **iOS** and **Android** → Editor still compiles
      (confirms the asmdef extension-DLL references resolve — see ROADMAP).
- [ ] Build to an **iOS device** (needs macOS/Xcode) and an **Android device**:
      long-press the icon shows the demo's shortcuts; tapping routes correctly
      (cold + warm). This is the one step that needs real hardware.
- [ ] No use of another publisher's assets; all art (`store/`) is original.
- [ ] Unique root namespace (`Playground.QuickActions`) and asmdef names.
- [ ] Demo lives under `Samples~/` and imports cleanly via the package page.

## 6. Submit

- [ ] Install **Asset Store Publishing tools** (Publisher portal → "Create new
      package" → use the Unity Editor uploader / Asset Store Tools package).
- [ ] Create the draft package in the portal, attach metadata + images.
- [ ] Upload the clean `.unitypackage` for the supported Unity version(s).
- [ ] Set price/availability and **Submit for review**.
- [ ] Review is manual (typically several business days to ~2 weeks). Expect a
      pass or a decline with specific feedback to address and resubmit.

## 7. Pre-flight one-liner

```bash
tools/verify.sh && python3 tools/gen_store_images.py   # green + fresh images
```

Then export the clean package and upload.
