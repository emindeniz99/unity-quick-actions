package com.emindeniz99.quickactions;

// Stateful smoke test for the Java layer, run by tools/verify.sh check 4 after
// compiling against the .verify stubs. The C# NUnit suite pins the facade above
// the JNI seam; this pins the Java BELOW it — the host-coexistence branches
// (marker scoping, host-collision drop, host-aware budget, remove-then-add,
// skip-empty-add), the trampoline's ownership gate, and the null-vs-empty read
// contract — against stateful ShortcutManager/org.json stubs that mirror AOSP
// semantics. Plain main() + asserts: no framework, exit 1 on any failure.

import android.app.Activity;
import android.content.Intent;
import android.content.pm.ShortcutInfo;
import android.content.pm.ShortcutManager;
import android.os.Build;
import android.os.PersistableBundle;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.List;

public final class QuickActionsBridgeSmokeTest {

    private static int failures;
    private static int checks;

    public static void main(String[] args) throws Exception {
        Build.VERSION.SDK_INT = 30;

        coexistenceWriteKeepsHostAndReplacesOurs();
        hostAndManifestCollisionsAreDropped();
        budgetSubtractsManifestAndHost();
        removeAllIsMarkerScoped();
        readBackIsMarkerScopedAndRankOrdered();
        readBackNullVsEmpty();
        rateLimitReportsNull();
        emptySetSkipsAddAndStillClears();
        parseFailureReportsNull();
        pinnedForeignIdsAreDroppedButDontShrinkBudget();
        removedPinnedCopiesAreDisabledAndReAddReEnables();
        iconIdentityRoundTripsThroughExtras();
        trampolineAcceptsOursManifestAndPinnedOnly();
        bitmapIconIsChosenAndFallsBackWhenUndecodable();
        payloadRoundTripsThroughExtrasAndIntent();
        pinRequestIsOwnershipGated();
        maxShortcutCountIsExposed();
        adaptiveAndPinDegradeBelowApi26();
        usageReportIsOwnershipGated();

        System.out.println("SMOKE: " + (failures == 0 ? "PASS" : "FAIL") + " (" + checks + " checks, " + failures + " failed)");
        if (failures != 0) System.exit(1);
    }

    // ---- scenarios ----

    private static void coexistenceWriteKeepsHostAndReplacesOurs() throws Exception {
        ShortcutManager mgr = new ShortcutManager();
        ShortcutInfo host = host("h1");
        mgr.dynamic.add(host);
        mgr.dynamic.add(ours("stale", 0));

        String applied = QuickActionsBridge.setShortcuts(activity(mgr), itemsJson("a", "b"));

        check(idsOf(applied).equals(List.of("a", "b")), "applied ids are exactly a,b: " + applied);
        check(containsSame(mgr.dynamic, host), "the host's own ShortcutInfo instance survives the write untouched");
        check(!hasId(mgr.dynamic, "stale"), "our stale marked entry was removed");
        check(hasId(mgr.dynamic, "a") && hasId(mgr.dynamic, "b"), "the new set landed");
        check(QuickActionsBridge.isOurShortcut(byId(mgr.dynamic, "a")), "written entries carry the ownership marker");
        check(!QuickActionsBridge.isOurShortcut(byId(mgr.dynamic, "h1")), "the host entry stays unmarked");
    }

    private static void hostAndManifestCollisionsAreDropped() throws Exception {
        ShortcutManager mgr = new ShortcutManager();
        ShortcutInfo host = host("h1");
        mgr.dynamic.add(host);
        mgr.manifest.add(host("m1"));

        String applied = QuickActionsBridge.setShortcuts(activity(mgr), itemsJson("h1", "m1", "c"));

        check(idsOf(applied).equals(List.of("c")), "colliding ids h1 (host) and m1 (manifest) dropped, c applied: " + applied);
        check(containsSame(mgr.dynamic, host), "the host's colliding shortcut was NOT hijacked in place");
        check(!QuickActionsBridge.isOurShortcut(byId(mgr.dynamic, "h1")), "host h1 still unmarked after the write");
    }

