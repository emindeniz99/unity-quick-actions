// Lives in the QUICKACTIONS_ENABLED-gated Editor.iOS assembly, so it runs ONLY
// when the package is enabled. It adds the QUICKACTIONS_ENABLED preprocessor
// define to the generated Xcode project so Plugins/iOS/QuickActions.mm compiles.
// In a production build (define off) this never runs, the macro is never added,
// and the .mm compiles to nothing — no +load swizzle, no symbols. (Native plugins
// can't use asmdef Define Constraints, so the gate is applied to the build output
// here; this works for read-only UPM packages because it edits the output, not
// the source .meta.)
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;

namespace Playground.QuickActions.Editor
{
    internal sealed class QuickActionsEnableMacroiOS : IPostprocessBuildWithReport
    {
        public int callbackOrder => 90;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS)
                return;

            var projectPath = PBXProject.GetPBXProjectPath(report.summary.outputPath);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            // QuickActions.mm compiles into the UnityFramework target.
            var target = project.GetUnityFrameworkTargetGuid();
            // Idempotent: an "Append" build re-runs post-processing over the existing
            // Xcode project, so don't add the macro twice. AddBuildProperty appends
            // (unlike the plist writer), so guard on the current value first.
            var existing = project.GetBuildPropertyForAnyConfig(target, "GCC_PREPROCESSOR_DEFINITIONS") ?? "";
            if (existing.Contains("QUICKACTIONS_ENABLED=1"))
            {
                project.WriteToFile(projectPath);
                return;
            }
            // Keep any project-/xcconfig-level defines (Unity sets several on this
            // target) before appending ours, so we extend rather than shadow them.
            if (!existing.Contains("$(inherited)"))
                project.AddBuildProperty(target, "GCC_PREPROCESSOR_DEFINITIONS", "$(inherited)");
            project.AddBuildProperty(target, "GCC_PREPROCESSOR_DEFINITIONS", "QUICKACTIONS_ENABLED=1");

            project.WriteToFile(projectPath);
        }
    }
}
