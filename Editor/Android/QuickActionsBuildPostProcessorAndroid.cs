// Gated by the Editor.Android asmdef's defineConstraints (UNITY_ANDROID), so it
// only compiles when Android is the active build target and the Android editor
// extension exists.
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using Playground.QuickActions;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

namespace Playground.QuickActions.Editor
{
    /// <summary>
    /// Bakes the static shortcuts from <see cref="QuickActionsSettings"/> into the
    /// generated Gradle project: writes <c>res/xml/quickactions_shortcuts.xml</c>
    /// (+ the required string resources) and injects the
    /// <c>android.app.shortcuts</c> meta-data into the launcher activity.
    ///
    /// Static shortcut intents target <see cref="QuickActionsTrampolineActivity"/>
    /// and encode the action id in the intent action (XML shortcut intents cannot
    /// carry extras); the trampoline decodes it. Guarded by <c>UNITY_ANDROID</c>.
    /// </summary>
    internal sealed class QuickActionsBuildPostProcessorAndroid : IPostGenerateGradleAndroidProject
    {
        private const string AndroidNs = "http://schemas.android.com/apk/res/android";
        private const string ActionPrefix = "com.playground.quickactions.PERFORM.";
        private const string TrampolineClass = "com.playground.quickactions.QuickActionsTrampolineActivity";
        private const string ShortcutsResource = "quickactions_shortcuts";

        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var settings = QuickActionsSettings.GetOrNull();
            if (settings == null || settings.StaticShortcuts.Count == 0)
                return;

            // The launcher activity (MAIN/LAUNCHER) may live in the unityLibrary
            // module (given here) or the sibling launcher module. Resources and the
            // meta-data must go in whichever module declares it.
            var modules = new[] { path, Path.GetFullPath(Path.Combine(path, "..", "launcher")) };
            string manifestPath = null, moduleDir = null;
            foreach (var module in modules)
            {
                var candidate = Path.Combine(module, "src", "main", "AndroidManifest.xml");
                if (File.Exists(candidate) && HasLauncherActivity(candidate))
                {
                    manifestPath = candidate;
                    moduleDir = module;
                    break;
                }
            }
            if (manifestPath == null)
            {
                Debug.LogWarning("[QuickActions] No launcher activity found; skipping static shortcuts.");
                return;
            }

            var appId = PlayerSettings.applicationIdentifier;
            WriteResources(moduleDir, settings, appId);
            InjectMetaData(manifestPath);
            Debug.Log($"[QuickActions] Wrote {settings.StaticShortcuts.Count} static shortcut(s) to the Android project.");
        }

        private static bool HasLauncherActivity(string manifestPath)
        {
            var doc = new XmlDocument();
            doc.Load(manifestPath);
            return FindLauncherActivity(doc) != null;
        }

        private static XmlElement FindLauncherActivity(XmlDocument doc)
        {
            foreach (XmlElement activity in doc.GetElementsByTagName("activity"))
            {
                foreach (XmlElement filter in activity.GetElementsByTagName("intent-filter"))
                {
                    var hasMain = false;
                    var hasLauncher = false;
                    foreach (XmlElement action in filter.GetElementsByTagName("action"))
                        hasMain |= action.GetAttribute("name", AndroidNs) == "android.intent.action.MAIN";
                    foreach (XmlElement category in filter.GetElementsByTagName("category"))
                        hasLauncher |= category.GetAttribute("name", AndroidNs) == "android.intent.category.LAUNCHER";
                    if (hasMain && hasLauncher)
                        return activity;
                }
            }
            return null;
        }

        private static void WriteResources(string moduleDir, QuickActionsSettings settings, string appId)
        {
            var xmlDir = Path.Combine(moduleDir, "src", "main", "res", "xml");
            var valuesDir = Path.Combine(moduleDir, "src", "main", "res", "values");
            Directory.CreateDirectory(xmlDir);
            Directory.CreateDirectory(valuesDir);

            var shortcuts = new StringBuilder();
            shortcuts.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            shortcuts.AppendLine("<shortcuts xmlns:android=\"http://schemas.android.com/apk/res/android\">");

            var strings = new StringBuilder();
            strings.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            strings.AppendLine("<resources>");

            var seen = new HashSet<string>();
            var index = 0;
            foreach (var item in settings.StaticShortcuts)
            {
                if (item == null || string.IsNullOrEmpty(item.Id) || string.IsNullOrEmpty(item.Title))
                    continue;

                if (!seen.Add(item.Id))
                    continue; // skip genuine duplicate ids

                // Resource names are index-based so they are unique regardless of
                // how the (raw) id is spelled; the shortcutId/action keep the raw id.
                var shortName = "qa_short_" + index;
                var longName = "qa_long_" + index;
                index++;
                var subtitle = string.IsNullOrEmpty(item.Subtitle) ? item.Title : item.Subtitle;
                strings.AppendLine($"  <string name=\"{shortName}\">{Escape(item.Title)}</string>");
                strings.AppendLine($"  <string name=\"{longName}\">{Escape(subtitle)}</string>");

                shortcuts.AppendLine("  <shortcut");
                shortcuts.AppendLine($"      android:shortcutId=\"{Escape(item.Id)}\"");
                shortcuts.AppendLine("      android:enabled=\"true\"");
                if (!string.IsNullOrEmpty(item.AndroidDrawable))
                    shortcuts.AppendLine($"      android:icon=\"@drawable/{Escape(item.AndroidDrawable)}\"");
                shortcuts.AppendLine($"      android:shortcutShortLabel=\"@string/{shortName}\"");
                shortcuts.AppendLine($"      android:shortcutLongLabel=\"@string/{longName}\">");
                shortcuts.AppendLine("    <intent");
                shortcuts.AppendLine($"        android:action=\"{Escape(ActionPrefix + item.Id)}\"");
                shortcuts.AppendLine($"        android:targetPackage=\"{Escape(appId)}\"");
                shortcuts.AppendLine($"        android:targetClass=\"{TrampolineClass}\" />");
                shortcuts.AppendLine("  </shortcut>");
            }

            shortcuts.AppendLine("</shortcuts>");
            strings.AppendLine("</resources>");

            File.WriteAllText(Path.Combine(xmlDir, ShortcutsResource + ".xml"), shortcuts.ToString());
            File.WriteAllText(Path.Combine(valuesDir, "quickactions_strings.xml"), strings.ToString());
        }

        private static void InjectMetaData(string manifestPath)
        {
            var doc = new XmlDocument();
            doc.Load(manifestPath);

            var activity = FindLauncherActivity(doc);
            if (activity == null)
                return;

            // Idempotent: don't add the meta-data twice.
            foreach (XmlElement meta in activity.GetElementsByTagName("meta-data"))
            {
                if (meta.GetAttribute("name", AndroidNs) == "android.app.shortcuts")
                    return;
            }

            var element = doc.CreateElement("meta-data");
            SetAndroidAttr(doc, element, "name", "android.app.shortcuts");
            SetAndroidAttr(doc, element, "resource", "@xml/" + ShortcutsResource);
            activity.AppendChild(element);
            doc.Save(manifestPath);
        }

        private static void SetAndroidAttr(XmlDocument doc, XmlElement element, string name, string value)
        {
            var attr = doc.CreateAttribute("android", name, AndroidNs);
            attr.Value = value;
            element.SetAttributeNode(attr);
        }

        private static string Escape(string value)
        {
            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }
    }
}