    private static void budgetSubtractsManifestAndHost() throws Exception {
        ShortcutManager mgr = new ShortcutManager(); // cap 4
        mgr.manifest.add(host("m1"));
        mgr.dynamic.add(host("h1"));

        String applied = QuickActionsBridge.setShortcuts(activity(mgr), itemsJson("a", "b", "c"));

        check(idsOf(applied).equals(List.of("a", "b")), "budget 4-1-1=2 keeps first two: " + applied);
        check(!hasId(mgr.dynamic, "c"), "trimmed item never reached the OS");
        check(hasId(mgr.dynamic, "h1"), "host item still present after the trim");
    }

    private static void removeAllIsMarkerScoped() {
        ShortcutManager mgr = new ShortcutManager();
        ShortcutInfo host = host("h1");
        mgr.dynamic.add(host);
        mgr.dynamic.add(ours("mine", 0));

        boolean ok = QuickActionsBridge.removeAll(activity(mgr));

        check(ok, "removeAll reports success");
        check(containsSame(mgr.dynamic, host), "removeAll keeps the host's shortcut");
        check(!hasId(mgr.dynamic, "mine"), "removeAll removes our marked shortcut");
    }

    private static void readBackIsMarkerScopedAndRankOrdered() throws Exception {
        ShortcutManager mgr = new ShortcutManager();
        mgr.dynamic.add(ours("second", 1));      // stored out of rank order on purpose
        mgr.dynamic.add(host("h1"));
        mgr.dynamic.add(oursWithSubtitle("first", 0, "Sub"));

        String json = QuickActionsBridge.getShortcutsJson(activity(mgr));

        JSONArray items = new JSONObject(json).optJSONArray("items");
        check(items != null && items.length() == 2, "read-back has exactly our two items: " + json);
        check("first".equals(items.optJSONObject(0).optString("Id", "")), "rank 0 comes first despite storage order");
        check("second".equals(items.optJSONObject(1).optString("Id", "")), "rank 1 comes second");
        check("Sub".equals(items.optJSONObject(0).optString("Subtitle", "?")), "subtitle round-trips");
        check("".equals(items.optJSONObject(1).optString("Subtitle", "?")), "no-subtitle item reads back empty (long label unset)");
    }

    private static void readBackNullVsEmpty() {
        ShortcutManager mgr = new ShortcutManager();
        mgr.dynamic.add(host("h1"));
        String onlyHost = QuickActionsBridge.getShortcutsJson(activity(mgr));
        check(onlyHost != null && onlyHost.contains("\"items\":[]"), "host-only device reads back EMPTY (success), not null: " + onlyHost);

        check(QuickActionsBridge.getShortcutsJson(activity(null)) == null, "no ShortcutManager -> null (failed read)");
        check(QuickActionsBridge.getShortcutsJson(null) == null, "no activity -> null (failed read)");

        Build.VERSION.SDK_INT = 24;
        check(QuickActionsBridge.getShortcutsJson(activity(mgr)) == null, "API<25 -> null (can't read)");
        Build.VERSION.SDK_INT = 30;
    }

    private static void rateLimitReportsNull() throws Exception {
        ShortcutManager mgr = new ShortcutManager();
        mgr.dynamic.add(ours("stale", 0));
        mgr.rateLimited = true;

        String applied = QuickActionsBridge.setShortcuts(activity(mgr), itemsJson("a"));

        check(applied == null, "rate-limited add reports null (write did not fully land)");
        check(!hasId(mgr.dynamic, "a"), "blocked add left the new item off the OS");
        check(!hasId(mgr.dynamic, "stale"), "stale removal had already landed (documented atomicity window)");
    }

    private static void emptySetSkipsAddAndStillClears() throws Exception {
        ShortcutManager mgr = new ShortcutManager();
        ShortcutInfo host = host("h1");
        mgr.dynamic.add(host);
        mgr.dynamic.add(ours("stale", 0));
        mgr.rateLimited = true; // an empty add would return false and misreport the clear

        String applied = QuickActionsBridge.setShortcuts(activity(mgr), itemsJson());

        check(applied != null && idsOf(applied).isEmpty(), "empty set skips the add and reports success: " + applied);
        check(!hasId(mgr.dynamic, "stale"), "our stale entry was still cleared");
        check(containsSame(mgr.dynamic, host), "host survives an empty-set write");
    }

