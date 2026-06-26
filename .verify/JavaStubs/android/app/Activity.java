package android.app;
import android.content.Context; import android.content.Intent; import android.content.pm.PackageManager; import android.os.Bundle;
public class Activity extends Context {
  protected void onCreate(Bundle b){}
  protected void onNewIntent(Intent i){}
  public Intent getIntent(){return null;}
  public void setIntent(Intent i){}
  public void finish(){}
  public PackageManager getPackageManager(){return null;}
  public void startActivity(Intent i){}
}
