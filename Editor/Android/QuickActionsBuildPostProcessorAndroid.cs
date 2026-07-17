// Gated by the Editor.Android asmdef's defineConstraints (UNITY_ANDROID), so it
// only compiles when Android is the active build target and the Android editor
// extension exists.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using EminDeniz99.QuickActions;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

namespace EminDeniz99.QuickActions.Editor
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
        private const string ActionPrefix = "com.emindeniz99.quickactions.PERFORM.";
        private const string TrampolineClass = "com.emindeniz99.quickactions.QuickActionsTrampolineActivity";
        private const string ShortcutsResource = "quickactions_shortcuts";
        private const string StringsResource = "quickactions_strings";

        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var settings = QuickActionsSettings.GetOrNull();
            var hasShortcuts = settings != null && settings.StaticShortcuts.Count > 0;

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
                if (hasShortcuts)
                    Debug.LogWarning("[QuickActions] No launcher activity found; skipping static shortcuts.");
                return;
            }

            if (!hasShortcuts)
            {
                // No static shortcuts configured. A reused/exported Gradle project may
                // still hold the res files + meta-data a previous build wrote; remove
                // them so stale long-press shortcuts don't ship. (Parity with iOS.)
                RemoveGeneratedShortcuts(moduleDir, manifestPath);
                return;
            }

            var appId = ResolveApplicationId(path);
            WriteResources(moduleDir, settings, appId);
            if (InjectMetaData(manifestPath))
                Debug.Log($"[QuickActions] Wrote {settings.StaticShortcuts.Count} static shortcut(s) to the Android project.");
        }

        // Removes the generated shortcut resources and the launcher meta-data so a
        // reused build directory doesn't keep shipping shortcuts after they're all
        // removed from the settings (mirrors the iOS post-processor's stale-plist clear).
        internal static void RemoveGeneratedShortcuts(string moduleDir, string manifestPath)
        {
            var removedMeta = RemoveShortcutsMetaData(manifestPath);
            var xml = Path.Combine(moduleDir, "src", "main", "res", "xml", ShortcutsResource + ".xml");
            var strings = Path.Combine(moduleDir, "src", "main", "res", "values", StringsResource + ".xml");
            var removedFiles = SafeDelete(xml) | SafeDelete(strings);
            if (removedMeta || removedFiles)
                Debug.Log("[QuickActions] Cleared stale static-shortcut output (no static shortcuts configured).");
        }

        // Removes the android.app.shortcuts meta-data from the launcher activity.
        // Returns true if anything was removed. Shared shape with the ungated stripper.
        internal static bool RemoveShortcutsMetaData(string manifestPath)
        {
            if (!File.Exists(manifestPath))
                return false;
            var doc = new XmlDocument();
            doc.Load(manifestPath);
            var removed = false;
            foreach (var meta in doc.GetElementsByTagName("meta-data").Cast<XmlElement>().ToList())
            {
                // Only remove OUR meta-data (resource == @xml/quickactions_shortcuts);
                // never touch a host app's own android.app.shortcuts declaration.
                if (meta.GetAttribute("name", AndroidNs) == "android.app.shortcuts" &&
                    meta.GetAttribute("resource", AndroidNs) == "@xml/" + ShortcutsResource)
                {
                    meta.ParentNode.RemoveChild(meta);
                    removed = true;
                }
            }
            if (removed)
                doc.Save(manifestPath);
            return removed;
        }

        internal static bool SafeDelete(string filePath)
        {
            if (!File.Exists(filePath))
                return false;
            try { File.Delete(filePath); return true; }
            catch { return false; }
        }

        // The static intent targets the trampoline by explicit package+class, so it
        // must use the REAL shipping applicationId. A Gradle `applicationId` override
        // (common in prod / flavors) isn't reflected by PlayerSettings, so prefer the
        // launcher module's build.gradle value and only fall back to the Player setting.
        private static string ResolveApplicationId(string unityLibraryPath)
        {
            try
            {
                var gradle = Path.GetFullPath(Path.Combine(unityLibraryPath, "..", "launcher", "build.gradle"));
                if (File.Exists(gradle))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(
                        File.ReadAllText(gradle), @"applicationId\s+['""]([^'""]+)['""]");
                    if (m.Success)
                        return m.Groups[1].Value;
                }
            }
            catch
            {
                // fall through to the Player setting
            }
            return PlayerSettings.applicationIdentifier;
        }

        private static bool HasLauncherActivity(string manifestPath)
        {
            var doc = new XmlDocument();
            doc.Load(manifestPath);
            return FindLauncherActivity(doc) != null;
        }

        private static XmlElement FindLauncherActivity(XmlDocument doc)
        {
            // The MAIN/LAUNCHER entry can be a plain <activity> OR an <activity-alias>
            // (aliases can own the launcher filter + meta-data). Search both, so a
            // manifest that exposes its launcher via an alias isn't skipped.
            foreach (var tag in new[] { "activity", "activity-alias" })
            {
                foreach (XmlElement component in doc.GetElementsByTagName(tag))
                {
                    foreach (XmlElement filter in component.GetElementsByTagName("intent-filter"))
                    {
                        var hasMain = false;
                        var hasLauncher = false;
                        foreach (XmlElement action in filter.GetElementsByTagName("action"))
                            hasMain |= action.GetAttribute("name", AndroidNs) == "android.intent.action.MAIN";
                        foreach (XmlElement category in filter.GetElementsByTagName("category"))
                            hasLauncher |= category.GetAttribute("name", AndroidNs) == "android.intent.category.LAUNCHER";
                        if (hasMain && hasLauncher)
                            return component;
                    }
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
                strings.AppendLine($"  <string name=\"{shortName}\" formatted=\"false\">{EscapeResValue(item.Title)}</string>");
                strings.AppendLine($"  <string name=\"{longName}\" formatted=\"false\">{EscapeResValue(subtitle)}</string>");

                shortcuts.AppendLine("  <shortcut");
                shortcuts.AppendLine($"      android:shortcutId=\"{Escape(item.Id)}\"");
                shortcuts.AppendLine("      android:enabled=\"true\"");
                if (!string.IsNullOrEmpty(item.AndroidDrawable))
                    shortcuts.AppendLine($"      android:icon=\"@drawable/{Escape(item.AndroidDrawable)}\"");
                else if (item.Icon != IconType.None)
                    // Unlike iOS (system-icon enum) and the Android *dynamic* path, a
                    // static res/xml shortcut can only reference a drawable resource, and
                    // emitting an unresolved @drawable would hard-fail aapt2. Warn instead
                    // of silently shipping no icon.
                    Debug.LogWarning($"[QuickActions] Static shortcut '{item.Id}' has Icon={item.Icon} but no " +
                        "AndroidDrawable; Android static shortcuts need a drawable resource name (its icon " +
                        "will be blank). Set AndroidDrawable to a bundled drawable, or add it at runtime with QuickActions.Add(...).");
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
            File.WriteAllText(Path.Combine(valuesDir, StringsResource + ".xml"), strings.ToString());
        }

        // Returns true when our shortcuts meta-data is present after the call (freshly
        // injected or already ours); false when the launcher already declares a DIFFERENT
        // android.app.shortcuts resource, in which case we warn instead of silently
        // shipping none (Android allows only one shortcuts resource per activity).
        private static bool InjectMetaData(string manifestPath)
        {
            var doc = new XmlDocument();
            doc.Load(manifestPath);

            var activity = FindLauncherActivity(doc);
            if (activity == null)
                return false;

            // Scan the launcher's android.app.shortcuts meta-data: ours (our resource)
            // and/or a host's (a different resource). Collect both before mutating so we
            // can resolve an Append-build state that has BOTH.
            XmlElement ours = null;
            XmlElement host = null;
            foreach (XmlElement meta in activity.GetElementsByTagName("meta-data"))
            {
                if (meta.GetAttribute("name", AndroidNs) != "android.app.shortcuts")
                    continue;
                if (meta.GetAttribute("resource", AndroidNs) == "@xml/" + ShortcutsResource)
                    ours = meta;
                else
                    host = meta;
            }

            if (host != null)
            {
                // A host app / another plugin owns the single shortcuts slot. Drop any
                // stale meta-data WE injected on a prior build first, so the manifest
                // isn't left with two android.app.shortcuts resources (invalid — Android
                // allows one per activity), then warn rather than silently ship none.
                if (ours != null)
                {
                    ours.ParentNode.RemoveChild(ours);
                    doc.Save(manifestPath);
                }
                Debug.LogWarning(
                    $"[QuickActions] The launcher activity already declares android.app.shortcuts " +
                    $"(resource={host.GetAttribute("resource", AndroidNs)}); the configured static shortcuts were NOT injected " +
                    $"(Android allows only one shortcuts resource per activity). Merge them into " +
                    $"that resource manually, or register them at runtime with QuickActions.Add(...).");
                return false;
            }

            if (ours != null)
                return true; // already ours — idempotent (Append re-runs)

            var element = doc.CreateElement("meta-data");
            SetAndroidAttr(doc, element, "name", "android.app.shortcuts");
            SetAndroidAttr(doc, element, "resource", "@xml/" + ShortcutsResource);
            activity.AppendChild(element);
            doc.Save(manifestPath);
            return true;
        }

        private static void SetAndroidAttr(XmlDocument doc, XmlElement element, string name, string value)
        {
            var attr = doc.CreateAttribute("android", name, AndroidNs);
            attr.Value = value;
            element.SetAttributeNode(attr);
        }

        private static string Escape(string value)
        {
            var sb = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                // Drop XML-1.0-illegal control chars (also keeps the action-encoded
                // id single-line so the trampoline's prefix match stays intact).
                if (c < 0x20)
                    continue;
                switch (c)
                {
                    case '&': sb.Append("&amp;"); break;
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '"': sb.Append("&quot;"); break;
                    case '\'': sb.Append("&apos;"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        // String-resource VALUES need Android resource escaping on top of XML
        // escaping: after aapt2 parses the XML, a bare apostrophe or double-quote is
        // a span delimiter and a leading '@'/'?' is a resource reference — all break
        // the build. (formatted="false" on the element neutralizes a literal '%'.)
        private static string EscapeResValue(string value)
        {
            var sb = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                if (c < 0x20)
                    continue;
                switch (c)
                {
                    case '&': sb.Append("&amp;"); break;   // XML well-formedness
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '\'': sb.Append("\\'"); break;    // Android: escape apostrophe
                    case '"': sb.Append("\\\""); break;    // Android: escape quote
                    case '\\': sb.Append("\\\\"); break;   // Android: escape backslash
                    default: sb.Append(c); break;
                }
            }
            if (sb.Length > 0 && (sb[0] == '@' || sb[0] == '?'))
                sb.Insert(0, '\\');
            // aapt trims unquoted leading/trailing whitespace; wrap in literal double
            // quotes to preserve intentional edge whitespace (raw '"' is legal in XML
            // element text, and the escapes above stay valid inside a quoted value).
            if (sb.Length > 0 && (sb[0] == ' ' || sb[sb.Length - 1] == ' '))
            {
                sb.Insert(0, '"');
                sb.Append('"');
            }
            return sb.ToString();
        }
    }
}
