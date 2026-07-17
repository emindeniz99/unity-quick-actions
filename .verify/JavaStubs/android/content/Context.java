package android.content;
import android.content.res.Resources;
public class Context {
  // .verify smoke-test injection point: what getSystemService hands back.
  public Object testSystemService;
  public Resources getResources(){return new Resources();}
  public String getPackageName(){return "com.example.app";}
  @SuppressWarnings("unchecked")
  public <T> T getSystemService(Class<T> c){ return (T) testSystemService; }
}