    private static void parseFailureReportsNull() {
        ShortcutManager mgr = new ShortcutManager();
        check(QuickActionsBridge.setShortcuts(activity(mgr), "not json") == null, "unparseable payload -> null");
        check(mgr.dynamic.isEmpty(), "unparseable payload changed nothing");
    }

    private static void pinnedForeignIdsAreDroppedButDontShrinkBudget() throws Exception {
        ShortcutManager mgr = new ShortcutManager(); // cap 4
        ShortcutInfo hostPinned = host("share"); // pinned-only, NOT dynamic — e.g. host removed it after the user pinned it
        mgr.pinned.add(hostPinned);
        mgr.pinned.add(ours("ourpin", 0)); // OUR pinned id must stay writable

        String applied = QuickActionsBridge.setShortcuts(activity(mgr), itemsJson("share", "a", "b", "c", "ourpin"));

        // 'share' dropped (real addDynamicShortcuts would update the host's PINNED
        // entry in place — hijack — or throw for an immutable ex-manifest pin);
        // 'ourpin' (marked) stays; pinned-only entries don't consume the dynamic
        // cap, so the budget is the full 4: a,b,c,ourpin all fit.
        check(idsOf(applied).equals(List.of("a", "b", "c", "ourpin")), "foreign pinned id dropped, ours kept, budget untouched: " + applied);
        check(!hasId(mgr.dynamic, "share"), "the foreign pinned id was never written dynamically");
        check(containsSame(mgr.pinned, hostPinned), "the host's pinned instance is untouched");
    }

    private static void removedPinnedCopiesAreDisabledAndReAddReEnables() throws Exception {
        ShortcutManager mgr = new ShortcutManager();
        ShortcutInfo ourPin = ours("fav", 0);
        ShortcutInfo hostPin = host("hpin");
        mgr.dynamic.add(ourPin);
        mgr.pinned.add(ourPin);   // the user pinned our shortcut
        mgr.pinned.add(hostPin);

        // Replacement set drops 'fav' -> its pinned copy must be DISABLED (not a
        // live ghost that keeps firing Performed), host pin untouched.
        check(QuickActionsBridge.setShortcuts(activity(mgr), itemsJson("other")) != null, "replacement write lands");
        check(!hasId(mgr.dynamic, "fav"), "removed id left the dynamic set");
        check(!ourPin.isEnabled(), "our removed pinned copy is disabled");
        check(hostPin.isEnabled(), "a host pinned shortcut is never disabled");

        // The trampoline must now reject the disabled pin (launcher blocks it;
        // an intent with that id can only be a spoof).
        QuickActionsTrampolineActivity t = new QuickActionsTrampolineActivity();
        t.testSystemService = mgr;
        java.lang.reflect.Method handle = QuickActionsTrampolineActivity.class.getDeclaredMethod("handleIntent", Intent.class);
        handle.setAccessible(true);
        handle.invoke(t, new Intent().putExtra(QuickActionsBridge.EXTRA_ACTION_ID, "fav"));
        check(QuickActionsBridge.consumePendingPerformed() == null, "a disabled pinned id is rejected by the trampoline");

        // Re-adding the id must re-enable the pinned copy (addDynamicShortcuts
        // alone cannot resurrect a disabled pin).
        check(QuickActionsBridge.setShortcuts(activity(mgr), itemsJson("fav")) != null, "re-add write lands");
        check(ourPin.isEnabled(), "re-added id re-enables its pinned copy");
        check(hasId(mgr.dynamic, "fav"), "re-added id is dynamic again");

        // RemoveAll disables our pin as well; host pin still untouched.
        check(QuickActionsBridge.removeAll(activity(mgr)), "removeAll succeeds");
        check(!ourPin.isEnabled(), "removeAll disables our pinned copy");
        check(hostPin.isEnabled(), "removeAll leaves host pins alone");

        // Compensating action: a re-add whose add phase is RATE-LIMITED must not
        // leave the pin enabled — Java re-enables before the add (a disabled id
        // can't be re-published), so a failed add re-disables it, keeping the
        // launcher consistent with the "nothing was added" result.
        mgr.rateLimited = true;
        check(QuickActionsBridge.setShortcuts(activity(mgr), itemsJson("fav")) == null, "rate-limited re-add reports null");
        check(!ourPin.isEnabled(), "failed re-add re-disables the pin (no live ghost)");
        mgr.rateLimited = false;
    }

