// Harness-only tests (same mechanism as AndroidStaticLocalizationTests): they
// drive the ANDROID build post-processor end to end on a fabricated Gradle
// project, which the Unity Test Runner structurally cannot reach — Editor/Android
// lives behind an asmdef whose defineConstraints are UNITY_ANDROID, so a test
// assembly referencing it would fail to compile whenever another build target is
// active. Living under `.verify/` keeps them invisible to Unity while
// `dotnet test` compiles the real source file.
//
// What they pin is a RELEASE-ONLY failure. Icon drawables are resolved by name at
// runtime (QuickActionsBridge.java: getIdentifier("ic_quickaction_" + …)), so
// under minifyEnabled + shrinkResources nothing statically references them: the
// shrinker can swap their bytes for a tiny placeholder while the resource-table
// entry survives, getIdentifier still returns non-zero, and the launcher draws a
// blank square — in release builds only, looking exactly like the un-configured
// state. res/raw/quickactions_keep.xml is the fix. These tests pin that the file
// is EMITTED, and what it says; whether a real shrinker honors it has not been
// run through a minified Gradle build (see ROADMAP verification item (c)).
using System;
using System.IO;
using System.Xml;
using NUnit.Framework;
using EminDeniz99.QuickActions;
using EminDeniz99.QuickActions.Editor;

namespace EminDeniz99.QuickActions.Tests
{
    [TestFixture]
    public class AndroidKeepRulesTests
    {
        private const string ToolsNs = "http://schemas.android.com/tools";

        private string _root;

