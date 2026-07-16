#!/usr/bin/env python3
"""Generate Unity .meta files for the package with deterministic GUIDs.

Unity creates these on import, but a distributed package must ship them so that
asset GUIDs (and therefore scene/script references and plugin platform settings)
are stable across machines. GUIDs are derived from the asset's package-relative
path via MD5, so re-running this is idempotent and never churns existing files.

Routing:
  * directories                       -> DefaultImporter (folderAsset)
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


IOS_EXT = {".mm", ".m", ".h", ".a"}
ANDROID_EXT = {".java", ".xml", ".aar"}


def meta_for(rel_path: str, is_dir: bool) -> str:
    guid = guid_for(rel_path)
    if is_dir:
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


def main() -> int:
    created = 0
    for root, dirs, files in os.walk(PKG):
        # Skip hidden/ignored dirs (Unity ignores names ending with ~ or starting .)
        # (the store~/dist~ collateral dirs are covered by the ~ rule).
        dirs[:] = [d for d in dirs
                   if not d.endswith("~") and not d.startswith(".")
                   and d != "__pycache__"  # python bytecode cache; gitignored, never an asset
                   ]

        for d in dirs:
            abs_dir = os.path.join(root, d)
            rel = os.path.relpath(abs_dir, PKG).replace(os.sep, "/")
            meta = abs_dir + ".meta"
            if not os.path.exists(meta):
                with open(meta, "w") as f:
                    f.write(meta_for(rel, True))
                created += 1

        for name in files:
            if name.endswith(".meta"):
                continue
            abs_file = os.path.join(root, name)
            rel = os.path.relpath(abs_file, PKG).replace(os.sep, "/")
            meta = abs_file + ".meta"
            if not os.path.exists(meta):
                with open(meta, "w") as f:
                    f.write(meta_for(rel, False))
                created += 1

    # Samples~ contents need .meta too (they are copied into Assets on import),
    # but os.walk skipped the ~ dir above. Handle it explicitly.
    samples = os.path.join(PKG, "Samples~")
    if os.path.isdir(samples):
        for root, dirs, files in os.walk(samples):
            for d in dirs:
                abs_dir = os.path.join(root, d)
                rel = os.path.relpath(abs_dir, PKG).replace(os.sep, "/")
                meta = abs_dir + ".meta"
                if not os.path.exists(meta):
                    with open(meta, "w") as f:
                        f.write(meta_for(rel, True))
                    created += 1
            for name in files:
                if name.endswith(".meta"):
                    continue
                abs_file = os.path.join(root, name)
                rel = os.path.relpath(abs_file, PKG).replace(os.sep, "/")
                meta = abs_file + ".meta"
                if not os.path.exists(meta):
                    with open(meta, "w") as f:
                        f.write(meta_for(rel, False))
                    created += 1

    print(f"gen_meta: created {created} .meta file(s)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
