using System.Collections.Generic;
using System.IO;
using Playground.QuickActions;
using UnityEditor;
using UnityEngine;

namespace Playground.QuickActions.Editor
{
    /// <summary>
    /// Project-level configuration for <b>static</b> quick actions — shortcuts
    /// baked into the build so they exist on the very first launch, before any
    /// runtime <see cref="QuickActions.Add"/> call. The build post-processors
    /// read this asset and write the platform manifests
    /// (iOS <c>Info.plist</c> / Android <c>shortcuts.xml</c>).
    ///
    /// Edit it under <b>Project Settings ▸ Quick Actions</b>. Dynamic shortcuts
    /// added at runtime are independent and unaffected.
    /// </summary>
    public sealed class QuickActionsSettings : ScriptableObject
    {
        [Tooltip("Shortcuts baked into the build. iOS shows up to 4 total (static + dynamic).")]
        [SerializeField]
        private List<QuickActionItem> staticShortcuts = new List<QuickActionItem>();

        /// <summary>The configured static shortcuts (may be empty).</summary>
        public IReadOnlyList<QuickActionItem> StaticShortcuts => staticShortcuts;

        /// <summary>Default location of the settings asset when auto-created.</summary>
        public const string DefaultAssetPath = "Assets/QuickActions/QuickActionsSettings.asset";

        /// <summary>Loads the settings asset, or null if the project has none.</summary>
        public static QuickActionsSettings GetOrNull()
        {
            var guids = AssetDatabase.FindAssets("t:QuickActionsSettings");
            if (guids == null || guids.Length == 0)
                return null;
            if (guids.Length > 1)
                Debug.LogWarning($"[QuickActions] Found {guids.Length} QuickActionsSettings assets; using the first.");
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<QuickActionsSettings>(path);
        }

        /// <summary>Loads the settings asset, creating it at the default path if absent.</summary>
        public static QuickActionsSettings GetOrCreate()
        {
            var existing = GetOrNull();
            if (existing != null)
                return existing;

            var settings = CreateInstance<QuickActionsSettings>();
            // Create the folder through the AssetDatabase (not Directory.Create
            // Directory, which it doesn't know about) so CreateAsset persists on a
            // clean project where Assets/QuickActions doesn't exist yet.
            EnsureAssetFolder(Path.GetDirectoryName(DefaultAssetPath));
            AssetDatabase.CreateAsset(settings, DefaultAssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        // Create every missing folder segment under "Assets/" via the AssetDatabase.
        private static void EnsureAssetFolder(string folder)
        {
            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder))
                return;
            var parts = folder.Split('/');
            var current = parts[0]; // "Assets"
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