        // The Customize hook is a process-global static and this harness runs every
        // fixture in one process; a leaked subscriber would make outcomes depend on
        // execution order (a "zero static shortcuts" test would silently get some).
        [SetUp]
        public void CreateProject()
        {
            QuickActionsStaticBuild.ResetForTests();
            _root = Path.Combine(Path.GetTempPath(), "qa-keep-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(UnityLibrary);
        }

        [TearDown]
        public void RemoveProject()
        {
            QuickActionsStaticBuild.ResetForTests();
            // Tolerate absence: a test may never have created the tree, and a leftover
            // temp directory must never be the reason a suite reports red.
            try
            {
                if (Directory.Exists(_root))
                    Directory.Delete(_root, true);
            }
            catch (IOException)
            {
            }
        }

        // The module the Gradle callback is handed. The sibling "launcher" module is
        // deliberately absent, which is the shape the post-processor must cope with.
        private string UnityLibrary => Path.Combine(_root, "unityLibrary");

        private string KeepFile =>
            Path.Combine(UnityLibrary, "src", "main", "res", "raw", "quickactions_keep.xml");

        private string ShortcutsFile =>
            Path.Combine(UnityLibrary, "src", "main", "res", "xml", "quickactions_shortcuts.xml");

        private string ManifestPath =>
            Path.Combine(UnityLibrary, "src", "main", "AndroidManifest.xml");

        // The manifest shape FindLauncherActivity looks for: an <activity> whose
        // <intent-filter> carries BOTH action MAIN and category LAUNCHER. `launcher:
        // false` writes the same file minus that filter — a real shape too (a library
        // module manifest), and the one that makes the post-processor bail out early.
        private void WriteManifest(bool launcher)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath));
            var filter = launcher
                ? "      <intent-filter>\n" +
                  "        <action android:name=\"android.intent.action.MAIN\" />\n" +
                  "        <category android:name=\"android.intent.category.LAUNCHER\" />\n" +
                  "      </intent-filter>\n"
                : "";
            File.WriteAllText(ManifestPath,
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\"\n" +
                "    package=\"com.example.app\">\n" +
                "  <application>\n" +
                "    <activity android:name=\"com.unity3d.player.UnityPlayerActivity\">\n" +
                filter +
                "    </activity>\n" +
                "  </application>\n" +
                "</manifest>\n");
        }

        private void RunPostProcessor() =>
            new QuickActionsBuildPostProcessorAndroid()
                .OnPostGenerateGradleAndroidProject(UnityLibrary);

        private static void WriteFile(string path, string contents)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, contents);
        }

        [Test]
        public void WritesKeepFile_WhenNoStaticShortcutsAreConfigured()
        {
            // The dynamic-only project is the MAIN case, not an edge one: a consumer
            // that adds every shortcut through QuickActions.Add(...) bakes no static
            // set at all, yet its icons are resolved by exactly the same name lookup.
            // Tying the keep rule to the static set would leave that project — the
            // most common one — unprotected.
            WriteManifest(launcher: true);

            RunPostProcessor();

            Assert.IsTrue(File.Exists(KeepFile), "the keep rule must ship without any static shortcuts");
            Assert.IsFalse(File.Exists(ShortcutsFile),
                "with no static shortcuts the cleanup branch must still have run instead of writing res/xml");
        }

        [Test]
        public void WritesKeepFile_EvenWithNoLauncherActivity()
        {
            // Launcher discovery gates the shortcuts META-DATA, which genuinely needs
            // an activity to attach to. The keep rule needs no activity: it protects
            // resources. Pins that the write happens BEFORE the manifestPath == null
            // return, so a project whose launcher lives somewhere this callback can't
            // see still gets its drawables kept.
            WriteManifest(launcher: false);
            RunPostProcessor();
            Assert.IsTrue(File.Exists(KeepFile), "a non-launcher manifest must not skip the keep rule");

            // …and the harsher variant: no manifest in this module at all.
            File.Delete(ManifestPath);
            File.Delete(KeepFile);
            RunPostProcessor();
            Assert.IsTrue(File.Exists(KeepFile), "a module with no manifest must not skip the keep rule");
        }

        [Test]
        public void KeepFile_IsWellFormedAndKeepsTheDrawablePrefix()
        {
            // Nothing in the build validates this file. res/raw is stored VERBATIM —
            // aapt2 does not parse it, which is exactly why AGP puts keep rules
            // there: tools: attributes are stripped from XML aapt2 compiles, so the
            // rule must survive uncompiled for the resource shrinker to read out of
            // the merged resources. A non-minified build never reads it at all. So a
            // malformed file, or one missing its xmlns:tools declaration, builds
            // green and ships while protecting nothing — the silent release-only
            // blank icons this file exists to prevent. These assertions are the only
            // guard there is; hence the parsed, namespaced attribute rather than a
            // substring of the text.
            WriteManifest(launcher: true);

            RunPostProcessor();

            var doc = new XmlDocument();
            doc.Load(KeepFile);
            Assert.AreEqual("resources", doc.DocumentElement.Name,
                "the resource shrinker only reads tools:keep off a <resources> root");
            Assert.AreEqual("@drawable/ic_quickaction_*",
                doc.DocumentElement.GetAttribute("keep", ToolsNs),
                "the keep value must be the tools-namespaced glob over the icon catalog");
        }

        [Test]
        public void KeepPattern_UsesTheSamePrefixTheJavaLookupBuilds()
        {
            // The two halves of this contract live in different languages: Java
            // concatenates "ic_quickaction_" + <catalog name> at runtime, C# writes
            // the glob at build time. Drift between them is silent — the build stays
            // green and only a minified release ships blank icons. tools~/check_frozen_strings.py
            // pins the literal across both files; this pins that the file we emit is
            // actually built from that constant.
            Assert.AreEqual("ic_quickaction_", QuickActionsBuildPostProcessorAndroid.IconPrefix);

            WriteManifest(launcher: true);
            RunPostProcessor();

            StringAssert.Contains("@drawable/" + QuickActionsBuildPostProcessorAndroid.IconPrefix + "*",
                File.ReadAllText(KeepFile));
        }

        [Test]
        public void RunningTwice_LeavesOneIdenticalKeepFile()
        {
            // Unity re-runs this callback on Append builds and on repeated exports
            // into the same directory. Appending or emitting a second variant name
            // would grow the project on every build (and two keep files with the same
            // rule is at best noise); the write must simply be idempotent.
            WriteManifest(launcher: true);

            RunPostProcessor();
            var first = File.ReadAllText(KeepFile);
            RunPostProcessor();

            Assert.AreEqual(first, File.ReadAllText(KeepFile), "a re-run must not change the file");
            Assert.AreEqual(1,
                Directory.GetFiles(Path.Combine(UnityLibrary, "src", "main", "res", "raw")).Length,
                "a re-run must not leave a second keep file behind");
        }

        [Test]
        public void KeepFile_SurvivesTheStaticShortcutCleanup()
        {
            // Dropping the last static shortcut clears the generated res/xml + strings
            // (so a reused build directory can't keep shipping them). The keep rule is
            // NOT part of that set: the package is still live and its runtime icons
            // still resolve by name. Deleting it here would make "removed my last
            // static shortcut" silently reintroduce blank release icons.
            WriteManifest(launcher: true);
            var strings = Path.Combine(UnityLibrary, "src", "main", "res", "values", "quickactions_strings.xml");
            var french = Path.Combine(UnityLibrary, "src", "main", "res", "values-fr", "quickactions_strings.xml");
            WriteFile(ShortcutsFile, "<shortcuts />");
            WriteFile(strings, "<resources />");
            WriteFile(french, "<resources />");
            WriteFile(KeepFile, "<resources />"); // a stale one from a previous build

            RunPostProcessor();

            Assert.IsFalse(File.Exists(ShortcutsFile), "the stale shortcuts resource must be cleared");
            Assert.IsFalse(File.Exists(strings), "the stale strings resource must be cleared");
            Assert.IsFalse(File.Exists(french), "the stale per-locale strings must be cleared");
            Assert.IsTrue(File.Exists(KeepFile), "the keep rule must outlive the static set");
            StringAssert.Contains("@drawable/ic_quickaction_*", File.ReadAllText(KeepFile),
                "and the stale copy must have been rewritten with the current rule");
        }

        [Test]
        public void RemoveGeneratedShortcuts_DoesNotTouchTheKeepFile()
        {
            // The cleanup is internal and also reachable on its own; pin the exclusion
            // at that level too, so a future caller (or a refactor that moves the
            // ordering around inside the callback) cannot delete the keep rule by
            // routing through here.
            WriteManifest(launcher: true);
            WriteFile(ShortcutsFile, "<shortcuts />");
            WriteFile(KeepFile, "<resources />");

            QuickActionsBuildPostProcessorAndroid.RemoveGeneratedShortcuts(UnityLibrary, ManifestPath);

            Assert.IsFalse(File.Exists(ShortcutsFile));
            Assert.IsTrue(File.Exists(KeepFile), "RemoveGeneratedShortcuts must leave the keep rule alone");
        }

        [Test]
        public void WritesKeepFileAlongsideStaticShortcuts()
        {
            // The two writers share a res/ tree and both create directories under it;
            // pin that neither run clobbers the other's output. ("my_icon" is not
            // covered by our glob and does not need to be: a STATIC item bakes
            // android:icon="@drawable/my_icon" into res/xml, a real reference the
            // shrinker follows. Only a runtime-only custom name needs the
            // consumer's own keep rule, as the README says.)
            WriteManifest(launcher: true);
            QuickActionsStaticBuild.Customize += ctx =>
                ctx.Shortcuts.Add(new QuickActionItem("x", "X") { AndroidDrawable = "my_icon" });

            RunPostProcessor();

            Assert.IsTrue(File.Exists(ShortcutsFile), "the static shortcuts must still be baked");
            Assert.IsTrue(File.Exists(KeepFile), "and the keep rule written alongside them");
        }
    }
}
