package android.content.pm; import java.util.List; import java.util.ArrayList;
// Stateful test double for the .verify smoke test. The compile surface matches
// the real API; the behavior mirrors the AOSP semantics the plugin relies on:
// setDynamicShortcuts replaces, addDynamicShortcuts updates same-id entries IN
// PLACE (else appends), removeDynamicShortcuts is never rate-limited, getters
// return copies. Never shipped.
public class ShortcutManager {
  public int maxShortcutCountPerActivity = 4;
  public boolean rateLimited = false;
  public final List<ShortcutInfo> dynamic = new ArrayList<>();
  public final List<ShortcutInfo> manifest = new ArrayList<>();
  public final List<ShortcutInfo> pinned = new ArrayList<>();
  public int getMaxShortcutCountPerActivity(){return maxShortcutCountPerActivity;}
  public boolean setDynamicShortcuts(List<ShortcutInfo> s){
    if (rateLimited) return false;
    dynamic.clear(); dynamic.addAll(s); return true;
  }
  public boolean addDynamicShortcuts(List<ShortcutInfo> s){
    if (rateLimited) return false;
    for (ShortcutInfo n : s) {
      boolean replaced = false;
      for (int i = 0; i < dynamic.size(); i++) {
        if (dynamic.get(i).getId().equals(n.getId())) { dynamic.set(i, n); replaced = true; break; }
      }
      if (!replaced) dynamic.add(n);
    }
    return true;
  }
  public void removeDynamicShortcuts(List<String> ids){
    dynamic.removeIf(s -> ids.contains(s.getId()));
  }
  public void removeAllDynamicShortcuts(){ dynamic.clear(); }
  public void disableShortcuts(java.util.List<String> ids){
    // AOSP: removes matching dynamic entries and greys out pinned copies.
    dynamic.removeIf(s -> ids.contains(s.getId()));
    for (ShortcutInfo s : pinned) if (ids.contains(s.getId())) s.enabled = false;
  }
  public void enableShortcuts(java.util.List<String> ids){
    for (ShortcutInfo s : pinned) if (ids.contains(s.getId())) s.enabled = true;
  }
  public List<ShortcutInfo> getManifestShortcuts(){return new ArrayList<>(manifest);}
  public List<ShortcutInfo> getDynamicShortcuts(){return new ArrayList<>(dynamic);}
  public List<ShortcutInfo> getPinnedShortcuts(){return new ArrayList<>(pinned);}
  // Usage-report surface: AOSP just forwards the id to the launcher's ranker —
  // but can throw IllegalStateException (locked user), so the smoke test can
  // toggle that to pin the bridge's never-throws-across-JNI guarantee.
  public boolean throwOnUsageReport = false;
  public final List<String> usageReports = new ArrayList<>();
  public void reportShortcutUsed(String id){
    if (throwOnUsageReport) throw new IllegalStateException("user is locked");
    usageReports.add(id);
  }
  // Pin-request surface (API 26+). AOSP: requestPinShortcut hands the request to
  // the launcher and returns true when dispatched; the smoke test records it.
  public boolean pinSupported = true;
  public final List<String> pinRequests = new ArrayList<>();
  public boolean isRequestPinShortcutSupported(){return pinSupported;}
  public boolean requestPinShortcut(ShortcutInfo s, android.content.IntentSender sender){
    if (!pinSupported) return false;
    pinRequests.add(s.getId());
    return true;
  }
}
