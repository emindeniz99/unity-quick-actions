package android.graphics.drawable; import android.content.Context; import android.graphics.Bitmap;
// `kind` lets the smoke test assert WHICH factory produced the icon (resource vs
// bitmap vs adaptive) — the real Icon exposes getType() similarly — `resId`
// which resource a resource icon points at, and `width`/`height` the pixel size
// of the bitmap it was built from (so the icon clamp is observable). Never shipped.
public class Icon {
  public final String kind;
  public final int resId;
  public final int width;
  public final int height;
  private Icon(String kind, int resId, int width, int height){
    this.kind = kind; this.resId = resId; this.width = width; this.height = height;
  }
  public static Icon createWithResource(Context c,int r){return new Icon("resource", r, 0, 0);}
  public static Icon createWithBitmap(Bitmap b){return new Icon("bitmap", 0, b.getWidth(), b.getHeight());}
  public static Icon createWithAdaptiveBitmap(Bitmap b){return new Icon("adaptive", 0, b.getWidth(), b.getHeight());}
}
