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
    public static void DisableDefine()
    {
        PlayerSettings.SetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.Android, "");
        AssetDatabase.SaveAssets();
    }

    public static void EnableDefine()
    {
        PlayerSettings.SetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.Android, "QUICKACTIONS_ENABLED");
        AssetDatabase.SaveAssets();
    }

    // Step 2 of 2 — run in a SEPARATE invocation, after scripts recompiled.
    public static void BuildAndroidNoDefine() => Build(BuildTarget.Android, "Builds/NoDefine.apk");

    // Sideload build for a real handset. BuildAndroid() inherits the project
    // default (Mono, armeabi-v7a), which will not install on the 64-bit-only
    // SoCs shipping since ~2023 — Mono has no ARM64 backend on Android, so
    // reaching arm64 means IL2CPP. Both ABIs go in one APK so the same file
    // works on any phone from 7.1 up.
    public static void BuildAndroidPhone()
    {
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
        EditorUserBuildSettings.buildAppBundle = false;   // .apk, never .aab
        Build(BuildTarget.Android, "Builds/QuickActionsDemo-phone.apk");
    }
    public static void BuildiOS()       => Build(BuildTarget.iOS,     "Builds/iOSProject");

    // Simulator variant: the default iOS build targets the device SDK, whose
    // IL2CPP output will not link for a simulator run.
    public static void BuildiOSSimulator()
    {
        PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
        // 2 = Universal. Unity ships baselib-sim-arm64.a and a fat
        // baselib-sim-x64arm64.a; asking for ARM64 alone still emitted an
        // x86_64 project here, which cannot install on an Apple-silicon
        // simulator, so build fat and let the simulator pick its slice.
        PlayerSettings.SetArchitecture(UnityEditor.Build.NamedBuildTarget.iOS, 2);
        Build(BuildTarget.iOS, "Builds/iOSSimulator");
    }

    private static void Build(BuildTarget target, string relativeOutput)
    {
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
            options = BuildOptions.Development,
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
