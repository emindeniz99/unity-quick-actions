#!/usr/bin/env python3
"""Generate Asset Store marketing images and example shortcut icons.

Outputs to store~/ at the exact sizes the Unity Asset Store requires:
  icon 160x160, card 420x280, cover 1950x1300, social 1200x630,
  screenshots 2400x1600 (>=1200 wide). Plus example shortcut drawables.

These are clean, on-brand placeholders/mockups — replace the screenshots with
real on-device captures before publishing (the store rejects pure Editor shots).
Run: python3 tools/gen_store_images.py
"""
import os
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "store~")
ICONS = os.path.join(OUT, "example-shortcut-icons")
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
FONT_B = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
MONO = "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf"

BG = (14, 17, 22)          # near-black
BG2 = (24, 28, 38)         # panel
ACCENT = (108, 92, 231)    # purple
ACCENT2 = (91, 141, 239)   # blue
INK = (236, 239, 244)      # near-white
MUTE = (150, 158, 172)


def f(path, size):
    return ImageFont.truetype(path, size)


def vgrad(w, h, top, bot):
    img = Image.new("RGB", (w, h), top)
    px = img.load()
    for y in range(h):
        t = y / max(1, h - 1)
        c = tuple(int(top[i] + (bot[i] - top[i]) * t) for i in range(3))
        for x in range(w):
            px[x, y] = c
    return img


def bolt(draw, cx, cy, s, color):
    """A lightning bolt centered at (cx,cy), scale s."""
    pts = [(0.15, -1.0), (-0.55, 0.15), (-0.05, 0.15), (-0.2, 1.0),
           (0.55, -0.2), (0.05, -0.2)]
    draw.polygon([(cx + x * s, cy + y * s) for x, y in pts], fill=color)


def rounded(draw, box, r, fill):
    draw.rounded_rectangle(box, radius=r, fill=fill)


def brand_tile(size, radius_ratio=0.22):
    """The brand mark: gradient rounded tile + lightning bolt."""
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    tile = vgrad(size, size, ACCENT2, ACCENT).convert("RGBA")
    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, size - 1, size - 1],
                                           radius=int(size * radius_ratio), fill=255)
    img.paste(tile, (0, 0), mask)
    bolt(d, size * 0.52, size * 0.5, size * 0.30, (255, 255, 255, 255))
    return img


def text(draw, xy, s, font, fill, anchor=None):
    draw.text(xy, s, font=font, fill=fill, anchor=anchor)


def make_icon():
    img = brand_tile(160)
    img.save(os.path.join(OUT, "icon.png"))


def make_card():
    img = vgrad(420, 280, (18, 22, 30), BG)
    d = ImageDraw.Draw(img)
    img.paste(brand_tile(96), (28, 92), brand_tile(96))
    text(d, (150, 104), "Quick Actions", f(FONT_B, 30), INK)
    text(d, (150, 144), "iOS & Android", f(FONT_B, 30), ACCENT2)
    text(d, (150, 196), "Home-screen app shortcuts", f(FONT, 17), MUTE)
    img.save(os.path.join(OUT, "card.png"))


def phone_mock(d, x, y, w, h, actions):
    """Draw a stylized phone showing a long-press quick-action menu."""
    rounded(d, [x, y, x + w, y + h], int(w * 0.10), (8, 10, 14))
    rounded(d, [x + 6, y + 6, x + w - 6, y + h - 6], int(w * 0.09), (20, 24, 32))
    # app icon
    icx, icy, isz = x + w // 2 - 34, y + int(h * 0.16), 68
    tile = brand_tile(isz)
    d._image.paste(tile, (icx, icy), tile)
    # quick-action menu panel
    pw, ph = int(w * 0.78), 52 * len(actions) + 16
    px, py = x + (w - pw) // 2, icy + isz + 22
    rounded(d, [px, py, px + pw, py + ph], 18, (32, 36, 48))
    for i, (glyph, label) in enumerate(actions):
        ly = py + 8 + i * 52
        if i:
            d.line([px + 14, ly, px + pw - 14, ly], fill=(50, 55, 68), width=1)
        d.ellipse([px + 16, ly + 12, px + 44, ly + 40], fill=glyph)
        text(d, (px + 60, ly + 16), label, f(FONT, 20), INK)


def make_cover():
    w, h = 1950, 1300
    img = vgrad(w, h, (16, 20, 28), (10, 12, 16))
    d = ImageDraw.Draw(img)
    d._image = img
    img.paste(brand_tile(150), (150, 150), brand_tile(150))
    text(d, (330, 158), "Quick Actions", f(FONT_B, 92), INK)
    text(d, (330, 262), "for iOS & Android", f(FONT_B, 64), ACCENT2)
    for i, s in enumerate([
        "Long-press the app icon → shortcuts",
        "Dynamic at runtime + static in build",
        "One C# API · no native edits",
        "Unity 2022 LTS → Unity 6",
    ]):
        text(d, (330, 430 + i * 78), "•  " + s, f(FONT, 40), MUTE)
    phone_mock(d, 1320, 360, 470, 820, [
        (ACCENT, "New Game"), (ACCENT2, "Continue"),
        ((230, 90, 120), "Daily Reward"), ((90, 200, 160), "Settings"),
    ])
    img.save(os.path.join(OUT, "cover.png"))


