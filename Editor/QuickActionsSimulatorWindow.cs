using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EminDeniz99.QuickActions.Editor
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

        // Cached: GetOrNull does a project-wide AssetDatabase.FindAssets, too slow to
        // run on every OnGUI repaint in a large project.
        private QuickActionsSettings _settings;

        // Built-in {placeholder} values for the ACTIVE build target, so the static
        // buttons preview the label that target would bake. Built-ins only: custom
        // placeholders and Customize subscribers run in a real build (a resolver
        // may shell out to git — not something to do per repaint), so their tokens
        // show raw here. Cached alongside _settings for the same repaint-cost reason.
        private Dictionary<string, string> _placeholderPreview;

        [MenuItem("Window/Quick Actions/Simulator")]
        private static void Open()
        {
            var window = GetWindow<QuickActionsSimulatorWindow>(false, "QA Simulator");
            window.minSize = new Vector2(360, 320);
        }

        private void OnEnable() => RefreshSettings();
        private void OnFocus() => RefreshSettings();
        private void OnProjectChange() => RefreshSettings();
        private void RefreshSettings()
        {
            _settings = QuickActionsSettings.GetOrNull();
            _placeholderPreview =
                QuickActionsStaticBuild.BuiltinValues(EditorUserBuildSettings.activeBuildTarget);
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
                    "Not in Play Mode. Clicking a shortcut will START Play Mode and " +
                    "deliver it as a COLD LAUNCH — like tapping the icon while the app " +
                    "is closed. The tap fires at startup, once your Awake/OnEnable " +
                    "handlers are subscribed.", MessageType.Info);
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

            // Static shortcuts from the settings asset (baked into the build at build
            // time). Titles preview built-in {placeholder} values for the active
            // build target — see the _placeholderPreview note.
            if (_settings != null && _settings.StaticShortcuts.Count > 0)
            {
                if (_placeholderPreview == null)
                    RefreshSettings();
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Static shortcuts (baked into the build)", EditorStyles.boldLabel);
                foreach (var item in _settings.StaticShortcuts)
                    if (item != null && !string.IsNullOrEmpty(item.Id))
                        DrawTapButton(
                            QuickActionsStaticBuild.Interpolate(item.Title, _placeholderPreview, null),
                            item.Id);
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
            if (EditorApplication.isPlaying)
            {
                // Warm tap: deliver to the running game's live handlers right now.
                QuickActions.EditorSimulateTap(id);
                Debug.Log($"[QuickActions] Simulated tap → Performed('{id}')");
            }
            else
            {
                // Cold launch: start the app (Play Mode) and deliver at startup.
                Debug.Log($"[QuickActions] Cold launch: entering Play Mode to deliver '{id}'…");
                QuickActionsPlayModeColdLaunch.RequestColdLaunch(id);
            }
        }
    }
}
