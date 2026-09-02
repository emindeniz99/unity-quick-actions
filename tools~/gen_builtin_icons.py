#!/usr/bin/env python3
"""Generate Editor/Android/QuickActionsBuiltInIcons.cs — the built-in Android
shortcut icons the build post-processor writes into the generated Gradle project.

Why this exists. On Android an IconType is resolved BY NAME at runtime
(QuickActionsBridge.java: getIdentifier("ic_quickaction_" + name), then
getIdentifier("ic_quickaction_builtin_" + name)), so a drawable has to exist in
the app or the launcher shows a blank square. The package ships four, under the
second prefix — its own, which no project writes — so a project's
ic_quickaction_<name> from any delivery channel (.androidlib, .aar, Maven) is
never overwritten or shadowed; precedence is the lookup order, not AGP's module
ranking.

What it emits. Four FILES per icon, all VectorDrawable/adaptive-icon XML: every
one of them density-independent, so it renders crisp at every launcher icon
density (a raster bucket would be upsampled on xxhdpi/xxxhdpi phones), and plain
text, so the generated C# carries readable XML rather than base64.

  res/drawable/ic_quickaction_builtin_<name>.xml             (API 25)
  res/drawable-anydpi-v26/ic_quickaction_builtin_<name>.xml   (API 26+)
  res/drawable/ic_quickaction_builtin_<name>_background.xml   \\ the -v26 file's
  res/drawable/ic_quickaction_builtin_<name>_foreground.xml   / two layers

The API 25 art is a white glyph on a full-bleed indigo disc: a legacy
(non-adaptive) shortcut drawable is drawn raw on API 25 and wrapped onto a WHITE
plate by API 26+ launchers (Launcher3's BaseIconFactory hardcodes Color.WHITE),
so the icon must carry its own contrast — white-on-transparent is invisible
there. That wrap is also why the -v26 variant exists: AOSP scales a legacy
drawable to 0.70 of the plate, so the disc lands inside a white ring, visibly
smaller than the adaptive icons next to it. An <adaptive-icon> under the SAME
resource name escapes that (the launcher takes an AdaptiveIconDrawable as-is,
masks it and moves on) — indigo edge to edge, and the same glyph scaled to sit
inside the 66-of-108 safe zone any launcher mask is guaranteed to show. The
qualifier picks the file; nothing at build time chooses between them.

Why embedded in a .cs. A Git/OpenUPM install and a .unitypackage one deliver the
same bytes with no package-path resolution, and tools~/verify.sh check 7 holds
the file to this generator byte for byte.

    python3 tools~/gen_builtin_icons.py                 # (re)write the .cs
    python3 tools~/gen_builtin_icons.py --check         # verify.sh: stale => exit 1
    python3 tools~/gen_builtin_icons.py --preview       # ASCII proof of each glyph
    python3 tools~/gen_builtin_icons.py --png-out DIR   # the same art as 96x96 PNGs
                                                        # (store collateral, and what a
                                                        # user's own .androidlib could
                                                        # carry under the user prefix)
"""
from __future__ import annotations

import argparse
import math
import pathlib
import struct
import sys
import zlib

ROOT = pathlib.Path(__file__).resolve().parent.parent
OUTPUT = ROOT / "Editor" / "Android" / "QuickActionsBuiltInIcons.cs"
# The NAMES-ONLY companion for the always-present Editor assembly: the Android
# post-processor assembly (where OUTPUT lives) is UNITY_ANDROID-only, so the
# settings page cannot reference it on another active target. Same ICONS list,
# same --check, so the two can never disagree about which members ship art.
OUTPUT_SET = ROOT / "Editor" / "QuickActionsBuiltInIconSet.cs"

# The vector's viewport (and the PNG preview's pixel size). 48dp is Google's
# app-shortcut icon size; the launcher scales the drawable to whatever it needs.
SIZE = 96
DP = 48
SUPERSAMPLE = 4  # PNG preview only: per axis → 16 coverage samples per pixel

# ---- ART STYLE --------------------------------------------------------------
# Change these and re-run; the harness compares text, not looks.
BACKGROUND = (0x3F, 0x51, 0xB5)   # indigo disc; None = transparent (glyph only)
BACKGROUND_RADIUS = 46            # of 48 — a 2 px transparent margin
GLYPH = (0xFF, 0xFF, 0xFF)

