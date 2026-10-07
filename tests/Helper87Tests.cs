using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class Helper87Tests
{
 [ModuleInitializer] internal static void Run(){
  static void Test(string name,bool pass){if(!pass)throw new Exception(name);Console.WriteLine("PASS 87 "+name);}
  Test("pause never queues a fresh travel",!HelperSessionPolicy.AcceptTravel(true,400,100,300));
  Test("resume rejects paused travel",!HelperSessionPolicy.AcceptTravel(false,250,100,300));
  Test("resume rejects cutoff boundary",!HelperSessionPolicy.AcceptTravel(false,300,100,300));
  Test("fresh explicit bring allowed",HelperSessionPolicy.AcceptTravel(false,301,100,300));
  Test("restart rejects previous session travel",!HelperSessionPolicy.AcceptTravel(false,350,400,0));
  Test("new session starts fresh",HelperSessionPolicy.AcceptTravel(false,400,400,0));
  var permission=new HelperPermission();permission.Start(true,true);permission.Control("pause");permission.SetQuestPause(true);permission.Stop();permission.Control("resume");
  Test("leader cannot restart stopped permission",!permission.Active);
  permission.Start(true,false);Test("follower start removes all prior pause state",permission.Active&&!permission.Paused&&!permission.QuestPaused&&!permission.Skip);
 }
}
