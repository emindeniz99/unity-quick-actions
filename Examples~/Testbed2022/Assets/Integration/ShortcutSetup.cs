// The README's quickstart component, verbatim, inside a consumer-style assembly
// definition (Testbed.Integration.asmdef) that references the package's runtime
// assembly. It is in no scene; it exists to be COMPILED by CI in both
// configurations the README describes:
//   * define ON  (android-build / ios-export / tests): the reference resolves and
//     the guarded bodies compile against the package;
//   * define OFF (the gate-off job): EminDeniz99.QuickActions is not compiled at
//     all, so this assembly's reference to it points at nothing — the README
//     claims Unity drops such a reference rather than failing the build, and the
//     component stays compiled as an inert MonoBehaviour. This file is that
//     claim's test; if the define-off build ever fails here, the README's asmdef
//     guidance is what has to change.
#if QUICKACTIONS_ENABLED
using EminDeniz99.QuickActions;
#endif
using UnityEngine;

public class ShortcutSetup : MonoBehaviour
{
#if QUICKACTIONS_ENABLED
    // Subscribe early: the cold-launch tap arrives one frame after startup.
    void Awake() => QuickActions.Performed += OnShortcut;
    // Performed is static and process-wide: never leave a handler behind.
    void OnDestroy() => QuickActions.Performed -= OnShortcut;
    
    // Fires on every tap, including the cold launch that started the app.
    void OnShortcut(string id) => Debug.Log($"Tapped: {id}");

    void Start()
    {
        QuickActions.Add(new QuickActionItem(
            id: "new_game", title: "New Game",
            subtitle: "Start fresh", icon: IconType.Add));
    }
#endif
}