# ---- ADAPTIVE (-v26) --------------------------------------------------------
# Android's adaptive-icon canvas is 108 units square, of which the launcher's
# mask shows the central 72 and only the central 66 — the SAFE ZONE — is
# guaranteed visible on every mask (circle, squircle, teardrop) and through the
# parallax/scale animations launchers play. Anything outside it may be clipped.
ADAPTIVE_SIZE = 108
ADAPTIVE_SAFE = 66
# The -v26 variant is a config-qualified file under the SAME resource name, so
# aapt2 picks it by API level and nothing that references the name has to know.
ADAPTIVE_DIRECTORY = "drawable-anydpi-v26"
# The two layers are resources in their own right, so they need names of their
# own: the built-in prefix + the catalog name + these, which keeps them inside
# the keep glob (@drawable/ic_quickaction_*), inside the define-off stripper's
# prefix sweep, and out of any name a project could be using. They go in the
# unqualified res/drawable/ (where Android Studio's own launcher layers live);
# an API 25 device carries ~1 KB of XML it never draws, which is cheaper than a
# second -v26 config that resolves to nothing below 26.
BACKGROUND_SUFFIX = "_background"
FOREGROUND_SUFFIX = "_foreground"
# 96-viewport glyph units -> 108-viewport ones. The legacy glyph fills ~74 % of
# its 92-unit disc; keeping that proportion against the adaptive icon's 72-unit
# VISIBLE circle puts the widest glyph (the star, 68 of 96) at ~53 of 108 — 49 %
# of the canvas, comfortably inside the 61 % safe zone. Reusing the geometry
# unscaled would put it at 71 % (76.5 of 108 once the canvas grows), i.e.
# clipped by a squircle mask.
GLYPH_SCALE = 0.78

# The package's OWN prefix (no project writes it), and the res/ subdirectory the
# API 25 art and both adaptive layers go in. The prefix appears here only because
# the <adaptive-icon> has to reference its layers BY NAME; the copies that matter
# at build and run time live in the C# post-processor, its stripper and the Java
# lookup, and tools~/check_frozen_strings.py pins those three against each other.
PREFIX = "ic_quickaction_builtin_"
RESOURCE_DIRECTORY = "drawable"

# (IconType member, drawable-name suffix, glyph)
# The suffix must equal the Java ICON_NAMES entry for that IconType value —
# the harness test pins it against the .java file. Sorted by suffix.
ICONS = [
    ("Add", "add", "add"),
    ("Compose", "compose", "pencil"),
    ("Favorite", "favorite", "star"),
    ("Play", "play", "play"),
]

# Same glyph GEOMETRY as tools~/gen_store_images.py once drew (that script now
# delegates here, so the store's example PNGs are this art too), on a 96 canvas.


def star_points():
    pts = []
    for k in range(10):
        ang = math.pi / 2 + k * math.pi / 5
        r = 34 if k % 2 == 0 else 15
        pts.append((48 + r * math.cos(ang), 48 - r * math.sin(ang)))
    return pts


def rect(x0, y0, x1, y1):
    # PIL's rectangle() was inclusive of x1/y1, so the drawn box was one pixel
    # wider than the coordinates; kept so the art does not shift by a pixel.
    return [(x0, y0), (x1 + 1, y0), (x1 + 1, y1 + 1), (x0, y1 + 1)]


GLYPHS = {
    "play": [[(34, 26), (34, 70), (72, 48)]],
    "add": [rect(44, 24, 52, 72), rect(24, 44, 72, 52)],
    "star": [star_points()],
    "pencil": [[(28, 70), (32, 56), (60, 28), (72, 40), (44, 68)]],
}


# ---- VectorDrawable ---------------------------------------------------------

def num(v):
    """Shortest exact-enough decimal: 48 → '48', 33.5 → '33.5', 12.345 → '12.35'."""
    s = "%.2f" % v
    s = s.rstrip("0").rstrip(".")
    return "0" if s in ("", "-0") else s


def polygon_path(polys):
    """Every polygon as a closed subpath; nonzero fill unions overlapping ones."""
    parts = []
    for poly in polys:
        parts.append("M" + "L".join("%s,%s" % (num(x), num(y)) for x, y in poly) + "Z")
    return "".join(parts)


