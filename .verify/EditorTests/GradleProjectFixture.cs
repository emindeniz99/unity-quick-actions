// The scaffold shared by the harness tests that run the Android build
// post-processor, or the define-off stripper, over a fabricated Gradle export:
// a fresh temp directory per test holding an empty unityLibrary module.
//
// NUnit runs this class's [SetUp] before a derived fixture's and a derived
// [TearDown] before this one's, so a fixture's own extra setup already sees the
// project, and its own cleanup runs while the project still exists. Derived
// fixtures give their extra [SetUp]/[TearDown] methods names of their own, so
// they run beside these rather than in place of them.
using System;
using System.IO;
using NUnit.Framework;
using EminDeniz99.QuickActions.Editor;

namespace EminDeniz99.QuickActions.Tests
{
    public abstract class GradleProjectFixture
    {
        protected const string AndroidNs = "http://schemas.android.com/apk/res/android";

        // What makes an <activity> or <activity-alias> a launcher component to
        // FindLauncherComponents: action MAIN and category LAUNCHER in one filter.
        protected const string LauncherFilter =
            "      <intent-filter>\n" +
            "        <action android:name=\"android.intent.action.MAIN\" />\n" +
            "        <category android:name=\"android.intent.category.LAUNCHER\" />\n" +
            "      </intent-filter>\n";

        // Names the temp directory, so a leftover one says which fixture left it.
        private readonly string _tempPrefix;

        protected GradleProjectFixture(string tempPrefix) => _tempPrefix = tempPrefix;

        // The export root, parent of unityLibrary and of the launcher module.
        protected string Root { get; private set; }

        // The module the Gradle callback is handed.
        protected string UnityLibrary => Path.Combine(Root, "unityLibrary");

        // The sibling module. Never created here: its absence is the shape the
        // post-processor must cope with, and a test that wants it writes into it.
        protected string Launcher => Path.Combine(Root, "launcher");

        protected string ManifestPath => Path.Combine(UnityLibrary, "src", "main", "AndroidManifest.xml");

        // The Customize hook is a process-global static and this harness runs every
        // fixture in one process; a leaked subscriber would make outcomes depend on
        // execution order (a "zero static shortcuts" test would silently get some).
        [SetUp]
        public void CreateProject()
        {
            QuickActionsStaticBuild.ResetForTests();
            Root = Path.Combine(Path.GetTempPath(), _tempPrefix + Guid.NewGuid().ToString("N"));
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
                if (Directory.Exists(Root))
                    Directory.Delete(Root, true);
            }
            catch (IOException)
            {
            }
        }

        protected void RunPostProcessor() =>
            new QuickActionsBuildPostProcessorAndroid()
                .OnPostGenerateGradleAndroidProject(UnityLibrary);

        // unityLibrary's manifest, with `application` as the <application> children.
        protected void WriteManifest(string application)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath));
            File.WriteAllText(ManifestPath,
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<manifest xmlns:android=\"" + AndroidNs + "\"\n" +
                "    package=\"com.example.app\">\n" +
                "  <application>\n" +
                application +
                "  </application>\n" +
                "</manifest>\n");
        }

        // One UnityPlayerActivity. `launcher: false` writes the same file minus its
        // launcher filter — a real shape too (a library module manifest), and the
        // one that makes the post-processor bail out early.
        protected void WriteManifest(bool launcher) =>
            WriteManifest(
                "    <activity android:name=\"com.unity3d.player.UnityPlayerActivity\">\n" +
                (launcher ? LauncherFilter : "") +
                "    </activity>\n");
    }
}
