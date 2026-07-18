package android.os;
import java.util.HashMap;
public class PersistableBundle {
  private final HashMap<String, Object> map = new HashMap<>();
  public void putBoolean(String key, boolean value) { map.put(key, value); }
  public boolean getBoolean(String key, boolean defaultValue) {
    Object v = map.get(key);
    return v instanceof Boolean ? (Boolean) v : defaultValue;
  }
  public void putInt(String key, int value) { map.put(key, value); }
  public int getInt(String key, int defaultValue) {
    Object v = map.get(key);
    return v instanceof Integer ? (Integer) v : defaultValue;
  }
  public void putString(String key, String value) { map.put(key, value); }
  public String getString(String key, String defaultValue) {
    Object v = map.get(key);
    return v instanceof String ? (String) v : defaultValue;
  }
}
