# Asset Store listing — fields (paste-ready)

Fill these into the Publisher portal package draft. Long-form copy is in
[`description.md`](./description.md).

| Field | Value |
|-------|-------|
| **Title** | Home-Screen Quick Actions (iOS & Android) |
| **Category** | Tools ▸ Integration |
| **Summary** | See [`summary.txt`](./summary.txt) |
| **Description** | See [`description.md`](./description.md) |
| **Tags / keywords** | See [`tags.txt`](./tags.txt) |
| **Version** | 0.4.1 (bump to 1.0.0 after device validation) |
| **Supported Unity** | 2021.3 LTS or higher (incl. Unity 6) — upload from a 2021.3 editor so the portal lists that minimum |
| **Render pipelines** | Built-in, URP, HDRP (no rendering — all compatible) |
| **Scripting backends** | Mono + IL2CPP |
| **Platforms** | iOS 9+, Android 7.1+ (API 25) |
| **Price** | **Free** (decided; may switch to paid ≥ $4.99 later) |
| **License** | Extension Asset / MIT source included |

## Key images (from `store~/`)

- Icon `store~/icon.png` (160×160)
- Card `store~/card.png` (420×280)
- Cover `store~/cover.png` (1950×1300)
- Social `store~/social.png` (1200×630)
- Screenshots `store~/screenshot-1..3.png` (2400×1600)

## Package upload

Upload `QuickActions.unitypackage`. It is a build output, not a committed file:
build it with `python3 tools/pack_unitypackage.py` (or `tools/release.sh`) →
`dist~/QuickActions.unitypackage`, or download the artifact attached to the
matching [GitHub Release](https://github.com/emindeniz99/unity-quick-actions/releases)
(`v0.4.0` is the first one).
It installs the package under `Assets/QuickActions/`.
