using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class GardenWork421Tests
{
 [ModuleInitializer] internal static void Run()
 {
  var now=DateTimeOffset.Parse("2026-11-05T22:00:00Z");
  var bed=new SharedGardenBed(1,"Curiel Root","Soil","confirmed","Curiel Root","Soil",now.AddDays(-4),now.AddHours(-13),3,true,Order:1,ObservedAt:now);
  var plan=new SharedGardenPlan("house","House","World","District",1,1,1,"",now.AddDays(-5),[bed]);
  var rule=new AdventureTaskRule(Kind:"growingcycle",Phase:"waiting");
  void Check(string name,GardenCareAction? actual,GardenCareAction? expected){if(actual!=expected)throw new Exception(name+": "+actual+" != "+expected);Console.WriteLine("PASS "+name);}
  GardenCareAction? Pick(SharedGardenPlan p,AdventureTaskRule r,bool plant=true,bool harvest=true,bool tend=true)=>GardenAdventureJobs.Action(p,r,plant,harvest,tend,now);
  Check("waiting next stage does not hide existing harvest",Pick(plan,rule),GardenCareAction.Harvest);
  Check("disabled harvest is respected",Pick(plan,rule,harvest:false),null);
  Check("keep mature is respected",Pick(plan with{Beds=[bed with{KeepMature=true}]},rule),null);
  Check("plant-only scope cannot harvest",Pick(plan,new(Kind:"garden",Jobs:["plant"])),null);
  var empty=plan with{Beds=[bed with{ActualCrop="Empty",Ready=false,Status="pending",Planted=null,Watered=null}]};
  Check("waiting stage cannot plant empty batch",Pick(empty,rule),null);
  Check("eligible empty batch can plant",Pick(empty,rule with{Phase="plant"}),GardenCareAction.Plant);
  Check("deferred planting does not restart",Pick(empty,rule with{Phase="plant"},plant:false),null);
  var seven=plan with{Beds=Enumerable.Range(1,8).Select(n=>bed with{Bed=n,Order=n,Status=n==8?"pending":"confirmed",ActualCrop=n==8?"Empty":"Curiel Root",Ready=n!=8}).ToArray()};
  Check("available harvest wins over missing planting",Pick(seven,rule with{Phase="plant"}),GardenCareAction.Harvest);
  var mixed=plan with{Beds=[bed,bed with{Bed=2,Order=2,Ready=false,NextTend=now.AddHours(5),Watered=now.AddHours(-7)}]};
  Check("full-batch harvest waits for mixed maturity",Pick(mixed,rule),null);
  var ready=plan with{Beds=[bed with{Ready=false,HarvestAt=now.AddHours(-3)}]};
  Check("estimated mature batch is eligible for verified harvest",Pick(ready,rule),GardenCareAction.Harvest);
  Check("harvesting remains eligible when planting is deferred",Pick(ready,rule,plant:false),GardenCareAction.Harvest);
  var growing=plan with{Beds=[bed with{Ready=false,NextTend=now.AddHours(-1),HarvestAt=now.AddDays(2)}]};
  Check("waiting batch still gets due care",Pick(growing,rule),GardenCareAction.Tend);
  Check("blocked batch leaves later eligible work",new[]{mixed,empty}.Select(p=>Pick(p,rule with{Phase="plant"},tend:false)).FirstOrDefault(a=>a!=null),GardenCareAction.Plant);
 }
}
