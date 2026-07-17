#ifndef QUICKACTIONS_H
#define QUICKACTIONS_H

// C API consumed from C# (EminDeniz99.QuickActions.Internal.IOSQuickActionsBridge)
// via DllImport("__Internal"). The implementation lives in QuickActions.mm,
// which also installs the UnityAppController hooks. This header is informational;
// the .mm does not depend on it.

#ifdef __cplusplus
extern "C" {
#endif

// Replace THIS PACKAGE'S subset of UIApplication.shortcutItems from
// {"items":[{Id,Title,Subtitle,Icon}]}. Items are stamped with a userInfo
// marker; unmarked host/other-plugin items are preserved (except unmarked
// items whose type matches an id being written — pre-marker leftovers of this
// package — which are adopted/replaced).
void _QuickActions_SetShortcuts(const char *json);

// Remove the dynamic shortcut items THIS PACKAGE created (marker-scoped);
// a host app's own items are preserved.
void _QuickActions_RemoveAll(void);

// Id the app was last launched/resumed from (this session), or NULL.
// malloc'd; caller frees via _QuickActions_FreeString.
char *_QuickActions_GetLastPerformed(void);

// Clear the persisted "last performed" id.
void _QuickActions_ResetLastPerformed(void);

// Pull-and-clear the next queued performed id for the C# Performed event, or
// NULL. malloc'd; caller frees via _QuickActions_FreeString.
char *_QuickActions_ConsumePendingPerformed(void);

// The dynamic shortcut items THIS PACKAGE created (marker-scoped; host items
// are never surfaced) as {"items":[...]} (Icon reported as 0). malloc'd; caller
// frees. Lets C# reconcile its list after a cold start. Returns NULL when the
// read FAILED (an off-main-thread call that timed out marshalling to the main
// queue) — distinct from the empty-success {"items":[]}.
char *_QuickActions_GetShortcutsJson(void);

// Frees a string returned by the getters above (paired with the native malloc).
void _QuickActions_FreeString(char *ptr);

#ifdef __cplusplus
}
#endif

#endif // QUICKACTIONS_H
