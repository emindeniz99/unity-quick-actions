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
| `screenshot-1.jpg` | 2400×1600 | **Real capture** — iOS Simulator long-press menu, composited by `tools~/make_store_screenshot.py` |
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

- `screenshot-1.jpg` is now a **real capture**. `screenshot-2` and `-3` are
  still generated mockups; the store accepts those, but a real IDE view for
  `-2` would convert better. Regenerate `-1` from any device capture with
  `python3 tools~/make_store_screenshot.py <capture.png> store~/screenshot-1.jpg`.
- Images contain minimal text and no watermarks (guideline-compliant). Keep the
  icon text-free.
- `example-shortcut-icons/` are functional PNGs, but the instruction that used
  to be here was **wrong and harmful**: it named
  `Assets/.../Plugins/Android/res/drawable/`, a path Unity **removed in 2021.2**
  — one minor version below this package's 2021.3 floor. Files placed there do
  not silently fail to load; they **fail the consumer's build**
  ("OBSOLETE - Providing Android resources in Assets/Plugins/Android/res was
  removed"). The correct procedure is in the root
  [README](../README.md#android-icons-need-a-drawable-in-your-project); use that.

See [`../MAINTAINING.md`](../MAINTAINING.md) for the release procedure.
