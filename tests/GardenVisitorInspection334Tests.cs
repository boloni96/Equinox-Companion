using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class GardenVisitorInspection334Tests
{
 [ModuleInitializer] internal static void Run(){
  void Check(bool value,string message){if(!value)throw new Exception(message);}
  var now=DateTimeOffset.UtcNow;
  var waiting=new AdventureTaskRule(Kind:"growingcycle",Phase:"waiting",Cid:"owner");
  Check(GardenAdventureJobs.InspectionDue(waiting,true,true,default,now),"Unknown beds can be inspected while owner route waits");
  Check(!GardenAdventureJobs.InspectionDue(waiting,false,true,default,now),"Disabled inspection remains disabled");
  Check(!GardenAdventureJobs.InspectionDue(waiting,true,true,now.AddMinutes(-1),now),"Inspection cooldown prevents repeated visits");
  Check(GardenAdventureJobs.InspectionDue(new(Kind:"garden",Jobs:["inspect"]),true,false,default,now),"Explicit inspection works with known beds");
  Check(!GardenAdventureJobs.InspectionDue(new(Kind:"harvest"),true,false,default,now),"Known harvest-only work is not changed to inspection");
  Check(GardenAdventureJobs.Tender("visitor","owner",4,[new("visitor",4)])=="visitor","Visitor already on destination region can inspect with switching off");
  Check(GardenAdventureJobs.Tender("visitor","owner",4,[new("visitor",2)])=="","Unreachable visitor does not guess travel");
  Console.WriteLine("PASS visitor inspection scheduling and cooldown");
 }
}
