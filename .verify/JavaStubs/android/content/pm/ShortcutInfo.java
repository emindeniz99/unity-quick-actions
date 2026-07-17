package android.content.pm;
import android.content.Context; import android.content.Intent; import android.graphics.drawable.Icon;
import android.os.PersistableBundle;
public class ShortcutInfo {
  // Field-backed so the .verify smoke test can exercise the plugin statefully.
  String id = ""; CharSequence shortLabel = ""; CharSequence longLabel; int rank; PersistableBundle extras; Intent intent;
  public String getId(){return id;}
  public CharSequence getShortLabel(){return shortLabel;}
  public CharSequence getLongLabel(){return longLabel;}
  public int getRank(){return rank;}
  public PersistableBundle getExtras(){return extras;}
  public static class Builder {
    private final ShortcutInfo info = new ShortcutInfo();
    public Builder(Context c,String id){info.id = id;}
    public Builder setShortLabel(CharSequence s){info.shortLabel = s; return this;}
    public Builder setLongLabel(CharSequence s){info.longLabel = s; return this;}
    public Builder setIntent(Intent i){info.intent = i; return this;}
    public Builder setIcon(Icon i){return this;}
    public Builder setRank(int r){info.rank = r; return this;}
    public Builder setExtras(PersistableBundle b){info.extras = b; return this;}
    public ShortcutInfo build(){return info;}
  }
}
