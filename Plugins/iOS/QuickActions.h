#ifndef QUICKACTIONS_H
#define QUICKACTIONS_H

// C API consumed from C# (Playground.QuickActions.Internal.IOSQuickActionsBridge)
// via DllImport("__Internal"). The implementation lives in QuickActions.mm,
// which also installs the UnityAppController hooks. This header is informational;
// the .mm does not depend on it.

#ifdef __cplusplus
extern "C" {
#endif

// Replace UIApplication.shortcutItems from {"items":[{Id,Title,Subtitle,Icon}]}.
void _QuickActions_SetShortcuts(const char *json);

// Clear all dynamic shortcut items.
void _QuickActions_RemoveAll(void);

// Id the app was last launched/resumed from (this session), or NULL.
// malloc'd; caller frees via _QuickActions_FreeString.
char *_QuickActions_GetLastPerformed(void);

// Clear the persisted "last performed" id.
void _QuickActions_ResetLastPerformed(void);

// Pull-and-clear the next queued performed id for the C# Performed event, or
// NULL. malloc'd; caller frees via _QuickActions_FreeString.
char *_QuickActions_ConsumePendingPerformed(void);

// Current OS shortcut items as {"items":[...]} (Icon reported as 0). malloc'd;
// caller frees. Lets C# reconcile its list after a cold start.
char *_QuickActions_GetShortcutsJson(void);

// Frees a string returned by the getters above (paired with the native malloc).
void _QuickActions_FreeString(char *ptr);

#ifdef __cplusplus
}
#endif

#endif // QUICKACTIONS_H
