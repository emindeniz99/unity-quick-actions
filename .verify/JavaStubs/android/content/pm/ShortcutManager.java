package android.content.pm; import java.util.List; import java.util.ArrayList;
public class ShortcutManager {
  public int getMaxShortcutCountPerActivity(){return 4;}
  public void setDynamicShortcuts(List<ShortcutInfo> s){}
  public void removeAllDynamicShortcuts(){}
  public List<ShortcutInfo> getManifestShortcuts(){return new ArrayList<ShortcutInfo>();}
}
