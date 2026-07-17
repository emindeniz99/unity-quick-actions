package com.emindeniz99.quickactions;

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

    public static final String EXTRA_ACTION_ID = "com.emindeniz99.quickactions.ACTION_ID";

    // Static shortcuts declared in res/xml cannot carry intent extras, so their id
    // is encoded as the intent action suffix: ACTION_PREFIX + "<id>". Keep in sync
    // with the Android build post-processor.
    public static final String ACTION_PREFIX = "com.emindeniz99.quickactions.PERFORM.";

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

    /**
     * Replace the dynamic shortcuts with the given set (trimming to the OS cap).
     * Returns a {@code {"items":[{"Id":..}]}} payload listing exactly the ids that
     * were APPLIED, or {@code null} when the write did not land (parse error, OS
     * rejection/throw, or background rate-limiting). The managed layer prunes its
     * list only from a non-null result, so a failed write never causes it to drop
     * just-added shortcuts by misreading a stale device state.
     */
    public static String setShortcuts(Activity activity, String json) {
        if (activity == null || Build.VERSION.SDK_INT < 25) return null;
        ShortcutManager manager = activity.getSystemService(ShortcutManager.class);
        if (manager == null) return null;

        List<ShortcutInfo> shortcuts = new ArrayList<>();
        try {
            JSONObject root = new JSONObject(json);
            JSONArray items = root.optJSONArray("items");
            if (items != null) {
                for (int i = 0; i < items.length(); i++) {
                    JSONObject item = items.optJSONObject(i);
                    if (item == null) continue;
                    // Rank = position among the shortcuts we keep, so the launcher
                    // preserves insertion-order priority (it sorts by rank, not by the
                    // order passed to setDynamicShortcuts).
                    ShortcutInfo shortcut = buildShortcut(activity, item, shortcuts.size());
                    if (shortcut != null) shortcuts.add(shortcut);
                }
            }
        } catch (Exception e) {
            android.util.Log.w("QuickActions", "Failed to parse shortcuts json", e);
            return null;
        }

        // The OS cap covers manifest (static) + dynamic shortcuts combined, so
        // leave room for any static ones; otherwise setDynamicShortcuts throws.
        // getManifestShortcuts/setDynamicShortcuts can throw IllegalStateException
        // (e.g. user locked) — keep it all inside the guard so nothing crosses JNI.
        try {
            List<ShortcutInfo> manifest = manager.getManifestShortcuts();
            // Drop ids that collide with a manifest (static) shortcut. setDynamic
            // Shortcuts throws IllegalArgumentException on such a collision, which
            // would otherwise discard the ENTIRE dynamic set, not just the offender.
            if (manifest != null && !manifest.isEmpty()) {
                java.util.HashSet<String> manifestIds = new java.util.HashSet<>();
                for (ShortcutInfo s : manifest) manifestIds.add(s.getId());
                java.util.Iterator<ShortcutInfo> it = shortcuts.iterator();
                while (it.hasNext()) {
                    if (manifestIds.contains(it.next().getId())) {
                        it.remove();
                        android.util.Log.w("QuickActions",
                                "Dropped a dynamic shortcut whose id collides with a static/manifest shortcut");
                    }
                }
            }

            // The OS cap covers manifest (static) + dynamic shortcuts combined.
            int budget = manager.getMaxShortcutCountPerActivity() - (manifest == null ? 0 : manifest.size());
            if (budget < 0) budget = 0;
            if (shortcuts.size() > budget) {
                android.util.Log.w("QuickActions", "Trimmed dynamic shortcuts to fit the OS cap: kept "
                        + budget + " of " + shortcuts.size() + " (static/manifest shortcuts share the cap)");
                shortcuts = new ArrayList<>(shortcuts.subList(0, budget));
            }
            // setDynamicShortcuts returns false when background rate-limiting blocks the
            // update — the write did NOT land, so report a no-op (null) rather than the
            // set we hoped to apply.
            if (!manager.setDynamicShortcuts(shortcuts)) {
                android.util.Log.w("QuickActions", "setDynamicShortcuts was rate-limited; shortcuts not updated");
                return null;
            }
            return appliedIdsJson(shortcuts);
        } catch (RuntimeException e) {
            android.util.Log.w("QuickActions", "setDynamicShortcuts failed", e);
            return null;
        }
    }

    // Serialize the ids we actually applied as {"items":[{"Id":..}]} so the managed
    // layer can prune its list to exactly this set. Returns null if it can't build the
    // payload (better to report a no-op than risk pruning to a wrong set).
    private static String appliedIdsJson(List<ShortcutInfo> shortcuts) {
        try {
            JSONArray items = new JSONArray();
            for (ShortcutInfo s : shortcuts) {
                JSONObject o = new JSONObject();
                o.put("Id", s.getId());
                items.put(o);
            }
            JSONObject root = new JSONObject();
            root.put("items", items);
            return root.toString();
        } catch (Exception e) {
            android.util.Log.w("QuickActions", "Failed to build applied-ids json", e);
            return null;
        }
    }

    /**
     * Remove all dynamic shortcuts. Returns true when the OS state is now clear
     * (including when there is nothing to remove), false when the removal failed
     * (e.g. IllegalStateException on a locked profile) so the managed layer can
     * keep its list instead of falsely marking itself empty.
     */
    public static boolean removeAll(Activity activity) {
        if (activity == null || Build.VERSION.SDK_INT < 25) return true; // nothing to remove
        ShortcutManager manager = activity.getSystemService(ShortcutManager.class);
        if (manager == null) return true;
        try {
            manager.removeAllDynamicShortcuts();
            return true;
        } catch (RuntimeException e) {
            // e.g. IllegalStateException on a locked profile — never cross JNI.
            android.util.Log.w("QuickActions", "removeAllDynamicShortcuts failed", e);
            return false;
        }
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
                try {
                    // getDynamicShortcuts itself can throw IllegalStateException
                    // (e.g. a locked/background device) — keep it inside the guard
                    // so nothing crosses JNI; reconcile with whatever we collected.
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
                } catch (RuntimeException e) {
                    android.util.Log.w("QuickActions", "getDynamicShortcuts failed", e);
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

    private static ShortcutInfo buildShortcut(Activity activity, JSONObject item, int rank) {
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
                .setRank(rank) // launchers order dynamic shortcuts by rank, not list order
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
