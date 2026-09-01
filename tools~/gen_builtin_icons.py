#!/usr/bin/env python3
"""Generate Editor/Android/QuickActionsBuiltInIcons.cs — the built-in Android
shortcut icons as PNG bytes embedded in C#.

Why embed
---------
On Android the Java bridge resolves an IconType by NAME: getIdentifier(
"ic_quickaction_" + name, "drawable", pkg). The package therefore has to get a
drawable with that name into the app, and the build post-processor already
writes into the generated Gradle project. Shipping the PNGs as package assets
would mean resolving the package's own path at build time — which differs
between a Git/OpenUPM install (Packages/…) and a .unitypackage one (Assets/…) —
plus a .meta per binary. Embedding the bytes makes every channel deliver the
same bytes, and lets the headless harness compare what the post-processor
wrote against the source of truth. ~2 KB of generated source.

Why no Pillow
-------------
tools~/verify.sh runs `--check` on every push, in CI containers that install
only dotnet and a JDK. The glyphs are a few polygons, so they are rasterised
here directly (supersampled, even-odd fill) and encoded with zlib. The output
is byte-deterministic: same source, same file.

Usage
-----
  python3 tools~/gen_builtin_icons.py            # (re)write the .cs
  python3 tools~/gen_builtin_icons.py --check    # exit 1 if the .cs is stale
  python3 tools~/gen_builtin_icons.py --preview  # ASCII proof of each glyph
  python3 tools~/gen_builtin_icons.py --png-out DIR   # dump the PNGs
"""
from __future__ import annotations

import argparse
import base64
import hashlib
import math
import pathlib
import struct
import sys
import zlib

ROOT = pathlib.Path(__file__).resolve().parent.parent
OUTPUT = ROOT / "Editor" / "Android" / "QuickActionsBuiltInIcons.cs"

# drawable-xhdpi: 48dp at 2x. One density only — a white/dark glyph upscales
# cleanly, and this is the qualifier the README tells users to use for their
# own drawables, so a user file and ours land in the same bucket.
SIZE = 96
DENSITY = "xhdpi"
SUPERSAMPLE = 4  # per axis → 16 coverage samples per pixel

# ---- ART STYLE --------------------------------------------------------------
# A legacy (non-adaptive) shortcut drawable is shown RAW on API 25 and WRAPPED
# onto an adaptive background by API 26+ launchers (Launcher3 wraps onto white),
# so the icon must carry its own contrast: a filled disc behind the glyph.
# Change these and re-run; the harness compares bytes, not looks.
BACKGROUND = (0x3F, 0x51, 0xB5)   # indigo disc; None = transparent (glyph only)
BACKGROUND_RADIUS = 46            # of 48 — a 2 px transparent margin
GLYPH = (0xFF, 0xFF, 0xFF)

# (IconType member, drawable-name suffix, glyph)
# The suffix must equal the Java ICON_NAMES entry for that IconType value —
# the harness test pins it against the .java file. Sorted by suffix.
ICONS = [
    ("Add", "add", "add"),
    ("Compose", "compose", "pencil"),
    ("Favorite", "favorite", "star"),
    ("Play", "play", "play"),
]

# Same geometry as tools~/gen_store_images.py make_shortcut_icons(), on a 96
# canvas, so the store screenshots and the shipped icons agree.


def star_points():
    pts = []
    for k in range(10):
        ang = math.pi / 2 + k * math.pi / 5
        r = 34 if k % 2 == 0 else 15
        pts.append((48 + r * math.cos(ang), 48 - r * math.sin(ang)))
    return pts


def rect(x0, y0, x1, y1):
    # PIL's rectangle() is inclusive of x1/y1, so the drawn box is one pixel
    # wider than the coordinates; mirror that so the two renderers agree.
    return [(x0, y0), (x1 + 1, y0), (x1 + 1, y1 + 1), (x0, y1 + 1)]


GLYPHS = {
    "play": [[(34, 26), (34, 70), (72, 48)]],
    "add": [rect(44, 24, 52, 72), rect(24, 44, 72, 52)],
    "star": [star_points()],
    "pencil": [[(28, 70), (32, 56), (60, 28), (72, 40), (44, 68)]],
}