    private static void iconIdentityRoundTripsThroughExtras() throws Exception {
        ShortcutManager mgr = new ShortcutManager();
        String json = "{\"items\":[{\"Id\":\"i1\",\"Title\":\"T\",\"Subtitle\":\"\",\"Icon\":5,\"AndroidDrawable\":\"\"},"
                + "{\"Id\":\"i2\",\"Title\":\"T\",\"Subtitle\":\"\",\"Icon\":0,\"AndroidDrawable\":\"my_icon\"}]}";
        check(QuickActionsBridge.setShortcuts(activity(mgr), json) != null, "icon write lands");

        // Simulate the cold start: read back and verify the icon identity survives
        // via the marker extras (the OS itself can't read icons back) — the next
        // push would otherwise strip the launcher-visible icons.
        JSONArray items = new JSONObject(QuickActionsBridge.getShortcutsJson(activity(mgr))).optJSONArray("items");
        check(items.optJSONObject(0).optInt("Icon", -1) == 5, "IconType round-trips through extras");
        check("my_icon".equals(items.optJSONObject(1).optString("AndroidDrawable", "?")), "AndroidDrawable round-trips through extras");
    }

    private static void trampolineAcceptsOursManifestAndPinnedOnly() throws Exception {
        ShortcutManager mgr = new ShortcutManager();
        mgr.dynamic.add(ours("a", 0));
        mgr.dynamic.add(host("h1"));
        mgr.manifest.add(host("m1"));          // manifest ids are accepted unmarked (res/xml can't carry extras)
        mgr.pinned.add(ours("pinned", 0));     // ours, pinned, no longer dynamic
        mgr.pinned.add(host("hostpin"));

        QuickActionsTrampolineActivity t = new QuickActionsTrampolineActivity();
        t.testSystemService = mgr;
        java.lang.reflect.Method handle = QuickActionsTrampolineActivity.class.getDeclaredMethod("handleIntent", Intent.class);
        handle.setAccessible(true);

        handle.invoke(t, new Intent().putExtra(QuickActionsBridge.EXTRA_ACTION_ID, "a"));
        check("a".equals(QuickActionsBridge.consumePendingPerformed()), "our dynamic id is recorded");

        handle.invoke(t, new Intent().putExtra(QuickActionsBridge.EXTRA_ACTION_ID, "h1"));
        check(QuickActionsBridge.consumePendingPerformed() == null, "a HOST dynamic id is rejected (spoof gate)");

        handle.invoke(t, new Intent().setAction(QuickActionsBridge.ACTION_PREFIX + "m1"));
        check("m1".equals(QuickActionsBridge.consumePendingPerformed()), "a manifest id via the action-encoded path is recorded (null intent = conservative accept)");

        // Manifest ownership: OUR baked statics encode the id in the intent action;
        // a host's own static shortcut carries a foreign action and must be rejected
        // (another app could otherwise forge Performed with the host's id).
        mgr.manifest.add(new ShortcutInfo.Builder(null, "ourstatic").setShortLabel("s")
                .setIntent(new Intent().setAction(QuickActionsBridge.ACTION_PREFIX + "ourstatic")).build());
        mgr.manifest.add(new ShortcutInfo.Builder(null, "hoststatic").setShortLabel("s")
                .setIntent(new Intent().setAction("com.host.OPEN_SETTINGS")).build());
        handle.invoke(t, new Intent().setAction(QuickActionsBridge.ACTION_PREFIX + "ourstatic"));
        check("ourstatic".equals(QuickActionsBridge.consumePendingPerformed()), "our baked static (prefix-action intent) is recorded");
        handle.invoke(t, new Intent().setAction(QuickActionsBridge.ACTION_PREFIX + "hoststatic"));
        check(QuickActionsBridge.consumePendingPerformed() == null, "a host static id (foreign-action intent) is rejected");

        handle.invoke(t, new Intent().putExtra(QuickActionsBridge.EXTRA_ACTION_ID, "pinned"));
        check("pinned".equals(QuickActionsBridge.consumePendingPerformed()), "our PINNED id is recorded after leaving the dynamic set");

        handle.invoke(t, new Intent().putExtra(QuickActionsBridge.EXTRA_ACTION_ID, "hostpin"));
        check(QuickActionsBridge.consumePendingPerformed() == null, "a host pinned id is rejected");

        handle.invoke(t, new Intent().putExtra(QuickActionsBridge.EXTRA_ACTION_ID, "unknown"));
        check(QuickActionsBridge.consumePendingPerformed() == null, "an unknown id is rejected");

        QuickActionsBridge.resetLastPerformed();
    }

