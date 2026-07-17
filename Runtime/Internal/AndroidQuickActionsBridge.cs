#if UNITY_ANDROID && !UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace EminDeniz99.QuickActions.Internal
{
    /// <summary>
    /// Android bridge. Calls the static helpers in
    /// <c>Plugins/Android/QuickActionsBridge.java</c> through JNI, passing the
    /// current Activity so the helper can reach <c>ShortcutManager</c> and build
    /// intents that target the trampoline activity.
    /// </summary>
    internal sealed class AndroidQuickActionsBridge : IQuickActionsBridge
    {
        private const string BridgeClass = "com.emindeniz99.quickactions.QuickActionsBridge";

        private static AndroidJavaObject CurrentActivity()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                return player.GetStatic<AndroidJavaObject>("currentActivity");
        }

        // ShortcutManager is API 25 (Android 7.1)+.
        public bool IsPlatformSupported
        {
            get
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    return version.GetStatic<int>("SDK_INT") >= 25;
            }
        }

        public IList<QuickActionItem> SetShortcuts(IList<QuickActionItem> items)
        {
            if (!IsPlatformSupported) return items; // can't confirm — accept-all
            var json = JsonUtility.ToJson(new QuickActionList(items));
            string applied;
            try
            {
                using (var bridge = new AndroidJavaClass(BridgeClass))
                using (var activity = CurrentActivity())
                    // Java trims to the OS cap and returns the ids it APPLIED (null on a
                    // failed/rate-limited write). We deliberately use this return value,
                    // NOT a separate getDynamicShortcuts() read-back: a read after a failed
                    // write reflects the stale prior set and would make us prune (delete)
                    // just-added shortcuts.
                    applied = bridge.CallStatic<string>("setShortcuts", activity, json);
            }
            catch (AndroidJavaException e)
            {
                Debug.LogWarning("[QuickActions] SetShortcuts failed: " + e.Message);
                return items; // couldn't confirm — accept-all, prune nothing
            }

            // Null/empty = the write did not land — accept-all so the facade never drops
            // items on a transient failure (a later first-access reconcile still corrects).
            if (string.IsNullOrEmpty(applied)) return items;

            var appliedIds = new HashSet<string>();
            foreach (var s in QuickActionList.Parse(applied))
                if (s != null) appliedIds.Add(s.Id);

            // Return the subset of the *input* (caller's own objects, icons intact), in
            // input order, whose ids the OS actually applied (trimmed set).
            var accepted = new List<QuickActionItem>();
            foreach (var it in items)
                if (it != null && appliedIds.Contains(it.Id))
                    accepted.Add(it);
            return accepted;
        }

        public void RemoveAll()
        {
            if (!IsPlatformSupported) return;
            using (var bridge = new AndroidJavaClass(BridgeClass))
            using (var activity = CurrentActivity())
                bridge.CallStatic("removeAll", activity);
        }

        public string GetLastPerformed() => CallStringStatic("getLastPerformed");

        public void ResetLastPerformed()
        {
            using (var bridge = new AndroidJavaClass(BridgeClass))
                bridge.CallStatic("resetLastPerformed");
        }

        public string ConsumePendingPerformed() => CallStringStatic("consumePendingPerformed");

        public IList<QuickActionItem> GetShortcuts()
        {
            if (!IsPlatformSupported)
                return new List<QuickActionItem>();
            try
            {
                using (var bridge = new AndroidJavaClass(BridgeClass))
                using (var activity = CurrentActivity())
                    return QuickActionList.Parse(bridge.CallStatic<string>("getShortcutsJson", activity));
            }
            catch (AndroidJavaException e)
            {
                // Defense in depth: the Java side already guards, but never let a
                // JNI exception escape into the facade's first-access reconcile.
                Debug.LogWarning("[QuickActions] GetShortcuts failed: " + e.Message);
                return new List<QuickActionItem>();
            }
        }

        private static string CallStringStatic(string method)
        {
            using (var bridge = new AndroidJavaClass(BridgeClass))
            {
                var value = bridge.CallStatic<string>(method);
                return string.IsNullOrEmpty(value) ? null : value;
            }
        }
    }
}
#endif
