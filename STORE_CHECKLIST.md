# Unity Asset Store — submission checklist

Everything needed to publish **Quick Actions for iOS & Android**. Sources:
[Submission Guidelines](https://assetstore.unity.com/publishing/submission-guidelines),
[Start publishing](https://assetstore.unity.com/publishing/publish-and-sell-assets),
[Marketing image sizes](https://support.unity.com/hc/en-us/articles/210122403).

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

## 2. Package metadata (draft below — reuse verbatim)

- [ ] **Title:** Quick Actions for iOS & Android
- [ ] **Category:** Tools ▸ Integration
- [ ] **Summary (≤ ~200 chars):** "Home-screen quick actions (long-press app-icon
      shortcuts) for iOS & Android. Create them at runtime or bake them into the
      build, get a tap callback — one C# API, zero native edits."
- [ ] **Description:** adapt [`README.md`](./README.md) (features, API, How it
      works, limitations). Keep it benefit-led; lead with the long-press value.
- [ ] **Tags:** ios, android, quick actions, app shortcuts, home screen,
      shortcuts, mobile, UIApplicationShortcutItem, ShortcutManager.
- [ ] **Version:** `0.1.0` (matches `package.json` + `CHANGELOG.md`).
- [ ] **Supported Unity:** 2022.3 LTS and newer (incl. Unity 6).
- [ ] **Render pipelines:** Built-in / URP / HDRP (no rendering — all compatible).
- [ ] **Price:** decide free vs paid (≥ $4.99).

## 3. Marketing images (generated → `store/`)

All sizes are pre-built by `python3 tools/gen_store_images.py`. See
[`store/README.md`](./store/README.md).

- [ ] **Icon** 160×160, no text — `store/icon.png`
- [ ] **Card** 420×280 — `store/card.png`
- [ ] **Cover** 1950×1300 — `store/cover.png`
- [ ] **Social** 1200×630 — `store/social.png`
- [ ] **Screenshots** ≥1200w (2400×1600 here), ≥1 — `store/screenshot-*.png`
- [ ] ⚠️ Replace `screenshot-1` with a **real on-device** long-press capture
      before submitting (store prefers in-context shots over mockups). Optional
      but improves approval odds & conversion.
- [ ] No watermarks; minimal text; not blurry/stretched; not bare Editor shots.

## 4. Package contents — what to ship (and what to strip)

Export a **clean** `.unitypackage` (see
[`tools/export-unitypackage.md`](./tools/export-unitypackage.md)) containing
ONLY the package:

- [ ] Include: `Runtime/`, `Editor/`, `Plugins/`, `Samples~/`, `package.json`,
      `README.md`, `CHANGELOG.md`, `LICENSE.md`, `ROADMAP.md`.
- [ ] **Exclude dev-only folders:** `.verify/`, `tools/`, `plans/`, `store/`,
      `STORE_CHECKLIST.md` — these are repo/publishing collateral, not for buyers.
- [ ] Every included asset has a committed `.meta` (run `tools/verify.sh`).
- [ ] Ships **full source** for the native plugins (`.mm`, `.java`) — no
      precompiled-only DLLs (allowed, but source is required for review trust).

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
