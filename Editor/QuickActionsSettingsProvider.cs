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
                }
            };
        }
    }
}
