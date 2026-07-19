// Removes the QuickActions trampoline <activity> from the generated Android
// manifest when QUICKACTIONS_ENABLED is NOT set, so the package is inert in a
// production build (the trampoline can no longer be launched). With the define
// off the gated QuickActionsTrampolineInjectorAndroid never runs, so normally
// there is nothing to strip — this is defense in depth against a stale entry
// (e.g. one hand-copied into a custom main manifest).
//
// This assembly is NOT gated by asmdef defineConstraints (it must be present when
// the define is OFF); instead the body is guarded by a compile-time
// `#if QUICKACTIONS_ENABLED` so it strips only when the define is off — the exact
// complement of the gated injector, so the two always agree. The gate itself is
// never decided by a runtime PlayerSettings read; a runtime read exists only as a
// stale-assembly COHERENCE check that fails the build loudly instead of choosing
// a side. It only depends on UNITY_ANDROID. Note: the trampoline
// .java still compiles into the APK as a dead, unreachable class (~1-2 KB) —
// Unity cannot conditionally exclude a loose native source from compilation. For
// a literally-zero production footprint, keep the package out of the prod project
// entirely (see README "Dev-only").
using System.IO;
using System.Linq;
using System.Xml;
using UnityEditor.Android;

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
        private const string ShortcutsResource = "quickactions_shortcuts";
        private const string StringsResource = "quickactions_strings";

        public int callbackOrder => 90;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
#if QUICKACTIONS_ENABLED
            // Gate is ON at COMPILE time — the same truth the gated injector uses. Using
            // the compile-time define (not a runtime PlayerSettings read) keeps the
            // stripper in lock-step with the injector across Player Settings, csc.rsp,
            // versionDefines and Unity 6 Build Profiles, so they can never disagree and
            // produce a manifest that advertises shortcuts targeting a stripped activity.
            //
            // One hole remains: a script that REMOVES the define and calls BuildPlayer in
            // the same editor invocation runs with these STALE assemblies (compiled
            // define-ON) — the injector just injected a trampoline the caller wanted
            // gone. Detect that incoherence and fail the build loudly instead of
            // silently shipping dev-only pieces in what was meant to be a prod build.
            if (EffectiveDefinesStillContainGate())
                return; // enabled and coherent — keep the trampoline
            throw new UnityEditor.Build.BuildFailedException(
                "[QuickActions] QUICKACTIONS_ENABLED was removed from the scripting defines, but the editor " +
                "assemblies are still compiled with it (defines changed without a script recompile — e.g. " +
                "SetScriptingDefineSymbols + BuildPlayer inside one batch invocation). This build would still " +
                "contain the dev-only quick-actions pieces. Split the define change and the build into two " +
                "editor invocations (so scripts recompile), or re-add the define. If you supply the define " +
                "only via csc.rsp, mirror it in Player Settings or the active Build Profile so this check can see it.");
#else
            // unityLibrary (given) or the sibling launcher module may hold the manifest.
            var modules = new[] { path, Path.GetFullPath(Path.Combine(path, "..", "launcher")) };
            foreach (var module in modules)
            {
                var manifestPath = Path.Combine(module, "src", "main", "AndroidManifest.xml");
                if (File.Exists(manifestPath))
                {
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
                    // Also drop our static-shortcuts meta-data so a reused/exported
                    // project can't still advertise package shortcuts whose intents
                    // target the trampoline we just removed. Only OUR entry
                    // (resource == @xml/quickactions_shortcuts) — never the host app's.
                    foreach (var meta in doc.GetElementsByTagName("meta-data").Cast<XmlElement>().ToList())
                    {
                        if (meta.GetAttribute("name", AndroidNs) == "android.app.shortcuts" &&
                            meta.GetAttribute("resource", AndroidNs) == "@xml/" + ShortcutsResource)
                        {
                            meta.ParentNode.RemoveChild(meta);
                            removed = true;
                        }
                    }
                    if (removed)
                        doc.Save(manifestPath);
                }

                // Delete our generated (uniquely named) shortcut resources too.
                SafeDelete(Path.Combine(module, "src", "main", "res", "xml", ShortcutsResource + ".xml"));
                SafeDelete(Path.Combine(module, "src", "main", "res", "values", StringsResource + ".xml"));
            }
#endif
        }

        private static void SafeDelete(string filePath)
        {
            if (!File.Exists(filePath))
                return;
            try { File.Delete(filePath); }
            catch { /* best-effort cleanup; leaving a stale file is non-fatal here */ }
        }

        // Kept OUTSIDE the #if so the stub harness type-checks it in every config;
        // only the compile-time-ON branch above calls it. True when the EFFECTIVE
        // defines still contain the gate: Player Settings for Android, plus the
        // active Unity 6 Build Profile (profiles ADD symbols on top of Player
        // Settings — a dev profile may carry the define alone; read reflectively,
        // the API doesn't exist on 2021/2022 LTS where Player Settings is the
        // whole truth).
        private static bool EffectiveDefinesStillContainGate()
        {
            var defines = UnityEditor.PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.Android);
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
    }
}
