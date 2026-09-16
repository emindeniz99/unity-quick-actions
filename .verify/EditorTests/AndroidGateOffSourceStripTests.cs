// Harness-only tests (same mechanism, and same reason for living under .verify/,
// as AndroidKeepRulesTests.cs: Editor/NativeGate sits behind an asmdef a Unity
// test assembly cannot reference on every build target, so the real source file
// is compiled straight into the dotnet test assembly).
//
// What they pin: the define-off gate deleting the package's two plugin .java
// SOURCES from the generated Gradle project before Gradle compiles them. Until
// 0.7.0 they shipped as dead, unreachable bytecode in every production APK —
// harmless, but the one thing left that a "with the define off, nothing of this
// package is in your build" claim could not cover.
//
// Both ends regress silently. Delete nothing and the classes come back with a
// green build and a green CI run everywhere except the one define-off job that
// counts dex references. Delete too much — a source ROOT rather than our package
// directory — and the build fails loudly on Unity's own UnityPlayerActivity or,
// worse, quietly takes a host's Java with it on a project shape no CI leg
// happens to build. So the scope is pinned from both sides: our two files go,
// and Unity's, a host app's, another plugin's and the source root itself stay.
using System;
using System.IO;
using NUnit.Framework;
using EminDeniz99.QuickActions.Editor.NativeGate;

namespace EminDeniz99.QuickActions.Tests
{
    [TestFixture]
    public class AndroidGateOffSourceStripTests
    {
        // Spelled out rather than read back from the stripper: the directory Unity
        // stages Plugins/Android/*.java into IS the contract, so a test that
        // derived it from the same constant as the code would agree with any typo.
        private static readonly string[] OurPackage = { "com", "emindeniz99", "quickactions" };

        private string _root;

        [SetUp]
        public void CreateProject()
        {
            _root = Path.Combine(Path.GetTempPath(), "qa-gateoff-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(UnityLibrary);
        }

        [TearDown]
        public void RemoveProject()
        {
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

        private string Launcher => Path.Combine(_root, "launcher");

        private string JavaRoot(string module) => Path.Combine(module, "src", "main", "java");

        private string OurJavaDir(string module) =>
            Path.Combine(JavaRoot(module), OurPackage[0], OurPackage[1], OurPackage[2]);

        private static string Write(string path, string contents)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, contents);
            return path;
        }

        [Test]
        public void Strip_RemovesBothPluginSources_FromUnityLibraryAndTheLauncherModule()
        {
            // The launcher copy is defensive: Unity stages the sources into
            // unityLibrary today, and the gate must not depend on that staying true.
            var files = new[]
            {
                Write(Path.Combine(OurJavaDir(UnityLibrary), "QuickActionsBridge.java"), "package com.emindeniz99.quickactions;"),
                Write(Path.Combine(OurJavaDir(UnityLibrary), "QuickActionsTrampolineActivity.java"), "package com.emindeniz99.quickactions;"),
                Write(Path.Combine(OurJavaDir(Launcher), "QuickActionsBridge.java"), "package com.emindeniz99.quickactions;"),
            };
            foreach (var file in files)
                Assume.That(File.Exists(file), "precondition: " + file);

            QuickActionsTrampolineStripperAndroid.Strip(UnityLibrary);

            foreach (var file in files)
                Assert.IsFalse(File.Exists(file),
                    "the define-off gate must delete the package's own sources before Gradle compiles them: " + file);
            Assert.IsFalse(Directory.Exists(OurJavaDir(UnityLibrary)), "our package directory goes with them");
        }

        [Test]
        public void Strip_LeavesEveryOtherJavaAlone()
        {
            const string body = "// not ours";
            Write(Path.Combine(OurJavaDir(UnityLibrary), "QuickActionsBridge.java"), "package com.emindeniz99.quickactions;");
            // Unity's own player shim — deleting a source ROOT instead of our
            // package directory takes this, and the build fails on every project.
            var unity = Write(Path.Combine(JavaRoot(UnityLibrary), "com", "unity3d", "player", "UnityPlayerActivity.java"), body);
            // A host app's code, and a sibling of ours one segment up: a prefix
            // match on "com/emindeniz99" rather than the full package would take it.
            var host = Write(Path.Combine(JavaRoot(UnityLibrary), "com", "acme", "game", "Boot.java"), body);
            var sibling = Write(Path.Combine(JavaRoot(UnityLibrary), "com", "emindeniz99", "otherplugin", "Thing.java"), body);
            // Another module's generated source, under a nested .androidlib —
            // where run 93 found a testbed BuildConfig.java sitting.
            var nested = Write(Path.Combine(UnityLibrary, "Icons.androidlib", "build", "generated", "source",
                "buildConfig", "debug", "com", "quickactions", "testbed", "BuildConfig.java"), body);

            QuickActionsTrampolineStripperAndroid.Strip(UnityLibrary);

            foreach (var theirs in new[] { unity, host, sibling, nested })
                Assert.AreEqual(body, File.ReadAllText(theirs), "must survive the gate: " + theirs);
            Assert.IsTrue(Directory.Exists(JavaRoot(UnityLibrary)), "the source root itself is never removed");
        }

        [Test]
        public void Strip_OnAProjectWithNoJavaAtAll_DoesNothingAndDoesNotThrow()
        {
            // The shape every define-off build of a project that never imported the
            // package's Android plugin has. A gate that throws out of
            // IPostGenerateGradleAndroidProject fails the build.
            Assert.DoesNotThrow(() => QuickActionsTrampolineStripperAndroid.Strip(UnityLibrary));
            Assert.IsFalse(Directory.Exists(JavaRoot(UnityLibrary)));
        }
    }
}
