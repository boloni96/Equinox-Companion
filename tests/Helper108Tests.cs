using System.Runtime.CompilerServices;
using EquinoxCompanion;
public static class Helper108Tests
{
 [ModuleInitializer] public static void Run(){
    void Check(bool ok,string label){if(!ok)throw new Exception("Helper108: "+label);}
    var now=DateTimeOffset.UnixEpoch;var until=now.AddSeconds(15);
    Check(HelperTextAdvancePolicy.Keep(true,true,true,"68695:20","68695:20",now,until),"matching active scene");
    Check(!HelperTextAdvancePolicy.Keep(false,true,true,"a","a",now,until),"pause stop or permission revocation releases");
    Check(!HelperTextAdvancePolicy.Keep(true,false,true,"a","a",now,until),"action ended releases");
    Check(!HelperTextAdvancePolicy.Keep(true,true,false,"a","a",now,until),"cutscene ended releases");
    Check(!HelperTextAdvancePolicy.Keep(true,true,true,"a","b",now,until),"next scene releases");
    Check(!HelperTextAdvancePolicy.Keep(true,true,true,"a","a",until,until),"timeout releases");
    foreach(var confirm in new[]{false,true}){
        var o=new HelperTextAdvanceOptions(confirm);
        Check(o.EnableCutsceneEsc==!confirm&&o.EnableCutsceneSkipConfirm==confirm,"confirmation only after prompt validation");
        Check(o.EnableQuestAccept==false&&o.EnableQuestComplete==false&&o.EnableRewardPick==false&&o.EnableRequestHandin==false&&o.EnableTalkSkip==false&&o.EnableRequestFill==false&&o.EnableAutoInteract==false,"unrelated automation disabled");
    }
 }
}