# ---- rasteriser -------------------------------------------------------------

def inside(poly, x, y):
    """Even-odd point-in-polygon."""
    hit = False
    n = len(poly)
    for i in range(n):
        x0, y0 = poly[i]
        x1, y1 = poly[(i + 1) % n]
        if (y0 > y) != (y1 > y):
            xi = x0 + (y - y0) * (x1 - x0) / (y1 - y0)
            if x < xi:
                hit = not hit
    return hit


def bbox(polys):
    xs = [p[0] for poly in polys for p in poly]
    ys = [p[1] for poly in polys for p in poly]
    return min(xs), min(ys), max(xs), max(ys)


def render(kind):
    """→ list of rows, each a list of straight-alpha (r, g, b, a) tuples."""
    polys = GLYPHS[kind]
    gx0, gy0, gx1, gy1 = bbox(polys)
    cx = cy = SIZE / 2
    samples = SUPERSAMPLE * SUPERSAMPLE
    rows = []
    for y in range(SIZE):
        row = []
        for x in range(SIZE):
            # Premultiplied accumulation, so partially covered edge pixels blend
            # toward transparent instead of toward black.
            sr = sg = sb = sa = 0.0
            for sy in range(SUPERSAMPLE):
                py = y + (sy + 0.5) / SUPERSAMPLE
                for sx in range(SUPERSAMPLE):
                    px = x + (sx + 0.5) / SUPERSAMPLE
                    color = None
                    if BACKGROUND is not None and (px - cx) ** 2 + (py - cy) ** 2 <= BACKGROUND_RADIUS ** 2:
                        color = BACKGROUND
                    if gx0 <= px <= gx1 and gy0 <= py <= gy1:
                        for poly in polys:
                            if inside(poly, px, py):
                                color = GLYPH
                                break
                    if color is not None:
                        sr += color[0]
                        sg += color[1]
                        sb += color[2]
                        sa += 1.0
            if sa == 0:
                row.append((0, 0, 0, 0))
            else:
                row.append((round(sr / sa), round(sg / sa), round(sb / sa),
                            round(255 * sa / samples)))
        rows.append(row)
    return rows


# ---- PNG --------------------------------------------------------------------

def chunk(tag, data):
    return (struct.pack(">I", len(data)) + tag + data
            + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF))


def encode_png(rows):
    raw = bytearray()
    for row in rows:
        raw.append(0)  # filter: None
        for r, g, b, a in row:
            raw += bytes((r, g, b, a))
    ihdr = struct.pack(">IIBBBBB", SIZE, SIZE, 8, 6, 0, 0, 0)
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr)
            + chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + chunk(b"IEND", b""))


def icons():
    """→ [(member, suffix, png_bytes)] in ICONS order."""
    return [(member, suffix, encode_png(render(kind))) for member, suffix, kind in ICONS]


# ---- C# ---------------------------------------------------------------------

def cs_literal(data, indent):
    """A base64 string, split into 76-char concatenated segments."""
    b64 = base64.b64encode(data).decode("ascii")
    parts = [b64[i:i + 76] for i in range(0, len(b64), 76)]
    return (" +\n" + indent).join('"%s"' % p for p in parts)


