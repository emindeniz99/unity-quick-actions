// Gated by the Editor.iOS asmdef's defineConstraints (UNITY_IOS), so it only
// compiles when iOS is the active build target and UnityEditor.iOS.Xcode exists.
using System.Collections.Generic;
using System.IO;
using EminDeniz99.QuickActions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace EminDeniz99.QuickActions.Editor
{
    /// <summary>
    /// Writes the static shortcuts from <see cref="QuickActionsSettings"/> — as
    /// prepared by <see cref="QuickActionsStaticBuild"/> (Customize hook +
    /// <c>{placeholder}</c> interpolation) — into the generated Xcode project's
    /// <c>Info.plist</c> as <c>UIApplicationShortcutItems</c>.
    /// Dynamic shortcuts (set at runtime) need none of this. The native tap path is
    /// identical for static and dynamic items, so no native change is required here.
    ///
    /// Guarded by <c>UNITY_IOS</c>, so the UnityEditor.iOS.Xcode dependency is only
    /// referenced when iOS is the active build target (i.e. when it is installed).
    /// </summary>
    internal sealed class QuickActionsBuildPostProcessoriOS : IPostprocessBuildWithReport
    {
        public int callbackOrder => 100;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS)
                return;

            // Template-image sync runs even with no/empty settings so an Append
            // build drops icons a previous build copied (manifest-scoped cleanup,
            // mirroring the plist ClearOurEntries path below).
            SyncTemplateImages(report.summary.outputPath, QuickActionsSettings.GetOrNull());

            var plistPath = Path.Combine(report.summary.outputPath, "Info.plist");
            if (!File.Exists(plistPath))
            {
                Debug.LogWarning($"[QuickActions] Info.plist not found at {plistPath}; skipping static shortcuts.");
                return;
            }

            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

            // The baked set is the PREPARED one — settings copies run through the
            // Customize hook, then {placeholder} interpolation — not the raw asset:
            // a customizer may add items to an asset-less project or empty the list.
            var shortcuts = QuickActionsStaticBuild.Prepare(BuildTarget.iOS,
                (report.summary.options & BuildOptions.Development) != 0);
            if (shortcuts.Count == 0)
            {
                // No static shortcuts to bake. On an *Append* build the plist may
                // still hold entries a previous build wrote; remove ONLY ours (marked)
                // so stale package shortcuts don't ship while a host app's / another
                // plugin's own UIApplicationShortcutItems are preserved.
                if (QuickActionsPlistShortcuts.ClearOurEntries(plist))
                {
                    plist.WriteToFile(plistPath);
                    Debug.Log("[QuickActions] Cleared stale UIApplicationShortcutItems (no static shortcuts configured).");
                }
                return;
            }

            // Merge, don't clobber: reuse any existing array so a host app's / other
            // plugin's entries survive; drop our own stale entries (the marker) so an
            // Append rebuild refreshes them; then append the current set. Unmarked
            // entries are kept unconditionally, even on an id collision — the id then
            // renders twice, the honest result of two publishers claiming one id
            // (same rule as the dynamic merge in Plugins/iOS/QuickActions.mm; no
            // "adopt the unmarked twin" heuristic — any discriminator also matches
            // genuine host entries, and the marker predates every release, so there
            // is no pre-marker install to migrate).
            var items = QuickActionsPlistShortcuts.GetOrCreateArray(plist);
            items.values.RemoveAll(QuickActionsPlistShortcuts.IsOurs);

            var seen = new HashSet<string>();
            var count = 0;
            foreach (var item in shortcuts)
            {
                if (item == null || string.IsNullOrEmpty(item.Id) || string.IsNullOrEmpty(item.Title))
                    continue;
                if (!seen.Add(item.Id))
                    continue; // skip duplicate ids (parity with the Android post-processor)

                var dict = items.AddDict();
                dict.SetString("UIApplicationShortcutItemType", item.Id);
                dict.SetString("UIApplicationShortcutItemTitle", item.Title);
                if (!string.IsNullOrEmpty(item.Subtitle))
                    dict.SetString("UIApplicationShortcutItemSubtitle", item.Subtitle);
                // Icon priority mirrors the dynamic path (QuickActions.mm): SF Symbol
                // > bundle template image > IconType glyph. Exactly ONE icon key is
                // written; Apple doesn't document which key wins when several are
                // present, so multi-key output would be undefined behavior. This
                // means a STATIC item with IosSystemImage renders iconless on
                // iOS 12 (which doesn't know IconSymbolName) — unlike the dynamic
                // path, which falls through at runtime via @available. Documented
                // in the IosSystemImage xmldoc; target iOS 12 with Icon /
                // IosTemplateImage for static items instead.
                if (!string.IsNullOrEmpty(item.IosSystemImage))
                    dict.SetString("UIApplicationShortcutItemIconSymbolName", item.IosSystemImage);
                else if (!string.IsNullOrEmpty(item.IosTemplateImage))
                    dict.SetString("UIApplicationShortcutItemIconFile", item.IosTemplateImage);
                else if (item.Icon != IconType.None)
                    dict.SetString("UIApplicationShortcutItemIconType", "UIApplicationShortcutIconType" + item.Icon);
                // Tag our entries so a later cleanup/refresh can find exactly ours.
                dict.CreateDict("UIApplicationShortcutItemUserInfo")
                    .SetBoolean(QuickActionsPlistShortcuts.MarkerKey, true);
                count++;
            }

            plist.WriteToFile(plistPath);
            Debug.Log($"[QuickActions] Wrote {count} static shortcut(s) to Info.plist.");
        }

        // Copies the configured template-image textures into the generated Xcode
        // project and adds them to the MAIN app target's resources (shortcut icons
        // load from the app bundle, not UnityFramework). Ownership for Append-build
        // cleanup is a manifest file listing exactly the file names we copied —
        // never delete anything not listed there (same never-touch-host rule as
        // the plist marker). Group-style PBX adds flatten into the bundle root, so
        // a PNG resolves as IosTemplateImage = file name without extension (a JPEG
        // needs the extension included — bare-name bundle lookup is PNG-only).
        //
        // Crash-safety ordering: all disk DELETES happen only AFTER the edited
        // pbxproj is persisted, and every per-texture failure warns + skips
        // instead of throwing — a mid-sync failure must never strand the on-disk
        // Xcode project referencing files that no longer exist (unfixable on
        // later Append builds once the manifest is gone), nor abort the
        // Info.plist static-shortcut step that runs after this.
        //
        // NOTE: the .verify harness compile-checks this method only (its PBX
        // stubs are no-ops) — behavior needs a real Editor + Xcode build; see
        // ROADMAP "v0.3 feature validation".
        private const string IconsFolder = "QuickActionsIcons";

        private static void SyncTemplateImages(string buildPath, QuickActionsSettings settings)
        {
            try
            {
                SyncTemplateImagesCore(buildPath, settings);
            }
            catch (System.Exception e)
            {
                // Fail loud but contained: the plist static-shortcut step (and the
                // rest of the build) must survive an icon-sync failure.
                Debug.LogWarning($"[QuickActions] Template-image sync failed; shortcut icons may be stale: {e.Message}");
            }
        }

        private static void SyncTemplateImagesCore(string buildPath, QuickActionsSettings settings)
        {
            var projPath = PBXProject.GetPBXProjectPath(buildPath);
            if (string.IsNullOrEmpty(projPath) || !File.Exists(projPath))
            {
                if (settings != null && settings.IosTemplateImages.Count > 0)
                    Debug.LogWarning("[QuickActions] Xcode project not found; skipping template-image icons.");
                return;
            }

            var iconsDir = Path.Combine(buildPath, IconsFolder);
            var manifestPath = Path.Combine(iconsDir, "quickactions_manifest.txt");

            var proj = new PBXProject();
            proj.ReadFromFile(projPath);

            // Phase 1 — in-memory only: drop the PBX references of what WE copied
            // last build (manifest-scoped) so renamed/removed textures don't ship
            // stale on an Append build. No disk deletes yet.
            var stale = new List<string>();
            if (File.Exists(manifestPath))
            {
                foreach (var name in File.ReadAllLines(manifestPath))
                {
                    if (string.IsNullOrWhiteSpace(name))
                        continue;
                    stale.Add(name);
                    var guid = proj.FindFileGuidByProjectPath(IconsFolder + "/" + name);
                    if (!string.IsNullOrEmpty(guid))
                        proj.RemoveFile(guid);
                }
            }

            // Phase 2 — copy the current set and register it. Every failure skips
            // the one texture (warn), never the whole sync.
            var copied = new List<string>();
            if (settings != null && settings.IosTemplateImages.Count > 0)
            {
                var target = proj.GetUnityMainTargetGuid();
                foreach (var texture in settings.IosTemplateImages)
                {
                    if (texture == null)
                        continue;
                    var assetPath = AssetDatabase.GetAssetPath(texture);
                    var extension = Path.GetExtension(assetPath).ToLowerInvariant();
                    if (extension != ".png" && extension != ".jpg" && extension != ".jpeg")
                    {
                        // iconWithTemplateImageName needs a loose image file in the
                        // bundle; a .psd/.tga source has no such file to copy.
                        Debug.LogWarning($"[QuickActions] Template image '{assetPath}' is not a PNG/JPEG file; skipped.");
                        continue;
                    }
                    // A texture in a non-embedded UPM package has a VIRTUAL
                    // "Packages/..." asset path — the bytes live under
                    // Library/PackageCache. Resolve to the physical file.
                    var sourcePath = File.Exists(assetPath) ? assetPath : FileUtil.GetPhysicalPath(assetPath);
                    if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                    {
                        Debug.LogWarning($"[QuickActions] Template image '{assetPath}' has no physical file; skipped.");
                        continue;
                    }
                    var fileName = Path.GetFileName(assetPath);
                    // Case-insensitive: iOS builds run on case-insensitive macOS
                    // filesystems, where Back.png and back.png silently overwrite
                    // each other on disk while getting two PBX references.
                    if (copied.Exists(n => string.Equals(n, fileName, System.StringComparison.OrdinalIgnoreCase)))
                    {
                        Debug.LogWarning($"[QuickActions] Duplicate template-image file name '{fileName}'; skipped.");
                        continue;
                    }
                    try
                    {
                        Directory.CreateDirectory(iconsDir);
                        File.Copy(sourcePath, Path.Combine(iconsDir, fileName), true);
                    }
                    catch (System.IO.IOException e)
                    {
                        Debug.LogWarning($"[QuickActions] Could not copy template image '{assetPath}': {e.Message}; skipped.");
                        continue;
                    }
                    var fileGuid = proj.AddFile(IconsFolder + "/" + fileName, IconsFolder + "/" + fileName);
                    proj.AddFileToBuild(target, fileGuid);
                    copied.Add(fileName);
                }
            }

            // Phase 3 — persist the pbxproj FIRST, then reconcile the disk: delete
            // only stale files that were not re-copied this build, and rewrite the
            // manifest last. If anything above threw, disk still matches the old
            // pbxproj and the manifest still records our files for the next run.
            proj.WriteToFile(projPath);
            foreach (var name in stale)
            {
                if (copied.Exists(n => string.Equals(n, name, System.StringComparison.OrdinalIgnoreCase)))
                    continue;
                var stalePath = Path.Combine(iconsDir, name);
                if (File.Exists(stalePath))
                    File.Delete(stalePath);
            }
            if (copied.Count > 0)
            {
                File.WriteAllLines(manifestPath, copied);
                Debug.Log($"[QuickActions] Copied {copied.Count} template image(s) into the Xcode project.");
            }
            else if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }

        // NO per-locale labels for the STATIC (baked) shortcuts on iOS. This is a
        // deliberate omission, not a gap nobody got to — an implementation was written
        // and withdrawn before release, and this note exists so it is not re-added the
        // same way.
        //
        // iOS localizes an Info.plist value by looking it up as a key in
        // <locale>.lproj/InfoPlist.strings, and the only way to get that layout out of
        // a group-style PBX add is a FOLDER reference — which Xcode copies verbatim
        // into the bundle root. Its output path is therefore
        // <App>.app/<locale>.lproj/InfoPlist.strings, and BOTH components of that path
        // are dictated by the platform: a host that localizes its own display name or
        // usage strings (the standard Unity pattern, and exactly the population that
        // would translate shortcut titles) emits the same path from its own folder
        // reference or variant group. Under Xcode's build system that is a hard
        // "Multiple commands produce …" failure; with a variant group on the host side
        // one copy silently overwrites the other. Either way the package breaks — or
        // silently replaces — output it does not own, which is the one thing the
        // ownership-marker design exists to prevent, and no manifest can police a
        // collision that happens in Copy Bundle Resources.
        //
        // The Android side has an escape hatch iOS does not: it writes its OWN file
        // name (quickactions_strings.xml) inside the shared values-<qualifier>/ folder,
        // so a host's strings.xml is never touched. Static-shortcut localization
        // therefore ships on Android only; iOS static shortcuts render in their base
        // language, and DYNAMIC shortcuts localize on both platforms at runtime.
        //
        // A safe future approach has to merge into whatever the host already has —
        // append our keys to an existing <locale>.lproj/InfoPlist.strings (marker
        // delimited, so cleanup stays scoped), or register with the host's variant
        // group instead of adding a second producer — and must be validated on a real
        // Xcode build, not compile-checked: the PBX stubs here are no-ops, which is
        // precisely why the collision shipped unnoticed.
    }

    /// <summary>
    /// Shared helpers for reading/merging our entries in the iOS
    /// <c>UIApplicationShortcutItems</c> plist array. Our entries carry a marker in
    /// their <c>UIApplicationShortcutItemUserInfo</c> so cleanup/refresh touches only
    /// ours and never a host app's own shortcuts.
    /// </summary>
    internal static class QuickActionsPlistShortcuts
    {
        internal const string ItemsKey = "UIApplicationShortcutItems";
        internal const string UserInfoKey = "UIApplicationShortcutItemUserInfo";
        internal const string MarkerKey = "com.emindeniz99.quickactions.managed";

        internal static PlistElementArray GetOrCreateArray(PlistDocument plist)
        {
            if (plist.root.values.TryGetValue(ItemsKey, out var existing) && existing is PlistElementArray arr)
                return arr;
            return plist.root.CreateArray(ItemsKey);
        }

        // True only for entries this package wrote (marked in their user info).
        internal static bool IsOurs(PlistElement entry)
        {
            if (!(entry is PlistElementDict dict))
                return false;
            if (!dict.values.TryGetValue(UserInfoKey, out var ui) || !(ui is PlistElementDict uiDict))
                return false;
            return uiDict.values.TryGetValue(MarkerKey, out var marker) && marker.AsBoolean();
        }

        // Removes our marked entries, dropping the whole key if nothing else remains.
        // Returns true if the plist changed.
        internal static bool ClearOurEntries(PlistDocument plist)
        {
            if (!plist.root.values.TryGetValue(ItemsKey, out var existing) || !(existing is PlistElementArray arr))
                return false;
            var removed = arr.values.RemoveAll(IsOurs);
            if (arr.values.Count == 0)
                plist.root.values.Remove(ItemsKey);
            return removed > 0;
        }
    }
}
