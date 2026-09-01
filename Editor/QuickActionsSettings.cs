using System.Collections.Generic;
using System.IO;
using EminDeniz99.QuickActions;
using UnityEditor;
using UnityEngine;

namespace EminDeniz99.QuickActions.Editor
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

        // Editor-UI append (the settings page's preset button). Callers own the
        // Undo/SetDirty bookkeeping — this only mutates the serialized list.
        internal void AddStaticShortcut(QuickActionItem item) => staticShortcuts.Add(item);

        [Tooltip("Write the package's four built-in Android shortcut drawables " +
                 "(ic_quickaction_builtin_{add,compose,favorite,play}, ~2 KB of vector XML) " +
                 "into every Android build, so IconType.Add/Compose/Favorite/Play render with " +
                 "nothing added to the project. Your own ic_quickaction_<name> takes precedence " +
                 "either way. Off: no package art reaches the APK, and those four render blank " +
                 "unless the project ships its own drawable.")]
        [SerializeField]
        private bool writeBuiltInAndroidIcons = true;

        /// <summary>
        /// Whether the Android build post-processor writes the package's built-in
        /// drawables (<c>ic_quickaction_builtin_&lt;name&gt;</c>) into the generated
        /// Gradle project. Default true. A project's own <c>ic_quickaction_&lt;name&gt;</c>
        /// takes precedence at runtime either way; this is the escape hatch for a
        /// project that wants no package art in its APK at all.
        /// </summary>
        public bool WriteBuiltInAndroidIcons => writeBuiltInAndroidIcons;

        [Tooltip("PNG (preferred) or JPEG textures copied into the iOS build (app " +
                 "target). A PNG resolves as IosTemplateImage = \"<file name without " +
                 "extension>\"; a JPEG must include its extension (bare-name bundle " +
                 "lookup is PNG-only). Use single-color template art (~35×35 pt).")]
        [SerializeField]
        private List<Texture2D> iosTemplateImages = new List<Texture2D>();

        /// <summary>
        /// Textures the iOS build post-processor copies into the generated Xcode
        /// project's app target for use with
        /// <see cref="QuickActionItem.IosTemplateImage"/> (and the static plist
        /// <c>IconFile</c>). A PNG is referenced by its file name <b>without</b>
        /// extension; a JPEG must be referenced <b>with</b> its extension —
        /// iOS's bare-name bundle-image lookup resolves only <c>.png</c>. The
        /// source asset must be a PNG/JPEG file on disk (not a compressed-only
        /// format).
        /// </summary>
        public IReadOnlyList<Texture2D> IosTemplateImages => iosTemplateImages;

        /// <summary>
        /// Default location of the settings asset when auto-created.
        /// <para>
        /// Deliberately <b>not</b> under <c>Assets/QuickActions/</c>. The
        /// drag-and-drop <c>.unitypackage</c> — and the Asset Store listing built
        /// from it — installs into exactly that folder, and updating such an
        /// asset means deleting and re-importing it, which would take the user's
        /// own configuration with it. Keeping the settings outside the install
        /// root makes reimport non-destructive.
        /// </para>
        /// <para>
        /// Moving this is safe for projects that already have one:
        /// <see cref="GetOrNull"/> finds the asset by <i>type</i>, not by path,
        /// so a settings asset at the old location keeps working untouched.
        /// </para>
        /// </summary>
        public const string DefaultAssetPath = "Assets/Settings/QuickActionsSettings.asset";

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
            // clean project where DefaultAssetPath's folder doesn't exist yet.
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
