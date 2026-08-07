# Store / marketing assets

Publishing collateral for the Unity Asset Store. **Not** part of the importable
package (excluded from `.meta` generation), so it ships no runtime weight.

Regenerate with `python3 tools~/gen_store_images.py`.

| File | Size | Asset Store slot |
|------|------|------------------|
| `icon.png` | 160×160 | Icon (grid thumbnail; no text — per guidelines) |
| `card.png` | 420×280 | Card (browse thumbnail) |
| `cover.png` | 1950×1300 | Cover (main product image) |
| `social.png` | 1200×630 | Social media |
| `screenshot-1.png` | 2400×1600 | Screenshot — long-press menu |
| `screenshot-2.png` | 2400×1600 | Screenshot — C# API |
| `screenshot-3.png` | 2400×1600 | Screenshot — feature grid |
| `example-shortcut-icons/*.png` | 96×96 | Example Android shortcut drawables |
| `device-ios.jpg` | 720×746 | **Real capture** — iOS long-press menu (README) |
| `device-android.jpg` | 440×580 | **Real capture** — Android static shortcuts on a cold install (README) |
| `device-android-dynamic.jpg` | 440×680 | **Real capture** — Android after a runtime `Add` (README) |

The three `device-*.jpg` files are genuine captures, cropped to the icon and
menu, saved as JPEG: these are photographic (gradient wallpapers), where q75 is
~5× smaller than PNG at the same visible quality (680 KB → 140 KB, measured).
The root README references them by absolute `raw.githubusercontent.com` URL,
because that README is also rendered as the package's front page on OpenUPM,
where relative paths are unreliable.

## Before publishing

- The three screenshots are **clean mockups**, not live captures. The store
  prefers real in-context shots — replace `screenshot-1` with an actual
  on-device long-press of the demo, and `screenshot-2` with a real IDE view if
  you want. Mockups are acceptable but real device shots convert better.
- Images contain minimal text and no watermarks (guideline-compliant). Keep the
  icon text-free.
- `example-shortcut-icons/` are functional: drop them into your app's
  `Assets/.../Plugins/Android/res/drawable/` (or any `res/drawable`) and set
  `QuickActionItem.AndroidDrawable = "ic_quickaction_play"` to use them.

See [`../MAINTAINING.md`](../MAINTAINING.md) for the release procedure.
