// Minimal compile-only stubs for the subset of UnityEditor used by this package.
// Never shipped, never executed — see .verify/README.md.
using System;
using UnityEngine;

namespace UnityEditor
{
    public class EditorWindow : ScriptableObject
    {
        public Vector2 minSize;
        public Vector2 maxSize;
        public GUIContent titleContent;
        public static T GetWindow<T>() where T : EditorWindow => CreateInstance<T>();
        public static T GetWindow<T>(bool utility, string title) where T : EditorWindow => CreateInstance<T>();
        public void Show() { }
        public void Close() { }
    }

    public static class EditorStyles
    {
        public static GUIStyle boldLabel => new GUIStyle();
        public static GUIStyle label => new GUIStyle();
        public static GUIStyle wordWrappedLabel => new GUIStyle();
        public static GUIStyle textArea => new GUIStyle();
        public static GUIStyle textField => new GUIStyle();
        public static GUIStyle miniButton => new GUIStyle();
    }

    public static class EditorGUILayout
    {
        public static void LabelField(string label) { }
        public static void LabelField(string label, GUIStyle style) { }
        public static void LabelField(string label1, string label2) { }
        public static void SelectableLabel(string text, params GUILayoutOption[] options) { }
        public static void SelectableLabel(string text, GUIStyle style, params GUILayoutOption[] options) { }
        public static void Space() { }
        public static void HelpBox(string message, MessageType type) { }
        public static string TextField(string label, string text) => text;
        public static bool Toggle(string label, bool value) => value;
    }

    public enum MessageType { None, Info, Warning, Error }

    public static class EditorApplication
    {
        public static bool isPlaying;
        public static bool isPlayingOrWillChangePlaymode;
        public static void EnterPlaymode() { }
        public static Action delayCall;
        public static Action<PlayModeStateChange> playModeStateChanged;
    }

    public enum PlayModeStateChange { EnteredEditMode, ExitingEditMode, EnteredPlayMode, ExitingPlayMode }

    public static class EditorUtility
    {
        public static bool scriptCompilationFailed;
    }

    public static class SessionState
    {
        public static void SetString(string key, string value) { }
        public static string GetString(string key, string defaultValue) => defaultValue;
        public static void EraseString(string key) { }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class InitializeOnLoadAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MenuItemAttribute : Attribute
    {
        public MenuItemAttribute(string itemName) { }
        public MenuItemAttribute(string itemName, bool isValidateFunction) { }
        public MenuItemAttribute(string itemName, bool isValidateFunction, int priority) { }
    }
}
