// Removes the QuickActions trampoline <activity> from the generated Android
// manifest when QUICKACTIONS_ENABLED is NOT set, so the package is inert in a
// production build (the trampoline can no longer be launched). With the define
// off the gated QuickActionsTrampolineInjectorAndroid never runs, so normally
// there is nothing to strip — this is defense in depth against a stale entry
// (e.g. one hand-copied into a custom main manifest).
//
// This assembly is deliberately NOT gated by QUICKACTIONS_ENABLED (it must run
// when the define is OFF). It only depends on UNITY_ANDROID. Note: the trampoline
// .java still compiles into the APK as a dead, unreachable class (~1-2 KB) —
// Unity cannot conditionally exclude a loose native source from compilation. For
// a literally-zero production footprint, keep the package out of the prod project
// entirely (see README "Dev-only").
using System.IO;
using System.Linq;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;

namespace EminDeniz99.QuickActions.Editor.NativeGate
{
    internal sealed class QuickActionsTrampolineStripperAndroid : IPostGenerateGradleAndroidProject
    {
        private const string AndroidNs = "http://schemas.android.com/apk/res/android";
        private const string TrampolineClass = "com.emindeniz99.quickactions.QuickActionsTrampolineActivity";
        // The injector authors the fully-qualified name. Also accept the relative
        // `.QuickActionsTrampolineActivity` shorthand so the gate can't silently
        // fail to strip if a hand-authored entry uses the short form (fail-safe:
        // prefer over-matching to leaving a live trampoline).
        private const string TrampolineClassShort = ".QuickActionsTrampolineActivity";

        public int callbackOrder => 90;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android) ?? string.Empty;
            if (defines.Split(';').Select(s => s.Trim()).Contains("QUICKACTIONS_ENABLED"))
                return; // enabled — keep the trampoline

            // unityLibrary (given) or the sibling launcher module may hold the manifest.
            var modules = new[] { path, Path.GetFullPath(Path.Combine(path, "..", "launcher")) };
            foreach (var module in modules)
            {
                var manifestPath = Path.Combine(module, "src", "main", "AndroidManifest.xml");
                if (!File.Exists(manifestPath))
                    continue;

                var doc = new XmlDocument();
                doc.Load(manifestPath);
                var removed = false;
                foreach (var activity in doc.GetElementsByTagName("activity").Cast<XmlElement>().ToList())
                {
                    var name = activity.GetAttribute("name", AndroidNs);
                    if (name == TrampolineClass || name == TrampolineClassShort)
                    {
                        activity.ParentNode.RemoveChild(activity);
                        removed = true;
                    }
                }
                if (removed)
                    doc.Save(manifestPath);
            }
        }
    }
}
