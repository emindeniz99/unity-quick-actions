package android.graphics;
// Mirrors the AOSP behavior the plugin relies on: decodeFile returns null for a
// missing/undecodable path (no throw), a Bitmap otherwise. The smoke test keys
// success on file existence — it never decodes real image bytes. Never shipped.
public class BitmapFactory {
  public static Bitmap decodeFile(String path) {
    if (path == null || path.isEmpty()) return null;
    return new java.io.File(path).exists() ? new Bitmap() : null;
  }
}
