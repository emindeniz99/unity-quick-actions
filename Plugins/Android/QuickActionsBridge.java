package com.playground.quickactions;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.content.pm.ShortcutInfo;
import android.content.pm.ShortcutManager;
import android.graphics.drawable.Icon;
import android.os.Build;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayDeque;
import java.util.ArrayList;
import java.util.List;

/**
 * Native helper behind the C# AndroidQuickActionsBridge. The managed layer owns
 * the authoritative shortcut list and pushes the full set here; this class turns
 * it into {@link ShortcutManager} dynamic shortcuts whose intents target
 * {@link QuickActionsTrampolineActivity}. Tap delivery is handled by the
 * trampoline, which records the action id here for the C# side to poll.
 *
 * All shortcut APIs require API 25 (Android 7.1); calls are no-ops below that.
 */
public final class QuickActionsBridge {

    public static final String EXTRA_ACTION_ID = "com.playground.quickactions.ACTION_ID";

    // Static shortcuts declared in res/xml cannot carry intent extras, so their id
    // is encoded as the intent action suffix: ACTION_PREFIX + "<id>". Keep in sync
    // with the Android build post-processor.
    public static final String ACTION_PREFIX = "com.playground.quickactions.PERFORM.";

    // Names of bundled drawables looked up for IconType values (index = enum int).
    // Index 0 (None) is intentionally empty. Provide drawables named
    // ic_quickaction_<name> in your project to use them.
    private static final String[] ICON_NAMES = {
            "", "compose", "play", "pause", "add", "location", "search", "share",
            "prohibit", "contact", "home", "mark_location", "favorite", "love",
            "cloud", "invitation", "confirmation", "mail", "message", "date",
            "time", "capture_photo", "capture_video", "task", "task_completed",
            "alarm", "bookmark", "shuffle", "audio", "update"
    };

    // In-memory queue of ids awaiting delivery to the C# Performed event.
    private static final ArrayDeque<String> sPending = new ArrayDeque<>();
    private static String sLastPerformed;

    private QuickActionsBridge() {
    }

    public static void setShortcuts(Activity activity, String json) {
        if (activity == null || Build.VERSION.SDK_INT < 25) return;
        ShortcutManager manager = activity.getSystemService(ShortcutManager.class);
        if (manager == null) return;

        List<ShortcutInfo> shortcuts = new ArrayList<>();
        try {
            JSONObject root = new JSONObject(json);
            JSONArray items = root.optJSONArray("items");
            if (items != null) {
                for (int i = 0; i < items.length(); i++) {
                    JSONObject item = items.optJSONObject(i);
                    if (item == null) continue;
                    ShortcutInfo shortcut = buildShortcut(activity, item);
                    if (shortcut != null) shortcuts.add(shortcut);
                }
            }
        } catch (Exception e) {
            android.util.Log.w("QuickActions", "Failed to parse shortcuts json", e);
            return;
        }

        // The OS cap covers manifest (static) + dynamic shortcuts combined, so
        // leave room for any static ones; otherwise setDynamicShortcuts throws.
        int budget = manager.getMaxShortcutCountPerActivity() - manager.getManifestShortcuts().size();
        if (budget < 0) budget = 0;
        if (shortcuts.size() > budget) {
            shortcuts = new ArrayList<>(shortcuts.subList(0, budget));
        }
        try {
            manager.setDynamicShortcuts(shortcuts);
        } catch (IllegalArgumentException | IllegalStateException e) {
            android.util.Log.w("QuickActions", "setDynamicShortcuts rejected", e);
        }
    }

    public static void removeAll(Activity activity) {
        if (activity == null || Build.VERSION.SDK_INT < 25) return;
        ShortcutManager manager = activity.getSystemService(ShortcutManager.class);
        if (manager != null) manager.removeAllDynamicShortcuts();
    }

    /**
     * The OS's current dynamic shortcuts as {"items":[...]} so the managed layer
     * can reconcile after a cold start. Icons aren't read back (reported as 0).
     */
    public static String getShortcutsJson(Activity activity) {
        JSONArray items = new JSONArray();
        if (activity != null && Build.VERSION.SDK_INT >= 25) {
            ShortcutManager manager = activity.getSystemService(ShortcutManager.class);
            if (manager != null) {
                for (ShortcutInfo s : manager.getDynamicShortcuts()) {
                    try {
                        JSONObject o = new JSONObject();
                        o.put("Id", s.getId());
                        CharSequence shortLabel = s.getShortLabel();
                        CharSequence longLabel = s.getLongLabel();
                        o.put("Title", shortLabel == null ? "" : shortLabel.toString());
                        o.put("Subtitle", longLabel == null ? "" : longLabel.toString());
                        o.put("Icon", 0);
                        o.put("AndroidDrawable", "");
                        items.put(o);
                    } catch (Exception e) {
                        android.util.Log.w("QuickActions", "Failed to read shortcut", e);
                    }
                }
            }
        }
        JSONObject root = new JSONObject();
        try {
            root.put("items", items);
        } catch (Exception e) {
            return "{\"items\":[]}";
        }
        return root.toString();
    }

    private static ShortcutInfo buildShortcut(Activity activity, JSONObject item) {
        String id = item.optString("Id", "");
        String title = item.optString("Title", "");
        if (id.isEmpty() || title.isEmpty()) return null;

        String subtitle = item.optString("Subtitle", "");

        Intent intent = new Intent(activity, QuickActionsTrampolineActivity.class);
        intent.setAction(Intent.ACTION_VIEW); // shortcut intents must declare an action
        intent.putExtra(EXTRA_ACTION_ID, id);

        ShortcutInfo.Builder builder = new ShortcutInfo.Builder(activity, id)
                .setShortLabel(title)
                .setLongLabel(subtitle.isEmpty() ? title : subtitle)
                .setIntent(intent);

        Icon icon = resolveIcon(activity, item);
        if (icon != null) builder.setIcon(icon);

        return builder.build();
    }

    private static Icon resolveIcon(Context context, JSONObject item) {
        String drawable = item.optString("AndroidDrawable", "");
        if (drawable.isEmpty()) {
            int iconType = item.optInt("Icon", 0);
            if (iconType > 0 && iconType < ICON_NAMES.length) {
                drawable = "ic_quickaction_" + ICON_NAMES[iconType];
            }
        }
        if (drawable.isEmpty()) return null;

        int resId = context.getResources().getIdentifier(drawable, "drawable", context.getPackageName());
        return resId != 0 ? Icon.createWithResource(context, resId) : null;
    }

    // ---- tap delivery (called by the trampoline activity) ----

    static synchronized void recordPerformed(String actionId) {
        if (actionId == null || actionId.isEmpty()) return;
        sLastPerformed = actionId;
        sPending.addLast(actionId);
    }

    // ---- queried by C# ----

    public static synchronized String consumePendingPerformed() {
        return sPending.isEmpty() ? null : sPending.pollFirst();
    }

    public static synchronized String getLastPerformed() {
        return sLastPerformed;
    }

    public static synchronized void resetLastPerformed() {
        sLastPerformed = null;
    }
}
