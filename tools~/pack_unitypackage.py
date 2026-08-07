#!/usr/bin/env python3
"""Build a classic, drag-and-drop `.unitypackage` WITHOUT running Unity.

A .unitypackage is just a gzip-compressed tar where each asset lives in a
directory named by its GUID and contains:
  <guid>/asset       the file bytes (omitted for folders)
  <guid>/asset.meta  the .meta content (carries the same GUID)
  <guid>/pathname    the project-relative path, e.g. Assets/QuickActions/Runtime/QuickActions.cs

On import Unity recreates each file at `pathname` using the GUID from the meta,
so scene/script references stay intact. This lets us ship the legacy
drag-into-the-Editor format alongside the modern UPM package.

Layout: package content is remapped under Assets/QuickActions/. Dev folders
(.verify, tools, docs, store~, Tests) and package.json are excluded.

Run: python3 tools~/pack_unitypackage.py  ->  dist~/QuickActions.unitypackage

`dist~/` is gitignored: the .unitypackage is a build output, never a committed
source file. CI runs this same script and attaches the result to the GitHub
Release, so users download it from the Releases page rather than from the tree.
"""
import gzip
import hashlib
import io
import os
import re
import tarfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "dist~")
OUT = os.path.join(OUT_DIR, "QuickActions.unitypackage")
ASSET_ROOT = "Assets/QuickActions"

# (source dir relative to package, target subpath under Assets/QuickActions)
INCLUDE_DIRS = [
    ("Runtime", "Runtime"),
    ("Editor", "Editor"),
    ("Plugins", "Plugins"),
    # Samples become a plain Example folder. NOTE: the remapped files keep their
    # original Samples~/Demo .meta GUIDs (so the demo scene's script reference still
    # resolves). That means this .unitypackage's Example/ assets share GUIDs with the
    # UPM package's Samples~/Demo — harmless because you install via ONE delivery
    # method; don't import both the UPM package and this .unitypackage into the same
    # project, or those few sample GUIDs would collide.
    ("Samples~/Demo", "Example"),
]
# LICENSE.md stays in. Dropping it (0.4.2) did not remove the MIT grant from
# the artifact — the shipped README still carries an MIT badge and says
# "MIT-licensed" in its first paragraph — it only broke that badge's
# ./LICENSE.md link for every customer. Dual distribution is the copyright
# holder's right; the honest fix is to DISCLOSE it in the store listing
# rather than hide the file and leave the claim dangling.
INCLUDE_FILES = ["README.md", "CHANGELOG.md", "LICENSE.md", "ROADMAP.md"]


def guid_of_meta(meta_path):
    with open(meta_path) as f:
        m = re.search(r"guid:\s*([0-9a-f]{32})", f.read())
    return m.group(1) if m else None


def folder_meta(guid):
    return (
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "folderAsset: yes\n"
        "DefaultImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n"
    )


def folder_guid(path):
    return hashlib.md5(("folder:" + path).encode()).hexdigest()


def collect():
    """Return (files, folders): files = [(target, bytes, meta_text, guid)],
    folders = ordered list of target folder paths."""
    files = []
    folders = set()

    def add_file(src_abs, target):
        meta = src_abs + ".meta"
        if not os.path.exists(meta):
            return
        guid = guid_of_meta(meta)
        with open(src_abs, "rb") as f:
            data = f.read()
        with open(meta) as f:
            meta_text = f.read()
        files.append((target, data, meta_text, guid))
        # Register ancestor folders — but never the bare "Assets" root. A real
        # Unity project has no Assets.meta, and shipping a folder entry for it
        # is exactly the shape Asset Store rule 5.2.d calls out. Unity tolerates
        # it in practice; it costs one condition to be correct.
        d = os.path.dirname(target)
        while d and d != "." and d != "Assets":
            folders.add(d)
            d = os.path.dirname(d)

    for src_rel, dst_rel in INCLUDE_DIRS:
        base = os.path.join(ROOT, src_rel)
        for dirpath, dirnames, filenames in os.walk(base):
            for name in filenames:
                if name.endswith(".meta"):
                    continue
                src_abs = os.path.join(dirpath, name)
                rel = os.path.relpath(src_abs, base)
                target = f"{ASSET_ROOT}/{dst_rel}/{rel}".replace(os.sep, "/")
                add_file(src_abs, target)

    for name in INCLUDE_FILES:
        add_file(os.path.join(ROOT, name), f"{ASSET_ROOT}/{name}")

    # Sort by target path so the tar member order (and thus the gzip bytes) is
    # deterministic regardless of os.walk's filesystem/inode ordering — os.walk
    # does NOT sort filenames, so without this the artifact would differ from
    # machine to machine despite identical source (the mtime=0 reproducibility
    # intent). Reproducibility is what lets anyone rebuild the released
    # .unitypackage from its tag and get the same bytes CI attached.
    return sorted(files, key=lambda t: t[0]), sorted(folders)


def add_entry(tar, arcname, data: bytes):
    info = tarfile.TarInfo(arcname)
    info.size = len(data)
    info.mode = 0o644
    tar.addfile(info, io.BytesIO(data))


def main():
    files, folders = collect()
    os.makedirs(OUT_DIR, exist_ok=True)

    # Deterministic output: gzip normally stamps the compression TIME into its
    # header, so two builds of identical content still differed byte-for-byte.
    # mtime=0 makes the artifact reproducible — same input, same bytes.
    with gzip.GzipFile(OUT, "wb", mtime=0) as gz, tarfile.open(fileobj=gz, mode="w") as tar:
        # folder entries (so Unity makes stable-GUID folders)
        for folder in folders:
            guid = folder_guid(folder)
            add_entry(tar, f"{guid}/asset.meta", folder_meta(guid).encode())
            add_entry(tar, f"{guid}/pathname", folder.encode())
        # file entries
        for target, data, meta_text, guid in files:
            add_entry(tar, f"{guid}/asset", data)
            add_entry(tar, f"{guid}/asset.meta", meta_text.encode())
            add_entry(tar, f"{guid}/pathname", target.encode())

    size = os.path.getsize(OUT)
    print(f"wrote {OUT} ({size} bytes): {len(files)} assets, {len(folders)} folders")


if __name__ == "__main__":
    main()
