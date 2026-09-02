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
        // Resolved by name over JNI. In a MINIFIED build R8 can rename/strip this
        // non-manifest class; ship a keep rule then — see the README "Known limits —
        // Android minification (R8/ProGuard + resource shrinking)".
        private const string BridgeClass = "com.emindeniz99.quickactions.QuickActionsBridge";

        // Cached for the hot poll path only (see CallStringStatic); the methods that
        // also need the Activity run at most once per user action and stay unbatched.
        private static AndroidJavaClass _bridgeClass;

        private static AndroidJavaObject CurrentActivity()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                return player.GetStatic<AndroidJavaObject>("currentActivity");
        }

        // ShortcutManager is API 25 (Android 7.1)+. SDK_INT never changes for a
        // process, so it is read once and cached; a FAILED read is neither cached
        // nor reported as "unsupported". The distinction matters: below API 25 an
        // empty shortcut set is a real, authoritative answer (the facade prunes
        // against it), whereas a JNI read that threw says nothing about the device —
        // treating it as API<25 would let one bad read wipe the user's real
        // shortcuts on the next write. So the read/write members below ask
        // TrySdkInt and take their failed-read path (null) when it fails, and the
        // query members answer "not supported" for that call only.
        private static int _sdkInt = -1; // -1: not read yet, or the last read failed

        private static bool TrySdkInt(out int sdkInt)
        {
            if (_sdkInt >= 0)
            {
                sdkInt = _sdkInt;
                return true;
            }
            try
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    _sdkInt = version.GetStatic<int>("SDK_INT");
                sdkInt = _sdkInt;
                return _sdkInt >= 0;
            }
            catch (AndroidJavaException e)
            {
                // Guarded like every other JNI path here: this is evaluated AHEAD of
                // the try blocks in the members below, so a throw from it would be the
                // one JNI exception that could escape into the caller's Add()/GetAll().
                Debug.LogWarning("[QuickActions] Could not read Build.VERSION.SDK_INT: " + e.Message);
                sdkInt = -1;
                return false;
            }
        }

        public bool IsPlatformSupported => TrySdkInt(out var sdkInt) && sdkInt >= 25;

        public int MaxShortcutCount
        {
            get
            {
                if (!IsPlatformSupported) return 0;
                try
                {
                    using (var bridge = new AndroidJavaClass(BridgeClass))
                    using (var activity = CurrentActivity())
                        return bridge.CallStatic<int>("getMaxShortcutCount", activity);
                }
                catch (AndroidJavaException e)
                {
                    Debug.LogWarning("[QuickActions] MaxShortcutCount failed: " + e.Message);
                    return 0;
                }
            }
        }

        // requestPinShortcut is API 26+; below that pinning doesn't exist.
        public bool IsPinSupported
        {
            get
            {
                try
                {
                    using (var bridge = new AndroidJavaClass(BridgeClass))
                    using (var activity = CurrentActivity())
                        return bridge.CallStatic<bool>("isPinSupported", activity);
                }
                catch (AndroidJavaException e)
                {
                    Debug.LogWarning("[QuickActions] IsPinSupported failed: " + e.Message);
                    return false;
                }
            }
        }

        public bool RequestPin(string id)
        {
            try
            {
                using (var bridge = new AndroidJavaClass(BridgeClass))
                using (var activity = CurrentActivity())
                    return bridge.CallStatic<bool>("requestPinShortcut", activity, id);
            }
            catch (AndroidJavaException e)
            {
                Debug.LogWarning("[QuickActions] RequestPin failed: " + e.Message);
                return false;
            }
        }

        public bool ReportUsed(string id)
        {
            if (!IsPlatformSupported) return false;
            try
            {
                using (var bridge = new AndroidJavaClass(BridgeClass))
                using (var activity = CurrentActivity())
                    return bridge.CallStatic<bool>("reportShortcutUsed", activity, id);
            }
            catch (AndroidJavaException e)
            {
                Debug.LogWarning("[QuickActions] ReportUsed failed: " + e.Message);
                return false;
            }
        }

        public IList<QuickActionItem> SetShortcuts(IList<QuickActionItem> items)
        {
            // Below API 25 ShortcutManager doesn't exist, so nothing is installed. Report
            // an empty accepted set (not accept-all) so the facade prunes its list and
            // GetAll()/IsAdded() don't claim shortcuts the OS never received — matching
            // IsPlatformSupported=false and the documented no-op behaviour. A read
            // that FAILED is a failed write instead (null): the facade re-syncs and
            // retries, and nothing is pruned on the strength of an unknown SDK level.
            if (!TrySdkInt(out var sdkInt)) return null;
            if (sdkInt < 25) return new List<QuickActionItem>();
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
                return null; // write may not have landed — signal failure (facade re-syncs)
            }

            // Null/empty = the write did not land (rejected/rate-limited/errored). Return
            // null so the facade reconciles with the real OS state on next access rather
            // than trusting its optimistic mutation (Add/RemoveById already changed the
            // list); it deliberately does NOT prune here (that would risk wiping a
            // just-added item by misreading a stale device state).
            if (string.IsNullOrEmpty(applied)) return null;

            // A payload the parser cannot read is a failed write report, not an
            // empty one — return null for the same re-sync as a failed call.
            var appliedItems = QuickActionList.Parse(applied);
            if (appliedItems == null) return null;
            var appliedIds = new HashSet<string>();
            foreach (var s in appliedItems)
                if (s != null) appliedIds.Add(s.Id);

            // Return the subset of the *input* (caller's own objects, icons intact), in
            // input order, whose ids the OS actually applied (trimmed set).
            var accepted = new List<QuickActionItem>();
            foreach (var it in items)
                if (it != null && appliedIds.Contains(it.Id))
                    accepted.Add(it);
            return accepted;
        }

        public bool RemoveAll()
        {
            if (!TrySdkInt(out var sdkInt)) return false; // unknown SDK level: report failure, not success
            if (sdkInt < 25) return true; // no dynamic shortcuts exist below API 25
            try
            {
                using (var bridge = new AndroidJavaClass(BridgeClass))
                using (var activity = CurrentActivity())
                    return bridge.CallStatic<bool>("removeAll", activity);
            }
            catch (AndroidJavaException e)
            {
                Debug.LogWarning("[QuickActions] RemoveAll failed: " + e.Message);
                return false; // couldn't remove — let the facade keep its list
            }
        }

        public string GetLastPerformed() => CallStringStatic("getLastPerformed");

        public void ResetLastPerformed()
        {
            try
            {
                using (var bridge = new AndroidJavaClass(BridgeClass))
                    bridge.CallStatic("resetLastPerformed");
            }
            catch (AndroidJavaException e)
            {
                Debug.LogWarning("[QuickActions] ResetLastPerformed failed: " + e.Message);
            }
        }

        public string ConsumePendingPerformed() => CallStringStatic("consumePendingPerformed");

        public IList<QuickActionItem> GetShortcuts()
        {
            // Below API 25 there are genuinely no dynamic shortcuts — a real (empty)
            // read, not a failure. An SDK level we could not read is a failed read
            // (null): the facade leaves its cache alone and retries, rather than
            // adopting an empty set it would later prune against.
            if (!TrySdkInt(out var sdkInt))
                return null;
            if (sdkInt < 25)
                return new List<QuickActionItem>();
            try
            {
                using (var bridge = new AndroidJavaClass(BridgeClass))
                using (var activity = CurrentActivity())
                {
                    // Java returns null when the read itself failed (locked device etc.).
                    // Propagate that as null so the facade doesn't treat a failed read as
                    // an authoritative-empty set.
                    var json = bridge.CallStatic<string>("getShortcutsJson", activity);
                    return json == null ? null : QuickActionList.Parse(json);
                }
            }
            catch (AndroidJavaException e)
            {
                // Defense in depth: never let a JNI exception escape the reconcile.
                // Return null (read failed) so the facade retries rather than caching empty.
                Debug.LogWarning("[QuickActions] GetShortcuts failed: " + e.Message);
                return null;
            }
        }

        private static string CallStringStatic(string method)
        {
            // These poll/query methods are called from the runtime drain (cold-launch
            // coroutine + every focus/pause + the safety-net beat). Never let a JNI
            // exception (e.g. the class stripped by R8 / not packaged) escape into that
            // path — degrade to null, matching the guarded
            // SetShortcuts/GetShortcuts/RemoveAll.
            //
            // The class handle is cached for the process rather than rebuilt per call:
            // `new AndroidJavaClass` is a JNI FindClass plus a NewGlobalRef, and the
            // beat runs this several times a second for the app's whole lifetime, in
            // every game that merely has the package installed. Caching leaves only the
            // marshalled static call on that path. A failure drops the handle so a
            // transient fault re-resolves on the next tick instead of sticking.
            try
            {
                _bridgeClass ??= new AndroidJavaClass(BridgeClass);
                var value = _bridgeClass.CallStatic<string>(method);
                return string.IsNullOrEmpty(value) ? null : value;
            }
            catch (AndroidJavaException e)
            {
                _bridgeClass = null;
                Debug.LogWarning("[QuickActions] " + method + " failed: " + e.Message);
                return null;
            }
        }
    }
}
#endif
