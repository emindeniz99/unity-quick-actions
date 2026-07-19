using System;
using System.Collections.Generic;
using System.Linq;
using EminDeniz99.QuickActions.Internal;
using UnityEngine;

namespace EminDeniz99.QuickActions
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
    /// icon identity survives the reconcile on both platforms (persisted in the
    /// ownership-marker payload: Android extras, iOS userInfo).
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
        /// <returns>
        /// True when the managed set now reflects the OS (loaded, or already loaded);
        /// false when the current OS shortcuts could not be read (so callers must not
        /// mutate/push against an unknown baseline) or during a re-entrant load.
        /// </returns>
        private static bool EnsureLoaded()
        {
            if (_loaded)
                return true;
            if (_loading)
                return false; // re-entrant call mid-load — not safe to treat as loaded yet
            _loading = true;
            try
            {
                var existing = Bridge.GetShortcuts();
                if (existing == null)
                    return false; // read FAILED — leave _loaded=false so the next access retries,
                                  // rather than caching an errored-empty as authoritative (which
                                  // would let the next write wipe the OS's real shortcuts).

                // A non-null read is authoritative — including a genuinely-empty one, which
                // must clear any un-acknowledged optimistic items from a prior failed write.
                _items.Clear();
                var seen = new HashSet<string>();
                foreach (var item in existing)
                {
                    // Trust nothing from the native payload: drop invalid/duplicate ids.
                    if (item != null && item.IsValid && seen.Add(item.Id))
                        _items.Add(item);
                }
                _loaded = true;
                return true;
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

        // SessionState key shared with the Editor Simulator's RequestColdLaunch (which
        // references this constant via InternalsVisibleTo, so the two sides can't
        // drift apart) — it survives the domain reload that entering Play Mode triggers.
        internal const string EditorColdLaunchKey = "QuickActions.PendingColdLaunch";

        /// <summary>
        /// Mirrors a real process restart when Enter Play Mode Options disable domain
        /// reload: in the Editor, statics survive play sessions, but on a device every
        /// launch starts clean — without this, a simulated tap's LastPerformed and the
        /// in-memory list would leak into the next play session. SubsystemRegistration
        /// runs before BeforeSceneLoad, so this cannot race the cold-launch seeder.
        /// Note: Performed subscribers are deliberately NOT wiped here — a wipe in an
        /// entry phase could race a legitimate same-phase user subscription (order
        /// within a RuntimeInitialize phase is undefined). Stale subscribers from the
        /// previous session are instead cleared on play EXIT (see
        /// <see cref="EditorClearPerformedSubscribers"/>).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void EditorResetForPlaySession()
        {
            _editorSimulatedLastPerformed = null;
            _editorPending.Clear();
            _items.Clear();
            _loaded = false;
            _loading = false;
            _bridge = null;
            LoggingEnable = false;
        }

        /// <summary>
        /// Called by the Editor assembly when Play Mode exits (EnteredEditMode), so
        /// that — with domain reload disabled — subscribers targeting the finished
        /// session's (now destroyed) MonoBehaviours can't leak into the next session.
        /// Safe at exit: nothing legitimate subscribes between sessions.
        /// </summary>
        internal static void EditorClearPerformedSubscribers() => Performed = null;

        /// <summary>
        /// Called on Play Mode EXIT to drop the finished session's <b>tap signals</b>
        /// (simulated last-performed + pending queue) so they can't replay into the
        /// next session or the Edit-Mode Simulator with domain reload disabled.
        /// The shortcut <b>list</b> is deliberately kept: on a real device dynamic
        /// shortcuts persist after the app quits, and keeping <c>_items</c> mirrors
        /// that — the Simulator can list the stopped session's runtime shortcuts and
        /// cold-launch one from Edit Mode, exactly like tapping a persisted shortcut
        /// on a closed app.
        /// </summary>
        internal static void EditorResetAfterPlaySession()
        {
            _editorSimulatedLastPerformed = null;
            _editorPending.Clear();
        }

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
        /// item is invalid, an action with the same <see cref="QuickActionItem.Id"/>
        /// is already added, the current OS shortcuts could not be read (so the
        /// package won't risk overwriting them from an unknown baseline — retry later),
        /// or the OS rejected the write (e.g. rate-limited while backgrounded on
        /// Android — also retry later); returns true on success.
        /// </summary>
        /// <remarks>
        /// The OS limits how many dynamic shortcuts it keeps (iOS shows ~4; Android
        /// caps at least 5, shared with any static shortcuts AND any dynamic
        /// shortcuts the host app itself published outside this API). On Android,
        /// surplus beyond the cap is dropped by the OS and immediately removed from
        /// the managed list too, so <see cref="GetAll"/>/<see cref="IsAdded"/> reflect
        /// what the device actually kept (they do not over-report). <c>Add</c> still
        /// returns true for a cap trim — the item was accepted into the set before
        /// the OS trim — but a subsequent <see cref="GetAll"/>/<see cref="IsAdded"/>
        /// shows the trim (a rejected <i>write</i> is different: that returns false).
        /// iOS keeps the full array (no cap). See the README "Known limits".
        /// </remarks>
        /// <exception cref="ArgumentNullException">The item is null.</exception>
        public static bool Add(QuickActionItem item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            if (!EnsureLoaded())
            {
                // Couldn't read the current OS set — don't push a partial set built on an
                // unknown baseline (that could silently wipe the user's existing shortcuts).
                Log("Add deferred: could not read the current shortcuts; OS set left unchanged.");
                return false;
            }

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

            var copy = item.Copy(); // store a copy so a later caller mutation can't diverge our state
            _items.Add(copy);
            if (!Push())
            {
                // The OS rejected the write (e.g. rate-limited in the background). Roll
                // back the optimistic add so queries match the device, force a
                // reconcile (the push may have PARTIALLY landed — see Push), and
                // report the failure so the caller can retry — mirroring the
                // failed-read contract.
                _items.Remove(copy);
                _loaded = false;
                Log($"Add failed: the OS did not accept the update for '{item.Id}'; retry later.");
                return false;
            }
            if (!_items.Contains(copy))
            {
                // The write landed, but the OS dropped THIS id: the bridge filtered it
                // out to fit the shared cap or because another publisher already owns
                // that dynamic/pinned id (Push pruned it from _items to stay honest).
                // GetAll()/IsAdded() now report it absent, so the add did not take —
                // report failure rather than a success the caller can't observe.
                Log($"Add failed: the OS dropped '{item.Id}' (cap reached or id owned by another publisher).");
                return false;
            }
            Log($"Added quick action '{item.Id}'.");
            return true;
        }

        /// <summary>
        /// Add several quick actions in one OS update. Invalid items and ids that
        /// already exist are skipped. If the current OS shortcuts can't be read, or
        /// the OS rejects the write, nothing is added (retry later).
        /// </summary>
        public static void AddList(IList<QuickActionItem> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));
            if (!EnsureLoaded())
            {
                Log("AddList deferred: could not read the current shortcuts; OS set left unchanged.");
                return;
            }

            var added = new List<QuickActionItem>();
            foreach (var item in items)
            {
                if (item == null || !item.IsValid || IsAdded(item.Id))
                {
                    Log($"AddList skipped an invalid or duplicate item ({item}).");
                    continue;
                }
                var copy = item.Copy(); // store a copy — see Add
                _items.Add(copy);
                added.Add(copy);
            }

            if (added.Count > 0 && !Push())
            {
                // Failed OS write — roll back every optimistic add and force a
                // reconcile (see Add).
                foreach (var copy in added)
                    _items.Remove(copy);
                _loaded = false;
                Log("AddList failed: the OS did not accept the update; nothing was added — retry later.");
                return;
            }
            // The write landed but the OS may have dropped some ids (shared cap, or an
            // id another publisher owns); Push already pruned them from _items. Surface
            // which ones did not take so a caller isn't left believing all were added.
            foreach (var copy in added)
                if (!_items.Contains(copy))
                    Log($"AddList: the OS dropped '{copy.Id}' (cap reached or id owned by another publisher).");
        }

        /// <summary>Snapshot of the currently installed quick actions.</summary>
        public static List<QuickActionItem> GetAll()
        {
            EnsureLoaded();
            // Return copies so a caller mutating the results can't change internal state.
            return _items.ConvertAll(a => a.Copy());
        }

        /// <summary>The added action with this id, or null.</summary>
        public static QuickActionItem GetById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            EnsureLoaded();
            return _items.FirstOrDefault(a => a.Id == id)?.Copy();
        }

        /// <summary>Remove a quick action. Returns true if one was removed.</summary>
        public static bool Remove(QuickActionItem item) => item != null && RemoveById(item.Id);

        /// <summary>
        /// Remove the quick action with this id. Returns true if one was removed;
        /// false when there is no such id, the current OS shortcuts could not be
        /// read, or the OS rejected the update (the action is kept — retry later).
        /// </summary>
        public static bool RemoveById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;
            if (!EnsureLoaded())
            {
                // Unknown baseline — removing + pushing could drop other live shortcuts.
                Log("RemoveById deferred: could not read the current shortcuts.");
                return false;
            }
            var index = _items.FindIndex(a => a.Id == id);
            if (index < 0)
                return false;

            var removed = _items[index];
            _items.RemoveAt(index);
            if (!Push())
            {
                // Failed OS write — the device may still show the item, so put it
                // back at its original position, force a reconcile, and report the
                // failure (see Add — same partial-landing contract).
                _items.Insert(index, removed);
                _loaded = false;
                Log($"RemoveById failed: the OS did not accept the update for '{id}'; retry later.");
                return false;
            }
            Log($"Removed quick action '{id}'.");
            return true;
        }

        /// <summary>
        /// Remove every quick action added through this API. A host app's own OS
        /// shortcuts (published outside this package) are untouched on both platforms.
        /// </summary>
        public static void RemoveAll()
        {
            // Clear the OS first; only drop our in-memory state if the removal actually
            // landed. If the native remove fails (throws, or reports false on a locked
            // profile), keep the list so we don't falsely mark ourselves empty while the
            // OS still shows shortcuts — a later access reconciles with the real state.
            if (!Bridge.RemoveAll())
            {
                Log("RemoveAll: the OS removal did not succeed; keeping the in-memory list.");
                return;
            }
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
#if UNITY_EDITOR
            // Also drop simulated-tap state, or a Simulator click before an edit-mode
            // test run would shadow the fake bridge's LastPerformed (flaky tests).
            _editorSimulatedLastPerformed = null;
            _editorPending.Clear();
#endif
        }

        private static bool Push()
        {
            var accepted = Bridge.SetShortcuts(_items);
            if (accepted == null)
            {
                // The OS write did not FULLY land (rejected/rate-limited/errored).
                // Report the failure so the caller rolls back its optimistic mutation
                // AND forces a reconcile: even an Add's push can have partially
                // applied on Android — the bridge may have filtered an EXISTING
                // managed id out of this push (a newly appeared manifest/pinned
                // collision, a budget shrunk by host shortcuts) and its stale-removal
                // phase runs before the blocked add, so an old id may already be gone
                // from the device. Icon identity survives the reconcile via the
                // Android marker extras. Don't prune here — a stale read is not
                // authoritative.
                return false;
            }
            // Same reference = accept-all (Null/iOS) — nothing was trimmed, nothing to prune.
            if (ReferenceEquals(accepted, _items))
                return true;

            // The OS trimmed some ids to fit its cap. Prune the surplus from the
            // authoritative list in place, keeping the surviving original objects
            // (icons intact) and their insertion order, so GetAll()/IsAdded() no
            // longer over-report shortcuts the OS never accepted.
            var keep = new HashSet<string>();
            foreach (var it in accepted)
                if (it != null) keep.Add(it.Id);
            _items.RemoveAll(it => !keep.Contains(it.Id));
            return true;
        }

        private static void Log(string message)
        {
            if (LoggingEnable)
                Debug.Log($"[QuickActions] {message}");
        }
    }
}
