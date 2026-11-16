using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class EstateTeleport560Tests
{
 [ModuleInitializer] internal static void Run(){
  void Check(int seconds,bool arrived,bool started,bool pending,int attempts,EstateTeleportAction expected){if(EstateTeleportRecovery.Decide(TimeSpan.FromSeconds(seconds),arrived,started,pending,attempts)!=expected)throw new Exception("Estate teleport recovery mismatch");}
  Check(0,false,false,false,1,EstateTeleportAction.Wait);
  Check(8,false,false,false,1,EstateTeleportAction.Retry);
  Check(8,false,false,false,2,EstateTeleportAction.Stop);
  Check(8,false,true,false,1,EstateTeleportAction.Wait);
  Check(8,false,false,true,1,EstateTeleportAction.Wait);
  Check(45,false,false,true,1,EstateTeleportAction.Stop);
  Check(45,false,true,false,1,EstateTeleportAction.Stop);
  Check(45,true,true,false,1,EstateTeleportAction.Wait);
  Console.WriteLine("560 estate teleport no-start retry, bounded pending wait and no duplicate in-flight requests passed.");
 }
}
