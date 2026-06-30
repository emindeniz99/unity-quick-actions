using UnityEditor;
using UnityEngine;

namespace Playground.QuickActions.Editor
{
    /// <summary>
    /// <b>Window ▸ Quick Actions ▸ Simulator.</b> Lists the registered runtime and
    /// static quick actions and lets you fire one as if the user tapped it on the
    /// home screen — it raises <see cref="QuickActions.Performed"/> through the same
    /// path a real tap takes, so you can exercise your routing code in Play Mode
    /// without building to a device. Editor-only: nothing here ships in a build.
    /// </summary>
    public sealed class QuickActionsSimulatorWindow : EditorWindow
    {
        private string _customId = "";

        [MenuItem("Window/Quick Actions/Simulator")]
        private static void Open()
        {
            var window = GetWindow<QuickActionsSimulatorWindow>(false, "QA Simulator");
            window.minSize = new Vector2(360, 320);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Quick Actions Simulator", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Fire a shortcut as if it was tapped on the home screen — raises " +
                "QuickActions.Performed through the real path. No device needed.",
                EditorStyles.wordWrappedLabel);

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(
                    "Enter Play Mode so the tap reaches your game's Performed handlers " +
                    "(usually wired up in Awake/OnEnable).", MessageType.Info);
                if (GUILayout.Button("▶ Enter Play Mode"))
                    EditorApplication.EnterPlaymode();
            }

            // Runtime shortcuts the game added this session (in-memory authoritative list).
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime shortcuts (added via QuickActions.Add)", EditorStyles.boldLabel);
            var runtimeItems = QuickActions.GetAll();
            if (runtimeItems.Count == 0)
                EditorGUILayout.LabelField("  (none yet — your game adds these at runtime)");
            else
                foreach (var item in runtimeItems)
                    DrawTapButton(item.Title, item.Id);

            // Static shortcuts from the settings asset (baked into the build at build time).
            var settings = QuickActionsSettings.GetOrNull();
            if (settings != null && settings.StaticShortcuts.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Static shortcuts (baked into the build)", EditorStyles.boldLabel);
                foreach (var item in settings.StaticShortcuts)
                    if (item != null && !string.IsNullOrEmpty(item.Id))
                        DrawTapButton(item.Title, item.Id);
            }

            // Arbitrary id — e.g. simulate a cold launch from any shortcut id.
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Custom id", EditorStyles.boldLabel);
            _customId = EditorGUILayout.TextField("Id", _customId);
            if (GUILayout.Button("Simulate tap"))
                Fire(_customId);
        }

        private void DrawTapButton(string title, string id)
        {
            var label = string.IsNullOrEmpty(title) ? id : $"{title}   —   {id}";
            if (GUILayout.Button(label))
                Fire(id);
        }

        private static void Fire(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning("[QuickActions] Simulator: enter an id first.");
                return;
            }
            // The same internal entry point the iOS/Android bridges drain taps into.
            QuickActions.Dispatch(id);
            Debug.Log($"[QuickActions] Simulated tap → Performed('{id}')" +
                      (EditorApplication.isPlaying
                          ? ""
                          : "  (not in Play Mode — your runtime handlers may not be subscribed)"));
        }
    }
}
