using System.Collections;
using Playground.QuickActions.Internal;
using UnityEngine;

namespace Playground.QuickActions
{
    /// <summary>
    /// Hidden, auto-created singleton that funnels native "a quick action was
    /// performed" notifications into <see cref="QuickActions.Performed"/>.
    ///
    /// Its GameObject name — "QuickActionsRuntime" — is the <c>UnitySendMessage</c>
    /// target hard-coded in the iOS native layer, so do not rename it. Delivery
    /// paths:
    ///   * Cold launch (iOS + Android) → polled one frame after startup, so user
    ///     scripts have had their Awake/OnEnable/Start to subscribe first.
    ///   * Android warm resume (via the trampoline activity) → polled in
    ///     <see cref="OnApplicationFocus"/>.
    ///   * iOS warm resume → native <c>UnitySendMessage</c> → <see cref="OnPerformed"/>.
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class QuickActionsRuntime : MonoBehaviour
    {
        private const string GameObjectName = "QuickActionsRuntime";

        private static QuickActionsRuntime _instance;

        // Becomes true after the deferred cold-launch dispatch, after which focus
        // changes are treated as warm resumes.
        private bool _ready;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null)
                return;

            var go = new GameObject(GameObjectName);
            _instance = go.AddComponent<QuickActionsRuntime>();
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
        }

        private void Awake() => StartCoroutine(DispatchColdLaunch());

        private IEnumerator DispatchColdLaunch()
        {
            // Wait one frame so the initial scene's Awake/OnEnable/Start have run
            // and any subscriber to QuickActions.Performed exists; otherwise the
            // cold-launch event is raised before anyone is listening.
            yield return null;
            _ready = true;
            PollPending();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // Android warm-resume arrives via the trampoline activity, which stores
            // the id and brings Unity to the front — no UnitySendMessage. Ignore the
            // initial focus (the cold launch is handled by DispatchColdLaunch).
            if (hasFocus && _ready)
                PollPending();
        }

        /// <summary>iOS warm-resume sink. Keep this exact public signature.</summary>
        public void OnPerformed(string actionId) => QuickActions.Dispatch(actionId);

        private static void PollPending()
        {
            // Drain the native queue: a cold launch (and Android warm resume) may
            // have more than one id buffered before scripting was ready.
            var bridge = QuickActionsBridgeFactory.Create();
            string id;
            while (!string.IsNullOrEmpty(id = bridge.ConsumePendingPerformed()))
                QuickActions.Dispatch(id);
        }
    }
}
