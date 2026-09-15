// Testbed-only evidence for harnesses that live OUTSIDE the process: every id
// that reaches QuickActions.Performed is appended to a file under
// persistentDataPath, which on the iOS Simulator is the app's data container —
// readable from the host through `xcrun simctl get_app_container <udid>
// <bundle-id> data`. The SpringBoard XCUITest (tools~/ios-ui) reads that file
// after tapping a quick action, so delivery is asserted on a fact, not on a
// log line that may or may not reach the unified log. Not part of the package
// or of the Demo sample. The class compiles with the define on and off, like
// the Demo sample does, so the gate-off footprint diff stays the package's
// own; only the subscription comes and goes with the define.
using System;
using System.IO;
#if QUICKACTIONS_ENABLED
using EminDeniz99.QuickActions;
#endif
using UnityEngine;

namespace QuickActionsTestbed
{
    internal static class QuickActionsPerformedMarker
    {
        private const string FileName = "quickactions-performed.log";

        // BeforeSceneLoad is early enough: the runtime drains the cold-launch
        // queue one frame after its own bootstrap, so this subscriber exists by
        // the time the launch id is dispatched.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
#if QUICKACTIONS_ENABLED
            QuickActions.Performed += Append;
#endif
        }

        private static void Append(string id)
        {
            try
            {
                File.AppendAllText(Path.Combine(Application.persistentDataPath, FileName), id + "\n");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Testbed] performed-marker write failed: " + e.Message);
            }
        }
    }
}
