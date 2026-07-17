package org.json;
import java.util.ArrayList;
public class JSONArray {
  private final ArrayList<Object> values = new ArrayList<>();
  public JSONArray(){}
  ArrayList<Object> list(){ return values; }
  public int length(){ return values.size(); }
  public JSONObject optJSONObject(int index){
    Object v = index >= 0 && index < values.size() ? values.get(index) : null;
    return v instanceof JSONObject ? (JSONObject) v : null;
  }
  public JSONArray put(Object value){ values.add(value); return this; }
  void write(StringBuilder b){
    b.append('[');
    for (int i = 0; i < values.size(); i++) {
      if (i > 0) b.append(',');
      Json.write(values.get(i), b);
    }
    b.append(']');
  }
  @Override public String toString(){ StringBuilder b = new StringBuilder(); write(b); return b.toString(); }
}
