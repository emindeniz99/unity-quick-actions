// Harness-only tests for Editor/Core's QuickActionsDefineGate, the one copy of
// the define rules the enable menu and both define-off cleanups share. No Unity
// test assembly references those ungated editor assemblies, so the sources are
// compiled straight into the dotnet test assembly, like the NativeGate tests.
//
// What they pin: the exact-token match the menu and the stale-build check both
// use, and that each cleanup, compiled here WITH the define as a stale editor's
// would be, fails the build through that shared check when no define is visible
// for its own target, and passes when it is (the coherent dev build). The stub
// PlayerSettings keeps defines per target, and there is no Unity 6 BuildProfile
// type to find, so Player Settings is the whole truth here.
using System;
using System.IO;
using NUnit.Framework;
using EminDeniz99.QuickActions.Editor.Core;
using EminDeniz99.QuickActions.Editor.NativeGate;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace EminDeniz99.QuickActions.Tests
{
    [TestFixture]
    public class DefineGateTests
    {
        // Spelled out rather than read back from the gate, so the value is pinned too.
        private const string Define = "QUICKACTIONS_ENABLED";
        private const string StaleMessageStart = "[QuickActions] QUICKACTIONS_ENABLED was removed";

        [Test]
        public void HasDefine_FindsTheWholeToken_AmongOthersAndInsideWhitespace()
        {
            Assert.IsTrue(QuickActionsDefineGate.HasDefine(Define));
            Assert.IsTrue(QuickActionsDefineGate.HasDefine("FOO;" + Define + ";BAR"));
            Assert.IsTrue(QuickActionsDefineGate.HasDefine("FOO; " + Define + " "));
        }

        [Test]
        public void HasDefine_RejectsADefineThatOnlyContainsIt()
        {
            // A substring test would read either of these as "enabled".
            Assert.IsFalse(QuickActionsDefineGate.HasDefine(Define + "_OFF"));
            Assert.IsFalse(QuickActionsDefineGate.HasDefine("NO_" + Define));
            Assert.IsFalse(QuickActionsDefineGate.HasDefine("FOO;BAR"));
        }

        [Test]
        public void HasDefine_NullEmptyOrOnlySeparators_IsFalse()
        {
            Assert.IsFalse(QuickActionsDefineGate.HasDefine(null));
            Assert.IsFalse(QuickActionsDefineGate.HasDefine(""));
            Assert.IsFalse(QuickActionsDefineGate.HasDefine(";;"));
        }

        [TearDown]
        public void ClearDefines()
        {
            // The stub keeps defines for the whole run; the stale-build tests need none.
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, string.Empty);
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.iOS, string.Empty);
        }

        [Test]
        public void AndroidStripper_StaleDefineOnAssembly_FailsTheBuildWithTheSharedMessage()
        {
            var e = Assert.Throws<BuildFailedException>(RunAndroidStripper);

            StringAssert.StartsWith(StaleMessageStart, e.Message);
            Assert.AreEqual(QuickActionsDefineGate.StaleAssembliesMessage, e.Message);
        }

        [Test]
        public void IosCleanup_StaleDefineOnAssembly_FailsTheBuildWithTheSharedMessage()
        {
            var e = Assert.Throws<BuildFailedException>(RunIosCleanup);

            StringAssert.StartsWith(StaleMessageStart, e.Message);
            Assert.AreEqual(QuickActionsDefineGate.StaleAssembliesMessage, e.Message);
        }

        // Each cleanup reads its own target: the define set for one platform only
        // passes that platform's check and still fails the other's.
        [Test]
        public void DefineVisibleForAndroidOnly_AndroidStripperPasses_IosCleanupFails()
        {
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, "FOO;" + Define);

            Assert.DoesNotThrow(RunAndroidStripper);
            Assert.Throws<BuildFailedException>(RunIosCleanup);
        }

        [Test]
        public void DefineVisibleForIosOnly_IosCleanupPasses_AndroidStripperFails()
        {
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.iOS, Define);

            Assert.DoesNotThrow(RunIosCleanup);
            Assert.Throws<BuildFailedException>(RunAndroidStripper);
        }

        private static void RunAndroidStripper()
        {
            var project = Path.Combine(Path.GetTempPath(), "qa-gate-" + Guid.NewGuid().ToString("N"));
            new QuickActionsTrampolineStripperAndroid().OnPostGenerateGradleAndroidProject(project);
        }

        private static void RunIosCleanup()
        {
            var report = new BuildReport
            {
                summary = new BuildSummary
                {
                    platform = BuildTarget.iOS,
                    outputPath = Path.Combine(Path.GetTempPath(), "qa-gate-" + Guid.NewGuid().ToString("N")),
                },
            };
            new QuickActionsGateCleanupiOS().OnPostprocessBuild(report);
        }
    }
}
