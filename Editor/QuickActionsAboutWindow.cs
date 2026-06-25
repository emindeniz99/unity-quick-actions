using UnityEditor;
using UnityEngine;

namespace Playground.QuickActions.Editor
{
    /// <summary>
    /// Small in-Editor reference window (Window ▸ Quick Actions) with a
    /// copy-pasteable quick-start and a link to the README. Purely informational —
    /// quick actions themselves are created at runtime from your own scripts.
    /// </summary>
    public sealed class QuickActionsAboutWindow : EditorWindow
    {
        private const string DocsUrl =
            "https://github.com/emindeniz99/playground/tree/main/projects/quick-actions-unity";

        private const string Snippet =
@"using Playground.QuickActions;

void Start()
{
    QuickActions.Performed += id => Debug.Log($""Tapped: {id}"");

    QuickActions.Add(new QuickActionItem(
        id: ""new_game"", title: ""New Game"",
        subtitle: ""Start fresh"", icon: IconType.Add));

    // Cold launch routing:
    if (QuickActions.LastPerformed != null)
        Debug.Log($""Launched from: {QuickActions.LastPerformed}"");
}";

        [MenuItem("Window/Quick Actions")]
        private static void Open()
        {
            var window = GetWindow<QuickActionsAboutWindow>(true, "Quick Actions");
            window.minSize = new Vector2(420, 320);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Quick Actions for iOS & Android", EditorStyles.boldLabel);
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
