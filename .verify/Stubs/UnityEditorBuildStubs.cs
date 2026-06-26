// Compile-only stubs for the UnityEditor build/settings APIs used by the static
// shortcut post-processors and the settings provider. Never shipped or executed.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor
{
    public static class AssetDatabase
    {
        public static string[] FindAssets(string filter) => Array.Empty<string>();
        public static string GUIDToAssetPath(string guid) => string.Empty;
        public static T LoadAssetAtPath<T>(string path) where T : UnityEngine.Object => null;
        public static void CreateAsset(UnityEngine.Object asset, string path) { }
        public static void SaveAssets() { }
    }

    public static class PlayerSettings
    {
        public static string applicationIdentifier => "com.example.app";
        public static string GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget target) => string.Empty;
    }

    public enum BuildTarget { NoTarget, iOS, Android, StandaloneWindows64, StandaloneOSX, StandaloneLinux64 }

    public enum SettingsScope { User, Project }

    public class SettingsProvider
    {
        public string label;
        public IEnumerable<string> keywords;
        public Action<string> guiHandler;
        public SettingsProvider(string path, SettingsScope scopes) { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class SettingsProviderAttribute : Attribute { }

    public class Editor
    {
        public static Editor CreateEditor(UnityEngine.Object obj) => new Editor();
        public static void CreateCachedEditor(UnityEngine.Object obj, Type editorType, ref Editor previousEditor)
        {
            previousEditor ??= new Editor();
        }
        public virtual void OnInspectorGUI() { }
    }
}

namespace UnityEditor.Build
{
    public struct NamedBuildTarget
    {
        public static readonly NamedBuildTarget Android = default;
        public static readonly NamedBuildTarget iOS = default;
    }

    public interface IOrderedCallback { int callbackOrder { get; } }
    public interface IPostprocessBuildWithReport : IOrderedCallback
    {
        void OnPostprocessBuild(UnityEditor.Build.Reporting.BuildReport report);
    }
    public interface IPreprocessBuildWithReport : IOrderedCallback
    {
        void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report);
    }
}

namespace UnityEditor.Build.Reporting
{
    public class BuildReport { public BuildSummary summary; }
    public class BuildSummary
    {
        public BuildTarget platform;
        public string outputPath;
    }
}

namespace UnityEditor.Android
{
    public interface IPostGenerateGradleAndroidProject : UnityEditor.Build.IOrderedCallback
    {
        void OnPostGenerateGradleAndroidProject(string path);
    }
}

namespace UnityEditor.iOS.Xcode
{
    public class PBXProject
    {
        public static string GetPBXProjectPath(string buildPath) => string.Empty;
        public void ReadFromFile(string path) { }
        public void WriteToFile(string path) { }
        public string GetUnityFrameworkTargetGuid() => string.Empty;
        public void AddBuildProperty(string targetGuid, string name, string value) { }
    }

    public class PlistDocument
    {
        public PlistElementDict root = new PlistElementDict();
        public void ReadFromFile(string path) { }
        public void WriteToFile(string path) { }
    }

    public abstract class PlistElement { }

    public class PlistElementDict : PlistElement
    {
        public PlistElementArray CreateArray(string key) => new PlistElementArray();
        public PlistElementDict CreateDict(string key) => new PlistElementDict();
        public void SetString(string key, string value) { }
        public void SetBoolean(string key, bool value) { }
        public void SetInteger(string key, int value) { }
    }

    public class PlistElementArray : PlistElement
    {
        public PlistElementDict AddDict() => new PlistElementDict();
        public void AddString(string value) { }
    }
}
