using System;
using System.Collections.Generic;
using System.Linq;
using Playground.QuickActions.Internal;
using UnityEngine;

namespace Playground.QuickActions
{
    /// <summary>
    /// Public entry point for home-screen quick actions on iOS and Android — the
    /// shortcuts revealed by long-pressing the app icon.
    ///
    /// Shortcuts are created at runtime with <see cref="Add"/> / <see cref="AddList"/>;
    /// the OS keeps them across launches until you change them. When the user taps
    /// one, <see cref="Performed"/> fires with its <see cref="QuickActionItem.Id"/>,
    /// and <see cref="LastPerformed"/> holds the id the app was last launched from
    /// (poll it at startup for cold launches).
    ///
    /// This is a process-wide singleton (one shortcut set per app). Call its API
    /// from the <b>main thread</b> only (it is not internally synchronized). On
    /// first access the in-memory list is reconciled with the <b>dynamic</b>
    /// shortcuts the OS already has (from a previous session), so
    /// <see cref="GetAll"/> / <see cref="IsAdded"/> stay accurate across launches;
    /// icons aren't recoverable, so reconciled items report <see cref="IconType.None"/>.
    /// Static (build-time) shortcuts are <i>not</i> reconciled — don't reuse a
    /// static shortcut's id at runtime. The first <see cref="Add"/> therefore
    /// merges with the reconciled set; call <see cref="RemoveAll"/> first to start
    /// from a clean slate.
    /// </summary>
    public static class QuickActions
    {
        private static IQuickActionsBridge _bridge;
        private static readonly List<QuickActionItem> _items = new List<QuickActionItem>();
        private static bool _loaded;
        private static bool _loading;

        private static IQuickActionsBridge Bridge => _bridge ??= QuickActionsBridgeFactory.Create();

        /// <summary>
        /// On first list access, seed the in-memory set from the <b>dynamic</b>
        /// shortcuts the OS already has (set in a previous session), so
        /// <see cref="GetAll"/> / <see cref="IsAdded"/> are accurate after a cold
        /// start. Static (build-time) shortcuts are not surfaced here. Marks loaded
        /// only on success, and guards against re-entrancy from the bridge call.
        /// </summary>
        private static void EnsureLoaded()
        {
            if (_loaded || _loading)
                return;
            _loading = true;
            try
            {
                var existing = Bridge.GetShortcuts();
                if (existing != null && existing.Count > 0)
                {
                    _items.Clear();
                    var seen = new HashSet<string>();
                    foreach (var item in existing)
                    {
                        // Trust nothing from the native payload: drop invalid/duplicate ids.
                        if (item != null && item.IsValid && seen.Add(item.Id))
                            _items.Add(item);
                    }
                }
                _loaded = true;
            }
            finally
            {
                _loading = false;
            }
        }

        /// <summary>When true, the API logs its operations through <c>Debug.Log</c>.</summary>
        public static bool LoggingEnable { get; set; }

        /// <summary>True on a device that supports quick actions; false in-Editor.</summary>
        public static bool IsPlatformSupported => Bridge.IsPlatformSupported;

        /// <summary>
        /// Id of the quick action the app was most recently launched or resumed
        /// from, for this session; null otherwise.
        ///
        /// This is a <b>pull-based alternative</b> to <see cref="Performed"/> for
        /// code that does not subscribe — do not route on both for the same tap
        /// (cold launch <i>or</i> warm resume), or that tap is handled twice (every
        /// tap also raises <see cref="Performed"/>).
        /// </summary>
        public static string LastPerformed
        {
            get
            {
#if UNITY_EDITOR
                // The in-Editor Simulator records simulated taps here (there is no
                // native bridge in the Editor), so LastPerformed is realistic too.
                if (_editorSimulatedLastPerformed != null)
                    return _editorSimulatedLastPerformed;
#endif
                return Bridge.GetLastPerformed();
            }
        }

        /// <summary>
        /// Raised on the main thread with the tapped action's
        /// <see cref="QuickActionItem.Id"/> whenever a quick action is performed —
        /// including the cold launch that started the app. Subscribe in
        /// <c>Awake</c>/<c>OnEnable</c> so the cold-launch event is not missed.
        /// This is the recommended channel; prefer it over polling
        /// <see cref="LastPerformed"/>.
        /// </summary>
        public static event Action<string> Performed;

        /// <summary>Clear the persisted <see cref="LastPerformed"/> id.</summary>
        public static void ResetLastPerformed()
        {
#if UNITY_EDITOR
            _editorSimulatedLastPerformed = null;
#endif
            Bridge.ResetLastPerformed();
        }

#if UNITY_EDITOR
        // Everything below is Editor-only (#if UNITY_EDITOR) — never compiled into a
        // player build. It lets the Simulator reproduce a real device tap in-Editor.

        // Set so LastPerformed reflects a simulated tap (no native bridge in-Editor).
        internal static string _editorSimulatedLastPerformed;

        // Cold-launch queue: drained by the real ConsumeNextPending path above, so a
        // simulated cold launch goes through QuickActionsRuntime exactly like the
        // native pending queue does on a device.
        private static readonly Queue<string> _editorPending = new Queue<string>();

        // SessionState key shared with the Editor Simulator's RequestColdLaunch — it
        // survives the domain reload that entering Play Mode triggers.
        private const string EditorColdLaunchKey = "QuickActions.PendingColdLaunch";

