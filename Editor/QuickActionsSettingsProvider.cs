using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Playground.QuickActions.Editor
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
    }
}
