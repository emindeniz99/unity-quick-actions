// Gated by the Editor.iOS asmdef's defineConstraints (UNITY_IOS), so it only
// compiles when iOS is the active build target and UnityEditor.iOS.Xcode exists.
using System.Collections.Generic;
using System.IO;
using Playground.QuickActions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace Playground.QuickActions.Editor
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

            var settings = QuickActionsSettings.GetOrNull();
            if (settings == null || settings.StaticShortcuts.Count == 0)
                return;

            var plistPath = Path.Combine(report.summary.outputPath, "Info.plist");
            if (!File.Exists(plistPath))
            {
                Debug.LogWarning($"[QuickActions] Info.plist not found at {plistPath}; skipping static shortcuts.");
                return;
            }

            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

            // Replace any existing array so re-runs are idempotent.
            var items = plist.root.CreateArray("UIApplicationShortcutItems");
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
                count++;
            }

            plist.WriteToFile(plistPath);
            Debug.Log($"[QuickActions] Wrote {count} static shortcut(s) to Info.plist.");
        }
    }
}
