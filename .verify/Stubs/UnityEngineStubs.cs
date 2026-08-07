// Minimal compile-only stubs for the subset of UnityEngine used by this package.
// They exist ONLY so the package C# can be type-checked outside the Unity Editor
// (see .verify/README.md). They are never shipped and never executed.
using System;

namespace UnityEngine
{
    public class Object
    {
        public string name;
        public HideFlags hideFlags;
        public static void DontDestroyOnLoad(Object target) { }
        public static void Destroy(Object obj) { }
        public static void DestroyImmediate(Object obj) { }
    }

    public class Component : Object { }

    public class Behaviour : Component { public bool enabled; }

    public class Coroutine { }

    public class MonoBehaviour : Behaviour
    {
        public Coroutine StartCoroutine(System.Collections.IEnumerator routine) => new Coroutine();
        public void StopCoroutine(Coroutine routine) { }
        public void StopAllCoroutines() { }
    }

    public class GameObject : Object
    {
        public GameObject() { }
        public GameObject(string name) { this.name = name; }
        public T AddComponent<T>() where T : Component, new() => new T();
    }

    [Flags]
    public enum HideFlags { None = 0, HideInHierarchy = 1 }

    public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad, AfterAssembliesLoaded, BeforeSplashScreen, SubsystemRegistration }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute() { }
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType loadType) { }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class AddComponentMenuAttribute : Attribute
    {
        public AddComponentMenuAttribute(string menuName) { }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeField : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class HideInInspector : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TooltipAttribute : Attribute
    {
        public TooltipAttribute(string tooltip) { }
    }

    public static class Debug
    {
        public static void Log(object message) { }
        public static void LogWarning(object message) { }
        public static void LogError(object message) { }
    }

    // Member NAMES and VALUES mirror UnityEngine.SystemLanguage exactly (the enum is
    // index-based), so the package's mapping switch type-checks against the same names
    // — and the same numbering — a real Editor has. Values are written out rather than
    // implied: the tail is NOT alphabetical in Unity (Hindi 42 before Unknown 43), and
    // Hungarian sits at 18 under two names, so an implicit ordering silently drifts.
    public enum SystemLanguage
    {
        Afrikaans = 0, Arabic = 1, Basque = 2, Belarusian = 3, Bulgarian = 4,
        Catalan = 5, Chinese = 6, Czech = 7, Danish = 8, Dutch = 9, English = 10,
        Estonian = 11, Faroese = 12, Finnish = 13, French = 14, German = 15,
        Greek = 16, Hebrew = 17,
        // Unity ships BOTH spellings at value 18: `Hugarian` is its long-standing typo
        // (kept for source compatibility, [Obsolete(error)] there, so nothing may
        // reference it) and `Hungarian` is the alias real code must use. Declaring both
        // keeps this stub honest about the ordinal slot without inviting the typo.
        Hugarian = 18, Hungarian = 18,
        Icelandic = 19, Indonesian = 20, Italian = 21, Japanese = 22, Korean = 23,
        Latvian = 24, Lithuanian = 25, Norwegian = 26, Polish = 27, Portuguese = 28,
        Romanian = 29, Russian = 30, SerboCroatian = 31, Slovak = 32, Slovenian = 33,
        Spanish = 34, Swedish = 35, Thai = 36, Turkish = 37, Ukrainian = 38,
        Vietnamese = 39, ChineseSimplified = 40, ChineseTraditional = 41,
        // Hindi = 42 is deliberately ABSENT: it arrived in Unity 2022.2, and this
        // stub mirrors the package's DECLARED MINIMUM (2021.3). Including it is how
        // an ungated `SystemLanguage.Hindi` compiled here for months and then failed
        // in a real 2021.3 Editor. Anything newer than the minimum belongs behind a
        // UNITY_x_OR_NEWER gate, and is verified in a real Editor, not here.
        Unknown = 43
    }

    public static class Application
    {
        public static void OpenURL(string url) { }

        // Deterministic in the harness (the real property reads the device); tests
        // that care about locale set QuickActions.Locale explicitly.
        public static SystemLanguage systemLanguage => SystemLanguage.English;
    }

    public static class JsonUtility
    {
        public static string ToJson(object obj) => string.Empty;
        public static string ToJson(object obj, bool prettyPrint) => string.Empty;
        public static T FromJson<T>(string json) => default;
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
    }

    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
    }

    public static class Screen
    {
        public static int width => 0;
        public static int height => 0;
    }

    public class GUIStyle { }

    public class GUIContent
    {
        public string text;
        public GUIContent() { }
        public GUIContent(string text) { this.text = text; }
    }

    public class GUILayoutOption { }

    public static class GUILayout
    {
        public static bool Button(string text, params GUILayoutOption[] options) => false;
        public static void Label(string text, params GUILayoutOption[] options) { }
        public static void Space(float pixels) { }
        public static GUILayoutOption Height(float value) => new GUILayoutOption();
        public static GUILayoutOption Width(float value) => new GUILayoutOption();

        public sealed class AreaScope : IDisposable
        {
            public AreaScope(Rect screenRect) { }
            public void Dispose() { }
        }

        public sealed class ScrollViewScope : IDisposable
        {
            public Vector2 scrollPosition;
            public ScrollViewScope(Vector2 scrollPosition) { this.scrollPosition = scrollPosition; }
            public void Dispose() { }
        }
    }

    public class Texture2D : Object { }

    public class ScriptableObject : Object
    {
        public static ScriptableObject CreateInstance(Type type) => (ScriptableObject)Activator.CreateInstance(type);
        public static T CreateInstance<T>() where T : ScriptableObject => Activator.CreateInstance<T>();
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CreateAssetMenuAttribute : Attribute
    {
        public string fileName;
        public string menuName;
        public int order;
    }

    public sealed class AndroidJavaClass : IDisposable
    {
        public AndroidJavaClass(string className) { }
        public T GetStatic<T>(string fieldName) => default;
        public void CallStatic(string methodName, params object[] args) { }
        public T CallStatic<T>(string methodName, params object[] args) => default;
        public void Dispose() { }
    }

    public sealed class AndroidJavaObject : IDisposable
    {
        public AndroidJavaObject(string className, params object[] args) { }
        public T Get<T>(string fieldName) => default;
        public T Call<T>(string methodName, params object[] args) => default;
        public void Call(string methodName, params object[] args) { }
        public void Dispose() { }
    }

    public class AndroidJavaException : System.Exception
    {
        public AndroidJavaException(string message) : base(message) { }
    }
}
