package org.json;
public class JSONObject {
  public JSONObject(){}
  public JSONObject(String s) throws Exception {}
  public JSONArray optJSONArray(String k){return null;}
  public String optString(String k,String d){return d;}
  public int optInt(String k,int d){return d;}
  public JSONObject put(String k, Object v) throws Exception {return this;}
  public JSONObject put(String k, int v) throws Exception {return this;}
  public String toString(){return "";}
}
