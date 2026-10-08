using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class Helper97Tests
{
 [ModuleInitializer] internal static void Run(){
  static void Test(string name,bool ok){if(!ok)throw new Exception(name);Console.WriteLine("PASS 97 "+name);}
  var trip=new FollowPortalSignal(new string('a',32),"Leader",1,406,"1",839,5,0,0,0,0,0,1,"",TravelKind:"leaveDuty",DutyId:500);
  string Reason(bool enabled=true,bool loaded=true,bool loading=false,uint world=406,uint territory=839,uint duty=500)=>FollowDutyLeavePolicy.ObsoleteReason(trip,enabled,loaded,loading,world,territory,duty);
  Test("completed solo duty does not hold travel queue",Reason(territory:130,duty:0).Length>0);
  Test("same duty remains eligible for loot-aware exit",Reason()=="");
  Test("loading transient is not proof of exit",Reason(loading:true,territory:130,duty:0)=="");
  Test("missing actor is not proof of exit",Reason(loaded:false,territory:130,duty:0)=="");
  Test("missing territory is not proof of exit",Reason(territory:0,duty:0)=="");
  Test("cleared duty id in original territory is insufficient",Reason(duty:0)=="");
  Test("another duty never receives old exit",Reason(duty:501).Length>0);
  Test("another world never receives old exit",Reason(world:407).Length>0);
  Test("disabled exit assistance cannot block queue",Reason(enabled:false).Length>0);
  Test("ordinary teleport remains untouched",FollowDutyLeavePolicy.ObsoleteReason(trip with {TravelKind="teleport"},true,true,false,406,130,0)=="");
 }
}
