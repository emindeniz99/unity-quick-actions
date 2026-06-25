#if UNITY_IOS && !UNITY_EDITOR
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Playground.QuickActions.Internal
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

        public bool IsPlatformSupported => true;

        public void SetShortcuts(IList<QuickActionItem> items)
        {
            _QuickActions_SetShortcuts(JsonUtility.ToJson(new QuickActionList(items)));
        }

        public void RemoveAll() => _QuickActions_RemoveAll();

        public string GetLastPerformed() => Consume(_QuickActions_GetLastPerformed());

        public void ResetLastPerformed() => _QuickActions_ResetLastPerformed();

        public string ConsumePendingPerformed() => Consume(_QuickActions_ConsumePendingPerformed());

        // The native side hands back a malloc'd C string we own; copy then free.
        private static string Consume(System.IntPtr ptr)
        {
            if (ptr == System.IntPtr.Zero)
                return null;
            var value = Marshal.PtrToStringAnsi(ptr);
            Marshal.FreeHGlobal(ptr);
            return string.IsNullOrEmpty(value) ? null : value;
        }
    }
}
#endif
