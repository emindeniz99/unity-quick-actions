#!/usr/bin/env python3
"""Generate Unity .meta files for the package with deterministic GUIDs.

Unity creates these on import, but a distributed package must ship them so that
asset GUIDs (and therefore scene/script references and plugin platform settings)
are stable across machines. GUIDs are derived from the asset's package-relative
path via MD5, so re-running this is idempotent and never churns existing files.

Routing:
  * directories                       -> DefaultImporter (folderAsset)
  * *.androidlib directories          -> PluginImporter (Android only), and
                                         their CONTENTS get no .meta at all
  * .cs                               -> MonoImporter
  * .asmdef                           -> AssemblyDefinitionImporter
  * Plugins/iOS/*.{mm,m,h,a}          -> PluginImporter (iOS only)
  * Plugins/Android/*.{java,xml,aar}  -> PluginImporter (Android only)
  * everything else (.md,.json,.unity,.txt,...) -> DefaultImporter

Skips: the package root, any `*~` directory (e.g. Samples~) — Unity ignores
those — hidden dirs (incl. `.verify/`), the `store~/` and `dist~/` collateral
dirs, and existing `.meta` files.
"""
import hashlib
import difflib
import os
import sys

PKG = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def guid_for(rel_path: str) -> str:
    return hashlib.md5(rel_path.encode("utf-8")).hexdigest()


def folder_meta(guid: str) -> str:
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


def default_meta(guid: str) -> str:
    return (
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "DefaultImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n"
    )


def mono_meta(guid: str) -> str:
    return (
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "MonoImporter:\n"
        "  externalObjects: {}\n"
        "  serializedVersion: 2\n"
        "  defaultReferences: []\n"
        "  executionOrder: 0\n"
        "  icon: {instanceID: 0}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n"
    )


def asmdef_meta(guid: str) -> str:
    return (
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "AssemblyDefinitionImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n"
    )


# PluginImporter that enables exactly one mobile platform and excludes the rest.
# The "Any/'' + Exclude *" block plus explicit per-platform entries is the
# long-standing format Unity itself writes and reads back cleanly on 2022+.
def plugin_meta(guid: str, platform: str) -> str:
    is_ios = platform == "iOS"
    exclude_android = 0 if platform == "Android" else 1
    exclude_ios = 0 if is_ios else 1
    return (
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "PluginImporter:\n"
        "  externalObjects: {}\n"
        "  serializedVersion: 2\n"
        "  iconMap: {}\n"
        "  executionOrder: {}\n"
        # NOTE: Unity ignores defineConstraints on NATIVE plugins (it only honors
        # them for managed/asmdef code). The QUICKACTIONS_ENABLED gate for native
        # plugins is instead enforced at build-output level: the iOS .mm is wrapped
        # in #if QUICKACTIONS_ENABLED and Editor/iOS/QuickActionsEnableMacroiOS adds
        # the macro to the Xcode project only when enabled; Editor/NativeGate/
        # QuickActionsTrampolineStripperAndroid strips the trampoline <activity>
        # from the manifest when disabled. Keep this empty.
        "  defineConstraints: []\n"
        "  isPreloaded: 0\n"
        "  isOverridable: 0\n"
        "  isExplicitlyReferenced: 0\n"
        "  validateReferences: 1\n"
        "  platformData:\n"
        "  - first:\n"
        "      '': Any\n"
        "    second:\n"
        "      enabled: 0\n"
        "      settings:\n"
        f"        Exclude Android: {exclude_android}\n"
        "        Exclude Editor: 1\n"
        f"        Exclude iPhone: {exclude_ios}\n"
        "        Exclude Linux64: 1\n"
        "        Exclude OSXUniversal: 1\n"
        "        Exclude Win: 1\n"
        "        Exclude Win64: 1\n"
        "        Exclude WindowsStoreApps: 1\n"
        "  - first:\n"
        "      Android: Android\n"
        "    second:\n"
        f"      enabled: {1 if platform == 'Android' else 0}\n"
        "      settings: {}\n"
        "  - first:\n"
        "      Any: \n"
        "    second:\n"
        "      enabled: 0\n"
        "      settings: {}\n"
        "  - first:\n"
        "      Editor: Editor\n"
        "    second:\n"
        "      enabled: 0\n"
        "      settings:\n"
        "        DefaultValueInitialized: true\n"
        "  - first:\n"
        "      iPhone: iOS\n"
        "    second:\n"
        f"      enabled: {1 if is_ios else 0}\n"
        "      settings: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n"
    )


