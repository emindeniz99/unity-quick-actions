package android.content;
import java.util.HashMap;
public class Intent {
  public static final String ACTION_VIEW="android.intent.action.VIEW";
  public static final int FLAG_ACTIVITY_REORDER_TO_FRONT=1;
  public static final int FLAG_ACTIVITY_NEW_TASK=2;
  // Field-backed so the .verify smoke test can drive the trampoline.
  private String action; private final HashMap<String,String> extras = new HashMap<>();
  public Intent(){}
  public Intent(Context c, Class<?> cls){}
  public Intent setAction(String a){action=a; return this;}
  public Intent putExtra(String k,String v){extras.put(k,v); return this;}
  public String getStringExtra(String k){return extras.get(k);}
  public String getAction(){return action;}
  public Intent addFlags(int f){return this;}
}
