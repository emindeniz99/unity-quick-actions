package android.util;
public class Log {
  // Smoke-test hook: every warning message, so a test can pin what got logged.
  public static final java.util.List<String> warnings = new java.util.ArrayList<>();
  public static int i(String t, String m){return 0;}
  public static int w(String t, String m){warnings.add(m); return 0;}
  public static int w(String t, String m, Throwable e){warnings.add(m); return 0;}
}
