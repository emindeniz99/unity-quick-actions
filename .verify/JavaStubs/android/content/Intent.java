package android.content;
public class Intent {
  public static final String ACTION_VIEW="";
  public static final int FLAG_ACTIVITY_REORDER_TO_FRONT=1;
  public static final int FLAG_ACTIVITY_NEW_TASK=2;
  public Intent(){}
  public Intent(Context c, Class<?> cls){}
  public Intent setAction(String a){return this;}
  public Intent putExtra(String k,String v){return this;}
  public String getStringExtra(String k){return null;}
  public String getAction(){return null;}
  public Intent addFlags(int f){return this;}
}
