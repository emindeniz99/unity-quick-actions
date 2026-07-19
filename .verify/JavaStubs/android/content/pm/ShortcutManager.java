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
}
