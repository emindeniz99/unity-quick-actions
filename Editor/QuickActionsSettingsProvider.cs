using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EminDeniz99.QuickActions.Editor
{
    /// <summary>
    /// Adds a <b>Project Settings ▸ Quick Actions</b> page for editing the static
    /// shortcut list. Renders the <see cref="QuickActionsSettings"/> asset's
    /// default inspector so the list is fully editable, and offers to create the
    /// asset on first use.
    /// </summary>
    internal static class QuickActionsSettingsProvider
    {
        private static UnityEditor.Editor _cachedEditor;

        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return new SettingsProvider("Project/Quick Actions", SettingsScope.Project)
            {
                label = "Quick Actions",
                keywords = new[] { "quick", "actions", "shortcut", "ios", "android", "home screen" },
                guiHandler = _ =>
                {
                    var settings = QuickActionsSettings.GetOrNull();
                    if (settings == null)
                    {
                        EditorGUILayout.HelpBox(
                            "No Quick Actions settings asset yet. Static shortcuts are baked " +
                            "into the build from this asset; dynamic shortcuts created at " +
                            "runtime don't need it.",
                            MessageType.Info);
                        if (GUILayout.Button("Create settings asset"))
                            QuickActionsSettings.GetOrCreate();
                        return;
                    }

                    EditorGUILayout.LabelField("Static shortcuts (baked into the build)", EditorStyles.boldLabel);
                    UnityEditor.Editor.CreateCachedEditor(settings, null, ref _cachedEditor);
                    _cachedEditor.OnInspectorGUI();

                    // The build post-processors silently skip duplicate/empty ids and
                    // empty titles, so surface those here — otherwise a misconfigured
                    // shortcut just goes missing from the build with no warning.
                    var problem = Validate(settings);
                    if (problem != null)
                        EditorGUILayout.HelpBox(problem, MessageType.Warning);

                    // Same early-warning idea for placeholder typos: at build time an
                    // unknown token is (deliberately) left verbatim, so "{verison}"
                    // would otherwise surface for the first time on a device.
                    var unknownTokens = ValidatePlaceholders(settings);
                    if (unknownTokens != null)
                        EditorGUILayout.HelpBox(unknownTokens, MessageType.Warning);

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField(
                        "Titles and subtitles may use build-time placeholders: {version}, " +
                        "{build}, {bundleId}, {productName}, {unityVersion}, {platform}. " +
                        "Double a brace ({{) for a literal one; register custom values " +
                        "with QuickActionsStaticBuild.RegisterPlaceholder.",
                        EditorStyles.wordWrappedLabel);
                    if (!HasShortcutId(settings, AppInfoId) &&
                        GUILayout.Button("Add \"app info\" shortcut — shows v{version} ({build}) on long-press"))
                        AddAppInfoShortcut(settings);
                },
                deactivateHandler = () =>
                {
                    // Don't leak the cached inspector (and avoid GUI on a destroyed
                    // target after the page closes), matching AssetSettingsProvider.
                    if (_cachedEditor != null)
                    {
                        Object.DestroyImmediate(_cachedEditor);
                        _cachedEditor = null;
                    }
                }
            };
        }

        /// <summary>
        /// Returns a human-readable warning if any static shortcut would be dropped
        /// at build time (empty id/title, or a duplicate id), else null.
        /// </summary>
        private static string Validate(QuickActionsSettings settings)
        {
            var seen = new HashSet<string>();
            var duplicates = new List<string>();
            var emptyCount = 0;
            foreach (var item in settings.StaticShortcuts)
            {
                if (item == null || string.IsNullOrEmpty(item.Id) || string.IsNullOrEmpty(item.Title))
                {
                    emptyCount++;
                    continue;
                }
                if (!seen.Add(item.Id))
                    duplicates.Add(item.Id);
            }

            if (emptyCount == 0 && duplicates.Count == 0)
                return null;

            var parts = new List<string>();
            if (emptyCount > 0)
                parts.Add($"{emptyCount} shortcut(s) missing an Id or Title");
            if (duplicates.Count > 0)
                parts.Add($"duplicate Id(s): {string.Join(", ", duplicates)}");
            return "These will be skipped in the build — " + string.Join("; ", parts) + ".";
        }

        /// <summary>
        /// Returns a warning naming every token that neither the built-in set nor a
        /// registered custom placeholder resolves, else null. Reuses the build-time
        /// parser itself, so this page and the bake can't disagree on what a token is.
        /// (Placeholders registered from a not-yet-compiled editor script are simply
        /// not visible yet — the warning text says how to register, not "invalid".)
        /// </summary>
        private static string ValidatePlaceholders(QuickActionsSettings settings)
        {
            var known = QuickActionsStaticBuild.KnownPlaceholders();
            var unknown = new SortedSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var item in settings.StaticShortcuts)
            {
                if (item == null)
                    continue;
                QuickActionsStaticBuild.Interpolate(item.Title, known, unknown);
                QuickActionsStaticBuild.Interpolate(item.Subtitle, known, unknown);
                CollectUnknown(item.LocalizedTitles, known, unknown);
                CollectUnknown(item.LocalizedSubtitles, known, unknown);
            }
            if (unknown.Count == 0)
                return null;
            return "Unknown placeholder(s): {" + string.Join("}, {", unknown) +
                "} — the build leaves them as-is. Register custom ones with " +
                "QuickActionsStaticBuild.RegisterPlaceholder (from an editor script), " +
                "or double the brace ({{) for a literal one.";
        }

        private static void CollectUnknown(List<LocalizedText> entries,
            Dictionary<string, string> known, ISet<string> unknown)
        {
            if (entries == null)
                return;
            foreach (var entry in entries)
                if (entry != null)
                    QuickActionsStaticBuild.Interpolate(entry.Text, known, unknown);
        }

        private const string AppInfoId = "app_info";

        private static bool HasShortcutId(QuickActionsSettings settings, string id)
        {
            foreach (var item in settings.StaticShortcuts)
                if (item != null && item.Id == id)
                    return true;
            return false;
        }

        // The preset the button offers: which build is on this device, readable
        // from a long-press without launching. Version goes in the SUBTITLE
        // because that is what both platforms actually show under long-press
        // (Android renders the long label — the subtitle — as the entry).
        private static void AddAppInfoShortcut(QuickActionsSettings settings)
        {
            Undo.RecordObject(settings, "Add app info shortcut");
            settings.AddStaticShortcut(new QuickActionItem(AppInfoId, "App info", "v{version} ({build})")
            {
                // SF Symbol on iOS 13+ (a static item renders iconless on iOS 12 —
                // documented on IosSystemImage). No Android icon preset: static
                // Android icons need a drawable shipped in the user's project.
                IosSystemImage = "info.circle",
            });
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
    }
}
