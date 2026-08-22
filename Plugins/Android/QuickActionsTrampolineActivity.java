package com.emindeniz99.quickactions;

import android.app.Activity;
import android.content.Intent;
import android.content.pm.ShortcutInfo;
import android.content.pm.ShortcutManager;
import android.os.Build;
import android.os.Bundle;

/**
 * Tiny, invisible activity that every quick-action intent targets. It records
 * the tapped action id, then hands control to the app's normal launcher
 * (the Unity activity) and finishes.
 *
 * Using a trampoline instead of subclassing Unity's activity keeps the plugin
 * working across Unity versions where the entry point differs
 * (UnityPlayerActivity in 2022 LTS vs UnityPlayerGameActivity in Unity 6+).
 * The Unity side reads the recorded id by polling on startup / focus, plus a
 * slow safety-net poll for activity implementations that surface neither a
 * focus nor an unpause event to scripting (Unity 6's GameActivity).
 *
 * The activity is standard launch mode and finishes inside onCreate, so every
 * tap arrives through a fresh onCreate (onNewIntent never fires here).
 */
public final class QuickActionsTrampolineActivity extends Activity {

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        handleIntent(getIntent());
        launchMainActivity();
        finish();
    }

    private void handleIntent(Intent intent) {
        if (intent == null) return;
        // Dynamic shortcuts pass the id as an extra; static (res/xml) shortcuts
        // encode it in the action suffix.
        String actionId = intent.getStringExtra(QuickActionsBridge.EXTRA_ACTION_ID);
        if (actionId == null) {
            String action = intent.getAction();
            if (action != null && action.startsWith(QuickActionsBridge.ACTION_PREFIX)) {
                actionId = action.substring(QuickActionsBridge.ACTION_PREFIX.length());
            }
        }
        // This activity is exported (the launcher must be able to start it), so any app
        // could launch it with an arbitrary ACTION_ID. Only record ids that correspond
        // to a REAL registered shortcut (dynamic or static/manifest) so a background app
        // can't spoof a "user performed X" signal into the game.
        if (isKnownShortcut(actionId)) {
            QuickActionsBridge.recordPerformed(actionId);
        } else if (actionId != null) {
            android.util.Log.w("QuickActions", "Ignored a trampoline intent for an unknown shortcut id");
        }
    }

    private boolean isKnownShortcut(String actionId) {
        if (actionId == null || actionId.isEmpty() || Build.VERSION.SDK_INT < 25) return false;
        ShortcutManager manager = getSystemService(ShortcutManager.class);
        if (manager == null) return false;
        try {
            // Dynamic ids must ALSO carry our ownership marker: under host
            // coexistence the dynamic set contains the host app's own shortcuts,
            // and without the marker check any app could spoof a Performed event
            // through this exported activity using a host shortcut's id.
            for (ShortcutInfo s : manager.getDynamicShortcuts())
                if (actionId.equals(s.getId()) && QuickActionsBridge.isOurShortcut(s)) return true;
            // Manifest (static) shortcuts can't carry extras, so ownership is
            // checked via the stored intent instead: the build baker encodes the id
            // in the intent action (ACTION_PREFIX + id), while a host app's own
            // static shortcuts carry their own actions — accepting those would let
            // another app forge a Performed event with a host id through this
            // exported activity. A null intent (some OEM read-backs) is accepted
            // conservatively so a genuine static tap is never dropped.
            for (ShortcutInfo s : manager.getManifestShortcuts()) {
                if (!actionId.equals(s.getId())) continue;
                Intent declared = s.getIntent();
                String declaredAction = declared == null ? null : declared.getAction();
                if (declaredAction == null || declaredAction.startsWith(QuickActionsBridge.ACTION_PREFIX))
                    return true;
            }
            // A shortcut the user PINNED stays launchable after it leaves the
            // dynamic set — its tap is legitimate, don't drop it. Ours keep the
            // marker when pinned (extras survive pinning); a pinned static id is
            // already covered by the manifest loop above. DISABLED pins (a
            // removed shortcut's ghost) don't count: the launcher blocks them,
            // so an intent with that id can only be a spoof.
            for (ShortcutInfo s : manager.getPinnedShortcuts())
                if (actionId.equals(s.getId()) && s.isEnabled() && QuickActionsBridge.isOurShortcut(s)) return true;
        } catch (RuntimeException e) {
            // Can't verify (e.g. locked device) — be conservative and drop it. A genuine
            // launcher tap happens after unlock, so this doesn't lose real taps.
            android.util.Log.w("QuickActions", "Could not validate shortcut id", e);
        }
        return false;
    }

    private void launchMainActivity() {
        // getLaunchIntentForPackage returns the app's MAIN/LAUNCHER intent with
        // FLAG_ACTIVITY_NEW_TASK | RESET_TASK_IF_NEEDED already set, which brings
        // the existing app task to the front (or starts it on a cold launch).
        // We use it as-is: adding REORDER_TO_FRONT would be ignored alongside
        // NEW_TASK and only muddy the behaviour.
        Intent launch = getPackageManager().getLaunchIntentForPackage(getPackageName());
        if (launch != null) {
            try {
                startActivity(launch);
            } catch (RuntimeException e) {
                // This runs on EVERY shortcut tap, so an uncaught throw here is a
                // process crash attributed to the game. ActivityNotFoundException
                // (host disabled its launcher component) and SecurityException
                // (OEM launch restrictions) are both reachable; every other native
                // entry point in this package already contains its exceptions.
                android.util.Log.w("QuickActions", "Could not launch the main activity", e);
            }
        }
    }
}
