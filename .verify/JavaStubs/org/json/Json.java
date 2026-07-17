package org.json;

// Minimal-but-correct JSON subset for the .verify smoke test: objects, arrays,
// strings (with escapes), integers/doubles, booleans, null — exactly what the
// plugin's payloads use. Compile surface of JSONObject/JSONArray matches the
// real org.json members the plugin calls. Never shipped.
final class Json {
  private final String s; private int i;
  private Json(String s){ this.s = s; }

  static Object parse(String text) throws Exception {
    Json p = new Json(text);
    p.ws();
    Object v = p.value();
    p.ws();
    if (p.i != text.length()) throw new Exception("trailing junk at " + p.i);
    return v;
  }

  private Object value() throws Exception {
    if (i >= s.length()) throw new Exception("unexpected end of input");
    char c = s.charAt(i);
    if (c=='{') return object();
    if (c=='[') return array();
    if (c=='"') return string();
    if (c=='t') { expect("true"); return Boolean.TRUE; }
    if (c=='f') { expect("false"); return Boolean.FALSE; }
    if (c=='n') { expect("null"); return null; }
    return number();
  }

  private JSONObject object() throws Exception {
    JSONObject o = new JSONObject();
    i++; ws();
    if (peek()=='}') { i++; return o; }
    while (true) {
      ws();
      if (peek() != '"') throw new Exception("key expected at " + i);
      String k = string();
      ws(); if (peek() != ':') throw new Exception("colon expected at " + i);
      i++; ws();
      o.map().put(k, value());
      ws();
      char c = peek();
      if (c==',') { i++; continue; }
      if (c=='}') { i++; return o; }
      throw new Exception("',' or '}' expected at " + i);
    }
  }

  private JSONArray array() throws Exception {
    JSONArray a = new JSONArray();
    i++; ws();
    if (peek()==']') { i++; return a; }
    while (true) {
      ws();
      a.list().add(value());
      ws();
      char c = peek();
      if (c==',') { i++; continue; }
      if (c==']') { i++; return a; }
      throw new Exception("',' or ']' expected at " + i);
    }
  }

  private String string() throws Exception {
    StringBuilder b = new StringBuilder();
    i++; // opening quote
    while (true) {
      if (i >= s.length()) throw new Exception("unterminated string");
      char c = s.charAt(i++);
      if (c=='"') return b.toString();
      if (c=='\\') {
        if (i >= s.length()) throw new Exception("unterminated escape");
        char e = s.charAt(i++);
        switch (e) {
          case '"': b.append('"'); break;
          case '\\': b.append('\\'); break;
          case '/': b.append('/'); break;
          case 'b': b.append('\b'); break;
          case 'f': b.append('\f'); break;
          case 'n': b.append('\n'); break;
          case 'r': b.append('\r'); break;
          case 't': b.append('\t'); break;
          case 'u':
            b.append((char) Integer.parseInt(s.substring(i, i+4), 16));
            i += 4; break;
          default: throw new Exception("bad escape \\" + e);
        }
      } else b.append(c);
    }
  }

  private Object number() throws Exception {
    int start = i;
    if (peek()=='-') i++;
    while (i < s.length() && Character.isDigit(s.charAt(i))) i++;
    boolean isDouble = false;
    if (i < s.length() && s.charAt(i)=='.') {
      isDouble = true; i++;
      while (i < s.length() && Character.isDigit(s.charAt(i))) i++;
    }
    if (i < s.length() && (s.charAt(i)=='e' || s.charAt(i)=='E')) {
      isDouble = true; i++;
      if (peek()=='+' || peek()=='-') i++;
      while (i < s.length() && Character.isDigit(s.charAt(i))) i++;
    }
    String lit = s.substring(start, i);
    if (lit.isEmpty() || lit.equals("-")) throw new Exception("bad number at " + start);
    return isDouble ? (Object) Double.parseDouble(lit) : (Object) Integer.valueOf(lit);
  }

  private void expect(String lit) throws Exception {
    if (!s.startsWith(lit, i)) throw new Exception("expected " + lit + " at " + i);
    i += lit.length();
  }

  private char peek(){ return i < s.length() ? s.charAt(i) : '\0'; }
  private void ws(){ while (i < s.length() && Character.isWhitespace(s.charAt(i))) i++; }

  static void write(Object v, StringBuilder b) {
    if (v == null) { b.append("null"); return; }
    if (v instanceof String) { writeString((String) v, b); return; }
    if (v instanceof JSONObject) { ((JSONObject) v).write(b); return; }
    if (v instanceof JSONArray) { ((JSONArray) v).write(b); return; }
    b.append(v.toString()); // Integer / Double / Boolean
  }

  static void writeString(String v, StringBuilder b) {
    b.append('"');
    for (int j = 0; j < v.length(); j++) {
      char c = v.charAt(j);
      switch (c) {
        case '"': b.append("\\\""); break;
        case '\\': b.append("\\\\"); break;
        case '\n': b.append("\\n"); break;
        case '\r': b.append("\\r"); break;
        case '\t': b.append("\\t"); break;
        default:
          if (c < 0x20) b.append(String.format("\\u%04x", (int) c));
          else b.append(c);
      }
    }
    b.append('"');
  }
}
