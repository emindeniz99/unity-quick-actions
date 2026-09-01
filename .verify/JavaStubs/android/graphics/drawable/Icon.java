package android.graphics.drawable; import android.content.Context; import android.graphics.Bitmap;
// `kind` lets the smoke test assert WHICH factory produced the icon (resource vs
// bitmap vs adaptive) — the real Icon exposes getType() similarly — and `resId`
// which resource a resource icon points at. Never shipped.
public class Icon {
  public final String kind;
  public final int resId;
  private Icon(String kind, int resId){ this.kind = kind; this.resId = resId; }
  public static Icon createWithResource(Context c,int r){return new Icon("resource", r);}
  public static Icon createWithBitmap(Bitmap b){return new Icon("bitmap", 0);}
  public static Icon createWithAdaptiveBitmap(Bitmap b){return new Icon("adaptive", 0);}
}
