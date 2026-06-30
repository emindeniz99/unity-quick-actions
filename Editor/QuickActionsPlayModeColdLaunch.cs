using UnityEditor;

namespace Playground.QuickActions.Editor
{
    /// <summary>
    /// Backs the Simulator's "cold launch": clicking a shortcut while NOT in Play
    /// Mode stashes the id and enters Play Mode. The runtime then seeds it into its
    /// pending queue before the first scene loads (QuickActions.EditorSeedColdLaunch)
    /// and its normal one-frame drain delivers it — so the app behaves exactly as if
    /// it was launched by tapping that shortcut while closed, through the real
    /// pipeline. The id is kept in <see cref="SessionState"/> so it survives the
    /// domain reload that entering Play Mode triggers. Editor-only.
    /// </summary>
    internal static class QuickActionsPlayModeColdLaunch
    {
        // Must match QuickActions.EditorColdLaunchKey (the runtime seeder reads it).
        private const string PendingKey = "QuickActions.PendingColdLaunch";

        /// <summary>Stash the id and enter Play Mode; the runtime delivers it at startup.</summary>
        public static void RequestColdLaunch(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;
            SessionState.SetString(PendingKey, id);
            EditorApplication.EnterPlaymode();
        }
    }
}
