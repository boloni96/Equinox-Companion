using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Outfits494Tests
{
 [ModuleInitializer] internal static void Run(){
  foreach(uint slot in new uint[]{1,2,34,35,36,37,38,39,40}){
   var category=GearCategorySelection.Category(slot,1,true);
   if(category!=112||GearCategorySelection.Matches(category,false,new HashSet<uint>{slot})||!GearCategorySelection.Matches(category,false,new HashSet<uint>{112}))throw new Exception("Level-one wearable gear must match Outfits exclusively");
   if(GearCategorySelection.Category(slot,2,true)!=slot)throw new Exception("Higher level gear must retain slot category");
  }
  if(GearCategorySelection.Category(55,1,false)!=55||GearCategorySelection.Category(35,0,true)!=35)throw new Exception("Nonwearable and unknown-level items must not be reclassified");
  var categories=new Dictionary<uint,uint>{{100,GearCategorySelection.Category(35,1,true)},{101,GearCategorySelection.Category(35,90,true)}};
  var stock=new Dictionary<ulong,ChestStack[]>{{1,[new(10000,0,100,1,false,1),new(10000,1,101,1,false,1)]},{2,[new(10000,0,0,0,false,0),new(10000,1,0,0,false,0)]},{0,[new(0,0,0,0,false,0),new(0,1,0,0,false,0),new(0,2,0,0,false,0)]}};
  foreach(var category in new uint[]{35,112}){
   var plan=new StorageRoutingPlan("category",1,[new(1,2,[category],[])],0);
   var result=StorageRouting.Build(plan,stock,id=>categories.GetValueOrDefault(id));
   if(result.Moves.Length==0||result.Moves.Any(m=>m.Move.Source.Item!=(category==112?100:101)))throw new Exception("Routing must respect exclusive Outfit category");
  }
  Console.WriteLine("494 level-one Outfit exclusivity, higher-level slots and category routing passed.");
 }
}
