// Injects the trampoline <activity> into the generated Gradle project's
// unityLibrary manifest. Unity does NOT merge a loose AndroidManifest.xml that
// lives inside a UPM package (only Assets/Plugins/Android/AndroidManifest.xml —
// the custom main manifest — or .aar/.androidlib manifests are merged), so
// declaring the activity at build time is the only path that works for every
// install flavor. Proven necessary by a real 2022.3 Gradle build: without this,
// the dev APK's merged manifest had no trampoline entry at all.
//
// Lives in the QUICKACTIONS_ENABLED-gated Editor.Android assembly, so a
// production build (define off) never injects anything; the ungated
// QuickActionsTrampolineStripperAndroid additionally strips any pre-existing
// entry there (defense in depth).
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace EminDeniz99.QuickActions.Editor
{
    internal sealed class QuickActionsTrampolineInjectorAndroid : IPostGenerateGradleAndroidProject
    {
        private const string AndroidNs = "http://schemas.android.com/apk/res/android";
        private const string TrampolineClass = "com.emindeniz99.quickactions.QuickActionsTrampolineActivity";

        // Before the static-shortcuts post-processor (100) and the stripper (90):
        // inject first so both see the final activity set.
        public int callbackOrder => 80;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning("[QuickActions] unityLibrary manifest not found; trampoline not injected.");
                return;
            }

            var doc = new XmlDocument();
            doc.Load(manifestPath);

            // Idempotent: an "Append" build re-runs post-processing.
            foreach (XmlElement existing in doc.GetElementsByTagName("activity"))
                if (existing.GetAttribute("name", AndroidNs) == TrampolineClass)
                    return;

            var application = doc.GetElementsByTagName("application").Count > 0
                ? (XmlElement)doc.GetElementsByTagName("application")[0]
                : null;
            if (application == null)
            {
                Debug.LogWarning("[QuickActions] <application> not found; trampoline not injected.");
                return;
            }

            var activity = doc.CreateElement("activity");
            SetAndroidAttr(doc, activity, "name", TrampolineClass);
            SetAndroidAttr(doc, activity, "exported", "true"); // launcher must be able to start it
            SetAndroidAttr(doc, activity, "theme", "@android:style/Theme.Translucent.NoTitleBar");
            SetAndroidAttr(doc, activity, "excludeFromRecents", "true");
            SetAndroidAttr(doc, activity, "noHistory", "true");
            SetAndroidAttr(doc, activity, "taskAffinity", "");
            application.AppendChild(activity);
            doc.Save(manifestPath);
            Debug.Log("[QuickActions] Injected the trampoline <activity> into the unityLibrary manifest.");
        }

        private static void SetAndroidAttr(XmlDocument doc, XmlElement element, string name, string value)
        {
            var attr = doc.CreateAttribute("android", name, AndroidNs);
            attr.Value = value;
            element.SetAttributeNode(attr);
        }
    }
}