def disc_path():
    c = SIZE / 2
    r = BACKGROUND_RADIUS
    return "M%s,%sA%s,%s 0 1,1 %s,%sA%s,%s 0 1,1 %s,%sZ" % (
        num(c - r), num(c), num(r), num(r), num(c + r), num(c), num(r), num(r), num(c - r), num(c))


def hex_color(rgb):
    return "#%02X%02X%02X" % rgb


PROVENANCE = [
    "<!-- Written by com.emindeniz99.quick-actions (tools~/gen_builtin_icons.py). A",
    "     project's own ic_quickaction_<name> takes precedence over this file. -->",
]


def vector_xml(kind):
    lines = [
        '<?xml version="1.0" encoding="utf-8"?>',
    ] + PROVENANCE + [
        '<vector xmlns:android="http://schemas.android.com/apk/res/android"',
        '    android:width="%ddp"' % DP,
        '    android:height="%ddp"' % DP,
        '    android:viewportWidth="%d"' % SIZE,
        '    android:viewportHeight="%d">' % SIZE,
    ]
    if BACKGROUND is not None:
        lines += [
            "  <path",
            '      android:fillColor="%s"' % hex_color(BACKGROUND),
            '      android:pathData="%s" />' % disc_path(),
        ]
    lines += [
        "  <path",
        '      android:fillColor="%s"' % hex_color(GLYPH),
        '      android:pathData="%s" />' % polygon_path(GLYPHS[kind]),
        "</vector>",
    ]
    return "\n".join(lines) + "\n"


# ---- the -v26 adaptive icon and its two layers -------------------------------

def adaptive_layer_xml(color, path_data):
    """One layer of the adaptive icon: a full 108-viewport vector holding one
    filled path. Both layers are drawn on the whole canvas — the launcher's mask
    decides what shows — so the geometry, not the viewport, is what keeps the
    glyph inside the safe zone."""
    return "\n".join([
        '<?xml version="1.0" encoding="utf-8"?>',
    ] + PROVENANCE + [
        '<vector xmlns:android="http://schemas.android.com/apk/res/android"',
        '    android:width="%ddp"' % ADAPTIVE_SIZE,
        '    android:height="%ddp"' % ADAPTIVE_SIZE,
        '    android:viewportWidth="%d"' % ADAPTIVE_SIZE,
        '    android:viewportHeight="%d">' % ADAPTIVE_SIZE,
        "  <path",
        '      android:fillColor="%s"' % color,
        '      android:pathData="%s" />' % path_data,
        "</vector>",
    ]) + "\n"


def background_xml():
    """Edge to edge, no disc: the mask cuts the shape, and any inset here would
    come back as the white ring this variant exists to remove."""
    # BACKGROUND = None is the legacy style's "glyph only, transparent" option,
    # which an adaptive icon has no equivalent of — a transparent background layer
    # leaves the launcher masking nothing. Fail loudly rather than invent a colour;
    # that art direction would mean dropping the -v26 variant, not recolouring it.
    if BACKGROUND is None:
        raise SystemExit("BACKGROUND is None: the -v26 adaptive variant needs a background "
                         "colour. Drop the adaptive rows from entries() instead.")
    s = ADAPTIVE_SIZE
    return adaptive_layer_xml(hex_color(BACKGROUND),
                              polygon_path([[(0, 0), (s, 0), (s, s), (0, s)]]))


def safe_zone_glyph(kind):
    """GLYPHS[kind] recentred on the 108 canvas and scaled by GLYPH_SCALE, which
    lands every point inside the safe circle (asserted by the harness)."""
    c, k = SIZE / 2.0, ADAPTIVE_SIZE / 2.0
    return [[(k + (x - c) * GLYPH_SCALE, k + (y - c) * GLYPH_SCALE) for x, y in poly]
            for poly in GLYPHS[kind]]


def foreground_xml(kind):
    return adaptive_layer_xml(hex_color(GLYPH), polygon_path(safe_zone_glyph(kind)))


