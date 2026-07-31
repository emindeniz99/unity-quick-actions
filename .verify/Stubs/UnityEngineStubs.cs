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

    // Member ORDER mirrors UnityEngine.SystemLanguage (the enum is index-based), so
    // the package's mapping switch type-checks against the same names Unity ships.
    public enum SystemLanguage
    {
        Afrikaans, Arabic, Basque, Belarusian, Bulgarian, Catalan, Chinese, Czech,
        Danish, Dutch, English, Estonian, Faroese, Finnish, French, German, Greek,
        Hebrew, Icelandic, Indonesian, Italian, Japanese, Korean, Latvian,
        Lithuanian, Norwegian, Polish, Portuguese, Romanian, Russian, SerboCroatian,
        Slovak, Slovenian, Spanish, Swedish, Thai, Turkish, Ukrainian, Vietnamese,
        ChineseSimplified, ChineseTraditional, Unknown, Hindi
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
