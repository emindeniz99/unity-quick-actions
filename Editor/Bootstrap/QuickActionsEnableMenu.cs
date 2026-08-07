using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace EminDeniz99.QuickActions.Editor.Bootstrap
{
    /// <summary>
    /// The one menu item that exists when the package is switched off.
    /// <para>
    /// Everything else in this package is constrained on
    /// <c>QUICKACTIONS_ENABLED</c>, which is deliberate — with the define absent
    /// the package compiles to nothing and leaves no trace in a player build.
    /// The cost is that a fresh import looks inert: no API, no menus, and the
    /// Demo scene shows a missing script, because the component it references is
    /// compiled away. Unity's own Asset Store validator reports that as a
    /// "missing component" warning, and a first-time user reasonably reads it as
    /// broken rather than off.
    /// </para>
    /// <para>
    /// So this assembly carries no define constraint and no platform constraint,
    /// and does exactly one thing: turn the package on for the platforms that
    /// matter. It adds nothing to a player build — it is Editor-only.
    /// </para>
    /// </summary>
    internal static class QuickActionsEnableMenu
    {
        private const string Define = "QUICKACTIONS_ENABLED";
        private const string MenuPath = "Window/Quick Actions/Enable Quick Actions";

        // The three targets the package supports. Standalone is included because
        // the in-Editor Simulator window runs under it, so leaving it out would
        // make Play Mode testing silently unavailable.
        private static readonly NamedBuildTarget[] Targets =
        {
            NamedBuildTarget.Standalone,
            NamedBuildTarget.Android,
            NamedBuildTarget.iOS,
        };

        private static readonly string[] TargetNames = { "Standalone", "Android", "iOS" };

        [MenuItem(MenuPath, priority = 100)]
        private static void Enable()
        {
            var missing = MissingTargets();
            if (missing.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Quick Actions",
                    $"{Define} is already set for Standalone, Android and iOS.\n\n" +
                    "The package is on. Window ▸ Quick Actions ▸ Simulator lets you " +
                    "test tap handling without a device.",
                    "OK");
                return;
            }

            var list = string.Join(", ", missing);
            var ok = EditorUtility.DisplayDialog(
                "Enable Quick Actions",
                $"Add the {Define} scripting define to: {list}?\n\n" +
                "The package is opt-in: without this define its API does not exist " +
                "and it contributes nothing to a build. Adding it triggers a script " +
                "recompile.\n\n" +
                "To turn the package off again, remove the define in " +
                "Project Settings ▸ Player ▸ Scripting Define Symbols.",
                "Add define", "Cancel");
            if (!ok)
                return;

            for (var i = 0; i < Targets.Length; i++)
            {
                var current = PlayerSettings.GetScriptingDefineSymbols(Targets[i]);
                if (HasDefine(current))
                    continue;
                var updated = string.IsNullOrEmpty(current) ? Define : current + ";" + Define;
                PlayerSettings.SetScriptingDefineSymbols(Targets[i], updated);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[QuickActions] Added {Define} to {list}. " +
                      "Once scripts finish recompiling, the API and the " +
                      "Window ▸ Quick Actions menus become available.");
        }

        // Greyed out once every target already has it, so the menu itself tells
        // you the current state without opening a dialog.
        [MenuItem(MenuPath, validate = true)]
        private static bool EnableValidate() => MissingTargets().Count > 0;

        private static System.Collections.Generic.List<string> MissingTargets()
        {
            var missing = new System.Collections.Generic.List<string>();
            for (var i = 0; i < Targets.Length; i++)
            {
                if (!HasDefine(PlayerSettings.GetScriptingDefineSymbols(Targets[i])))
                    missing.Add(TargetNames[i]);
            }
            return missing;
        }

        // Exact token match. A substring test would be fooled by an unrelated
        // define that merely contains this one as a prefix or suffix.
        private static bool HasDefine(string symbols)
        {
            if (string.IsNullOrEmpty(symbols))
                return false;
            foreach (var token in symbols.Split(';'))
            {
                if (token.Trim() == Define)
                    return true;
            }
            return false;
        }
    }
}
