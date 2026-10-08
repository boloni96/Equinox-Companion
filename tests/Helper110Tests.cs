using System.Runtime.CompilerServices;
using EquinoxCompanion;
public static class Helper110Tests
{
 [ModuleInitializer] public static void Run(){
    void Check(bool ok,string label){if(!ok)throw new Exception("Helper110: "+label);}
    var now=DateTimeOffset.UnixEpoch;var until=now.AddMilliseconds(100);
    Check(HelperNativeSkipPolicy.Escape(true,true,true,"q:1","q:1",3,now,until),"matching scene Escape");
    foreach(var id in new[]{1,2,33,321,322})Check(!HelperNativeSkipPolicy.Escape(true,true,true,"q:1","q:1",id,now,until),"no unrelated input");
    Check(!HelperNativeSkipPolicy.Escape(false,true,true,"q:1","q:1",3,now,until),"permission pause stop blocks");
    Check(!HelperNativeSkipPolicy.Escape(true,false,true,"q:1","q:1",3,now,until),"no old action");
    Check(!HelperNativeSkipPolicy.Escape(true,true,false,"q:1","q:1",3,now,until),"outside cutscene blocked");
    Check(!HelperNativeSkipPolicy.Escape(true,true,true,"q:1","q:2",3,now,until),"changed scene blocked");
    Check(!HelperNativeSkipPolicy.Escape(true,true,true,"q:1","q:1",3,until,until),"pulse bounded");
    Check(!new FollowThemSettings().UseTextAdvanceCutsceneSkip,"built-in default provider");
    Check(!new FollowThemSettings().VerifiedCutsceneSkip,"skip still requires opt-in");
 }
}
