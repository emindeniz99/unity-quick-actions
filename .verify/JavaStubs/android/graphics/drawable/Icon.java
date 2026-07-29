package android.graphics.drawable; import android.content.Context; import android.graphics.Bitmap;
// `kind` lets the smoke test assert WHICH factory produced the icon (resource vs
// bitmap vs adaptive) — the real Icon exposes getType() similarly. Never shipped.
public class Icon {
  public final String kind;
  private Icon(String kind){ this.kind = kind; }
  public static Icon createWithResource(Context c,int r){return new Icon("resource");}
  public static Icon createWithBitmap(Bitmap b){return new Icon("bitmap");}
  public static Icon createWithAdaptiveBitmap(Bitmap b){return new Icon("adaptive");}
}