# An .androidlib is a whole Gradle library MODULE that Unity imports as one
# plug-in: the folder carries the PluginImporter and the files inside it are
# never imported individually, so they get no .meta (Unity's own
# com.unity.mobile.notifications ships its androidlib the same way). This is the
# shape a real Unity writes for one and the shape the CI fixture at
# Examples~/Testbed2022/Assets/QuickActionIcons.androidlib.meta carries — the
# only .androidlib in this repo whose build has actually been measured, so it is
# copied rather than invented. It differs from plugin_meta() above (isOverridable,
# the Standalone rows, "Exclude iOS" where a file plug-in says "Exclude iPhone")
# because Unity itself writes a different row set for a folder plug-in.
def androidlib_meta(guid: str) -> str:
    return (
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "PluginImporter:\n"
        "  externalObjects: {}\n"
        "  serializedVersion: 2\n"
        "  iconMap: {}\n"
        "  executionOrder: {}\n"
        "  defineConstraints: []\n"
        "  isPreloaded: 0\n"
        "  isOverridable: 1\n"
        "  isExplicitlyReferenced: 0\n"
        "  validateReferences: 1\n"
        "  platformData:\n"
        "  - first:\n"
        "      '': Any\n"
        "    second:\n"
        "      enabled: 0\n"
        "      settings:\n"
        "        Exclude Android: 0\n"
        "        Exclude Editor: 1\n"
        "        Exclude Linux64: 1\n"
        "        Exclude OSXUniversal: 1\n"
        "        Exclude Win: 1\n"
        "        Exclude Win64: 1\n"
        "        Exclude iOS: 1\n"
        "  - first:\n"
        "      Android: Android\n"
        "    second:\n"
        "      enabled: 1\n"
        "      settings:\n"
        "        CPU: ARMv7\n"
        "  - first:\n"
        "      Any: \n"
        "    second:\n"
        "      enabled: 0\n"
        "      settings: {}\n"
        "  - first:\n"
        "      Editor: Editor\n"
        "    second:\n"
        "      enabled: 0\n"
        "      settings:\n"
        "        CPU: AnyCPU\n"
        "        DefaultValueInitialized: true\n"
        "        OS: AnyOS\n"
        "  - first:\n"
        "      Standalone: Linux64\n"
        "    second:\n"
        "      enabled: 0\n"
        "      settings:\n"
        "        CPU: None\n"
        "  - first:\n"
        "      Standalone: OSXUniversal\n"
        "    second:\n"
        "      enabled: 0\n"
        "      settings:\n"
        "        CPU: None\n"
        "  - first:\n"
        "      Standalone: Win\n"
        "    second:\n"
        "      enabled: 0\n"
        "      settings:\n"
        "        CPU: None\n"
        "  - first:\n"
        "      Standalone: Win64\n"
        "    second:\n"
        "      enabled: 0\n"
        "      settings:\n"
        "        CPU: None\n"
        "  - first:\n"
        "      iPhone: iOS\n"
        "    second:\n"
        "      enabled: 0\n"
        "      settings:\n"
        "        AddToEmbeddedBinaries: false\n"
        "        CPU: AnyCPU\n"
        "        CompileFlags: \n"
        "        FrameworkDependencies: \n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n"
    )


IOS_EXT = {".mm", ".m", ".h", ".a"}
ANDROID_EXT = {".java", ".xml", ".aar"}
ANDROIDLIB_SUFFIX = ".androidlib"


def meta_for(rel_path: str, is_dir: bool) -> str:
    guid = guid_for(rel_path)
    if is_dir:
        if rel_path.endswith(ANDROIDLIB_SUFFIX):
            return androidlib_meta(guid)
        return folder_meta(guid)
    ext = os.path.splitext(rel_path)[1].lower()
    if ext == ".cs":
        return mono_meta(guid)
    if ext == ".asmdef":
        return asmdef_meta(guid)
    if rel_path.startswith("Plugins/iOS/") and ext in IOS_EXT:
        return plugin_meta(guid, "iOS")
    if rel_path.startswith("Plugins/Android/") and ext in ANDROID_EXT:
        return plugin_meta(guid, "Android")
    return default_meta(guid)


