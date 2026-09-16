// Testbed-only: publishes ONE shortcut through QuickActions.Add at startup, when
// the harness asks for it. The SpringBoard XCUITest (tools~/ios-ui) can only tap
// what the OS already shows, and the menu it taps today holds nothing but the
// three Info.plist statics the settings asset bakes in — so a runtime-added item
// has never been long-pressed on iOS. It cannot tap the demo's own "Add"
// button either: the demo draws with IMGUI, which puts no accessibility element
// on screen for XCUITest to find. This is the Android autotest hook's iOS
// counterpart (Samples~/Demo reads an intent extra there), kept in the testbed
// rather than in the sample because only CI ever asks for it.
//
// Asked for two ways, because which one survives IL2CPP on iOS is exactly what
// the first run finds out:
//
//   SIMCTL_CHILD_QA_SEED_RUNTIME=1 xcrun simctl launch <udid> <app> -qa-seed-runtime
//
// simctl passes SIMCTL_CHILD_* into the app's environment with the prefix
// stripped, and everything after the bundle id as argv. The seed file records
// WHICH of the two the player actually saw, so the answer lands in the artifact
// instead of being guessed at.
//
// One item, not three: iOS shows at most four quick actions and the statics take
// the first three, so a second seeded item would simply never appear.
//
// Not part of the package or of the Demo sample. Like the performed marker next
// to it, the class compiles with the define on and off — only the QuickActions
// calls come and go — so the gate-off footprint diff stays the package's own.
using System;
using System.IO;
#if QUICKACTIONS_ENABLED
using EminDeniz99.QuickActions;
#endif
using UnityEngine;

namespace QuickActionsTestbed
{
    internal static class QuickActionsRuntimeSeeder
    {
        /// <summary>Id, title and subtitle the XCUITest is configured with — keep
        /// them in sync with `ios-springboard` in .github/workflows/unity-ci.yml.
        /// The title shares no prefix with any static ("New Game", "Continue",
        /// "Daily Reward") so the test's label fallback cannot match the wrong
        /// row.</summary>
        private const string Id = "runtime_add";
        private const string Title = "Runtime Add";
        private const string Subtitle = "Added by QuickActions.Add";

        private const string EnvVar = "QA_SEED_RUNTIME";
        private const string Argument = "-qa-seed-runtime";
        private const string FileName = "quickactions-seeded.log";

        // AfterSceneLoad, unlike the marker's BeforeSceneLoad: this one CALLS the
        // package rather than subscribing to it, and the cold-launch id is
        // dispatched one frame after bootstrap — seeding after the scene is up
        // keeps the two off the same frame.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Seed()
        {
            var how = AskedFor();
            if (how == null)
                return;
#if QUICKACTIONS_ENABLED
            var added = QuickActions.Add(new QuickActionItem(Id, Title, Subtitle, IconType.Add));
            Write((added ? "added " : "refused ") + Id + " (asked via " + how + ")");
#else
            Write("skipped " + Id + " (asked via " + how + "): QUICKACTIONS_ENABLED is off");
#endif
        }

        /// <summary>"env", "argv", "env+argv" — or null when nothing asked. Both
        /// lookups can throw on a stripped player, and a testbed that dies here
        /// would take every other assertion of the run with it.</summary>
        private static string AskedFor()
        {
            var env = false;
            try
            {
                var value = Environment.GetEnvironmentVariable(EnvVar);
                env = !string.IsNullOrEmpty(value) && value != "0";
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Testbed] " + EnvVar + " not readable: " + e.Message);
            }

            var argv = false;
            try
            {
                foreach (var arg in Environment.GetCommandLineArgs())
                {
                    if (arg != Argument) continue;
                    argv = true;
                    break;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Testbed] command line not readable: " + e.Message);
            }

            if (env && argv) return "env+argv";
            if (env) return "env";
            return argv ? "argv" : null;
        }

        // Next to the performed marker, in the same container the host reads
        // through `xcrun simctl get_app_container <udid> <bundle-id> data`:
        // finding this file is how CI knows the seed landed before it starts the
        // long press. Overwritten, not appended — only the last launch matters.
        private static void Write(string line)
        {
            Debug.Log("[Testbed] seed: " + line);
            try
            {
                File.WriteAllText(Path.Combine(Application.persistentDataPath, FileName), line + "\n");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Testbed] seed-marker write failed: " + e.Message);
            }
        }
    }
}
