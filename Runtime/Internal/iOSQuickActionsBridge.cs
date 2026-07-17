#if UNITY_IOS && !UNITY_EDITOR
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace EminDeniz99.QuickActions.Internal
{
    /// <summary>
    /// iOS bridge. Talks to the Objective-C++ layer in
    /// <c>Plugins/iOS/QuickActions.mm</c> via <c>__Internal</c> P/Invoke.
    /// </summary>
    internal sealed class IOSQuickActionsBridge : IQuickActionsBridge
    {
        [DllImport("__Internal")] private static extern void _QuickActions_SetShortcuts(string json);
        [DllImport("__Internal")] private static extern void _QuickActions_RemoveAll();
        [DllImport("__Internal")] private static extern System.IntPtr _QuickActions_GetLastPerformed();
        [DllImport("__Internal")] private static extern void _QuickActions_ResetLastPerformed();
        [DllImport("__Internal")] private static extern System.IntPtr _QuickActions_ConsumePendingPerformed();
        [DllImport("__Internal")] private static extern System.IntPtr _QuickActions_GetShortcutsJson();
        [DllImport("__Internal")] private static extern void _QuickActions_FreeString(System.IntPtr ptr);

        public bool IsPlatformSupported => true;

        public IList<QuickActionItem> SetShortcuts(IList<QuickActionItem> items)
        {
            _QuickActions_SetShortcuts(JsonUtility.ToJson(new QuickActionList(items)));
            // iOS has no dynamic-shortcut cap (it just shows the first ~4) and its
            // native write is async on the main thread, so accept-all is both correct
            // and avoids racing the write with a read-back.
            return items;
        }

        public bool RemoveAll() { _QuickActions_RemoveAll(); return true; }

        public string GetLastPerformed() => Consume(_QuickActions_GetLastPerformed());

        public void ResetLastPerformed() => _QuickActions_ResetLastPerformed();

        public string ConsumePendingPerformed() => Consume(_QuickActions_ConsumePendingPerformed());

        public System.Collections.Generic.IList<QuickActionItem> GetShortcuts()
        {
            // Native returns null on a failed read (e.g. an off-main-thread call that
            // timed out marshalling to the main queue). Propagate null so the facade
            // doesn't treat a failed read as an authoritative-empty set.
            var json = Consume(_QuickActions_GetShortcutsJson());
            return json == null ? null : QuickActionList.Parse(json);
        }

        // The native side hands back a malloc'd UTF-8 C string we own. Decode as
        // UTF-8 (ids/titles may be non-ASCII) and free it with the matching native
        // free() rather than Marshal.FreeHGlobal (allocator pairing).
        private static string Consume(System.IntPtr ptr)
        {
            if (ptr == System.IntPtr.Zero)
                return null;
            var value = Marshal.PtrToStringUTF8(ptr);
            _QuickActions_FreeString(ptr);
            return string.IsNullOrEmpty(value) ? null : value;
        }
    }
}
#endif