        /// <summary>
        /// Editor Simulator entry point for a <b>warm</b> tap (app already running):
        /// record <paramref name="id"/> as last-performed and raise <see cref="Performed"/>
        /// immediately — the same observable result as a real native tap.
        /// </summary>
        internal static void EditorSimulateTap(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;
            _editorSimulatedLastPerformed = id;
            Dispatch(id);
        }

        /// <summary>
        /// Runs before the first scene loads when entering Play Mode. If the Simulator
        /// requested a cold launch (id stashed in <see cref="SessionState"/> before the
        /// domain reload), seed it into the pending queue so the normal one-frame
        /// <see cref="QuickActionsRuntime"/> drain delivers it — i.e. the app behaves as
        /// if it was launched by tapping that shortcut while closed.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EditorSeedColdLaunch()
        {
            var id = UnityEditor.SessionState.GetString(EditorColdLaunchKey, "");
            if (string.IsNullOrEmpty(id))
                return;
            UnityEditor.SessionState.EraseString(EditorColdLaunchKey);
            _editorPending.Enqueue(id);
            _editorSimulatedLastPerformed = id; // launched-from id, like the device
        }
#endif

        /// <summary>
        /// Add one quick action. Returns false (without changing anything) when the
        /// item is invalid or an action with the same <see cref="QuickActionItem.Id"/>
        /// is already added; returns true on success.
        /// </summary>
        /// <exception cref="ArgumentNullException">The item is null.</exception>
        public static bool Add(QuickActionItem item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            EnsureLoaded();

            if (!item.IsValid)
            {
                Log($"Add ignored: item needs a non-empty Id and Title ({item}).");
                return false;
            }

            if (IsAdded(item.Id))
            {
                Log($"Add ignored: a quick action with Id '{item.Id}' already exists.");
                return false;
            }

            _items.Add(item);
            Push();
            Log($"Added quick action '{item.Id}'.");
            return true;
        }

        /// <summary>
        /// Add several quick actions in one OS update. Invalid items and ids that
        /// already exist are skipped.
        /// </summary>
        public static void AddList(IList<QuickActionItem> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));
            EnsureLoaded();

            var changed = false;
            foreach (var item in items)
            {
                if (item == null || !item.IsValid || IsAdded(item.Id))
                {
                    Log($"AddList skipped an invalid or duplicate item ({item}).");
                    continue;
                }
                _items.Add(item);
                changed = true;
            }

            if (changed)
                Push();
        }

        /// <summary>Snapshot of the currently installed quick actions.</summary>
        public static List<QuickActionItem> GetAll()
        {
            EnsureLoaded();
            return new List<QuickActionItem>(_items);
        }

        /// <summary>The added action with this id, or null.</summary>
        public static QuickActionItem GetById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            EnsureLoaded();
            return _items.FirstOrDefault(a => a.Id == id);
        }

        /// <summary>Remove a quick action. Returns true if one was removed.</summary>
        public static bool Remove(QuickActionItem item) => item != null && RemoveById(item.Id);

        /// <summary>Remove the quick action with this id. Returns true if one was removed.</summary>
        public static bool RemoveById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;
            EnsureLoaded();
            if (_items.RemoveAll(a => a.Id == id) == 0)
                return false;

            Push();
            Log($"Removed quick action '{id}'.");
            return true;
        }

        /// <summary>Remove every quick action.</summary>
        public static void RemoveAll()
        {
            // Clear the OS first; if it throws, leave our in-memory state intact so
            // a later access reconciles with what the OS actually still has.
            Bridge.RemoveAll();
            _items.Clear();
            _loaded = true; // now authoritative — skip any later OS reconcile
            Log("Removed all quick actions.");
        }

        /// <summary>True if an action with this item's id is added.</summary>
        public static bool IsAdded(QuickActionItem item) => item != null && IsAdded(item.Id);

        /// <summary>True if an action with this id is added.</summary>
        public static bool IsAdded(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;
            EnsureLoaded();
            return _items.Any(a => a.Id == id);
        }

        // ---- internal: called by QuickActionsRuntime ----

        internal static void Dispatch(string actionId)
        {
            if (string.IsNullOrEmpty(actionId))
                return;
            Log($"Performed quick action '{actionId}'.");
            Performed?.Invoke(actionId);
        }

        /// <summary>
        /// Pull-and-clear the next queued "performed" id from the shared bridge.
        /// Used by <see cref="QuickActionsRuntime"/> so there is a single bridge
        /// instance (the platform bridges are stateless; native state is global).
        /// </summary>
        internal static string ConsumeNextPending()
        {
#if UNITY_EDITOR
            // In the Editor the Simulator seeds a cold-launch id here so the normal
            // runtime drain delivers it through the real path (see EditorSeedColdLaunch).
            if (_editorPending.Count > 0)
                return _editorPending.Dequeue();
#endif
            return Bridge.ConsumePendingPerformed();
        }

        /// <summary>
        /// Test seam: swap the platform bridge and reset cached state. Passing null
        /// restores the default bridge for the build target.
        /// </summary>
        internal static void OverrideBridgeForTesting(IQuickActionsBridge bridge)
        {
            _bridge = bridge;
            _loaded = false;
            _items.Clear();
        }

        private static void Push() => Bridge.SetShortcuts(_items);

        private static void Log(string message)
        {
            if (LoggingEnable)
                Debug.Log($"[QuickActions] {message}");
        }
    }
}
