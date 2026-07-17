// Ungated (UNITY_IOS only) iOS gate cleanup — the iOS analogue of the Android
// trampoline stripper. When QUICKACTIONS_ENABLED is NOT set, it removes the
// QUICKACTIONS_ENABLED=1 macro that the gated macro-injector may have left in a
// reused/"Append" Xcode project (so Plugins/iOS/QuickActions.mm compiles to
// nothing — no +load swizzle, no symbols) and strips our marked static-shortcut
// entries from Info.plist. A fresh "Replace" build regenerates both clean, so
// this matters for Append/exported projects where the gate must still be inert.
//
// This assembly is NOT gated by asmdef defineConstraints (it must be present when
// the define is OFF); instead the body is guarded by a compile-time
// `#if QUICKACTIONS_ENABLED` so it cleans up only when the define is off — the exact
// complement of the gated macro injector, so the two always agree. The gate itself
// is never decided by a runtime PlayerSettings read (that could diverge under Build
// Profiles / csc.rsp / versionDefines); a runtime read exists only as a stale-assembly
// COHERENCE check that fails the build loudly instead of choosing a side. It only
// depends on UNITY_IOS.
using System.IO;
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
#if QUICKACTIONS_ENABLED
            // Gate is ON at compile time (matches the gated macro injector) — keep the
            // macro and shortcuts. See the header note on why this is compile-time, not
            // a runtime PlayerSettings read. One hole remains: a script that removes
            // the define and builds in the SAME editor invocation runs with stale
            // define-ON assemblies (the macro injector just ran). Detect that and fail
            // the build loudly rather than silently shipping the dev-only native layer.
            if (EffectiveDefinesStillContainGate())
                return;
            throw new BuildFailedException(
                "[QuickActions] QUICKACTIONS_ENABLED was removed from the scripting defines, but the editor " +
                "assemblies are still compiled with it (defines changed without a script recompile — e.g. " +
                "SetScriptingDefineSymbols + BuildPlayer inside one batch invocation). This build would still " +
                "contain the dev-only quick-actions pieces. Split the define change and the build into two " +
                "editor invocations (so scripts recompile), or re-add the define. If you supply the define " +
                "only via csc.rsp, mirror it in Player Settings or the active Build Profile so this check can see it.");
#else
            RemoveMacro(report.summary.outputPath);
            RemoveOurPlistEntries(report.summary.outputPath);
#endif
        }

        // Kept OUTSIDE the #if so the stub harness type-checks it in every config; only
        // the compile-time-ON branch calls it. Player Settings for iOS, plus the active
        // Unity 6 Build Profile (profiles ADD symbols on top of Player Settings; read
        // reflectively — the API doesn't exist on 2021/2022 LTS, where Player Settings
        // is the whole truth).
        private static bool EffectiveDefinesStillContainGate()
        {
            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.iOS);
            foreach (var define in defines.Split(';'))
                if (define.Trim() == "QUICKACTIONS_ENABLED")
                    return true;
            try
            {
                var profileType = System.Type.GetType("UnityEditor.Build.Profile.BuildProfile, UnityEditor.CoreModule");
                var getActive = profileType?.GetMethod("GetActiveBuildProfile",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var profile = getActive?.Invoke(null, null);
                if (profile != null &&
                    profileType.GetProperty("scriptingDefines")?.GetValue(profile) is string[] profileDefines)
                    return System.Array.IndexOf(profileDefines, "QUICKACTIONS_ENABLED") >= 0;
            }
            catch (System.Exception)
            {
                // Reflection shape drifted — fall through to "not found" and let the
                // loud BuildFailedException explain the remedies.
            }
            return false;
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
