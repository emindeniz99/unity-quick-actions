package android.graphics.drawable;
// API 26+. Only the static inset fraction is used, to convert the OS icon-size
// budget (which bounds the icon's VISIBLE part) into an adaptive bitmap's full
// size. 0.25 is the AOSP value, i.e. a 1.5x allowance. Never shipped.
public class AdaptiveIconDrawable {
  public static float getExtraInsetFraction() { return 0.25f; }
}
