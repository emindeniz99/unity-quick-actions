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
        // Must match Editor/iOS/QuickActionsBuildPostProcessoriOS.cs — the gated
        // assembly that WRITES these, which does not compile when the define is off,
        // so this one cannot share the constants with it. Pinned together by
        // tools~/check_frozen_strings.py so the two copies cannot drift.
        private const string IconsFolder = "QuickActionsIcons";
        private const string IconManifestName = "quickactions_manifest.txt";

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
            RemoveOurTemplateImages(report.summary.outputPath);
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
                if (profile != null)
                {
                    // Unity 6 exposes scriptingDefines as a public FIELD (not a
                    // property) — probe both so a future API change can't silently
                    // blind this check and fail coherent dev-profile builds.
                    object value = profileType.GetProperty("scriptingDefines")?.GetValue(profile)
                        ?? profileType.GetField("scriptingDefines")?.GetValue(profile);
                    if (value is string[] profileDefines)
                        return System.Array.IndexOf(profileDefines, "QUICKACTIONS_ENABLED") >= 0;
                }
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

        // Remove the template-image icons a PREVIOUS define-on build copied in, the
        // PBX references that registered them, and the manifest that records them.
        // Without this the gate is not inert on an Append build: the gated
        // post-processor owns the only copy of this logic (SyncTemplateImagesCore),
        // and its assembly does not even compile when the define is off — so nothing
        // ran, and the images kept shipping in the app bundle of a build that has no
        // quick actions at all. The Android side has asserted its equivalent (no
        // package classes in the dex) since the source stripper landed.
        //
        // Ownership-scoped exactly like RemoveOurPlistEntries: the manifest lists the
        // files WE copied, so a file a host dropped into the same folder is left
        // alone — and the folder itself is removed only once it is empty. Kept
        // OUTSIDE the #if, like EffectiveDefinesStillContainGate, so the stub harness
        // type-checks and drives it in every config.
        //
        // Not covered by CI, and it cannot be as the workflow stands: the gate-off
        // job exports both projects fresh ("Replace"), where this folder never
        // existed to begin with, and the testbeds configure no IosTemplateImages —
        // so the control such an assertion needs (the define-ON export carrying the
        // folder) does not exist either, and a check would pass by vacuum. The bug
        // this fixes only appears on an Append build over a directory a previous
        // define-ON build wrote. .verify/EditorTests/IosGateOffIconCleanupTests.cs is
        // the coverage; read it before changing the scope here.
        internal static void RemoveOurTemplateImages(string outputPath)
        {
            var iconsDir = Path.Combine(outputPath, IconsFolder);
            var manifestPath = Path.Combine(iconsDir, IconManifestName);
            if (!File.Exists(manifestPath))
                return; // no manifest = this build path never received icons of ours

            string[] names;
            try
            {
                names = File.ReadAllLines(manifestPath);
            }
            catch (IOException e)
            {
                Debug.LogWarning($"[QuickActions] Could not read the template-image manifest; " +
                    $"icons from a previous enabled build may still ship: {e.Message}");
                return;
            }

            // Unregister first, so a failure between the two leaves the pbxproj
            // pointing at files that still exist rather than at files that do not.
            // A missing project is not an error here: there is simply nothing to
            // unregister, and the files below are still ours to delete.
            var projectPath = PBXProject.GetPBXProjectPath(outputPath);
            if (!string.IsNullOrEmpty(projectPath) && File.Exists(projectPath))
            {
                var project = new PBXProject();
                project.ReadFromFile(projectPath);
                var unregistered = 0;
                foreach (var name in names)
                {
                    if (string.IsNullOrWhiteSpace(name))
                        continue;
                    var guid = project.FindFileGuidByProjectPath(IconsFolder + "/" + name.Trim());
                    if (string.IsNullOrEmpty(guid))
                        continue;
                    project.RemoveFile(guid);
                    unregistered++;
                }
                if (unregistered > 0)
                    project.WriteToFile(projectPath);
            }

            var deleted = 0;
            foreach (var name in names)
            {
                if (string.IsNullOrWhiteSpace(name))
                    continue;
                var path = Path.Combine(iconsDir, name.Trim());
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                        deleted++;
                    }
                }
                catch (IOException e)
                {
                    // One locked file must not abort the rest of the cleanup, and must
                    // not fail a build whose only fault is a stale icon.
                    Debug.LogWarning($"[QuickActions] Could not delete template image '{name}': {e.Message}");
                }
            }

            try
            {
                File.Delete(manifestPath);
                // Only when empty: anything still in there is not ours.
                if (Directory.Exists(iconsDir) &&
                    Directory.GetFileSystemEntries(iconsDir).Length == 0)
                    Directory.Delete(iconsDir);
            }
            catch (IOException e)
            {
                Debug.LogWarning($"[QuickActions] Could not remove the template-image manifest: {e.Message}");
            }

            if (deleted > 0)
                Debug.Log($"[QuickActions] Removed {deleted} static template image(s) from the Xcode project (gate is off).");
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
