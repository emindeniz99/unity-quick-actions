package android.os;
import java.util.HashMap;
public class PersistableBundle {
  private final HashMap<String, Object> map = new HashMap<>();
  public void putBoolean(String key, boolean value) { map.put(key, value); }
  public boolean getBoolean(String key, boolean defaultValue) {
    Object v = map.get(key);
    return v instanceof Boolean ? (Boolean) v : defaultValue;
  }
}
