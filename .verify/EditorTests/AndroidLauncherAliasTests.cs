// Harness-only tests (same mechanism as AndroidKeepRulesTests): they drive the
// Android build post-processor on a fabricated Gradle project.
//
// Apps that switch their launcher icon declare several MAIN/LAUNCHER
// <activity-alias> entries and enable one at a time at runtime. The launcher
// reads static shortcuts from the component it shows, so our
// android.app.shortcuts meta-data must sit on every launcher component, enabled
// or not, or the shortcuts disappear after a switch.
using System.IO;
using System.Linq;
using System.Xml;
using NUnit.Framework;
using EminDeniz99.QuickActions;
using EminDeniz99.QuickActions.Editor;
using EminDeniz99.QuickActions.Editor.NativeGate;

namespace EminDeniz99.QuickActions.Tests
{
    [TestFixture]
    public class AndroidLauncherAliasTests : GradleProjectFixture
    {
        private const string Ours = "@xml/quickactions_shortcuts";
        private const string Host = "@xml/host_shortcuts";

        private const string Activity = "com.unity3d.player.UnityPlayerActivity";
        private const string AliasDefault = "com.example.app.IconDefault";
        private const string AliasGold = "com.example.app.IconGold";
        private const string ShareAlias = "com.example.app.ShareAlias";

        public AndroidLauncherAliasTests() : base("qa-alias-") { }

        // The collision test counts warnings, and the log is process-global.
        [SetUp]
        public void ClearWarnings() => UnityEngine.Debug.Warnings.Clear();

        // A launcher <activity>, two launcher aliases (the second disabled, as an
        // icon switcher ships it), and one non-launcher alias that must stay untouched.
        // hostOnGold puts a host's own android.app.shortcuts on the disabled alias.
        private void WriteAliasManifest(bool hostOnGold = false) =>
            WriteManifest(
                "    <activity android:name=\"" + Activity + "\">\n" +
                LauncherFilter +
                "    </activity>\n" +
                "    <activity-alias android:name=\"" + AliasDefault + "\" android:enabled=\"true\"\n" +
                "        android:exported=\"true\" android:targetActivity=\"" + Activity + "\">\n" +
                LauncherFilter +
                "    </activity-alias>\n" +
                "    <activity-alias android:name=\"" + AliasGold + "\" android:enabled=\"false\"\n" +
                "        android:exported=\"true\" android:targetActivity=\"" + Activity + "\">\n" +
                LauncherFilter +
                (hostOnGold
                    ? "      <meta-data android:name=\"android.app.shortcuts\" android:resource=\"" + Host + "\" />\n"
                    : "") +
                "    </activity-alias>\n" +
                "    <activity-alias android:name=\"" + ShareAlias + "\"\n" +
                "        android:exported=\"true\" android:targetActivity=\"" + Activity + "\">\n" +
                "      <intent-filter>\n" +
                "        <action android:name=\"android.intent.action.SEND\" />\n" +
                "        <category android:name=\"android.intent.category.DEFAULT\" />\n" +
                "      </intent-filter>\n" +
                "    </activity-alias>\n");

        private static void AddStaticShortcut() =>
            QuickActionsStaticBuild.Customize += ctx => ctx.Shortcuts.Add(new QuickActionItem("x", "X"));

        // The android.app.shortcuts resources declared directly on one component.
        private string[] ShortcutsMeta(string component)
        {
            var doc = new XmlDocument();
            doc.Load(ManifestPath);
            var element = doc.SelectNodes("//activity | //activity-alias").Cast<XmlElement>()
                .Single(e => e.GetAttribute("name", AndroidNs) == component);
            return element.ChildNodes.OfType<XmlElement>()
                .Where(m => m.Name == "meta-data" && m.GetAttribute("name", AndroidNs) == "android.app.shortcuts")
                .Select(m => m.GetAttribute("resource", AndroidNs))
                .ToArray();
        }

        [Test]
        public void EveryLauncherComponent_GetsOurMetaDataOnce()
        {
            WriteAliasManifest();
            AddStaticShortcut();

            RunPostProcessor();
            RunPostProcessor(); // an Append build re-runs the callback on the same manifest

            foreach (var component in new[] { Activity, AliasDefault, AliasGold })
                CollectionAssert.AreEqual(new[] { Ours }, ShortcutsMeta(component),
                    component + " is a launcher component and must carry exactly one of ours");
            CollectionAssert.IsEmpty(ShortcutsMeta(ShareAlias), "a non-launcher alias must be left alone");
            StringAssert.Contains("android:enabled=\"false\"", File.ReadAllText(ManifestPath),
                "the disabled alias must stay disabled");
        }

        [Test]
        public void Cleanup_RemovesOursFromEveryLauncherComponent()
        {
            WriteAliasManifest();
            AddStaticShortcut();
            RunPostProcessor();
            foreach (var component in new[] { Activity, AliasDefault, AliasGold })
                CollectionAssert.AreEqual(new[] { Ours }, ShortcutsMeta(component), "precondition: " + component);

            QuickActionsStaticBuild.ResetForTests(); // no static items left
            RunPostProcessor();

            foreach (var component in new[] { Activity, AliasDefault, AliasGold })
                CollectionAssert.IsEmpty(ShortcutsMeta(component), component + " still carries our meta-data");

            // The define-off stripper's sweep covers the same components.
            AddStaticShortcut();
            RunPostProcessor();
            QuickActionsTrampolineStripperAndroid.Strip(UnityLibrary);

            foreach (var component in new[] { Activity, AliasDefault, AliasGold })
                CollectionAssert.IsEmpty(ShortcutsMeta(component), component + " survived the define-off strip");
        }

        [Test]
        public void HostDeclarationOnOneAlias_IsLeftAloneAndOthersStillGetOurs()
        {
            WriteAliasManifest(hostOnGold: true);
            AddStaticShortcut();

            RunPostProcessor();
            RunPostProcessor();

            // One warning per run, naming only the component the host owns.
            var collisions = UnityEngine.Debug.Warnings.FindAll(w => w.Contains("NOT injected"));
            Assert.AreEqual(2, collisions.Count, string.Join("\n", UnityEngine.Debug.Warnings));
            Assert.IsTrue(collisions.TrueForAll(w => w.Contains(AliasGold)
                && !w.Contains(Activity) && !w.Contains(AliasDefault)));

            CollectionAssert.AreEqual(new[] { Host }, ShortcutsMeta(AliasGold),
                "the host's own declaration must be kept, and ours not added beside it");
            CollectionAssert.AreEqual(new[] { Ours }, ShortcutsMeta(Activity));
            CollectionAssert.AreEqual(new[] { Ours }, ShortcutsMeta(AliasDefault));

            QuickActionsStaticBuild.ResetForTests();
            RunPostProcessor();

            CollectionAssert.AreEqual(new[] { Host }, ShortcutsMeta(AliasGold), "the cleanup must not touch the host's");
            CollectionAssert.IsEmpty(ShortcutsMeta(Activity));
            CollectionAssert.IsEmpty(ShortcutsMeta(AliasDefault));
        }
    }
}