def make_social():
    w, h = 1200, 630
    img = vgrad(w, h, (16, 20, 28), (10, 12, 16))
    d = ImageDraw.Draw(img)
    d._image = img
    img.paste(brand_tile(120), (90, 90), brand_tile(120))
    text(d, (240, 96), "Quick Actions", f(FONT_B, 66), INK)
    text(d, (240, 176), "for iOS & Android", f(FONT_B, 46), ACCENT2)
    text(d, (92, 300), "Home-screen app shortcuts for Unity games.", f(FONT, 34), INK)
    text(d, (92, 352), "Runtime + static, one callback, no native edits.", f(FONT, 30), MUTE)
    phone_mock(d, 860, 150, 250, 430, [
        (ACCENT, "New Game"), (ACCENT2, "Continue"), ((230, 90, 120), "Daily"),
    ])
    img.save(os.path.join(OUT, "social.png"))


def banner(d, w, title, sub):
    d.rectangle([0, 0, w, 150], fill=BG2)
    text(d, (60, 38), title, f(FONT_B, 54), INK)
    text(d, (60, 100), sub, f(FONT, 30), MUTE)


def make_shot1():
    w, h = 2400, 1600
    img = vgrad(w, h, (16, 20, 28), (10, 12, 16))
    d = ImageDraw.Draw(img)
    d._image = img
    banner(d, w, "Long-press → instant shortcuts",
            "The actions users see when they press your app icon")
    phone_mock(d, w // 2 - 320, 240, 640, 1120, [
        (ACCENT, "New Game"), (ACCENT2, "Continue Run"),
        ((230, 90, 120), "Daily Reward"), ((90, 200, 160), "Settings"),
    ])
    img.save(os.path.join(OUT, "screenshot-1.png"))


def make_shot2():
    w, h = 2400, 1600
    img = vgrad(w, h, (16, 20, 28), (10, 12, 16))
    d = ImageDraw.Draw(img)
    banner(d, w, "One simple C# API", "Add, remove, and react to taps")
    code = [
        ("using Playground.QuickActions;", MUTE),
        ("", INK),
        ("QuickActions.Performed += id => Route(id);", INK),
        ("", INK),
        ("QuickActions.Add(new QuickActionItem(", INK),
        ('    id: \"new_game\", title: \"New Game\",', INK),
        ("    subtitle: \"Start fresh\", icon: IconType.Add));", INK),
        ("", INK),
        ("// Static shortcuts: Project Settings ▸ Quick Actions", (120, 200, 140)),
    ]
    rounded(d, [120, 240, w - 120, 240 + len(code) * 70 + 60], 24, (22, 26, 34))
    fm = f(MONO, 44)
    for i, (line, col) in enumerate(code):
        text(d, (170, 290 + i * 70), line, fm, col)
    img.save(os.path.join(OUT, "screenshot-2.png"))


def make_shot3():
    w, h = 2400, 1600
    img = vgrad(w, h, (16, 20, 28), (10, 12, 16))
    d = ImageDraw.Draw(img)
    banner(d, w, "Built for shipping", "Everything you need, nothing you don't")
    feats = [
        ("Dynamic + static", "Create at runtime or bake into the build"),
        ("iOS + Android", "UIApplicationShortcutItem & ShortcutManager"),
        ("Zero native edits", "App-delegate swizzling + trampoline activity"),
        ("Unity 2022 → 6", "Survives the UnityPlayerActivity → GameActivity change"),
        ("Cold + warm taps", "Performed event & LastPerformed"),
        ("Tested + documented", "Unit tests, samples, full README"),
    ]
    cw, ch, gx, gy = 1060, 360, 120, 220
    for i, (t, s) in enumerate(feats):
        cx = gx + (i % 2) * (cw + 80)
        cy = gy + (i // 2) * (ch + 60)
        rounded(d, [cx, cy, cx + cw, cy + ch], 28, (22, 26, 34))
        bolt(d, cx + 70, cy + 90, 46, ACCENT2)
        text(d, (cx + 130, cy + 54), t, f(FONT_B, 48), INK)
        text(d, (cx + 40, cy + 170), s, f(FONT, 34), MUTE)
    img.save(os.path.join(OUT, "screenshot-3.png"))


def make_shortcut_icons():
    """White glyphs on transparent — drop into res/drawable on Android."""
    specs = {
        "ic_quickaction_play": "play",
        "ic_quickaction_add": "add",
        "ic_quickaction_favorite": "star",
        "ic_quickaction_compose": "pencil",
    }
    for name, kind in specs.items():
        s = 96
        img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)
        w = (255, 255, 255, 255)
        if kind == "play":
            d.polygon([(34, 26), (34, 70), (72, 48)], fill=w)
        elif kind == "add":
            d.rectangle([44, 24, 52, 72], fill=w)
            d.rectangle([24, 44, 72, 52], fill=w)
        elif kind == "star":
            import math
            pts = []
            for k in range(10):
                ang = math.pi / 2 + k * math.pi / 5
                r = 34 if k % 2 == 0 else 15
                pts.append((48 + r * math.cos(ang), 48 - r * math.sin(ang)))
            d.polygon(pts, fill=w)
        elif kind == "pencil":
            d.polygon([(28, 70), (32, 56), (60, 28), (72, 40), (44, 68)], fill=w)
        img.save(os.path.join(ICONS, name + ".png"))


def main():
    os.makedirs(OUT, exist_ok=True)
    os.makedirs(ICONS, exist_ok=True)
    make_icon(); make_card(); make_cover(); make_social()
    make_shot1(); make_shot2(); make_shot3()
    make_shortcut_icons()
    print("store images written to", OUT)


if __name__ == "__main__":
    main()
