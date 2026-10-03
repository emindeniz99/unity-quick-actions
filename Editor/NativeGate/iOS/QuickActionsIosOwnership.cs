// What this package owns in an exported Xcode project, and how it is recognised.
// The gated post-processor (Editor/iOS) writes these and the define-off cleanup
// here removes them. This assembly exists under UNITY_IOS with the define on or
// off and already references the Xcode extension, so both read this one copy;
// the always-compiled Editor/Core cannot, since PlistDocument lives in a DLL that
// is only present with iOS Build Support.
using UnityEditor.iOS.Xcode;

namespace EminDeniz99.QuickActions.Editor.NativeGate
{
    /// <summary>
    /// Shared helpers for reading/merging our entries in the iOS
    /// <c>UIApplicationShortcutItems</c> plist array. Our entries carry a marker in
    /// their <c>UIApplicationShortcutItemUserInfo</c> so cleanup/refresh touches only
    /// ours and never a host app's own shortcuts.
    /// </summary>
    internal static class QuickActionsPlistShortcuts
    {
        internal const string ItemsKey = "UIApplicationShortcutItems";
        internal const string UserInfoKey = "UIApplicationShortcutItemUserInfo";
        internal const string MarkerKey = "com.emindeniz99.quickactions.managed";

        internal static PlistElementArray GetOrCreateArray(PlistDocument plist)
        {
            if (plist.root.values.TryGetValue(ItemsKey, out var existing) && existing is PlistElementArray arr)
                return arr;
            return plist.root.CreateArray(ItemsKey);
        }

        // True only for entries this package wrote (marked in their user info).
        internal static bool IsOurs(PlistElement entry)
        {
            if (!(entry is PlistElementDict dict))
                return false;
            if (!dict.values.TryGetValue(UserInfoKey, out var ui) || !(ui is PlistElementDict uiDict))
                return false;
            return uiDict.values.TryGetValue(MarkerKey, out var marker) && marker.AsBoolean();
        }

        // Removes our marked entries, dropping the whole key if nothing else remains.
        // Returns true if the plist changed.
        internal static bool ClearOurEntries(PlistDocument plist)
        {
            if (!plist.root.values.TryGetValue(ItemsKey, out var existing) || !(existing is PlistElementArray arr))
                return false;
            var removed = arr.values.RemoveAll(IsOurs);
            if (arr.values.Count == 0)
                plist.root.values.Remove(ItemsKey);
            return removed > 0;
        }
    }

    /// <summary>
    /// The folder template-image icons are copied into, and the manifest in it
    /// naming exactly the files this package copied: the only files either side
    /// ever deletes.
    /// </summary>
    internal static class QuickActionsTemplateImages
    {
        internal const string IconsFolder = "QuickActionsIcons";
        internal const string IconManifestName = "quickactions_manifest.txt";
    }
}
