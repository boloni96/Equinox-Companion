using System.Runtime.CompilerServices;
using EquinoxCompanion;
public static class Helper107Tests
{
 [ModuleInitializer] public static void Run(){
    void Check(bool ok,string label){if(!ok)throw new Exception("Helper107: "+label);}
    const long now=100000;
    var fresh=new HelperFollower("s","Follower",1,"",true,false,false,"resume",now-1000);
    var stale=fresh with {Updated=now-20000};
    Check(HelperPolicy.RecordingAudience(stale,now),"record during brief status gap");
    Check(!HelperPolicy.ReadyAudience(["s"],[stale],now),"stale permission never sends");
    Check(HelperPolicy.ReadyAudience(["s"],[fresh],now),"fresh original session sends");
    Check(!HelperPolicy.ReadyAudience(["s"],[fresh with {Id="new"}],now),"new session cannot inherit");
    Check(!HelperPolicy.RecordingAudience(stale with {Updated=now-60000},now),"recording grace bounded");
    foreach(var f in new[]{fresh with {Quest=false},fresh with {QuestPaused=true},fresh with {Paused=true},fresh with {Control="stop"}}){
        Check(!HelperPolicy.RecordingAudience(f,now),"pause stop or revoked quest prevents recording");
        Check(!HelperPolicy.ReadyAudience(["s"],[f],now),"pause stop or revoked quest prevents send");
    }
    Check(!HelperPolicy.RecordingAudience(fresh,now,true),"skip permission not inferred");
    Check(!HelperPolicy.ReadyAudience(["s","missing"],[fresh],now),"all recorded recipients must be ready");
 }
}
