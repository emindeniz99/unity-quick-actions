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
// a side. It only depends on UNITY_ANDROID.
//
// It also deletes the package's two plugin .java SOURCES before Gradle compiles
// them, which is what keeps them out of a define-off APK. UNITY has no mechanism
// for that — PluginImporter's defineConstraints is a managed-plugin feature and
// does not gate a loose native source — and the wording here used to take that
// for "it cannot be done". It can. Run 93 of the CI workflow printed where they
// land:
//
//   unityLibrary/src/main/java/com/emindeniz99/quickactions/QuickActionsBridge.java
//   unityLibrary/src/main/java/com/emindeniz99/quickactions/QuickActionsTrampolineActivity.java
//
// staged at 20:34:12, this callback invoked on that same unityLibrary root at
// 20:34:23 — before Gradle reads the source set. It is the root the res/xml,
// res/values and res/raw deletions below already reach. Until 0.7.0 both classes
// shipped as dead, unreachable bytecode (~20 KB) in every define-off build; the
// define-off CI job counted them (4 dex references either way) instead of gating
// them, and now requires zero.
//
// Scoped to the package DIRECTORY, never to a source root: only
// src/main/java/com/emindeniz99/quickactions goes, so no host or third-party
// Java can be caught by it. A project that wants literally nothing of the
// package in its prod build can still keep it out entirely (README "Dev-only").
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
        private const string KeepResource = "quickactions_keep";
        // The prefix the gated post-processor writes the package's OWN drawables
        // under — distinct from the ic_quickaction_ a project's icons carry, which
        // is what makes a prefix sweep safe. Pinned across files by
        // tools~/check_frozen_strings.py.
        private const string BuiltInIconPrefix = "ic_quickaction_builtin_";
        // The package Plugins/Android/*.java declare. Unity stages them into
        // <module>/src/main/java/<package as directories>, so the directory is
        // DERIVED from the name below rather than spelled out as a path — a rename
        // cannot leave the two out of step. The name itself is pinned across files
        // by tools~/check_frozen_strings.py, which is what makes a rename that
        // missed this copy an error instead of a silently un-gated build.
        private const string PluginJavaPackage = "com.emindeniz99.quickactions";

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
            Strip(path);
#endif
        }

        // The define-off branch, as a method the harness can reach: the .verify test
        // assembly compiles WITH QUICKACTIONS_ENABLED (it has to, to reach the gated
        // post-processor), which makes the #else above unreachable there. Kept
        // outside the #if for the same reason EffectiveDefinesStillContainGate is.
        internal static void Strip(string path)
        {
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
                // The gated post-processor writes the icon keep rule (define ON); with
                // the define off it must not survive into a reused/exported project.
                SafeDelete(Path.Combine(module, "src", "main", "res", "raw", KeepResource + ".xml"));
                // ...and the per-locale copies the baker writes next to the base
                // strings file (res/values-<qualifier>/quickactions_strings.xml).
                // Ours by exact file name inside a values-* folder — a host app's
                // own values-fr/strings.xml is never touched. This assembly cannot
                // reuse the gated GeneratedLocalizedStringFiles helper (its asmdef
                // is defineConstraint'ed away here), hence the local sweep; guarded
                // because the sibling launcher module may have no res/ at all and
                // GetDirectories on a missing path would throw out of the callback.
                var resDir = Path.Combine(module, "src", "main", "res");
                if (Directory.Exists(resDir))
                {
                    foreach (var localeDir in Directory.GetDirectories(resDir, "values-*"))
                        SafeDelete(Path.Combine(localeDir, StringsResource + ".xml"));
                    // ...and the built-in shortcut icons the gated post-processor writes
                    // (drawable*/ic_quickaction_builtin_<name>*.xml). Only in the module
                    // it writes to (unityLibrary — the `path` this callback is handed),
                    // by its own prefix, in any extension: no project writes
                    // ic_quickaction_builtin_, and a project's ic_quickaction_<name> —
                    // wherever it lives, this module's res included — never matches.
                    // The drawable* glob is why the API 26+ variant under
                    // drawable-anydpi-v26/ goes with the plain one, and the prefix why
                    // that variant's two layers (the catalog name plus
                    // _background/_foreground) go with them: a production build must
                    // come out carrying no piece of the package's art, not three
                    // quarters of it.
                    if (module == path)
                        foreach (var drawableDir in Directory.GetDirectories(resDir, "drawable*"))
                            foreach (var icon in Directory.GetFiles(drawableDir, BuiltInIconPrefix + "*"))
                                SafeDelete(icon);
                }

                // The plugin sources themselves. Unity stages Plugins/Android/*.java
                // into this module's source set before calling us, and Gradle has not
                // read it yet, so deleting the package directory is what keeps the two
                // classes out of classes.dex rather than leaving them as dead
                // bytecode. Our own package path only — the sweep never sees a source
                // root, a host's code, or another plugin's.
                var javaDir = Path.Combine(module, "src", "main", "java");
                foreach (var segment in PluginJavaPackage.Split('.'))
                    javaDir = Path.Combine(javaDir, segment);
                SafeDeleteDirectory(javaDir);
            }
        }

        private static void SafeDelete(string filePath)
        {
            if (!File.Exists(filePath))
                return;
            try { File.Delete(filePath); }
            catch { /* best-effort cleanup; leaving a stale file is non-fatal here */ }
        }

        // Same contract as SafeDelete, for the one directory this gate owns. Best
        // effort on purpose: a build must not fail because a file was locked, and
        // the worst case of a failure here is the pre-0.7.0 behaviour — the dead
        // classes ship — which CI's define-off job reports.
        private static void SafeDeleteDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
                return;
            try { Directory.Delete(directoryPath, recursive: true); }
            catch { /* best-effort cleanup; the classes are unreachable either way */ }
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
