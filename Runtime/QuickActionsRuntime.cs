using System.Collections;
using UnityEngine;

namespace EminDeniz99.QuickActions
{
    /// <summary>
    /// Hidden, auto-created singleton that funnels native "a quick action was
    /// performed" notifications into <see cref="QuickActions.Performed"/> through a
    /// single pull channel. Delivery paths:
    ///   * Cold launch (iOS + Android) → drained one frame after startup, so user
    ///     scripts have had their Awake/OnEnable/Start to subscribe first.
    ///   * Warm resume (iOS + Android) → drained in <see cref="OnApplicationFocus"/>
    ///     when the app returns to the foreground.
    ///   * Safety net → a slow poll, for activity implementations that emit
    ///     neither focus nor unpause into scripting on a trampoline round-trip
    ///     (observed on Unity 6's GameActivity).
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class QuickActionsRuntime : MonoBehaviour
    {
        private const string GameObjectName = "QuickActionsRuntime";

        private static QuickActionsRuntime _instance;

        // Set after the one-frame cold-launch dispatch. Gating focus/pause on it
        // prevents draining the cold-launch id before user scripts have subscribed
        // (the initial focus fires during startup, before Awake/Start run). The
        // coroutine always completes on this DontDestroyOnLoad object, so this is
        // not a delivery single-point-of-failure in practice.
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
            // Safety net for the warm path. The two callbacks below are how a
            // resume normally drains the queue, but they are not universal:
            // Unity 6's GameActivity completed a trampoline round-trip on the
            // API 35 emulator with the native pause/resume pair collapsed a
            // millisecond apart and NEITHER C# callback firing, stranding the
            // tapped id in the queue until the next real focus change. A slow
            // beat guarantees delivery no matter which lifecycle events the
            // activity implementation emits; when the queue is empty each tick
            // is a single cheap native read.
            var beat = new WaitForSecondsRealtime(0.25f);
            while (true)
            {
                yield return beat;
                PollPending();
            }
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