def adaptive_icon_xml(suffix):
    """The -v26 file itself: no art, just the two layers by name. Names, not
    inline drawables, because <adaptive-icon> takes drawable references."""
    return "\n".join([
        '<?xml version="1.0" encoding="utf-8"?>',
    ] + PROVENANCE + [
        '<adaptive-icon xmlns:android="http://schemas.android.com/apk/res/android">',
        '  <background android:drawable="@drawable/%s%s%s" />' % (PREFIX, suffix, BACKGROUND_SUFFIX),
        '  <foreground android:drawable="@drawable/%s%s%s" />' % (PREFIX, suffix, FOREGROUND_SUFFIX),
        "</adaptive-icon>",
    ]) + "\n"


# The C# Layer member for each row of entries() — the post-processor keys the
# static bake off Legacy (the entry whose Name is the catalog name and which
# exists on every API level), and the harness keys its geometry checks off the
# rest.
LEGACY, ADAPTIVE, BACKGROUND_LAYER, FOREGROUND_LAYER = (
    "Legacy", "Adaptive", "Background", "Foreground")


def entries():
    """→ [(member, name, directory, layer, xml_text)]: EVERY file the build
    post-processor writes, declared up front and in ICONS order. Nothing is
    decided from the exported tree, which is what keeps the port to Unity 6's
    AndroidProjectFilesModifier (every output declared before it exists)
    mechanical."""
    out = []
    for member, suffix, kind in ICONS:
        out.append((member, suffix, RESOURCE_DIRECTORY, LEGACY, vector_xml(kind)))
        out.append((member, suffix, ADAPTIVE_DIRECTORY, ADAPTIVE, adaptive_icon_xml(suffix)))
        out.append((member, suffix + BACKGROUND_SUFFIX, RESOURCE_DIRECTORY, BACKGROUND_LAYER,
                    background_xml()))
        out.append((member, suffix + FOREGROUND_SUFFIX, RESOURCE_DIRECTORY, FOREGROUND_LAYER,
                    foreground_xml(kind)))
    return out


# ---- C# ---------------------------------------------------------------------

def cs_literal(text, indent):
    """The XML as one C# string: a concatenation of escaped "…\\n" line literals."""
    out = []
    for line in text.split("\n")[:-1]:  # the text ends in "\n", so the last split is ""
        out.append('"%s\\n"' % line.replace("\\", "\\\\").replace('"', '\\"'))
    return (" +\n" + indent).join(out)


