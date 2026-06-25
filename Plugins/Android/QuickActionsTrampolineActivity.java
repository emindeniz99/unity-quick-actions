package com.playground.quickactions;

import android.app.Activity;
import android.content.Intent;
import android.os.Bundle;

/**
 * Tiny, invisible activity that every quick-action intent targets. It records
 * the tapped action id, then hands control to the app's normal launcher
 * (the Unity activity) and finishes.
 *
 * Using a trampoline instead of subclassing Unity's activity keeps the plugin
 * working across Unity versions where the entry point differs
 * (UnityPlayerActivity in 2022 LTS vs UnityPlayerGameActivity in Unity 6+).
 * The Unity side reads the recorded id by polling on startup / focus.
 */
public final class QuickActionsTrampolineActivity extends Activity {

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        handleIntent(getIntent());
        launchMainActivity();
        finish();
    }

    @Override
    protected void onNewIntent(Intent intent) {
        super.onNewIntent(intent);
        setIntent(intent);
        handleIntent(intent);
        launchMainActivity();
        finish();
    }

    private void handleIntent(Intent intent) {
        if (intent == null) return;
        String actionId = intent.getStringExtra(QuickActionsBridge.EXTRA_ACTION_ID);
        QuickActionsBridge.recordPerformed(actionId);
    }

    private void launchMainActivity() {
        Intent launch = getPackageManager().getLaunchIntentForPackage(getPackageName());
        if (launch == null) return;
        // Bring the existing app task to the front without recreating it when the
        // app is already running; start it fresh on a cold launch.
        launch.addFlags(Intent.FLAG_ACTIVITY_REORDER_TO_FRONT | Intent.FLAG_ACTIVITY_NEW_TASK);
        startActivity(launch);
    }
}
