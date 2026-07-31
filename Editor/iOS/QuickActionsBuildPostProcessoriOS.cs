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
    /// Writes the static shortcuts from <see cref="QuickActionsSettings"/> into the
    /// generated Xcode project's <c>Info.plist</c> as <c>UIApplicationShortcutItems</c>.
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
            // Same run-always rule for the per-locale label tables: a locale removed
            // from the settings must stop shipping on an Append build.
            SyncLocalizedLabels(report.summary.outputPath, QuickActionsSettings.GetOrNull());

            var plistPath = Path.Combine(report.summary.outputPath, "Info.plist");
            if (!File.Exists(plistPath))
            {
                Debug.LogWarning($"[QuickActions] Info.plist not found at {plistPath}; skipping static shortcuts.");
                return;
            }

            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

            var settings = QuickActionsSettings.GetOrNull();
            if (settings == null || settings.StaticShortcuts.Count == 0)
            {
                // No static shortcuts configured. On an *Append* build the plist may
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
            foreach (var item in settings.StaticShortcuts)
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

        // Per-locale labels for the STATIC (baked) shortcuts. iOS localizes an
        // Info.plist string by looking its VALUE up as a key in InfoPlist.strings, so
        // the base title/subtitle we wrote into UIApplicationShortcutItemTitle /
        // ...Subtitle doubles as the lookup key — there is no separate key column to
        // invent, and an untranslated locale simply falls back to that base text.
        //
        // WHY a FOLDER reference and not plain file adds: a group-style PBX file add
        // flattens into the bundle root (see SyncTemplateImages), which would collapse
        // every locale's InfoPlist.strings onto one path. A folder reference is copied
        // verbatim, so the bundle ends up with the <locale>.lproj/ layout iOS resolves
        // localizations through.
        //
        // Ownership, crash-safety and failure handling are SyncTemplateImages' rules,
        // unchanged: a manifest lists exactly the .lproj folders we created (nothing
        // else is ever deleted), the edited pbxproj is persisted BEFORE any disk
        // delete, and every per-locale failure warns and skips instead of throwing —
        // a half-finished sync must never leave the project referencing files that
        // are gone, nor abort the Info.plist step.
        //
        // NOTE: like SyncTemplateImages, the .verify harness only compile-checks this
        // (its PBX stubs are no-ops); the copy step needs a real Editor + Xcode build.
        private const string LocalizationFolder = "QuickActionsLocalization";
        private const string LocalizationManifest = "quickactions_l10n_manifest.txt";

        private static void SyncLocalizedLabels(string buildPath, QuickActionsSettings settings)
        {
            try
            {
                SyncLocalizedLabelsCore(buildPath, settings);
            }
            catch (System.Exception e)
            {
                // Contained like the icon sync: the static shortcuts still ship, in
                // their base language.
                Debug.LogWarning($"[QuickActions] Localized shortcut labels were not written; " +
                    $"static shortcuts will render in their base language: {e.Message}");
            }
        }

        private static void SyncLocalizedLabelsCore(string buildPath, QuickActionsSettings settings)
        {
            var tables = CollectLocalizedLabels(settings);

            var projPath = PBXProject.GetPBXProjectPath(buildPath);
            if (string.IsNullOrEmpty(projPath) || !File.Exists(projPath))
            {
                if (tables.Count > 0)
                    Debug.LogWarning("[QuickActions] Xcode project not found; skipping localized shortcut labels.");
                return;
            }

            var localizationDir = Path.Combine(buildPath, LocalizationFolder);
            var manifestPath = Path.Combine(localizationDir, LocalizationManifest);

            var proj = new PBXProject();
            proj.ReadFromFile(projPath);

            // Phase 1 — in-memory only: drop the PBX references of the .lproj folders
            // WE created last build, so a locale that is gone stops shipping.
            var stale = new List<string>();
            if (File.Exists(manifestPath))
            {
                foreach (var name in File.ReadAllLines(manifestPath))
                {
                    if (string.IsNullOrWhiteSpace(name))
                        continue;
                    stale.Add(name);
                    var guid = proj.FindFileGuidByProjectPath(LocalizationFolder + "/" + name);
                    if (!string.IsNullOrEmpty(guid))
                        proj.RemoveFile(guid);
                }
            }

            // Phase 2 — write this build's tables and register them.
            var written = new List<string>();
            if (tables.Count > 0)
            {
                var target = proj.GetUnityMainTargetGuid();
                foreach (var table in tables)
                {
                    var folder = table.Key + ".lproj";
                    var projectPath = LocalizationFolder + "/" + folder;
                    try
                    {
                        Directory.CreateDirectory(Path.Combine(localizationDir, folder));
                        // UTF-8: the modern .strings encoding (Xcode reads it since
                        // the UTF-16-only days ended), and the only one that can hold
                        // these labels without a transcoding step.
                        File.WriteAllText(Path.Combine(localizationDir, folder, "InfoPlist.strings"), table.Value);
                    }
                    catch (System.IO.IOException e)
                    {
                        Debug.LogWarning($"[QuickActions] Could not write '{projectPath}': {e.Message}; " +
                            "that locale falls back to the base labels.");
                        continue;
                    }
                    var guid = proj.AddFolderReference(projectPath, projectPath);
                    if (string.IsNullOrEmpty(guid))
                    {
                        Debug.LogWarning($"[QuickActions] Xcode refused a folder reference for '{projectPath}'; " +
                            "that locale falls back to the base labels.");
                        continue;
                    }
                    proj.AddFileToBuild(target, guid);
                    written.Add(folder);
                }
            }

            // Phase 3 — persist the pbxproj FIRST, then reconcile the disk (delete
            // only the folders we listed last build and did not rewrite this one).
            proj.WriteToFile(projPath);
            foreach (var name in stale)
            {
                // Case-insensitive like the template-image sync: iOS builds run on
                // case-insensitive macOS filesystems, where fr.lproj and FR.lproj are
                // one directory — matching case-sensitively here would delete the
                // folder we just wrote.
                if (written.Exists(n => string.Equals(n, name, System.StringComparison.OrdinalIgnoreCase)))
                    continue;
                var staleDir = Path.Combine(localizationDir, name);
                if (Directory.Exists(staleDir))
                    Directory.Delete(staleDir, true);
            }
            if (written.Count > 0)
            {
                File.WriteAllLines(manifestPath, written);
                Debug.Log($"[QuickActions] Wrote localized static-shortcut labels for {written.Count} locale(s).");
            }
            else if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }

        // locale -> InfoPlist.strings body, built from the same items (and the same
        // validity/duplicate rules) the plist step bakes, so the two can't disagree.
        private static SortedDictionary<string, string> CollectLocalizedLabels(QuickActionsSettings settings)
        {
            // Locale keys are matched case-insensitively — the runtime resolver
            // treats "fr" and "FR" as one locale, and on the case-insensitive macOS
            // filesystem their .lproj folders ARE one directory, so they must merge
            // here rather than fight over it.
            var entries = new SortedDictionary<string, Dictionary<string, string>>(
                System.StringComparer.OrdinalIgnoreCase);
            if (settings != null)
            {
                var seen = new HashSet<string>();
                foreach (var item in settings.StaticShortcuts)
                {
                    if (item == null || string.IsNullOrEmpty(item.Id) || string.IsNullOrEmpty(item.Title))
                        continue;
                    if (!seen.Add(item.Id))
                        continue;
                    AddLocalizedLabels(entries, item.LocalizedTitles, item.Title, item.Id);
                    // A subtitle the plist never wrote has no key to translate.
                    if (!string.IsNullOrEmpty(item.Subtitle))
                        AddLocalizedLabels(entries, item.LocalizedSubtitles, item.Subtitle, item.Id);
                }
            }

            var tables = new SortedDictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var locale in entries)
            {
                var body = new System.Text.StringBuilder("/* Generated by QuickActions — do not edit. */\n");
                foreach (var pair in locale.Value)
                    body.Append('"').Append(EscapeStringsLiteral(pair.Key)).Append("\" = \"")
                        .Append(EscapeStringsLiteral(pair.Value)).Append("\";\n");
                tables[locale.Key] = body.ToString();
            }
            return tables;
        }

        private static void AddLocalizedLabels(SortedDictionary<string, Dictionary<string, string>> entries,
            List<LocalizedText> localized, string key, string id)
        {
            if (localized == null)
                return;
            foreach (var entry in localized)
            {
                // Skip exactly what the runtime resolver skips, so a static item and
                // a dynamic one with the same table render the same way.
                if (entry == null || string.IsNullOrEmpty(entry.Locale) || string.IsNullOrEmpty(entry.Text))
                    continue;
                var locale = LprojName(entry.Locale);
                if (locale == null)
                {
                    Debug.LogWarning($"[QuickActions] Static shortcut '{id}': locale '{entry.Locale}' is not a usable " +
                        ".lproj name; that translation was skipped (the base label still ships).");
                    continue;
                }
                if (!entries.TryGetValue(locale, out var table))
                    entries[locale] = table = new Dictionary<string, string>();
                if (table.TryGetValue(key, out var existing))
                {
                    // The key IS the base text, so two shortcuts sharing a base label
                    // share one entry — a conflict can only be resolved by changing
                    // one of the base labels, so say so instead of silently picking.
                    if (existing != entry.Text)
                        Debug.LogWarning($"[QuickActions] Static shortcut '{id}': '{key}' already translates to " +
                            $"'{existing}' in {locale} (two shortcuts share that base label); kept the first. " +
                            "Give them distinct base titles to translate them differently.");
                    continue;
                }
                table[key] = entry.Text;
            }
        }

        // A .lproj folder name is the locale tag itself; keep it to characters that
        // are safe in a directory name AND meaningful to iOS's lookup (letters,
        // digits, '-' and '_'), starting with a letter. Null = unusable.
        private static string LprojName(string locale)
        {
            if (string.IsNullOrEmpty(locale))
                return null;
            var first = locale[0];
            if ((first < 'a' || first > 'z') && (first < 'A' || first > 'Z'))
                return null;
            foreach (var c in locale)
            {
                var ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')
                         || c == '-' || c == '_';
                if (!ok)
                    return null;
            }
            return locale;
        }

        // .strings literals are C-style: backslash, quote and the control characters
        // that would end the line must be escaped or the file fails to parse (and
        // takes every entry after it down with it).
        private static string EscapeStringsLiteral(string value)
        {
            var escaped = new System.Text.StringBuilder(value.Length);
            foreach (var c in value)
            {
                switch (c)
                {
                    case '\\': escaped.Append("\\\\"); break;
                    case '"': escaped.Append("\\\""); break;
                    case '\n': escaped.Append("\\n"); break;
                    case '\r': escaped.Append("\\r"); break;
                    case '\t': escaped.Append("\\t"); break;
                    default: escaped.Append(c); break;
                }
            }
            return escaped.ToString();
        }
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
