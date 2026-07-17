// Ungated (UNITY_IOS only) iOS gate cleanup — the iOS analogue of the Android
// trampoline stripper. When QUICKACTIONS_ENABLED is NOT set, it removes the
// QUICKACTIONS_ENABLED=1 macro that the gated macro-injector may have left in a
// reused/"Append" Xcode project (so Plugins/iOS/QuickActions.mm compiles to
// nothing — no +load swizzle, no symbols) and strips our marked static-shortcut
// entries from Info.plist. A fresh "Replace" build regenerates both clean, so
// this matters for Append/exported projects where the gate must still be inert.
//
// This assembly is deliberately NOT gated by QUICKACTIONS_ENABLED (it must run
// when the define is OFF). It only depends on UNITY_IOS.
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace EminDeniz99.QuickActions.Editor.NativeGate
{
    internal sealed class QuickActionsGateCleanupiOS : IPostprocessBuildWithReport
    {
        private const string ItemsKey = "UIApplicationShortcutItems";
        private const string UserInfoKey = "UIApplicationShortcutItemUserInfo";
        private const string MarkerKey = "com.emindeniz99.quickactions.managed";

        public int callbackOrder => 95;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS)
                return;

            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.iOS) ?? string.Empty;
            if (defines.Split(';').Select(s => s.Trim()).Contains("QUICKACTIONS_ENABLED"))
                return; // enabled — keep the macro and shortcuts

            RemoveMacro(report.summary.outputPath);
            RemoveOurPlistEntries(report.summary.outputPath);
        }

        // Strip QUICKACTIONS_ENABLED=1 from the UnityFramework target so the .mm
        // compiles to nothing in a production build.
        private static void RemoveMacro(string outputPath)
        {
            var projectPath = PBXProject.GetPBXProjectPath(outputPath);
            if (string.IsNullOrEmpty(projectPath) || !File.Exists(projectPath))
                return;

            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            var target = project.GetUnityFrameworkTargetGuid();
            var value = project.GetBuildPropertyForAnyConfig(target, "GCC_PREPROCESSOR_DEFINITIONS") ?? string.Empty;
            if (!value.Contains("QUICKACTIONS_ENABLED=1"))
                return;

            // No public "remove one value" API; UpdateBuildProperty removes the given
            // value across configs and keeps $(inherited) so we don't shadow Unity's.
            project.UpdateBuildProperty(target, "GCC_PREPROCESSOR_DEFINITIONS",
                new[] { "$(inherited)" }, new[] { "QUICKACTIONS_ENABLED=1" });
            project.WriteToFile(projectPath);
            Debug.Log("[QuickActions] Removed the QUICKACTIONS_ENABLED macro (gate is off).");
        }

        // Remove ONLY our marked entries so a host app's own shortcuts survive.
        private static void RemoveOurPlistEntries(string outputPath)
        {
            var plistPath = Path.Combine(outputPath, "Info.plist");
            if (!File.Exists(plistPath))
                return;

            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            if (!plist.root.values.TryGetValue(ItemsKey, out var existing) || !(existing is PlistElementArray arr))
                return;

            var removed = arr.values.RemoveAll(e =>
                e is PlistElementDict d
                && d.values.TryGetValue(UserInfoKey, out var ui) && ui is PlistElementDict uiDict
                && uiDict.values.TryGetValue(MarkerKey, out var marker) && marker.AsBoolean());
            if (removed == 0)
                return;

            if (arr.values.Count == 0)
                plist.root.values.Remove(ItemsKey);
            plist.WriteToFile(plistPath);
            Debug.Log("[QuickActions] Stripped static Quick Actions shortcuts from Info.plist (gate is off).");
        }
    }
}