def iter_assets():
    """Every asset that needs a .meta, as (absolute path, package-relative, is_dir).

    The single source of truth for what counts as an asset: both the generate
    pass and --check walk this, so a skip rule can never drift between them.
    """
    for root, dirs, files in os.walk(PKG):
        # Skip hidden/ignored dirs (Unity ignores names ending with ~ or starting .)
        # (the store~/dist~ collateral dirs are covered by the ~ rule).
        dirs[:] = [d for d in dirs
                   if not d.endswith("~") and not d.startswith(".")
                   and d != "__pycache__"  # python bytecode cache; gitignored, never an asset
                   ]
        for d in dirs:
            abs_dir = os.path.join(root, d)
            yield abs_dir, os.path.relpath(abs_dir, PKG).replace(os.sep, "/"), True
        # Yielded the .androidlib itself (it is the plug-in), now stop: nothing
        # inside a folder plug-in is imported as an asset in its own right.
        dirs[:] = [d for d in dirs if not d.endswith(ANDROIDLIB_SUFFIX)]
        for name in files:
            if name.endswith(".meta"):
                continue
            # Unity's importer ignores the same names it ignores for dirs, so a
            # .meta for one (e.g. .gitignore.meta) would be an orphan.
            if name.startswith(".") or name.endswith("~"):
                continue
            abs_file = os.path.join(root, name)
            yield abs_file, os.path.relpath(abs_file, PKG).replace(os.sep, "/"), False

    # Samples~ contents need .meta too (they are copied into Assets on import),
    # but os.walk skipped the ~ dir above. Handle it explicitly.
    samples = os.path.join(PKG, "Samples~")
    if os.path.isdir(samples):
        for root, dirs, files in os.walk(samples):
            for d in dirs:
                abs_dir = os.path.join(root, d)
                yield abs_dir, os.path.relpath(abs_dir, PKG).replace(os.sep, "/"), True
            dirs[:] = [d for d in dirs if not d.endswith(ANDROIDLIB_SUFFIX)]
            for name in files:
                if name.endswith(".meta"):
                    continue
                abs_file = os.path.join(root, name)
                yield abs_file, os.path.relpath(abs_file, PKG).replace(os.sep, "/"), False


def body(text):
    """A .meta minus its guid line.

    The guid is deliberately excluded: several committed .meta files carry a
    GUID a real Unity assigned before the asset was moved, and rewriting those
    would break every reference pointing at them. Everything else — the
    importer the file routes to, and a plugin's platform flags — is generated
    from the path and must match, which is what this check compares.
    """
    return [l for l in text.splitlines() if not l.startswith("guid:")]


def check() -> int:
    """Report metas that are missing, orphaned, or route to the wrong importer."""
    problems = []
    expected_metas = set()
    for path, rel, is_dir in iter_assets():
        meta = path + ".meta"
        expected_metas.add(os.path.abspath(meta))
        if not os.path.exists(meta):
            problems.append(f"{rel}: no .meta (run tools~/gen_meta.py and commit it)")
            continue
        want = body(meta_for(rel, is_dir))
        got = body(open(meta, encoding="utf-8").read())
        if want != got:
            # Show the differing lines, not the head of each file: the importer
            # block is long and identical for pages, so a head-based message
            # would print two blocks that look the same.
            delta = [l for l in difflib.unified_diff(
                got, want, fromfile="committed", tofile="expected", lineterm="", n=0)
                if l.startswith(("+", "-")) and not l.startswith(("+++", "---"))]
            problems.append(
                f"{rel}.meta: importer/settings do not match what this path routes to.\n"
                + "\n".join("      " + l for l in delta[:12]))

    # Orphans: a .meta whose asset is gone. Unity re-creates the asset's meta on
    # import, so a leftover is silent locally but ships in the package.
    for root, dirs, files in os.walk(PKG):
        dirs[:] = [d for d in dirs
                   if not (d.endswith("~") and d != "Samples~") and not d.startswith(".")
                   and d != "__pycache__"]
        for name in files:
            if not name.endswith(".meta"):
                continue
            meta = os.path.abspath(os.path.join(root, name))
            if meta in expected_metas:
                continue
            rel = os.path.relpath(meta, PKG).replace(os.sep, "/")
            # A .meta INSIDE a folder plug-in is never read by Unity but ships
            # with the package, so catch it here rather than let it look like an
            # asset that simply has no meta.
            if ANDROIDLIB_SUFFIX + "/" in rel:
                problems.append(
                    f"{rel}: inside an .androidlib — the folder is the plug-in, "
                    "its contents are not assets; delete this .meta")
                continue
            asset = meta[: -len(".meta")]
            if not os.path.exists(asset):
                problems.append(f"{rel}: orphan — no asset at {os.path.basename(asset)}")

    if problems:
        print("gen_meta --check found %d problem(s):" % len(problems), file=sys.stderr)
        for p in problems:
            print("  " + p, file=sys.stderr)
        return 1
    print("gen_meta: .meta set is complete, orphan-free and correctly routed")
    return 0


def main() -> int:
    if "--check" in sys.argv:
        return check()
    # iter_assets() is the single source of truth for what counts as an asset;
    # walking it here (rather than repeating its skip rules) is what keeps the
    # generate pass and --check from ever disagreeing about one.
    created = 0
    for path, rel, is_dir in iter_assets():
        meta = path + ".meta"
        if not os.path.exists(meta):
            with open(meta, "w") as f:
                f.write(meta_for(rel, is_dir))
            created += 1

    print(f"gen_meta: created {created} .meta file(s)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