    private static void bitmapIconIsChosenAndFallsBackWhenUndecodable() throws Exception {
        ShortcutManager mgr = new ShortcutManager();
        // A real temp file = decodable per the BitmapFactory stub; a missing path
        // must fall back down the icon chain instead of erroring the whole write.
        java.io.File png = java.io.File.createTempFile("qa_icon", ".png");
        String json = "{\"items\":["
                + "{\"Id\":\"b1\",\"Title\":\"T\",\"AndroidBitmapFile\":\"" + png.getAbsolutePath() + "\"},"
                + "{\"Id\":\"b2\",\"Title\":\"T\",\"AndroidBitmapFile\":\"" + png.getAbsolutePath() + "\",\"AndroidBitmapAdaptive\":true},"
                + "{\"Id\":\"b3\",\"Title\":\"T\",\"AndroidBitmapFile\":\"/nonexistent/qa.png\",\"Icon\":5}]}";
        check(QuickActionsBridge.setShortcuts(activity(mgr), json) != null, "bitmap-icon write lands");
        check(byId(mgr.dynamic, "b1").icon != null && "bitmap".equals(byId(mgr.dynamic, "b1").icon.kind),
                "existing file becomes a bitmap icon");
        check(byId(mgr.dynamic, "b2").icon != null && "adaptive".equals(byId(mgr.dynamic, "b2").icon.kind),
                "adaptive flag selects createWithAdaptiveBitmap");
        // b3: missing file falls back to the IconType catalog — but the catalog
        // drawable isn't registered in the stub resources, so no icon results;
        // the write itself must still land (fallback, not failure).
        check(hasId(mgr.dynamic, "b3"), "undecodable bitmap still installs the shortcut (icon falls back)");
        // Identity round-trips for the reconcile push.
        JSONArray items = new JSONObject(QuickActionsBridge.getShortcutsJson(activity(mgr))).optJSONArray("items");
        JSONObject b2 = null;
        for (int i = 0; i < items.length(); i++)
            if ("b2".equals(items.optJSONObject(i).optString("Id", ""))) b2 = items.optJSONObject(i);
        check(b2 != null && png.getAbsolutePath().equals(b2.optString("AndroidBitmapFile", "?")),
                "bitmap path round-trips through extras");
        check(b2 != null && b2.optBoolean("AndroidBitmapAdaptive", false),
                "adaptive flag round-trips through extras");
        png.delete();
    }

    private static void payloadRoundTripsThroughExtrasAndIntent() throws Exception {
        ShortcutManager mgr = new ShortcutManager();
        String json = "{\"items\":[{\"Id\":\"p1\",\"Title\":\"T\",\"Payload\":\"level=7\"}]}";
        check(QuickActionsBridge.setShortcuts(activity(mgr), json) != null, "payload write lands");
        ShortcutInfo p1 = byId(mgr.dynamic, "p1");
        check("level=7".equals(p1.getExtras().getString(QuickActionsBridge.EXTRA_PAYLOAD, "?")),
                "payload persisted in the marker extras");
        check("level=7".equals(p1.getIntent().getStringExtra(QuickActionsBridge.EXTRA_PAYLOAD)),
                "payload rides the launch intent (readable by a host-side receiver)");
        // Cold-start reconcile must hand the payload back to C# (GetById contract).
        JSONArray items = new JSONObject(QuickActionsBridge.getShortcutsJson(activity(mgr))).optJSONArray("items");
        check("level=7".equals(items.optJSONObject(0).optString("Payload", "?")),
                "payload round-trips through the read-back");
    }