def generate():
    out = []
    w = out.append
    w("// <auto-generated>")
    w("//   Written by tools~/gen_builtin_icons.py. Do not edit by hand: change the")
    w("//   generator and re-run it. tools~/verify.sh fails when this file is stale.")
    w("// </auto-generated>")
    w("//")
    w("// The built-in Android shortcut icons as VectorDrawable / adaptive-icon XML. On")
    w("// Android an IconType is resolved BY NAME at runtime — QuickActionsBridge.java")
    w("// tries the project's ic_quickaction_<name> first, then")
    w("// ic_quickaction_builtin_<name> — so the drawable has to exist in the app, and")
    w("// the build post-processor writes these into the generated Gradle project under")
    w("// the second prefix. XML, so one density-independent file per layer; embedded")
    w("// rather than shipped as assets so a Git/OpenUPM install and a .unitypackage one")
    w("// deliver the same bytes with no package-path resolution, and so the headless")
    w("// harness can hold the post-processor to this source of truth byte for byte.")
    w("//")
    w("// Two variants under ONE resource name, chosen by the res/ qualifier and never")
    w("// by anything at build time. The API 25 file: %ddp on a %d viewport," % (DP, SIZE))
    w("// %s glyph%s. The -v26 one: an <adaptive-icon> over two layers of" % (
        "white" if GLYPH == (255, 255, 255) else hex_color(GLYPH),
        "" if BACKGROUND is None else " on a %s disc" % hex_color(BACKGROUND)))
    w("// its own — a full-bleed %s background and the same glyph scaled into the"
      % (hex_color(BACKGROUND) if BACKGROUND is not None else "transparent"))
    w("// %d-of-%d safe zone — because API 26+ launchers otherwise wrap the legacy file"
      % (ADAPTIVE_SAFE, ADAPTIVE_SIZE))
    w("// onto a white plate at 0.70 of the viewport: a disc inside a white ring,")
    w("// smaller than the adaptive icons beside it.")
    w("namespace EminDeniz99.QuickActions.Editor")
    w("{")
    w("    internal static class QuickActionsBuiltInIcons")
    w("    {")
    w("        /// <summary>What a given <see cref=\"Entry\"/> is. The two variants of one icon")
    w("        /// share a resource NAME (the qualifier picks between them); the two adaptive")
    w("        /// layers are resources in their own right, referenced only by that")
    w("        /// icon's <c>&lt;adaptive-icon&gt;</c>.</summary>")
    w("        internal enum IconLayer")
    w("        {")
    w("            /// <summary>res/drawable — the plain vector, what API 25 draws.</summary>")
    w("            Legacy,")
    w("            /// <summary>res/%s — the &lt;adaptive-icon&gt;, same name.</summary>" % ADAPTIVE_DIRECTORY)
    w("            Adaptive,")
    w("            /// <summary>That icon's full-bleed background layer.</summary>")
    w("            Background,")
    w("            /// <summary>That icon's glyph layer, inside the safe zone.</summary>")
    w("            Foreground,")
    w("        }")
    w("")
    w("        internal sealed class Entry")
    w("        {")
    w("            /// <summary>The catalog value this file belongs to (the adaptive layers")
    w("            /// belong to the same one their icon does).</summary>")
    w("            public readonly IconType Icon;")
    w("            /// <summary>The &lt;name&gt; in ic_quickaction_builtin_&lt;name&gt;.xml. For")
    w("            /// <see cref=\"IconLayer.Legacy\"/> and <see cref=\"IconLayer.Adaptive\"/> it must")
    w("            /// equal the Java ICON_NAMES entry for <see cref=\"Icon\"/>'s value; the layers")
    w("            /// carry that name plus a suffix, which keeps them inside the keep glob and")
    w("            /// the define-off sweep.</summary>")
    w("            public readonly string Name;")
    w("            /// <summary>The res/ subdirectory this file goes in — the qualifier that")
    w("            /// makes one resource name resolve to two different files by API level.</summary>")
    w("            public readonly string Directory;")
    w("            /// <summary>Which of the four files of an icon this is.</summary>")
    w("            public readonly IconLayer Layer;")
    w("            /// <summary>The drawable XML, verbatim.</summary>")
    w("            public readonly string Xml;")
    w("")
    w("            public Entry(IconType icon, string name, string directory, IconLayer layer, string xml)")
    w("            {")
    w("                Icon = icon;")
    w("                Name = name;")
    w("                Directory = directory;")
    w("                Layer = layer;")
    w("                Xml = xml;")
    w("            }")
    w("        }")
    w("")
    w("        /// <summary>The viewport the API 25 vector draws on (width and height).</summary>")
    w("        internal const int Viewport = %d;" % SIZE)
    w("        /// <summary>The viewport the -v26 layers draw on: Android's adaptive-icon")
    w("        /// canvas.</summary>")
    w("        internal const int AdaptiveViewport = %d;" % ADAPTIVE_SIZE)
    w("        /// <summary>The diameter, on that canvas, of the centred circle every launcher")
    w("        /// mask is guaranteed to show. The foreground glyph stays inside it.</summary>")
    w("        internal const int AdaptiveSafeZone = %d;" % ADAPTIVE_SAFE)
    w("")
    w("        /// <summary>Every file the build post-processor writes, declared up front:")
    w("        /// four per icon, each with the subdirectory it belongs in. Nothing is decided")
    w("        /// from the exported tree.</summary>")
    w("        internal static readonly Entry[] Entries =")
    w("        {")
    for member, name, directory, layer, xml in entries():
        w("            new Entry(IconType.%s, \"%s\", \"%s\", IconLayer.%s," % (member, name, directory, layer))
        w("                %s)," % cs_literal(xml, "                "))
    w("        };")
    w("    }")
    w("}")
    return "\n".join(out) + "\n"


