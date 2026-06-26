using System.Collections;
using UnityEngine;

namespace Playground.QuickActions
{
    /// <summary>
    /// Hidden, auto-created singleton that funnels native "a quick action was
    /// performed" notifications into <see cref="QuickActions.Performed"/> through a
    /// single pull channel. Delivery paths:
    ///   * Cold launch (iOS + Android) → drained one frame after startup, so user
    ///     scripts have had their Awake/OnEnable/Start to subscribe first.
    ///   * Warm resume (iOS + Android) → drained in <see cref="OnApplicationFocus"/>
    ///     when the app returns to the foreground.
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
            // Warm resume: the OS has stored the tapped id (iOS performAction /
            // Android trampoline) and the app is returning to the foreground.
            // Ignore the initial focus — the cold launch is handled by
            // DispatchColdLaunch.
            if (hasFocus && _ready)
                PollPending();
        }

        private void OnApplicationPause(bool paused)
        {
            // Second, independent resume signal. Some OEMs don't reliably fire
            // OnApplicationFocus when a translucent activity (the Android
            // trampoline) is involved, but OnApplicationPause(false) is guaranteed
            // on resume. Draining is idempotent (the queue is consumed once).
            if (!paused && _ready)
                PollPending();
        }

        private static void PollPending()
        {
            // Drain the native queue: a cold launch (and Android warm resume) may
            // have more than one id buffered before scripting was ready. Goes
            // through the shared QuickActions bridge (single instance).
            string id;
            while (!string.IsNullOrEmpty(id = QuickActions.ConsumeNextPending()))
                QuickActions.Dispatch(id);
        }
    }
}
