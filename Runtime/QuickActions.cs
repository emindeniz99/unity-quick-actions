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
    /// </summary>
    public static class QuickActions
    {
        private static IQuickActionsBridge _bridge;
        private static readonly List<QuickActionItem> _items = new List<QuickActionItem>();

        private static IQuickActionsBridge Bridge => _bridge ??= QuickActionsBridgeFactory.Create();

        /// <summary>When true, the API logs its operations through <c>Debug.Log</c>.</summary>
        public static bool LoggingEnable { get; set; }

        /// <summary>True on a device that supports quick actions; false in-Editor.</summary>
        public static bool IsPlatformSupported => Bridge.IsPlatformSupported;

        /// <summary>
        /// Id of the quick action the app was most recently launched or resumed
        /// from, for this session; null otherwise.
        ///
        /// This is a <b>pull-based alternative</b> to <see cref="Performed"/> for
        /// code that does not subscribe — do not route on both for the same tap, or
        /// a cold launch is handled twice (the launch also raises
        /// <see cref="Performed"/>).
        /// </summary>
        public static string LastPerformed => Bridge.GetLastPerformed();

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
        public static void ResetLastPerformed() => Bridge.ResetLastPerformed();

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

        /// <summary>Snapshot of the currently added quick actions (this session).</summary>
        public static List<QuickActionItem> GetAll() => new List<QuickActionItem>(_items);

        /// <summary>The added action with this id, or null.</summary>
        public static QuickActionItem GetById(string id) => _items.FirstOrDefault(a => a.Id == id);

        /// <summary>Remove a quick action. Returns true if one was removed.</summary>
        public static bool Remove(QuickActionItem item) => item != null && RemoveById(item.Id);

        /// <summary>Remove the quick action with this id. Returns true if one was removed.</summary>
        public static bool RemoveById(string id)
        {
            if (string.IsNullOrEmpty(id) || _items.RemoveAll(a => a.Id == id) == 0)
                return false;

            Push();
            Log($"Removed quick action '{id}'.");
            return true;
        }

        /// <summary>Remove every quick action.</summary>
        public static void RemoveAll()
        {
            _items.Clear();
            Bridge.RemoveAll();
            Log("Removed all quick actions.");
        }

        /// <summary>True if an action with this item's id is added.</summary>
        public static bool IsAdded(QuickActionItem item) => item != null && IsAdded(item.Id);

        /// <summary>True if an action with this id is added.</summary>
        public static bool IsAdded(string id) => !string.IsNullOrEmpty(id) && _items.Any(a => a.Id == id);

        // ---- internal: called by QuickActionsRuntime ----

        internal static void Dispatch(string actionId)
        {
            if (string.IsNullOrEmpty(actionId))
                return;
            Log($"Performed quick action '{actionId}'.");
            Performed?.Invoke(actionId);
        }

        private static void Push() => Bridge.SetShortcuts(_items);

        private static void Log(string message)
        {
            if (LoggingEnable)
                Debug.Log($"[QuickActions] {message}");
        }
    }
}
