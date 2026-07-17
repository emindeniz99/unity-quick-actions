package com.emindeniz99.quickactions;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.content.pm.ShortcutInfo;
import android.content.pm.ShortcutManager;
import android.graphics.drawable.Icon;
import android.os.Build;
import android.os.PersistableBundle;

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

    // Marks the dynamic shortcuts THIS package created — the same key string as the
    // iOS userInfo marker in Plugins/iOS/QuickActions.mm (keep them in sync). Every
    // write/remove/read below operates only on the marked subset, so a host app's
    // own ShortcutManager entries are never absorbed, republished, or removed.
    static final String MANAGED_MARKER_KEY = "com.emindeniz99.quickactions.managed";

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
     * True only for shortcuts carrying our extras marker (null-safe). The single
     * ownership test used by every gate here and by the trampoline's id check.
     * Extras survive OS persistence and publisher read-backs (only icons are
     * stripped), so the marker is stable across cold starts and reboots.
     */
    static boolean isOurShortcut(ShortcutInfo shortcut) {
        if (shortcut == null) return false;
        PersistableBundle extras = shortcut.getExtras();
        return extras != null && extras.getBoolean(MANAGED_MARKER_KEY, false);
    }

    /**
     * Replace THIS PACKAGE'S subset of the dynamic shortcuts with the given set
     * (trimming to the OS cap, which is shared with manifest shortcuts AND any
     * dynamic shortcuts other publishers — the host app itself — installed).
     * Host shortcuts are never modified: stale marked entries are removed via
     * {@code removeDynamicShortcuts} and the new set is added via
     * {@code addDynamicShortcuts}, never a full-set {@code setDynamicShortcuts}.
     * Returns a {@code {"items":[{"Id":..}]}} payload listing exactly the ids that
     * were APPLIED, or {@code null} when the write did not FULLY land (parse error,
     * OS rejection/throw, or background rate-limiting — in which case the stale
     * removals may already have applied; the managed layer reconciles on next
     * access). The managed layer prunes its list only from a non-null result, so a
     * failed write never causes it to drop just-added shortcuts by misreading a
     * stale device state.
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

            // Partition the CURRENT dynamic set into ours (marked) vs another
            // publisher's (unmarked — the host app's own shortcuts). Everything
            // below touches only our subset.
            java.util.HashSet<String> ourOsIds = new java.util.HashSet<>();
            java.util.HashSet<String> hostIds = new java.util.HashSet<>();
            List<ShortcutInfo> dynamic = manager.getDynamicShortcuts();
            if (dynamic != null) {
                for (ShortcutInfo s : dynamic) {
                    if (isOurShortcut(s)) ourOsIds.add(s.getId());
                    else hostIds.add(s.getId());
                }
            }

            // Drop our items whose id collides with a HOST dynamic shortcut:
            // addDynamicShortcuts updates same-id entries IN PLACE, which would
            // silently hijack the host's shortcut — the exact failure this
            // marker-scoping exists to prevent.
            java.util.Iterator<ShortcutInfo> ours = shortcuts.iterator();
            while (ours.hasNext()) {
                if (hostIds.contains(ours.next().getId())) {
                    ours.remove();
                    android.util.Log.w("QuickActions",
                            "Dropped a dynamic shortcut whose id collides with another publisher's dynamic shortcut");
                }
            }

            // The OS cap is per-activity and covers manifest (static) + ALL dynamic
            // shortcuts combined — the host's included. getManifestShortcuts() is
            // package-wide; a Unity app has a single main activity so this is exact.
            // (Edge case: a host that declares manifest shortcuts on OTHER main
            // activities would over-count here and under-fill the dynamic budget —
            // negligible for the single-activity apps this targets.)
            int budget = manager.getMaxShortcutCountPerActivity()
                    - (manifest == null ? 0 : manifest.size()) - hostIds.size();
            if (budget < 0) budget = 0;
            if (shortcuts.size() > budget) {
                android.util.Log.w("QuickActions", "Trimmed dynamic shortcuts to fit the OS cap: kept "
                        + budget + " of " + shortcuts.size() + " (static/manifest shortcuts share the cap"
                        + (hostIds.isEmpty() ? "" : "; " + hostIds.size() + " host shortcut(s) left less room") + ")");
                shortcuts = new ArrayList<>(shortcuts.subList(0, budget));
            }

            // Remove only OUR stale ids (present on the OS as ours, absent from the
            // new set). removeDynamicShortcuts is never rate-limited, so a blocked
            // add below can't resurrect them.
            List<String> stale = new ArrayList<>();
            java.util.HashSet<String> newIds = new java.util.HashSet<>();
            for (ShortcutInfo s : shortcuts) newIds.add(s.getId());
            for (String id : ourOsIds) {
                if (!newIds.contains(id)) stale.add(id);
            }
            if (!stale.isEmpty()) manager.removeDynamicShortcuts(stale);

            // Skip the add entirely when there is nothing to add: addDynamicShortcuts
            // with an empty list still burns a rate-limit token and can return false
            // AFTER the removals above landed, misreporting a successful clear as a
            // failed write. addDynamicShortcuts returns false when background
            // rate-limiting blocks the update — the adds did NOT land, so report a
            // no-op (null) rather than the set we hoped to apply.
            if (!shortcuts.isEmpty() && !manager.addDynamicShortcuts(shortcuts)) {
                android.util.Log.w("QuickActions", "addDynamicShortcuts was rate-limited; shortcuts not updated");
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
     * Remove the dynamic shortcuts THIS PACKAGE created (marker-scoped) — a host
     * app's own dynamic shortcuts are untouched. Returns true when our subset is
     * now clear (including when there is nothing to remove), false when the
     * removal failed (e.g. IllegalStateException on a locked profile) so the
     * managed layer can keep its list instead of falsely marking itself empty.
     */
    public static boolean removeAll(Activity activity) {
        if (activity == null || Build.VERSION.SDK_INT < 25) return true; // nothing to remove
        ShortcutManager manager = activity.getSystemService(ShortcutManager.class);
        if (manager == null) return true;
        try {
            List<String> ours = new ArrayList<>();
            List<ShortcutInfo> dynamic = manager.getDynamicShortcuts();
            if (dynamic != null) {
                for (ShortcutInfo s : dynamic) {
                    if (isOurShortcut(s)) ours.add(s.getId());
                }
            }
            if (!ours.isEmpty()) manager.removeDynamicShortcuts(ours);
            return true;
        } catch (RuntimeException e) {
            // e.g. IllegalStateException on a locked profile — never cross JNI.
            android.util.Log.w("QuickActions", "removeAll failed", e);
            return false;
        }
    }

    /**
     * The dynamic shortcuts THIS PACKAGE created (marker-scoped; a host app's own
     * shortcuts are never surfaced) as {"items":[...]} so the managed layer can
     * reconcile after a cold start. Icons aren't read back (reported as 0).
     * Returns {@code null} when the read did NOT succeed (unsupported / no manager /
     * a getDynamicShortcuts throw on a locked/direct-boot device), so the managed
     * layer can tell "the OS is genuinely empty" from "the read failed" and avoid
     * caching an errored-empty as the authoritative set (which would then wipe the
     * user's real shortcuts on the next write).
     */
    public static String getShortcutsJson(Activity activity) {
        if (activity == null || Build.VERSION.SDK_INT < 25) return null; // can't read
        ShortcutManager manager = activity.getSystemService(ShortcutManager.class);
        if (manager == null) return null;
        try {
            // getDynamicShortcuts itself can throw IllegalStateException (e.g. a
            // locked/direct-boot device); treat that as a failed read (return null),
            // NOT an empty result — keep it inside the guard so nothing crosses JNI.
            // Only OUR shortcuts (marked): absorbing a host's items here would make
            // the managed layer republish them with our trampoline intents and no
            // icons on the next push — corrupting the host's own deep links.
            List<ShortcutInfo> mine = new ArrayList<>();
            for (ShortcutInfo s : manager.getDynamicShortcuts()) {
                if (isOurShortcut(s)) mine.add(s);
            }
            // getDynamicShortcuts return order is unspecified; ranks encode the
            // caller's insertion order (setRank in buildShortcut). Sort by rank so a
            // cold-start reconcile can't scramble the order — the next push would
            // otherwise re-rank by the scrambled order and make it permanent.
            java.util.Collections.sort(mine, new java.util.Comparator<ShortcutInfo>() {
                @Override
                public int compare(ShortcutInfo a, ShortcutInfo b) {
                    return a.getRank() - b.getRank();
                }
            });
            JSONArray items = new JSONArray();
            for (ShortcutInfo s : mine) {
                JSONObject o = new JSONObject();
                o.put("Id", s.getId());
                CharSequence shortLabel = s.getShortLabel();
                CharSequence longLabel = s.getLongLabel();
                o.put("Title", shortLabel == null ? "" : shortLabel.toString());
                o.put("Subtitle", longLabel == null ? "" : longLabel.toString());
                o.put("Icon", 0);
                o.put("AndroidDrawable", "");
                items.put(o);
            }
            JSONObject root = new JSONObject();
            root.put("items", items);
            return root.toString();
        } catch (Exception e) {
            android.util.Log.w("QuickActions", "getShortcutsJson read failed", e);
            return null; // failed read — signal distinctly (not empty-success)
        }
    }

    private static ShortcutInfo buildShortcut(Activity activity, JSONObject item, int rank) {
        String id = item.optString("Id", "");
        String title = item.optString("Title", "");
        if (id.isEmpty() || title.isEmpty()) return null;

        String subtitle = item.optString("Subtitle", "");

        Intent intent = new Intent(activity, QuickActionsTrampolineActivity.class);
        intent.setAction(Intent.ACTION_VIEW); // shortcut intents must declare an action
        intent.putExtra(EXTRA_ACTION_ID, id);

        // Ownership marker (see MANAGED_MARKER_KEY): extras survive OS persistence
        // and read-backs, so every later write/remove/read can recognize this
        // shortcut as ours across cold starts and reboots.
        PersistableBundle extras = new PersistableBundle();
        extras.putBoolean(MANAGED_MARKER_KEY, true);

        ShortcutInfo.Builder builder = new ShortcutInfo.Builder(activity, id)
                .setShortLabel(title)
                .setRank(rank) // launchers order dynamic shortcuts by rank, not list order
                .setExtras(extras)
                .setIntent(intent);
        // Leave the long label UNSET when there's no subtitle, so a cold-restart
        // reconcile reads it back as "" (not the title) — matching iOS and the
        // caller's original empty Subtitle.
        if (!subtitle.isEmpty())
            builder.setLongLabel(subtitle);

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
