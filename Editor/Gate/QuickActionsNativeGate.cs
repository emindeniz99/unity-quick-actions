using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Playground.QuickActions.Editor.Gate
{
    /// <summary>
    /// Includes/excludes the QuickActions <b>native</b> plugins (iOS .mm/.h,
    /// Android .java + AndroidManifest.xml) from a build to match the managed
    /// package's opt-in <c>QUICKACTIONS_ENABLED</c> gate.
    ///
    /// Unity honors asmdef/managed Define Constraints but <b>not</b> native-plugin
    /// ones, so the managed assemblies are gated by the define while the native
    /// plugins are toggled here at build time. The decision mirrors the managed
    /// side exactly: native is included iff the gated <c>Playground.QuickActions</c>
    /// assembly is part of this build (i.e. the define is set). Otherwise the
    /// native plugins are excluded, so a production build links no QuickActions
    /// code and the iOS app-delegate swizzle never runs.
    ///
    /// This assembly is deliberately NOT gated by <c>QUICKACTIONS_ENABLED</c> so it
    /// runs even when the define is off.
    /// </summary>
    internal sealed class QuickActionsNativeGate : IPreprocessBuildWithReport
    {
        private const string ManagedAssembly = "Playground.QuickActions";

        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            var target = report.summary.platform;
            if (target != BuildTarget.iOS && target != BuildTarget.Android)
                return;

            // The managed runtime asmdef compiles only when QUICKACTIONS_ENABLED is
            // set. Mirror that: if its assembly is loaded for this build, enable the
            // native plugins; otherwise disable them. This avoids parsing define
            // strings and stays correct across legacy defines and Build Profiles.
            var enabled = AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => a.GetName().Name == ManagedAssembly);

            foreach (var importer in PluginImporter.GetAllImporters())
            {
                if (!IsQuickActionsNativePlugin(importer.assetPath))
                    continue;
                if (importer.GetCompatibleWithPlatform(target) != enabled)
                {
                    importer.SetCompatibleWithPlatform(target, enabled);
                    importer.SaveAndReimport();
                }
            }
        }

        private static bool IsQuickActionsNativePlugin(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return false;
            var path = assetPath.Replace('\\', '/');
            if (!path.Contains("/Plugins/iOS/") && !path.Contains("/Plugins/Android/"))
                return false;

            switch (Path.GetFileName(path))
            {
                case "QuickActions.mm":
                case "QuickActions.h":
                case "QuickActionsBridge.java":
                case "QuickActionsTrampolineActivity.java":
                    return true;
                case "AndroidManifest.xml":
                    return path.ToLowerInvariant().Contains("quickactions");
                default:
                    return false;
            }
        }
    }
}
