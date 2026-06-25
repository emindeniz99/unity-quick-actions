namespace Playground.QuickActions.Internal
{
    /// <summary>
    /// Picks the right <see cref="IQuickActionsBridge"/> for the build target.
    /// Editor always gets the no-op bridge (no device APIs); device builds get
    /// the platform implementation guarded by the matching <c>UNITY_*</c> symbol.
    /// </summary>
    internal static class QuickActionsBridgeFactory
    {
        internal static IQuickActionsBridge Create()
        {
#if UNITY_EDITOR
            return new NullQuickActionsBridge();
#elif UNITY_IOS
            return new IOSQuickActionsBridge();
#elif UNITY_ANDROID
            return new AndroidQuickActionsBridge();
#else
            return new NullQuickActionsBridge();
#endif
        }
    }
}
