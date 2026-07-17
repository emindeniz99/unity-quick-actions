using UnityEditor;
using UnityEngine;

namespace EminDeniz99.QuickActions.Editor
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
    [InitializeOnLoad]
    internal static class QuickActionsPlayModeColdLaunch
    {
        static QuickActionsPlayModeColdLaunch()
        {
            // Disarm a stale request so it can't fire a phantom cold launch on a later,
            // unrelated Play. SessionState lives for the whole editor session, and play
            // entry can silently fail after the key was written (compile error, the
            // user cancelling the save-scene dialog).
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                SessionState.EraseString(QuickActions.EditorColdLaunchKey);
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode)
                    return;
                SessionState.EraseString(QuickActions.EditorColdLaunchKey);
                // With domain reload disabled, statics survive play exit — drop the
                // finished session's Performed subscribers (their MonoBehaviour
                // targets are destroyed) and its in-memory shortcut state so neither
                // leaks into the next session or the Edit-Mode Simulator window.
                QuickActions.EditorClearPerformedSubscribers();
                QuickActions.EditorResetAfterPlaySession();
            };
        }

        /// <summary>Stash the id and enter Play Mode; the runtime delivers it at startup.</summary>
        public static void RequestColdLaunch(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;
            if (EditorUtility.scriptCompilationFailed)
            {
                Debug.LogWarning("[QuickActions] Simulator: fix compile errors first — Play Mode can't start.");
                return;
            }
            SessionState.SetString(QuickActions.EditorColdLaunchKey, id);
            EditorApplication.EnterPlaymode();
            // If entry is refused (e.g. the save-scene dialog is cancelled) no play-mode
            // event ever fires — check afterwards and disarm.
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    SessionState.EraseString(QuickActions.EditorColdLaunchKey);
            };
        }
    }
}
