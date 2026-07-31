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
    /// ownership-marker payload: Android extras, iOS userInfo). That same payload
    /// carries each item's base text and per-locale tables, so a device-language
    /// change between launches re-renders the shortcuts on the next reconcile —
    /// see <see cref="Locale"/>.
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
        // Armed when a localization-refresh push was REFUSED by the OS. The load
        // itself succeeded, so the managed set stays authoritative — only the labels
        // the device renders are stale — and the next list access spends exactly ONE
        // more push trying to fix them. It is spent whether or not that retry is
        // accepted, and nothing re-arms it but another locale change: from then on the
        // labels are corrected by the app's next successful Add/Update/Remove, whose
        // push renders the current locale anyway. Without the latch every later read
        // re-detected the same staleness and issued another OS write, turning
        // GetAll/GetById/IsAdded into unbounded writers.
        private static bool _refreshRetryArmed;

        private static IQuickActionsBridge Bridge => _bridge ??= QuickActionsBridgeFactory.Create();

        /// <summary>
        /// On first list access, seed the in-memory set from the <b>dynamic</b>
        /// shortcuts the OS already has (set in a previous session), so
        /// <see cref="GetAll"/> / <see cref="IsAdded"/> are accurate after a cold
        /// start. Static (build-time) shortcuts are not surfaced here. Marks loaded
        /// only on success, and guards against re-entrancy from the bridge call.
        ///
        /// This is also where a language change that happened while the app was NOT
        /// running is caught: each restored item's base text and per-locale tables
        /// come back with it, so a set still rendered in the previous locale is
        /// re-pushed exactly once (see <see cref="Locale"/>) — and, if the OS refuses
        /// that push, exactly once more on the next access and then no longer.
        /// </summary>
        /// <returns>
        /// True when the managed set now reflects the OS (loaded, or already loaded) —
        /// including when the localization refresh push failed, since that changes only
        /// the rendered language, not which ids are installed; false when the current OS
        /// shortcuts could not be read (so callers must not mutate/push against an
        /// unknown baseline) or during a re-entrant load. A true answer therefore always
        /// means <c>_loaded</c> is true and <c>_items</c> is the authoritative set —
        /// <see cref="AddList"/> and <see cref="IsAdded"/> rely on that: a re-entrant
        /// reload mid-<c>AddList</c> would clear the optimistic copies appended so far
        /// and silently drop them from the push.
        /// </returns>
        private static bool EnsureLoaded()
        {
            if (_loaded)
            {
                RetryLocalizationRefresh();
                return true;
            }
            if (_loading)
                return false; // re-entrant call mid-load — not safe to treat as loaded yet
            _loading = true;
            var stale = 0;
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
                    if (item == null || !item.IsValid || !seen.Add(item.Id))
                        continue;
                    // What came back is what the device SHOWS — the text resolved at
                    // the last push. Restore the base text and the per-locale tables
                    // from the payload's blob, and count the items whose shown text no
                    // longer matches the current locale.
                    if (QuickActionLocalization.Restore(item, Locale))
                        stale++;
                    _items.Add(item);
                }
                _loaded = true;
            }
            finally
            {
                _loading = false;
            }

            if (stale > 0)
            {
                // The device language (or Locale) changed while the app was not
                // running, so the launcher still renders the old language. ONE push
                // re-renders it — the feature's core promise. Loop-safe: this runs
                // only on a load that actually found a mismatch, _loaded is already
                // true so the push can't re-enter the load, and the push writes the
                // current locale's text, so the next load finds nothing stale.
                Log($"Localization refresh: {stale} quick action(s) still rendered in another locale; re-pushing for '{Locale}'.");
                if (!Push())
                {
                    // The OS refused it (rate-limited, locked profile…). The LOAD
                    // succeeded, so _items stays authoritative and _loaded stays true —
                    // only the rendered language is wrong. Arm one retry instead of
                    // re-reading forever (see _refreshRetryArmed).
                    _refreshRetryArmed = true;
                    Log("Localization refresh failed: the OS did not accept the update; the shortcuts still show the previous locale (one retry armed).");
                }
            }
            return true;
        }

        /// <summary>
        /// Spend the single retry a refused localization refresh armed, if any.
        ///
        /// The load already succeeded and <c>_items</c> holds the base text plus both
        /// tables, so the retry is just the push — no second OS read. The latch is
        /// disarmed BEFORE the attempt, so this can fire at most once per arming even
        /// if the OS refuses again: a read-only API must never become an unbounded
        /// stream of OS writes. Nothing is lost by giving up — an accepted push from
        /// any later mutation re-renders the labels anyway, and a
        /// <see cref="Locale"/> change re-arms this.
        /// </summary>
        private static void RetryLocalizationRefresh()
        {
            if (!_refreshRetryArmed)
                return;
            _refreshRetryArmed = false;
            Log($"Localization refresh: retrying the refused re-push for '{Locale}' (once).");
            if (!Push())
                Log("Localization refresh retry failed: the shortcuts keep the previous locale's labels until the next successful update.");
        }

        /// <summary>When true, the API logs its operations through <c>Debug.Log</c>.</summary>
        public static bool LoggingEnable { get; set; }

        // null = "not chosen yet" (the getter then asks the device). An explicit
        // empty string is a caller saying "use the base text only", so the two must
        // stay distinguishable.
        private static string _locale;

        /// <summary>
        /// Locale used to pick each installed item's label from
        /// <see cref="QuickActionItem.LocalizedTitles"/> /
        /// <see cref="QuickActionItem.LocalizedSubtitles"/> — a BCP-47-ish tag
        /// ("fr", "pt-BR"). Defaults to the device language
        /// (<see cref="Application.systemLanguage"/> mapped to an ISO code;
        /// languages outside the mapping answer "en").
        ///
        /// Assign it when the app has its own language picker: a <b>different</b>
        /// value re-pushes the installed shortcuts immediately, so their labels
        /// change with the rest of the UI (assigning the same value — case
        /// differences included, since resolution ignores case — does nothing).
        /// This holds for the first assignment of a session too: the setter
        /// reconciles with the OS first, so shortcuts a previous session installed
        /// re-render even when nothing else has touched the list yet. Items keep
        /// their base text and tables, so switching languages back and forth always
        /// resolves from the author's original strings. With nothing installed the
        /// assignment costs one native read and no write: the next push uses it.
        /// </summary>
        public static string Locale
        {
            get => _locale ??= QuickActionLocalization.FromSystemLanguage(Application.systemLanguage);
            set
            {
                var next = value ?? string.Empty;
                if (string.Equals(Locale, next, StringComparison.OrdinalIgnoreCase))
                    return; // no observable difference — don't spend an OS write on it
                _locale = next;
                // Reconcile BEFORE deciding. On a cold start _items is empty because
                // nothing has loaded yet, not because nothing is installed — treating
                // those as the same thing is what made the FIRST assignment of a
                // session (the in-app language-picker path) a silent no-op, leaving
                // the launcher in the previous language until some other API call
                // happened to load. The new locale is already in place above, so the
                // load's own staleness check compares the device against it and
                // re-renders what disagrees; nothing is left for this setter to push.
                var alreadyLoaded = _loaded;
                if (!EnsureLoaded())
                {
                    // Unknown baseline — pushing now could wipe shortcuts we never saw
                    // (same rule as Add/RemoveById). The locale still took effect for
                    // the next successful push.
                    Log($"Locale set to '{next}', but the current shortcuts could not be read; the labels re-render on the next successful update.");
                    return;
                }
                if (!alreadyLoaded || _items.Count == 0)
                    return; // the reconcile above already re-rendered whatever needed it
                Log($"Locale set to '{next}'; re-pushing {_items.Count} quick action(s).");
                if (!Push())
                {
                    // The OS refused the re-render (rate-limited, locked profile…):
                    // the device still shows the previous locale's labels. Only the
                    // rendered language is wrong — _items is still what the OS holds —
                    // so arm the same single retry a refused reconcile refresh arms,
                    // rather than dropping _loaded and making every later read a write.
                    _refreshRetryArmed = true;
                    Log("Locale change failed: the OS did not accept the update; the shortcuts still show the previous locale (one retry armed).");
                }
            }
        }

        /// <summary>True on a device that supports quick actions; false in-Editor.</summary>
        public static bool IsPlatformSupported => Bridge.IsPlatformSupported;

        /// <summary>
        /// How many shortcuts the OS accepts/shows for this app. Android:
        /// <c>getMaxShortcutCountPerActivity</c>. iOS: 4 (the Home Screen display
        /// limit; there is no OS query — extra items are accepted but not shown).
        /// On <b>both</b> platforms the budget is shared with static (baked)
        /// shortcuts — and on Android also with any dynamic shortcuts the host app
        /// published outside this API — so fewer slots may actually be free for
        /// your dynamic items. 0 in the Editor / on unsupported platforms.
        /// </summary>
        public static int MaxShortcutCount => Bridge.MaxShortcutCount;

        /// <summary>
        /// True when the launcher can pin shortcuts to the home screen
        /// (Android 8.0+ with a compatible launcher). Always false on iOS (no
        /// pinned-shortcut concept) and in the Editor.
        /// </summary>
        public static bool IsPinSupported => Bridge.IsPinSupported;

        /// <summary>
        /// Ask the launcher to pin the added quick action with this id to the home
        /// screen (Android 8.0+). The launcher shows its own confirm UI; the OS
        /// reports no outcome, so true means the request was <b>dispatched</b>, not
        /// that the user accepted it. Returns false when pinning is unsupported
        /// (iOS/Editor), the id is not a currently added action of this package,
        /// or the native request failed.
        /// </summary>
        public static bool RequestPin(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;
            if (!IsAdded(id))
            {
                // Only OUR currently-installed shortcuts are pinnable through this
                // API — the Java side re-checks ownership against the live OS set.
                Log($"RequestPin ignored: no added quick action with Id '{id}'.");
                return false;
            }
            var dispatched = Bridge.RequestPin(id);
            Log(dispatched
                ? $"RequestPin dispatched for '{id}' (user confirms in launcher UI)."
                : $"RequestPin failed for '{id}' (unsupported launcher or native error).");
            return dispatched;
        }

        /// <summary>
        /// Tell the launcher the user just acted on this added quick action's
        /// feature <b>inside the app</b> (Android
        /// <c>ShortcutManager.reportShortcutUsed</c> — improves launcher/assistant
        /// ranking predictions). Call it when the user reaches the same feature
        /// through normal UI, not when a shortcut tap launched it. Returns true
        /// when the signal was sent; false on iOS/Editor (no analog), when the id
        /// is not a currently added action of this package (a removed-but-still-
        /// pinned id is refused here too), or when the native call failed.
        /// </summary>
        public static bool ReportUsed(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;
            if (!IsAdded(id))
            {
                // Same ownership gate as RequestPin: only our installed shortcuts.
                Log($"ReportUsed ignored: no added quick action with Id '{id}'.");
                return false;
            }
            var reported = Bridge.ReportUsed(id);
            if (reported)
                Log($"Reported usage of quick action '{id}'.");
            return reported;
        }

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
            _refreshRetryArmed = false;
            _bridge = null;
            LoggingEnable = false;
            _locale = null; // re-read the device language, like a real process start
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
        /// the OS rejected the write (e.g. rate-limited while backgrounded on
        /// Android — also retry later), or the OS dropped this item (the shared cap is
        /// full, or another publisher already owns the id — see remarks); returns true
        /// only when the action actually landed.
        /// </summary>
        /// <remarks>
        /// The OS limits how many dynamic shortcuts it keeps (iOS shows ~4; Android
        /// caps at least 5, shared with any static shortcuts AND any dynamic
        /// shortcuts the host app itself published outside this API). On Android,
        /// surplus beyond the cap is dropped by the OS and immediately removed from
        /// the managed list too, so <see cref="GetAll"/>/<see cref="IsAdded"/> reflect
        /// what the device actually kept (they do not over-report). If the item you
        /// add is the one that doesn't fit — the cap is already full, or another
        /// publisher's dynamic/pinned shortcut owns that id — <c>Add</c> returns
        /// <b>false</b>: the OS didn't install it, so reporting success would be a lie
        /// <see cref="GetAll"/>/<see cref="IsAdded"/> immediately contradict. iOS keeps
        /// the full array (no cap). See the README "Known limits".
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

        /// <summary>
        /// Replace the added quick action with the same <see cref="QuickActionItem.Id"/>
        /// in place — position (and so launcher rank) preserved, one OS update, and
        /// on Android a user-pinned copy is refreshed too (same-id entries update
        /// in place). Returns false when the item is invalid or no action with that
        /// id is added (nothing changes; both deterministic — use <see cref="Add"/>
        /// for the latter), when the current OS shortcuts could not be read or the
        /// OS rejected the write (nothing changes, the previous item is restored —
        /// transient, retry later), or when the OS <b>dropped</b> the updated item
        /// (the shared budget shrank since — see <see cref="Add"/>'s remarks). In
        /// that last case the shortcut is <b>gone</b>: the push already replaced
        /// the previous item and the OS kept neither, so
        /// <see cref="GetAll"/>/<see cref="IsAdded"/> report it absent — re-<see cref="Add"/>
        /// once there is room if you still want it.
        /// </summary>
        /// <exception cref="ArgumentNullException">The item is null.</exception>
        public static bool Update(QuickActionItem item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            if (!EnsureLoaded())
            {
                Log("Update deferred: could not read the current shortcuts; OS set left unchanged.");
                return false;
            }
            if (!item.IsValid)
            {
                Log($"Update ignored: item needs a non-empty Id and Title ({item}).");
                return false;
            }
            var index = _items.FindIndex(a => a.Id == item.Id);
            if (index < 0)
            {
                Log($"Update ignored: no added quick action with Id '{item.Id}' — use Add.");
                return false;
            }

            var previous = _items[index];
            var copy = item.Copy(); // store a copy — see Add
            var idsBeforePush = _items.ConvertAll(a => a.Id);
            _items[index] = copy;
            if (!Push())
            {
                // Failed OS write — restore the previous item at its position and
                // force a reconcile (see Add — same partial-landing contract).
                _items[index] = previous;
                _loaded = false;
                Log($"Update failed: the OS did not accept the update for '{item.Id}'; retry later.");
                return false;
            }
            // The push can also drop OTHER ids when the shared budget shrank since
            // the last write — surface each loss like AddList does, so a caller
            // isn't left believing an unrelated shortcut is still installed.
            foreach (var id in idsBeforePush)
                if (id != item.Id && !IsAdded(id))
                    Log($"Update: the OS dropped '{id}' (cap reached or id owned by another publisher).");
            if (!_items.Contains(copy))
            {
                // The write landed but the OS dropped this id (the shared budget can
                // shrink between pushes — e.g. a host published more shortcuts since).
                // The previous item is gone from the device too; stay honest.
                Log($"Update failed: the OS dropped '{item.Id}' (cap reached or id owned by another publisher).");
                return false;
            }
            Log($"Updated quick action '{item.Id}'.");
            return true;
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
            _refreshRetryArmed = false; // nothing left to re-render
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
            _refreshRetryArmed = false;
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
            // Serialize RESOLVED copies: the natives and the OS must see the final
            // strings (they do no locale work of their own), while _items keeps the
            // base text and the per-locale tables — mutating them here would make
            // the next locale switch translate an already-translated label.
            var payload = new List<QuickActionItem>(_items.Count);
            foreach (var item in _items)
                payload.Add(QuickActionLocalization.Resolved(item, Locale));

            var accepted = Bridge.SetShortcuts(payload);
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
            if (ReferenceEquals(accepted, payload))
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
