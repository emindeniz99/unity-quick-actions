package android.graphics;
// Inert stand-in: the smoke test needs Bitmap as an identity token (decodeFile
// succeeded vs returned null) plus the dimensions the icon clamp reads and
// rewrites. createScaledBitmap mirrors AOSP by returning a NEW instance at the
// requested size (the source is left alone). Never shipped.
public class Bitmap {
  private final int width;
  private final int height;
  public Bitmap() { this(1, 1); }
  public Bitmap(int width, int height) { this.width = width; this.height = height; }
  public int getWidth() { return width; }
  public int getHeight() { return height; }
  public static Bitmap createScaledBitmap(Bitmap src, int w, int h, boolean filter) {
    return new Bitmap(w, h);
  }
}