def generate():
    out = []
    w = out.append
    w("// <auto-generated>")
    w("//   Written by tools~/gen_builtin_icons.py. Do not edit by hand: change the")
    w("//   generator and re-run it. tools~/verify.sh fails when this file is stale.")
    w("// </auto-generated>")
    w("//")
    w("// The built-in Android shortcut icons as PNG bytes. On Android an IconType is")
    w("// resolved BY NAME at runtime — getIdentifier(\"ic_quickaction_\" + name) in")
    w("// QuickActionsBridge.java — so the drawable has to exist in the app, and the")
    w("// build post-processor writes these into the generated Gradle project. They")
    w("// are embedded rather than shipped as assets so a Git/OpenUPM install and a")
    w("// .unitypackage one deliver the same bytes with no package-path resolution,")
    w("// and so the headless harness can hold the post-processor to this source of")
    w("// truth byte for byte.")
    w("//")
    w("// %dx%d RGBA, one density bucket (drawable-%s), %s glyph%s." % (
        SIZE, SIZE, DENSITY,
        "white" if GLYPH == (255, 255, 255) else "#%02X%02X%02X" % GLYPH,
        "" if BACKGROUND is None else " on a #%02X%02X%02X disc" % BACKGROUND))
    w("namespace EminDeniz99.QuickActions.Editor")
    w("{")
    w("    internal static class QuickActionsBuiltInIcons")
    w("    {")
    w("        internal sealed class Entry")
    w("        {")
    w("            /// <summary>The catalog value this drawable renders.</summary>")
    w("            public readonly IconType Icon;")
    w("            /// <summary>The &lt;name&gt; in ic_quickaction_&lt;name&gt; — must equal the")
    w("            /// Java ICON_NAMES entry for <see cref=\"Icon\"/>'s value.</summary>")
    w("            public readonly string Name;")
    w("            private readonly string _base64;")
    w("")
    w("            public Entry(IconType icon, string name, string base64)")
    w("            {")
    w("                Icon = icon;")
    w("                Name = name;")
    w("                _base64 = base64;")
    w("            }")
    w("")
    w("            /// <summary>The PNG, decoded fresh on every call.</summary>")
    w("            public byte[] Bytes => System.Convert.FromBase64String(_base64);")
    w("        }")
    w("")
    w("        /// <summary>Pixel width and height of every entry.</summary>")
    w("        internal const int PixelSize = %d;" % SIZE)
    w("        /// <summary>The res/ qualifier the entries are written under.</summary>")
    w("        internal const string DensityQualifier = \"%s\";" % DENSITY)
    w("")
    w("        internal static readonly Entry[] Entries =")
    w("        {")
    for member, suffix, png in icons():
        w("            // sha256 %s" % hashlib.sha256(png).hexdigest())
        w("            new Entry(IconType.%s, \"%s\"," % (member, suffix))
        w("                %s)," % cs_literal(png, "                "))
    w("        };")
    w("    }")
    w("}")
    return "\n".join(out) + "\n"


# ---- CLI --------------------------------------------------------------------

def preview(rows):
    """ASCII: '#' glyph, 'o' disc, '.' transparent (2 columns per pixel row-pair)."""
    lines = []
    for y in range(0, SIZE, 2):
        line = []
        for x in range(0, SIZE, 1):
            r, g, b, a = rows[y][x]
            if a < 64:
                line.append(".")
            elif (r, g, b) == GLYPH:
                line.append("#")
            else:
                line.append("o")
        lines.append("".join(line))
    return "\n".join(lines)


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n", 1)[0])
    ap.add_argument("--check", action="store_true", help="fail if the generated file is stale")
    ap.add_argument("--preview", action="store_true", help="print an ASCII proof of each glyph")
    ap.add_argument("--png-out", metavar="DIR", help="also write ic_quickaction_<name>.png files there")
    args = ap.parse_args()

    if args.preview:
        for member, suffix, kind in ICONS:
            print("ic_quickaction_%s (IconType.%s):" % (suffix, member))
            print(preview(render(kind)))
            print()

    if args.png_out:
        d = pathlib.Path(args.png_out)
        d.mkdir(parents=True, exist_ok=True)
        for member, suffix, png in icons():
            (d / ("ic_quickaction_%s.png" % suffix)).write_bytes(png)
            print("wrote %s (%d bytes)" % (d / ("ic_quickaction_%s.png" % suffix), len(png)))

    text = generate()
    if args.check:
        current = OUTPUT.read_text(encoding="utf-8") if OUTPUT.exists() else None
        if current != text:
            print("BUILT-IN ICONS: FAIL — %s is stale (or missing)." % OUTPUT.relative_to(ROOT), file=sys.stderr)
            print("  Re-run: python3 tools~/gen_builtin_icons.py", file=sys.stderr)
            return 1
        print("Built-in icons OK (%d entries, %s up to date)" % (len(ICONS), OUTPUT.relative_to(ROOT)))
        return 0

    # --preview / --png-out are additive; anything but --check (re)writes the .cs.
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(text, encoding="utf-8")
    print("wrote %s (%d bytes, %d icons)" % (OUTPUT.relative_to(ROOT), len(text.encode()), len(ICONS)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
