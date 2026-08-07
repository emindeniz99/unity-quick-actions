#!/usr/bin/env python3
"""Composite a REAL device/Simulator capture onto the Asset Store canvas.

The store wants screenshots at least 1200 px wide, and prefers real in-context
shots over mockups. A raw phone capture satisfies neither well on its own: a
portrait phone screen is the wrong shape for a landscape listing thumbnail, and
gets letterboxed into a sliver.

This scales the capture to fill the canvas height, centres it, and fills the
background with the same gradient `gen_store_images.py` uses, so a genuine
screenshot sits in the same visual family as the generated art.

  python3 tools~/make_store_screenshot.py <capture.png> store~/screenshot-1.jpg

Needs Pillow (dev-only):  pip install Pillow
"""
from __future__ import annotations

import os
import sys

try:
    from PIL import Image
except ImportError:
    sys.exit("This script needs Pillow (dev-only): pip install Pillow")

W, H = 2400, 1600          # the size the store listing uses
TOP = (16, 20, 28)         # matches gen_store_images.vgrad()
BOT = (10, 12, 16)
MARGIN = 90                # breathing room above/below the device


def vgrad(w: int, h: int, top, bot) -> "Image.Image":
    img = Image.new("RGB", (w, h), top)
    px = img.load()
    for y in range(h):
        t = y / max(1, h - 1)
        c = tuple(int(top[i] + (bot[i] - top[i]) * t) for i in range(3))
        for x in range(w):
            px[x, y] = c
    return img


def main(argv: list[str]) -> int:
    if len(argv) != 3:
        sys.exit(__doc__)
    src_path, out_path = argv[1], argv[2]
    if not os.path.isfile(src_path):
        sys.exit(f"no such capture: {src_path}")

    shot = Image.open(src_path).convert("RGB")
    target_h = H - 2 * MARGIN
    scale = target_h / shot.height
    new_w = max(1, int(shot.width * scale))
    if new_w > W - 2 * MARGIN:                  # very wide capture: fit width
        scale = (W - 2 * MARGIN) / shot.width
        new_w = W - 2 * MARGIN
        target_h = int(shot.height * scale)
    shot = shot.resize((new_w, target_h), Image.LANCZOS)

    canvas = vgrad(W, H, TOP, BOT)
    canvas.paste(shot, ((W - new_w) // 2, (H - target_h) // 2))
    # A device capture is photographic (wallpaper gradients, blur), where PNG is
    # the wrong container: the same image is ~70% smaller as JPEG q90 with no
    # visible difference. The generated flat art stays PNG, where PNG wins.
    if os.path.splitext(out_path)[1].lower() in (".jpg", ".jpeg"):
        canvas.save(out_path, quality=90, optimize=True)
    else:
        canvas.save(out_path)
    print(f"wrote {out_path} ({W}x{H}) from {src_path} "
          f"({Image.open(src_path).size[0]}x{Image.open(src_path).size[1]})")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
