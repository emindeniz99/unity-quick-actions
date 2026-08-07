# Store / marketing assets

Publishing collateral for the Unity Asset Store. **Not** part of the importable
package (excluded from `.meta` generation), so it ships no runtime weight.

Regenerate with `python3 tools/gen_store_images.py`.

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
