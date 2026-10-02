package android.content;
import android.content.res.Resources;
public class Context {
  // .verify smoke-test injection point: what getSystemService hands back.
  public Object testSystemService;
  // Smoke-test hook: when set, getSystemService throws it instead.
  public RuntimeException failGetSystemService;
  public Resources getResources(){return new Resources();}
  public String getPackageName(){return "com.example.app";}
  @SuppressWarnings("unchecked")
  public <T> T getSystemService(Class<T> c){
    if (failGetSystemService != null) throw failGetSystemService;
    return (T) testSystemService;
  }
}
