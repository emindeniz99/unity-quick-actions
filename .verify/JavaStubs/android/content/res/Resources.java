package android.content.res;
// `identifiers` lets the smoke test register drawable names, so it can assert which
// name the plugin's icon lookup resolved (and in which order). Never shipped.
public class Resources {
  public static final java.util.Map<String,Integer> identifiers = new java.util.HashMap<>();
  public int getIdentifier(String n,String d,String p){ Integer id = identifiers.get(n); return id == null ? 0 : id; }
}
