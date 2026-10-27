using System.Runtime.CompilerServices;
using EquinoxCompanion;
public static class GardenSources259Tests
{
 [ModuleInitializer] public static void Run()
 {
  void Check(bool ok,string label){if(!ok)throw new Exception("GardenSources259: "+label);Console.WriteLine("PASS garden sources: "+label);}
  var at=DateTimeOffset.Parse("2026-10-27T07:00:00Z");
  var house=new SharedHouse("external","ABCD","Private house","Outside owner","Kraken","Empyreum",28,52,"Large","Other Owner","","",null,false);
  var c=new SharedCharacter("visitor","Visitor Name","Kraken","Dynamis","North America","MAIN",[]);
  var roster=new SharedRoster(1,at,[new SharedPerson("sir","Sir",[c])],GardenHouses:[house]);
  var plans=GardenPlanSourceBuilder.Build(roster);
  Check(plans.Length==3&&plans.All(p=>p.Beds.Length==8&&p.Capacity==3),"external owner has all three batches without a character link");
  Check(plans.SelectMany(p=>p.Beds).All(b=>b.ActualCrop=="Not synced yet"&&!b.Ready),"unobserved beds stay unknown, never empty or complete");
  Check(c.Houses.Length==0&&GardenBedSyncSession.CanCalibrate(roster,c.Name,c.World,house.Id,false),"visitor can calibrate without becoming owner or member");
  var care=new SharedGardenCare(house.Id,house.GameHouseId,house.Name,house.World,house.District,house.Ward,house.Plot,3,[],[new SharedGardenCareBed(5,true,false,null,null,null,Crop:"Royal Kukuru",ObservedAt:at)],PhysicalPatch:2);
  plans=GardenPlanSourceBuilder.Build(roster with {GardenHouses=[house with {Size=""}],GardenCare=[care]});
  Check(plans.Length==3&&plans.Single(p=>p.Batch==3).Beds.Single(b=>b.Bed==5).Ready,"known third care batch survives an unknown size");
  Check(plans.Single(p=>p.Batch==3).PhysicalPatch==2,"physical patch identity is retained");
  var bed=new SharedGardenBed(1,"Krakka Root","Grade 3 Thanalan Topsoil","starter","Krakka Root","",at,null,3,false,Order:1,ReplantOrder:9,StarterSoil:"Potting Soil");
  var saved=new SharedGardenPlan(house.Id,house.Name,house.World,house.District,28,52,2,"Onion",at,[bed],Capacity:3,PhysicalPatch:3);
  plans=GardenPlanSourceBuilder.Build(roster with {GardenHouses=[house with {Size="Small"}],GardenPlans=[saved],GardenBatchOrders=[new(house.Id,[2,3,1],at)]});
  Check(plans.Length==3&&ReferenceEquals(plans.Single(p=>p.Batch==2).Beds[0],bed),"saved starter and replant steps survive capacity expansion unchanged");
  Check(plans.Single(p=>p.Batch==2).PhysicalPatch==3&&plans.Single(p=>p.Batch==1).PhysicalPatch==2,"saved batch order stays separate from physical patch");
  var linked=c with {Houses=[house with {Size="Small"}]};
  plans=GardenPlanSourceBuilder.Build(roster with {People=[new SharedPerson("sir","Sir",[linked])],GardenHouses=[house]});
  Check(plans.Length==3,"duplicate metadata cannot let a stale small size hide large batches");
  foreach(var (size,count) in new[]{("Small",1),("Medium",2),("Large",3)})Check(GardenPlanSourceBuilder.Build(roster with {GardenHouses=[house with {Size=size}]}).Length==count,size+" capacity remains correct");
  plans=GardenPlanSourceBuilder.Build(roster with {GardenHouses=[house with {Id="other",Size="Small"}],GardenCare=[care]});
  Check(plans.Length==1&&plans[0].HouseId=="other","another house's care records cannot enlarge this house");
 }
}
