package android.content.pm;
import android.content.Context; import android.content.Intent; import android.graphics.drawable.Icon;
public class ShortcutInfo {
  public String getId(){return "";}
  public CharSequence getShortLabel(){return "";}
  public CharSequence getLongLabel(){return "";}
  public static class Builder {
    public Builder(Context c,String id){}
    public Builder setShortLabel(CharSequence s){return this;}
    public Builder setLongLabel(CharSequence s){return this;}
    public Builder setIntent(Intent i){return this;}
    public Builder setIcon(Icon i){return this;}
    public ShortcutInfo build(){return new ShortcutInfo();}
  }
}
