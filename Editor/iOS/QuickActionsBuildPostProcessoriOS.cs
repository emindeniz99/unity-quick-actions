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
                if (item.Icon != IconType.None)
                    dict.SetString("UIApplicationShortcutItemIconType", "UIApplicationShortcutIconType" + item.Icon);
                // Tag our entries so a later cleanup/refresh can find exactly ours.
                dict.CreateDict("UIApplicationShortcutItemUserInfo")
                    .SetBoolean(QuickActionsPlistShortcuts.MarkerKey, true);
                count++;
            }

            plist.WriteToFile(plistPath);
            Debug.Log($"[QuickActions] Wrote {count} static shortcut(s) to Info.plist.");
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