    private static void pinRequestIsOwnershipGated() throws Exception {
        ShortcutManager mgr = new ShortcutManager();
        mgr.dynamic.add(host("h1"));
        check(QuickActionsBridge.setShortcuts(activity(mgr), itemsJson("mine")) != null, "pin fixture write lands");

        check(QuickActionsBridge.isPinSupported(activity(mgr)), "pin support reported when the launcher supports it");
        check(QuickActionsBridge.requestPinShortcut(activity(mgr), "mine"), "pinning OUR dynamic id dispatches");
        check(mgr.pinRequests.equals(List.of("mine")), "exactly the requested id reached the launcher");

        check(!QuickActionsBridge.requestPinShortcut(activity(mgr), "h1"),
                "a HOST shortcut can never be pinned through this package (ownership gate)");
        check(!QuickActionsBridge.requestPinShortcut(activity(mgr), "ghost"), "an uninstalled id is refused");
        check(mgr.pinRequests.size() == 1, "refused requests never reach the launcher");

        mgr.pinSupported = false;
        check(!QuickActionsBridge.isPinSupported(activity(mgr)), "unsupported launcher reports false");
        check(!QuickActionsBridge.requestPinShortcut(activity(mgr), "mine"), "unsupported launcher refuses the request");
    }

    private static void maxShortcutCountIsExposed() {
        ShortcutManager mgr = new ShortcutManager();
        mgr.maxShortcutCountPerActivity = 6;
        check(QuickActionsBridge.getMaxShortcutCount(activity(mgr)) == 6, "OS cap is exposed as-is");
        check(QuickActionsBridge.getMaxShortcutCount(null) == 0, "no activity reports 0");
    }

    private static void adaptiveAndPinDegradeBelowApi26() throws Exception {
        // createWithAdaptiveBitmap and requestPinShortcut are API 26+; on API 25
        // (the package's minimum, where ShortcutManager itself exists) the
        // adaptive flag must degrade to a plain bitmap and pinning must refuse —
        // NOT throw or dispatch. The SDK guards in QuickActionsBridge.java are the
        // code under test here.
        ShortcutManager mgr = new ShortcutManager();
        java.io.File png = java.io.File.createTempFile("qa_icon25", ".png");
        int prev = Build.VERSION.SDK_INT;
        Build.VERSION.SDK_INT = 25;
        try {
            String json = "{\"items\":[{\"Id\":\"a25\",\"Title\":\"T\","
                    + "\"AndroidBitmapFile\":\"" + png.getAbsolutePath() + "\",\"AndroidBitmapAdaptive\":true}]}";
            check(QuickActionsBridge.setShortcuts(activity(mgr), json) != null, "API 25 write lands");
            check(byId(mgr.dynamic, "a25").icon != null && "bitmap".equals(byId(mgr.dynamic, "a25").icon.kind),
                    "adaptive flag degrades to a plain bitmap below API 26");
            check(!QuickActionsBridge.isPinSupported(activity(mgr)),
                    "pin support reports false below API 26 even on a pin-capable launcher");
            check(!QuickActionsBridge.requestPinShortcut(activity(mgr), "a25"),
                    "pin request is refused below API 26");
            check(mgr.pinRequests.isEmpty(), "no refused pin request reaches the launcher");
        } finally {
            Build.VERSION.SDK_INT = prev;
            png.delete();
        }
    }

