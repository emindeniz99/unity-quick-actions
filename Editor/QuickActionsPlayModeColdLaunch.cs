using UnityEditor;
using UnityEngine;

namespace Playground.QuickActions.Editor
{
    /// <summary>
    /// Backs the Simulator's "cold launch" behaviour: when you click a shortcut
    /// while NOT in Play Mode, it enters Play Mode and delivers the tap at startup —
    /// like tapping the app icon while the app is closed on a real device. The
    /// pending id is kept in <see cref="SessionState"/> so it survives the domain
    /// reload that entering Play Mode triggers. Editor-only.
    /// </summary>
    [InitializeOnLoad]
    internal static class QuickActionsPlayModeColdLaunch
    {
        private const string PendingKey = "QuickActions.PendingColdLaunch";

        static QuickActionsPlayModeColdLaunch()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        /// <summary>Remember the id and enter Play Mode; delivered once playing.</summary>
        public static void RequestColdLaunch(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;
            SessionState.SetString(PendingKey, id);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode)
                return;
            var id = SessionState.GetString(PendingKey, "");
            if (string.IsNullOrEmpty(id))
                return;
            SessionState.EraseString(PendingKey);

            // Wait one frame so the first scene's Awake/OnEnable/Start have run and
            // subscribed to Performed — mirroring the real cold-launch delivery,
            // which the runtime dispatches one frame after startup.
            var startFrame = Time.frameCount;
            void Tick()
            {
                if (Time.frameCount <= startFrame)
                    return;
                EditorApplication.update -= Tick;
                QuickActions.EditorSimulateTap(id);
                Debug.Log($"[QuickActions] Simulated COLD LAUNCH → Performed('{id}')");
            }
            EditorApplication.update += Tick;
        }
    }
}
