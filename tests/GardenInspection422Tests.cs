using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class GardenInspection422Tests
{
 [ModuleInitializer] internal static void Run(){
  var now=DateTimeOffset.Parse("2026-11-05T22:00:00Z");
  var bed=new SharedGardenBed(1,"Curiel Root","Soil","planned","Empty","",null,null,3,false,Order:1,ObservedAt:now.AddMinutes(-40));
  var plan=new SharedGardenPlan("house","Goblet W18 P43","World","District",18,43,3,"",now.AddDays(-6),[bed],CompletedAt:now.AddDays(-5));
  var rule=new AdventureTaskRule(Kind:"growingcycle",Phase:"plant");
  void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);}
  var stale=GardenAdventureJobs.StalledPlanting(plan,rule,true,true,true,now);
  Check(stale,"known empty batch with completed plan and pending route needs recheck");
  Check(!GardenAdventureJobs.InspectionDue(rule,true,false,default,now),"known empty beds do not need inspection for a route mismatch");
  Check(GardenAdventureJobs.InspectionDue(rule,true,true,default,now),"unknown beds still need inspection");
  Check(!GardenAdventureJobs.InspectionDue(rule,true,stale,now.AddMinutes(-2),now),"recheck does not loop after fresh inspection");
  Check(!GardenAdventureJobs.InspectionDue(rule,false,stale,default,now),"disabled inspection stays disabled");
  Check(!GardenAdventureJobs.StalledPlanting(plan,rule,false,true,true,now),"disabled planting does not force recheck");
  Check(!GardenAdventureJobs.StalledPlanting(plan,rule with{Phase="waiting"},true,true,true,now),"waiting future stage does not force planting recheck");
  Check(!GardenAdventureJobs.StalledPlanting(plan with{CompletedAt=null},rule,true,true,true,now),"eligible empty batch proceeds without stale recheck");
  var reason=GardenAdventureJobs.NoActionReason(plan,rule,true,true,true,now);
  Check(reason.Contains("Batch 3")&&reason.Contains("marked complete"),"pause identifies actual completion mismatch and batch");
  Check(!reason.Contains("check seeds"),"completion mismatch does not guess missing supplies");
  var occupied=plan with{CompletedAt=null,Beds=[bed with{ActualCrop="Curiel Root",Status="different",NextTend=now.AddHours(10)}]};
  Check(GardenAdventureJobs.NoActionReason(occupied,rule,true,false,false,now).Contains("existing crop"),"occupied next step reports its exact blocker");
 }
}
