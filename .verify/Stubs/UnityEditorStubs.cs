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
        public static GUIStyle miniLabel => new GUIStyle();
    }

    // The PropertyDrawer surface the IconType drawer uses: compile-only, never drawn.
    public class SerializedObject
    {
        public UnityEngine.Object targetObject;
    }

    public class SerializedProperty
    {
        public int intValue;
        public bool hasMultipleDifferentValues;
        public SerializedObject serializedObject;
    }

    public abstract class PropertyDrawer
    {
        public virtual void OnGUI(Rect position, SerializedProperty property, GUIContent label) { }
        public virtual float GetPropertyHeight(SerializedProperty property, GUIContent label) => 0f;
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class CustomPropertyDrawer : Attribute
    {
        public CustomPropertyDrawer(Type type) { }
        public CustomPropertyDrawer(Type type, bool useForChildren) { }
    }

    public static class EditorGUI
    {
        public static bool showMixedValue;
        public static GUIContent BeginProperty(Rect totalPosition, GUIContent label, SerializedProperty property) => label;
        public static void EndProperty() { }
        public static void BeginChangeCheck() { }
        public static bool EndChangeCheck() => false;
        public static Enum EnumPopup(Rect position, GUIContent label, Enum selected) => selected;
        public static bool PropertyField(Rect position, SerializedProperty property, GUIContent label) => false;
        public static void LabelField(Rect position, string label, GUIStyle style) { }
        public static void LabelField(Rect position, GUIContent label, GUIStyle style) { }
    }

    public static class EditorGUIUtility
    {
        public static float singleLineHeight => 18f;
        public static float standardVerticalSpacing => 2f;
        public static float currentViewWidth => 400f;
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
        public static double timeSinceStartup;
        public static void EnterPlaymode() { }
        public static Action delayCall;
        public static Action<PlayModeStateChange> playModeStateChanged;
    }

    public enum PlayModeStateChange { EnteredEditMode, ExitingEditMode, EnteredPlayMode, ExitingPlayMode }

    public static class EditorUtility
    {
        // Modal dialogs: the 2-button form returns the user's choice, the
        // 1-button form returns nothing. Both are stubbed as no-ops that report
        // "cancelled", so headless compilation never blocks on a dialog.
        public static bool DisplayDialog(string title, string message, string ok, string cancel) => false;
        public static void DisplayDialog(string title, string message, string ok) { }

        public static bool scriptCompilationFailed;

        public static void SetDirty(UnityEngine.Object target) { }
    }

    public static class Undo
    {
        public static void RecordObject(UnityEngine.Object objectToUndo, string name) { }
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
        // Unity exposes `priority` and `validate` as settable members, so call
        // sites use named-argument syntax; positional ctors alone do not compile.
        public int priority;
        public bool validate;
        public MenuItemAttribute(string itemName) { }
        public MenuItemAttribute(string itemName, bool isValidateFunction) { }
        public MenuItemAttribute(string itemName, bool isValidateFunction, int priority) { }
    }
}
