// Harness-only tests, same mechanism and same reason as AndroidGateOffSourceStripTests:
// Editor/NativeGate sits behind an asmdef a Unity test assembly cannot reference on
// every build target, so the real source file is compiled straight into the dotnet
// test assembly.
//
// What they pin: the define-off gate removing the iOS template images a PREVIOUS
// define-ON build copied into the exported Xcode project. Until now nothing did —
// the only copy of that logic lives in SyncTemplateImagesCore, inside the gated
// Editor/iOS assembly, which does not compile when the define is off. So an Append
// build with the gate turned off kept shipping the icons of a build that has no
// quick actions at all, which is exactly what the gate exists to prevent (the
// Android side has asserted its equivalent — no package classes in the dex — since
// the source stripper landed).
//
// Scope is pinned from both sides, like the Android strip: every file OUR manifest
// names goes, and a file a host dropped into the same folder stays — and keeps the
// folder alive with it.
//
// Only the disk half is reachable here. The PBX half needs a real Xcode project,
// and the .verify stub's PBXProject.GetPBXProjectPath returns empty, which the code
// treats as "nothing to unregister" and proceeds — deliberately, because the files
// are ours to delete whether or not a project still references them. The disk half
// is the half that decides what ends up in the app bundle.
using System;
using System.IO;
using NUnit.Framework;
using EminDeniz99.QuickActions.Editor.NativeGate;

namespace EminDeniz99.QuickActions.Tests
{
    [TestFixture]
    public class IosGateOffIconCleanupTests
    {
        // Spelled out rather than read back from the cleanup: these two names are the
        // contract between two assemblies that cannot reference each other, so a test
        // deriving them from the same constant as the code would agree with any typo.
        // (tools~/check_frozen_strings.py pins the two source copies against each other;
        // this pins them against what the test believes.)
        private const string IconsFolder = "QuickActionsIcons";
        private const string ManifestName = "quickactions_manifest.txt";

        private string _output;

        private string IconsDir => Path.Combine(_output, IconsFolder);
        private string ManifestPath => Path.Combine(IconsDir, ManifestName);

        [SetUp]
        public void CreateExportedProject()
        {
            _output = Path.Combine(Path.GetTempPath(), "qa-iosgate-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_output);
        }

        [TearDown]
        public void RemoveExportedProject()
        {
            if (Directory.Exists(_output))
                Directory.Delete(_output, true);
        }

        private void GiveIcons(params string[] names)
        {
            Directory.CreateDirectory(IconsDir);
            foreach (var name in names)
                File.WriteAllText(Path.Combine(IconsDir, name), "png-bytes");
            File.WriteAllLines(ManifestPath, names);
        }

        [Test]
        public void Cleanup_RemovesEveryIconTheManifestNames_AndTheManifestAndFolderWithThem()
        {
            GiveIcons("back.png", "star.png");

            QuickActionsGateCleanupiOS.RemoveOurTemplateImages(_output);

            Assert.IsFalse(File.Exists(Path.Combine(IconsDir, "back.png")),
                "an icon this package copied must not survive a define-off build");
            Assert.IsFalse(File.Exists(Path.Combine(IconsDir, "star.png")));
            Assert.IsFalse(File.Exists(ManifestPath), "the manifest goes with what it records");
            Assert.IsFalse(Directory.Exists(IconsDir),
                "the folder was ours alone, so nothing should be left standing");
        }

        [Test]
        public void Cleanup_LeavesAFileTheManifestDoesNotName_AndKeepsTheFolderForIt()
        {
            // Ownership-scoped exactly like the plist removal: the manifest is the
            // record of what WE wrote. Deleting the folder wholesale would take a
            // host's file with it, on a project shape no CI leg happens to build.
            GiveIcons("ours.png");
            File.WriteAllText(Path.Combine(IconsDir, "host-owned.png"), "not ours");

            QuickActionsGateCleanupiOS.RemoveOurTemplateImages(_output);

            Assert.IsFalse(File.Exists(Path.Combine(IconsDir, "ours.png")));
            Assert.IsTrue(File.Exists(Path.Combine(IconsDir, "host-owned.png")),
                "a file this package never copied must be left alone");
            Assert.IsTrue(Directory.Exists(IconsDir),
                "the folder must stay while it still holds someone else's file");
            Assert.IsFalse(File.Exists(ManifestPath));
        }

        [Test]
        public void Cleanup_WithoutAManifest_DoesNothingAndDoesNotThrow()
        {
            // A project that never received icons of ours — the common case — and the
            // one where an over-eager sweep would do the most damage.
            Directory.CreateDirectory(IconsDir);
            File.WriteAllText(Path.Combine(IconsDir, "someone-elses.png"), "not ours");

            Assert.DoesNotThrow(() => QuickActionsGateCleanupiOS.RemoveOurTemplateImages(_output));

            Assert.IsTrue(File.Exists(Path.Combine(IconsDir, "someone-elses.png")),
                "no manifest means nothing here is ours to remove");
        }

        [Test]
        public void Cleanup_OnAProjectWithNoIconsFolderAtAll_DoesNothingAndDoesNotThrow()
        {
            Assert.DoesNotThrow(() => QuickActionsGateCleanupiOS.RemoveOurTemplateImages(_output));
            Assert.IsFalse(Directory.Exists(IconsDir));
        }

        [Test]
        public void Cleanup_SurvivesAManifestNamingFilesThatAreAlreadyGone()
        {
            // The disk and the manifest can disagree: a developer deleting the folder
            // by hand, or a half-finished previous sweep. Neither may fail the build.
            Directory.CreateDirectory(IconsDir);
            File.WriteAllLines(ManifestPath, new[] { "vanished.png", "", "   ", "also-gone.png" });

            Assert.DoesNotThrow(() => QuickActionsGateCleanupiOS.RemoveOurTemplateImages(_output));

            Assert.IsFalse(File.Exists(ManifestPath), "the manifest is still cleared");
            Assert.IsFalse(Directory.Exists(IconsDir));
        }
    }
}
