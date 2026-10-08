using System.Runtime.CompilerServices;
using EquinoxCompanion;
public static class Helper109Tests
{
 [ModuleInitializer] public static void Run(){
    void Check(bool ok,string label){if(!ok)throw new Exception("Helper109: "+label);}
    const string prompt="Duty calls. Commence battle for “In the Dark of Night”?";
    var npc=new HelperNpc("n",2009664,"Destination",152,1,1,new(1,2,3),new(1,2,4),0);
    var leave=new HelperAction("a","Leader",1,"choice",0,npc,prompt,HelperDutyPolicy.LeaveSignature(prompt),"SelectYesno","quest-step:3",QuestId:68695);
    Check(HelperDutyPolicy.MatchesLeave(leave,prompt,"In the Dark of Night",true,3),"matching Leave");
    Check(!HelperDutyPolicy.Matches(leave,prompt,"In the Dark of Night",true,3),"Leave never interpreted as Proceed");
    var proceed=leave with {Kind="soloDuty",Signature=HelperPolicy.Signature([prompt])};
    Check(!HelperDutyPolicy.IsLeave(proceed),"Proceed never interpreted as Leave");
    Check(!HelperDutyPolicy.MatchesLeave(leave,prompt,"Other quest",true,3),"wrong quest rejected");
    Check(!HelperDutyPolicy.MatchesLeave(leave,prompt,"In the Dark of Night",true,4),"different progress rejected");
    Check(!HelperDutyPolicy.MatchesLeave(leave,prompt,"In the Dark of Night",false,3),"unaccepted quest rejected");
    Check(!HelperDutyPolicy.IsLeave(leave with {Text="Skip cutscene?"}),"cutscene confirmation excluded");
    Check(!HelperDutyPolicy.IsLeave(leave with {Addon="SelectString"}),"other menu excluded");
    Check(HelperDutyPolicy.MatchesLeave(leave with {Addon="DifficultySelectYesNo"},prompt,"In the Dark of Night",true,3),"difficulty variant supported");
 }
}