def generate_set():
    out = []
    w = out.append
    w("// <auto-generated>")
    w("//   Written by tools~/gen_builtin_icons.py. Do not edit by hand: change the")
    w("//   generator and re-run it. tools~/verify.sh fails when this file is stale.")
    w("// </auto-generated>")
    w("//")
    w("// Which IconType members ship a built-in Android drawable — names only, no art.")
    w("// The drawables themselves are in Editor/Android/QuickActionsBuiltInIcons.cs,")
    w("// whose assembly is UNITY_ANDROID-only; this copy lives in the always-present")
    w("// Editor assembly so the settings page can flag, on any active target, which")
    w("// catalog entries render blank on Android without a drawable of the project's.")
    w("namespace EminDeniz99.QuickActions.Editor")
    w("{")
    w("    internal static class QuickActionsBuiltInIconSet")
    w("    {")
    w("        /// <summary>Every member with a built-in Android drawable, in catalog order.</summary>")
    w("        internal static readonly IconType[] Icons =")
    w("        {")
    for member, _suffix, _kind in ICONS:
        w("            IconType.%s," % member)
    w("        };")
    w("")
    w("        /// <summary>True when the package writes a drawable for <paramref name=\"icon\"/>")
    w("        /// into every Android build; false for <see cref=\"IconType.None\"/> and for the")
    w("        /// members that need an ic_quickaction_&lt;name&gt; drawable from the project.</summary>")
    w("        internal static bool HasAndroidArt(IconType icon)")
    w("        {")
    w("            foreach (var builtIn in Icons)")
    w("                if (builtIn == icon)")
    w("                    return true;")
    w("            return false;")
    w("        }")
    w("    }")
    w("}")
    return "\n".join(out) + "\n"


# ---- PNG preview (same art, rasterised) --------------------------------------

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


def write_pngs(directory):
    """ic_quickaction_<name>.png — the USER prefix: these are examples of what a
    project's own drawable could be, not the built-ins (those are vectors)."""
    d = pathlib.Path(directory)
    d.mkdir(parents=True, exist_ok=True)
    for member, suffix, kind in ICONS:
        target = d / ("ic_quickaction_%s.png" % suffix)
        target.write_bytes(encode_png(render(kind)))
        print("wrote %s (%d bytes)" % (target, target.stat().st_size))


def preview(rows):
    """ASCII: '#' glyph, 'o' disc, '.' transparent (one row per two pixel rows)."""
    lines = []
    for y in range(0, SIZE, 2):
        line = []
        for x in range(SIZE):
            r, g, b, a = rows[y][x]
            if a < 64:
                line.append(".")
            elif (r, g, b) == GLYPH:
                line.append("#")
            else:
                line.append("o")
        lines.append("".join(line))
    return "\n".join(lines)


# ---- CLI --------------------------------------------------------------------

def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n", 1)[0])
    ap.add_argument("--check", action="store_true", help="fail if the generated file is stale")
    ap.add_argument("--preview", action="store_true", help="print an ASCII proof of each glyph")
    ap.add_argument("--png-out", metavar="DIR", help="also write ic_quickaction_<name>.png files there")
    args = ap.parse_args()

    if args.preview:
        # The API 25 art only: the rasteriser draws the 96 canvas, and the -v26
        # foreground is the same geometry scaled onto a 108 one.
        for member, suffix, kind in ICONS:
            print("%s%s (IconType.%s):" % (PREFIX, suffix, member))
            print(preview(render(kind)))
            print()

    if args.png_out:
        write_pngs(args.png_out)

    outputs = [(OUTPUT, generate()), (OUTPUT_SET, generate_set())]
    if args.check:
        for path, text in outputs:
            current = path.read_text(encoding="utf-8") if path.exists() else None
            if current != text:
                print("BUILT-IN ICONS: FAIL — %s is stale (or missing)." % path.relative_to(ROOT), file=sys.stderr)
                print("  Re-run: python3 tools~/gen_builtin_icons.py", file=sys.stderr)
                return 1
        print("Built-in icons OK (%d icons, %d files, %s and %s up to date)" % (
            len(ICONS), len(entries()), OUTPUT.relative_to(ROOT), OUTPUT_SET.relative_to(ROOT)))
        return 0

    # --preview / --png-out are additive; anything but --check (re)writes the .cs files.
    for path, text in outputs:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")
        print("wrote %s (%d bytes, %d icons, %d files)" % (
            path.relative_to(ROOT), len(text.encode()), len(ICONS), len(entries())))
    return 0


if __name__ == "__main__":
    sys.exit(main())