    private static void usageReportIsOwnershipGated() throws Exception {
        ShortcutManager mgr = new ShortcutManager();
        mgr.dynamic.add(host("h1"));
        check(QuickActionsBridge.setShortcuts(activity(mgr), itemsJson("mine")) != null, "usage fixture write lands");

        check(QuickActionsBridge.reportShortcutUsed(activity(mgr), "mine"), "reporting OUR id reaches the launcher");
        check(mgr.usageReports.equals(List.of("mine")), "exactly the reported id was forwarded");
        check(!QuickActionsBridge.reportShortcutUsed(activity(mgr), "h1"),
                "a HOST id is refused (would skew the host's launcher ranking)");
        check(!QuickActionsBridge.reportShortcutUsed(activity(mgr), "ghost"), "an uninstalled id is refused");
        check(mgr.usageReports.size() == 1, "refused reports never reach the launcher");

        // A user-pinned copy of OURS that left the dynamic set is still a live
        // launcher entry — AOSP accepts usage reports for it (parity with the
        // trampoline's pinned-ours acceptance). A host pin stays refused.
        mgr.pinned.add(ours("pinnedmine", 0));
        mgr.pinned.add(host("hostpin"));
        check(QuickActionsBridge.reportShortcutUsed(activity(mgr), "pinnedmine"), "OUR pinned-only id is accepted");
        check(!QuickActionsBridge.reportShortcutUsed(activity(mgr), "hostpin"), "a host pinned id is refused");

        // Locked-user throw must degrade to false, never cross JNI.
        mgr.throwOnUsageReport = true;
        check(!QuickActionsBridge.reportShortcutUsed(activity(mgr), "mine"), "a native throw degrades to false");
        mgr.throwOnUsageReport = false;

        // Below API 25 ShortcutManager doesn't exist — guard reports false.
        int prev = Build.VERSION.SDK_INT;
        Build.VERSION.SDK_INT = 24;
        check(!QuickActionsBridge.reportShortcutUsed(activity(mgr), "mine"), "below API 25 reports false");
        Build.VERSION.SDK_INT = prev;
    }

    // ---- helpers ----

    private static void check(boolean condition, String what) {
        checks++;
        if (!condition) {
            failures++;
            System.err.println("FAIL: " + what);
        }
    }

    private static Activity activity(ShortcutManager mgr) {
        Activity a = new Activity();
        a.testSystemService = mgr;
        return a;
    }

    private static ShortcutInfo host(String id) {
        // Another publisher's shortcut: NO ownership marker.
        return new ShortcutInfo.Builder(null, id).setShortLabel(id).build();
    }

    private static ShortcutInfo ours(String id, int rank) {
        PersistableBundle extras = new PersistableBundle();
        extras.putBoolean(QuickActionsBridge.MANAGED_MARKER_KEY, true);
        return new ShortcutInfo.Builder(null, id).setShortLabel(id).setRank(rank).setExtras(extras).build();
    }

    private static ShortcutInfo oursWithSubtitle(String id, int rank, String subtitle) {
        PersistableBundle extras = new PersistableBundle();
        extras.putBoolean(QuickActionsBridge.MANAGED_MARKER_KEY, true);
        return new ShortcutInfo.Builder(null, id).setShortLabel(id).setLongLabel(subtitle).setRank(rank).setExtras(extras).build();
    }

    private static String itemsJson(String... ids) throws Exception {
        JSONArray items = new JSONArray();
        for (String id : ids) {
            JSONObject o = new JSONObject();
            o.put("Id", id);
            o.put("Title", "T " + id);
            o.put("Subtitle", "");
            o.put("Icon", 0);
            o.put("AndroidDrawable", "");
            items.put(o);
        }
        return new JSONObject().put("items", items).toString();
    }

    private static List<String> idsOf(String appliedJson) throws Exception {
        List<String> ids = new ArrayList<>();
        JSONArray items = new JSONObject(appliedJson).optJSONArray("items");
        for (int i = 0; items != null && i < items.length(); i++)
            ids.add(items.optJSONObject(i).optString("Id", ""));
        return ids;
    }

    private static boolean hasId(List<ShortcutInfo> list, String id) { return byId(list, id) != null; }

    private static ShortcutInfo byId(List<ShortcutInfo> list, String id) {
        for (ShortcutInfo s : list) if (id.equals(s.getId())) return s;
        return null;
    }

    private static boolean containsSame(List<ShortcutInfo> list, ShortcutInfo instance) {
        for (ShortcutInfo s : list) if (s == instance) return true;
        return false;
    }

    private QuickActionsBridgeSmokeTest() { }
}
