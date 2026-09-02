using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Build entry points for the unity CLI (`unity build --execute-method ...`).
// Unity has no built-in command-line build, so every CI build needs one of these.
public static class TestbedBuilder
{
    private const string Scene = "Assets/Samples/QuickActionsDemo/QuickActionsDemo.unity";

    public static void BuildAndroid()   => Build(BuildTarget.Android, "Builds/QuickActionsDemo.apk");

    // The package's core promise: with QUICKACTIONS_ENABLED absent it must leave
    // NO trace in the shipped app — no trampoline activity, no shortcut
    // meta-data, no classes. This builds the same demo with the define stripped
    // so the two APKs can be diffed.
    // Step 1 of 2 — the package refuses to build if the define changes inside the
    // same invocation, because the editor assemblies would still be compiled
    // with it and the "no trace" claim would be a lie. So this only flips it.
    // Both mobile targets at once: CI's gate-off job builds an Android APK AND an
    // iOS export from one flipped project, and iOS has its own gate (the macro
    // injector) to prove inert.
    public static void DisableDefine()
    {
        PlayerSettings.SetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.Android, "");
        PlayerSettings.SetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.iOS, "");
        AssetDatabase.SaveAssets();
    }

    public static void EnableDefine()
    {
        PlayerSettings.SetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.Android, "QUICKACTIONS_ENABLED");
        PlayerSettings.SetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.iOS, "QUICKACTIONS_ENABLED");
        AssetDatabase.SaveAssets();
    }

    // Step 2 of 2 — run in a SEPARATE invocation, after scripts recompiled.
    public static void BuildAndroidNoDefine() => Build(BuildTarget.Android, "Builds/NoDefine.apk");

    // The same in BuildAndroidPhone's configuration (IL2CPP, both ABIs), so the
    // two APKs differ in nothing but the define: their byte difference is the
    // package's footprint, which CI's gate-off job measures on every push.
    public static void BuildAndroidPhoneNoDefine() => BuildPhone("Builds/NoDefine-phone.apk");

    // ...and the iOS Simulator export with the define off, for the negative
    // half of the iOS gate (no QUICKACTIONS_ENABLED macro in the .pbxproj, no
    // marked UIApplicationShortcutItems).
    public static void BuildiOSSimulatorNoDefine() => BuildSimulator("Builds/iOSSimulatorNoDefine");

    // Sideload build for a real handset. BuildAndroid() inherits the project
    // default (Mono, armeabi-v7a), which will not install on the 64-bit-only
    // SoCs shipping since ~2023 — Mono has no ARM64 backend on Android, so
    // reaching arm64 means IL2CPP. Both ABIs go in one APK so the same file
    // works on any phone from 7.1 up.
    public static void BuildAndroidPhone() => BuildPhone("Builds/QuickActionsDemo-phone.apk");

    // The same build in RELEASE configuration — the one shape CI never produced.
    // PRODUCTION_READINESS.md's last "not run" row: every Android leg in
    // .github/workflows/unity-ci.yml builds a DEVELOPMENT player at the project's
    // default managed stripping level with no minification, so neither of the two
    // transformations that only a shipping build applies has ever run over this
    // package.
    //
    //   * Managed Stripping High — the package ships no link.xml. Its only
    //     reflection-adjacent types are the [Serializable] JSON DTOs
    //     QuickActionList / QuickActionItem, which cross the JNI boundary through
    //     JsonUtility; that the linker keeps them is an argument, and this is the
    //     build that tests it.
    //   * R8 (minifyRelease) — the C# runtime reaches the Java class
    //     com.emindeniz99.quickactions.QuickActionsBridge BY NAME over JNI
    //     (Runtime/Internal/AndroidQuickActionsBridge.cs). Nothing references it
    //     statically, so a minifier is free to rename or strip it, and the lookup
    //     then fails at runtime with no crash and no warning: shortcuts simply
    //     never register. README.md's "Known limits — Android minification"
    //     answers that with a keep rule in
    //     Assets/Plugins/Android/proguard-user.txt, and this build uses that file
    //     VERBATIM (checked in beside this script) rather than a CI-only variation
    //     of it — the documented recipe is the supported configuration, so the
    //     recipe is the thing that has to hold.
    //
    // Deliberately the SAME output path as BuildAndroidPhone: this method runs in
    // its own CI job instance, over its own checkout, so the artifact upload, the
    // aapt2 assertions and the emulator smoke all keep working unchanged on the
    // file name they already know.
    //
    // Signing: no testbed configures a keystore (androidUseCustomKeystore is 0 in
    // all three ProjectSettings.asset), and Unity falls back to the Android debug
    // keystore for a release build too — this relies on that, so the APK the
    // emulator installs is signed without the repo ever holding a key. Nothing
    // here is a shippable artifact.
    //
    // minifyWithR8 is NOT set, deliberately: Unity's scripting reference marks
    // PlayerSettings.Android.minifyWithR8 obsolete — "This property is now
    // obsolete. R8 is always used", because "Android Gradle Plugin 7.0 always uses
    // R8" — on 2022.3 and every line after it
    // (docs.unity3d.com/2022.3/Documentation/ScriptReference/PlayerSettings.Android-minifyWithR8.html),
    // and 2022.3's manual no longer documents the "Use R8" checkbox that 2021.3's
    // Publishing Settings still describe. Setting it there would buy an obsolete
    // warning and change nothing. It is also why CI runs this method on the 2022.3
    // testbed only: on 2021.3 (AGP 4.x, where that checkbox is still real) the
    // same call would minify with ProGuard, which is not what README.md's rule is
    // written against.
    public static void BuildAndroidPhoneRelease()
    {
        PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.Android, ManagedStrippingLevel.High);
        // minifyRelease is the RELEASE build type's minifier, and a development
        // player assembles the DEBUG build type (whose switch is minifyDebug), so
        // the `development: false` below is not a detail of this build — it is
        // what makes the minifier run at all.
        PlayerSettings.Android.minifyRelease = true;
        EnableCustomProguardFile();
        BuildPhone("Builds/QuickActionsDemo-phone.apk", development: false);
    }

    // Publishing Settings ▸ Minify ▸ "Custom Proguard File". The file's mere
    // presence under Assets/Plugins/Android/ is NOT enough: Unity's manual
    // describes the checkbox as what brings it into the build ("Enable the
    // appropriate checkbox. Unity creates a default file in your project, and the
    // file location appears below the checkbox" —
    // docs.unity3d.com/2022.3/Documentation/Manual/class-PlayerSettingsAndroid.html),
    // and the state that checkbox writes is `useCustomProguardFile` in
    // ProjectSettings/ProjectSettings.asset — present, at 0, in all three
    // testbeds. PlayerSettings exposes no scripting property for it
    // (PlayerSettings.Android has minifyDebug and minifyRelease and nothing about
    // ProGuard), so the serialized field the checkbox writes is set directly.
    //
    // Set from this entry point instead of committed as 1, because only this entry
    // point minifies: flipping it for every build of these testbeds would change
    // the input of legs that are green today — gate-off and android-shrink-verify
    // both build this same project.
    private static void EnableCustomProguardFile()
    {
        const string path = "ProjectSettings/ProjectSettings.asset";
        var objects = AssetDatabase.LoadAllAssetsAtPath(path);
        var serialized = objects != null && objects.Length > 0 ? new SerializedObject(objects[0]) : null;
        var property = serialized?.FindProperty("useCustomProguardFile");
        if (property == null)
        {
            // Loud, never silent: without the checkbox R8 never reads the keep
            // rule, and the build would go on to "prove" the documented recipe
            // while testing nothing of the sort.
            Debug.LogError($"[TestbedBuilder] could not set useCustomProguardFile in {path} — " +
                           "Assets/Plugins/Android/proguard-user.txt would be ignored and R8 left " +
                           "free to strip QuickActionsBridge. Refusing to build.");
            EditorApplication.Exit(1);
            return;
        }
        property.boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        Debug.Log("[TestbedBuilder] useCustomProguardFile = true (Assets/Plugins/Android/proguard-user.txt)");
    }

    // The SAME configuration as BuildAndroidPhone, invoked a second time in the
    // same CI job over the same project directory, so the aapt2 assertions can be
    // re-run against an INCREMENTAL build. Everything CI knows about the
    // package's Android output it learned from clean builds on fresh runners;
    // the second time round Unity reuses the Gradle project it staged under
    // Library/, and whether the package's IPostGenerateGradleAndroidProject
    // callback re-runs — or whether files it wrote and Unity never declared
    // survive a re-stage — decides whether the icons, res/raw/quickactions_keep.xml,
    // the baked shortcut resources and the trampoline activity are still there.
    // A separate output path rather than an overwrite: incrementality lives in
    // Library/, not in the APK's file name, so the first APK stays on disk for
    // comparison and both are uploaded as their own artifacts.
    public static void BuildAndroidPhoneSecond() => BuildPhone("Builds/QuickActionsDemo-phone-2.apk");

    private static void BuildPhone(string relativeOutput, bool development = true)
    {
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
        EditorUserBuildSettings.buildAppBundle = false;   // .apk, never .aab
        Build(BuildTarget.Android, relativeOutput, development: development);
    }

    // Gradle-project export, for the resource-shrinker experiment in
    // .github/workflows/unity-ci.yml (job `android-shrink-verify`). The package
    // writes res/raw/quickactions_keep.xml so the icon drawables — reachable
    // only through getIdentifier("ic_quickaction_" + name) — survive
    // shrinkResources; whether AGP actually honours that keep rule can only be
    // answered by a minified release build. Exporting is what lets CI flip
    // minifyEnabled + shrinkResources on POST-export: turning them on inside
    // the testbed would ship experiment-only build configuration to everyone
    // who reads the example. ARM64 alone (not both ABIs) because the experiment
    // inspects the resource table, not compiled code, and one ABI halves the
    // IL2CPP time.
    public static void ExportAndroidGradle()
    {
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        EditorUserBuildSettings.buildAppBundle = false;   // .apk shape, never .aab
        Build(BuildTarget.Android, "Builds/AndroidProject", exportAndroidProject: true);
    }
    public static void BuildiOS()       => Build(BuildTarget.iOS,     "Builds/iOSProject");

    // Simulator variant: the default iOS build targets the device SDK, whose
    // IL2CPP output will not link for a simulator run.
    public static void BuildiOSSimulator() => BuildSimulator("Builds/iOSSimulator");

    private static void BuildSimulator(string relativeOutput)
    {
        PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
        // 2 = Universal. Unity ships baselib-sim-arm64.a and a fat
        // baselib-sim-x64arm64.a; asking for ARM64 alone still emitted an
        // x86_64 project here, which cannot install on an Apple-silicon
        // simulator, so build fat and let the simulator pick its slice.
        PlayerSettings.SetArchitecture(UnityEditor.Build.NamedBuildTarget.iOS, 2);
        Build(BuildTarget.iOS, relativeOutput);
    }

    private static void Build(BuildTarget target, string relativeOutput, bool exportAndroidProject = false, bool development = true)
    {
        // exportAsGoogleAndroidProject persists in Library/EditorUserBuildSettings.asset,
        // so it must be stated on EVERY Android build, not just the one that wants
        // it — otherwise a build after a failed or cached ExportAndroidGradle run
        // silently emits a Gradle directory named *.apk. Asserting it here covers
        // every current and future Android entry point; resetting it at the end of
        // ExportAndroidGradle would not, because a failed build exits the editor.
        if (target == BuildTarget.Android)
        {
            EditorUserBuildSettings.exportAsGoogleAndroidProject = exportAndroidProject;
        }

        var output = Path.Combine(Directory.GetCurrentDirectory(), relativeOutput);
        Directory.CreateDirectory(Path.GetDirectoryName(output));

        PlayerSettings.companyName = "QuickActionsTestbed";
        PlayerSettings.productName = "QuickActionsDemo";
        PlayerSettings.applicationIdentifier = "com.quickactions.testbed";

        var options = new BuildPlayerOptions
        {
            scenes = new[] { Scene },
            locationPathName = output,
            target = target,
            // Development by default: every entry point but the release one has
            // always built a development player, and nothing about them changes
            // here. The flag also chooses the Gradle build type the player is
            // assembled as — debug vs release — which is what
            // PlayerSettings.Android.minifyRelease keys off.
            options = development ? BuildOptions.Development : BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;
        Debug.Log($"[TestbedBuilder] {target} => {summary.result} ({summary.totalErrors} errors, {summary.totalWarnings} warnings)");
        if (summary.result != BuildResult.Succeeded)
        {
            EditorApplication.Exit(1);
        }
    }
}
