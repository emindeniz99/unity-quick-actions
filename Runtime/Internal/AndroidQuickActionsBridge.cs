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

        public void SetShortcuts(IList<QuickActionItem> items)
        {
            if (!IsPlatformSupported) return;
            var json = JsonUtility.ToJson(new QuickActionList(items));
            using (var bridge = new AndroidJavaClass(BridgeClass))
            using (var activity = CurrentActivity())
                bridge.CallStatic("setShortcuts", activity, json);
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
