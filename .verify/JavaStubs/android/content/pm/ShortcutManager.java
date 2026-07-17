package android.content.pm; import java.util.List; import java.util.ArrayList;
public class ShortcutManager {
  public int getMaxShortcutCountPerActivity(){return 4;}
  public boolean setDynamicShortcuts(List<ShortcutInfo> s){return true;}
  public void removeAllDynamicShortcuts(){}
  public List<ShortcutInfo> getManifestShortcuts(){return new ArrayList<ShortcutInfo>();}
  public List<ShortcutInfo> getDynamicShortcuts(){return new ArrayList<ShortcutInfo>();}
}
