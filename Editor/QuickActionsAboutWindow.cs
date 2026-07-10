using UnityEditor;
using UnityEngine;

namespace EminDeniz99.QuickActions.Editor
{
    /// <summary>
    /// Small in-Editor reference window (Window ▸ Quick Actions ▸ About) with a
    /// copy-pasteable quick-start and a link to the README. Purely informational —
    /// quick actions themselves are created at runtime from your own scripts.
    /// </summary>
    public sealed class QuickActionsAboutWindow : EditorWindow
    {
        private const string DocsUrl =
            "https://github.com/emindeniz99/playground/tree/main/projects/quick-actions-unity";

        private const string Snippet =
@"using EminDeniz99.QuickActions;

void Awake()
{
    // Fires on every tap, including the cold launch that started the app.
    QuickActions.Performed += id => Debug.Log($""Tapped: {id}"");
}

void Start()
{
    QuickActions.Add(new QuickActionItem(
        id: ""new_game"", title: ""New Game"",
        subtitle: ""Start fresh"", icon: IconType.Add));
}";

        // Must be a leaf under the submenu — "Window/Quick Actions" itself can't be
        // both this command and the parent of ".../Simulator" (Unity menu conflict).
        [MenuItem("Window/Quick Actions/About")]
        private static void Open()
        {
            var window = GetWindow<QuickActionsAboutWindow>(true, "Quick Actions");
            window.minSize = new Vector2(420, 320);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Home-Screen Quick Actions (iOS & Android)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Home-screen shortcuts (long-press the app icon). Create them at " +
                "runtime; the OS keeps them across launches.",
                EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Quick start", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(Snippet, EditorStyles.textArea,
                GUILayout.Height(180));

            EditorGUILayout.Space();
            if (GUILayout.Button("Open documentation"))
                Application.OpenURL(DocsUrl);
        }
    }
}
