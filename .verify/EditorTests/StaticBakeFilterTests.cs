// Harness-only tests for QuickActionsBakeFilter, the one rule for which static
// shortcuts a build writes: both platform bakers skip what it rejects and the
// settings page warns about the same items. It lives in the gated Editor
// assembly, which the Unity test assembly does not reference.
//
// The last test drives the Android post-processor end to end on a fabricated
// Gradle project (same mechanism as AndroidKeepRulesTests), so the rule is
// pinned where it decides what ships, not only in isolation. The iOS baker
// cannot be driven here: the stub PlistDocument never reads or writes a file.
using System;
using System.IO;
using System.Linq;
using System.Xml;
using NUnit.Framework;
using EminDeniz99.QuickActions;
using EminDeniz99.QuickActions.Editor;
using Verdict = EminDeniz99.QuickActions.Editor.QuickActionsBakeFilter.Verdict;

namespace EminDeniz99.QuickActions.Tests
{
    [TestFixture]
    public class StaticBakeFilterTests
    {
        private const string AndroidNs = "http://schemas.android.com/apk/res/android";

        private string _root;

        [SetUp]
        public void CreateProject()
        {
            QuickActionsStaticBuild.ResetForTests();
            _root = Path.Combine(Path.GetTempPath(), "qa-bake-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(UnityLibrary);
        }

        [TearDown]
        public void RemoveProject()
        {
            QuickActionsStaticBuild.ResetForTests();
            try
            {
                if (Directory.Exists(_root))
                    Directory.Delete(_root, true);
            }
            catch (IOException)
            {
            }
        }

        private string UnityLibrary => Path.Combine(_root, "unityLibrary");

        [Test]
        public void Check_RejectsNullAndItemsWithoutAnIdOrTitle()
        {
            var filter = new QuickActionsBakeFilter();

            Assert.AreEqual(Verdict.MissingIdOrTitle, filter.Check(null));
            Assert.AreEqual(Verdict.MissingIdOrTitle, filter.Check(new QuickActionItem(null, "Title")));
            Assert.AreEqual(Verdict.MissingIdOrTitle, filter.Check(new QuickActionItem("", "Title")));
            Assert.AreEqual(Verdict.MissingIdOrTitle, filter.Check(new QuickActionItem("id", null)));
            Assert.AreEqual(Verdict.MissingIdOrTitle, filter.Check(new QuickActionItem("id", "")));
        }

        [Test]
        public void Check_FirstItemWithAnIdBakes_LaterOnesAreDuplicates()
        {
            var filter = new QuickActionsBakeFilter();

            Assert.AreEqual(Verdict.Bake, filter.Check(new QuickActionItem("play", "Play")));
            Assert.AreEqual(Verdict.DuplicateId, filter.Check(new QuickActionItem("play", "Play again")));
            Assert.AreEqual(Verdict.DuplicateId, filter.Check(new QuickActionItem("play", "And again")));
            Assert.AreEqual(Verdict.Bake, filter.Check(new QuickActionItem("shop", "Shop")));
        }

        [Test]
        public void Check_ARejectedItemDoesNotClaimItsId()
        {
            // An item dropped for a missing title must not make the next, valid
            // item with the same id count as its duplicate.
            var filter = new QuickActionsBakeFilter();

            Assert.AreEqual(Verdict.MissingIdOrTitle, filter.Check(new QuickActionItem("play", "")));
            Assert.AreEqual(Verdict.Bake, filter.Check(new QuickActionItem("play", "Play")));
        }

        [Test]
        public void Check_ComparesIdsCaseSensitively()
        {
            var filter = new QuickActionsBakeFilter();

            Assert.AreEqual(Verdict.Bake, filter.Check(new QuickActionItem("play", "Play")));
            Assert.AreEqual(Verdict.Bake, filter.Check(new QuickActionItem("Play", "Play")));
        }

        [Test]
        public void Accepts_IsTrueOnlyForBake()
        {
            var filter = new QuickActionsBakeFilter();

            Assert.IsTrue(filter.Accepts(new QuickActionItem("play", "Play")));
            Assert.IsFalse(filter.Accepts(new QuickActionItem("play", "Play")));
            Assert.IsFalse(filter.Accepts(new QuickActionItem("", "Play")));
        }

        [Test]
        public void AndroidBake_WritesExactlyTheItemsTheFilterAccepts()
        {
            WriteLauncherManifest();
            QuickActionsStaticBuild.Customize += ctx =>
            {
                ctx.Shortcuts.Add(new QuickActionItem("play", "First"));
                ctx.Shortcuts.Add(new QuickActionItem("play", "Second"));
                ctx.Shortcuts.Add(new QuickActionItem("", "No id"));
                ctx.Shortcuts.Add(new QuickActionItem("shop", ""));
                ctx.Shortcuts.Add(null);
                ctx.Shortcuts.Add(new QuickActionItem("shop", "Shop"));
            };

            new QuickActionsBuildPostProcessorAndroid().OnPostGenerateGradleAndroidProject(UnityLibrary);

            var res = Path.Combine(UnityLibrary, "src", "main", "res");
            var shortcuts = new XmlDocument();
            shortcuts.Load(Path.Combine(res, "xml", "quickactions_shortcuts.xml"));
            var ids = shortcuts.GetElementsByTagName("shortcut").Cast<XmlElement>()
                .Select(s => s.GetAttribute("shortcutId", AndroidNs))
                .ToArray();
            CollectionAssert.AreEqual(new[] { "play", "shop" }, ids);

            var strings = File.ReadAllText(Path.Combine(res, "values", "quickactions_strings.xml"));
            StringAssert.Contains(">First</string>", strings, "the first item with an id is the one baked");
            StringAssert.DoesNotContain("Second", strings);
        }

        private void WriteLauncherManifest()
        {
            var manifest = Path.Combine(UnityLibrary, "src", "main", "AndroidManifest.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(manifest));
            File.WriteAllText(manifest,
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<manifest xmlns:android=\"" + AndroidNs + "\"\n" +
                "    package=\"com.example.app\">\n" +
                "  <application>\n" +
                "    <activity android:name=\"com.unity3d.player.UnityPlayerActivity\">\n" +
                "      <intent-filter>\n" +
                "        <action android:name=\"android.intent.action.MAIN\" />\n" +
                "        <category android:name=\"android.intent.category.LAUNCHER\" />\n" +
                "      </intent-filter>\n" +
                "    </activity>\n" +
                "  </application>\n" +
                "</manifest>\n");
        }
    }
}
