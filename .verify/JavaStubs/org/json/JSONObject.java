package org.json;
import java.util.LinkedHashMap;
public class JSONObject {
  private final LinkedHashMap<String,Object> values = new LinkedHashMap<>();
  public JSONObject(){}
  public JSONObject(String source) throws Exception {
    Object v = Json.parse(source);
    if (!(v instanceof JSONObject)) throw new Exception("not a JSON object");
    values.putAll(((JSONObject) v).values);
  }
  LinkedHashMap<String,Object> map(){ return values; }
  public JSONArray optJSONArray(String key){
    Object v = values.get(key);
    return v instanceof JSONArray ? (JSONArray) v : null;
  }
  public String optString(String key, String defaultValue){
    Object v = values.get(key);
    if (v == null) return defaultValue;
    return v instanceof String ? (String) v : String.valueOf(v);
  }
  public int optInt(String key, int defaultValue){
    Object v = values.get(key);
    return v instanceof Number ? ((Number) v).intValue() : defaultValue;
  }
  public JSONObject put(String key, Object value) throws Exception { values.put(key, value); return this; }
  public JSONObject put(String key, int value) throws Exception { values.put(key, value); return this; }
  void write(StringBuilder b){
    b.append('{');
    boolean first = true;
    for (java.util.Map.Entry<String,Object> e : values.entrySet()) {
      if (!first) b.append(',');
      first = false;
      Json.writeString(e.getKey(), b);
      b.append(':');
      Json.write(e.getValue(), b);
    }
    b.append('}');
  }
  @Override public String toString(){ StringBuilder b = new StringBuilder(); write(b); return b.toString(); }
}
