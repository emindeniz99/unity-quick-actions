using System.Collections.Generic;

namespace EminDeniz99.QuickActions.Internal
{
    /// <summary>
    /// No-op bridge used in the Editor and on unsupported platforms so the public
    /// API is always safe to call. Quick actions only exist on device.
    /// </summary>
    internal sealed class NullQuickActionsBridge : IQuickActionsBridge
    {
        public bool IsPlatformSupported => false;
        // Accept-all: return the same reference so the facade prunes nothing (there
        // is no OS to trim, and the editor list must stay intact).
        public IList<QuickActionItem> SetShortcuts(IList<QuickActionItem> items) => items;
        public void RemoveAll() { }
        public string GetLastPerformed() => null;
        public void ResetLastPerformed() { }
        public string ConsumePendingPerformed() => null;
        public IList<QuickActionItem> GetShortcuts() => new List<QuickActionItem>();
    }
}
