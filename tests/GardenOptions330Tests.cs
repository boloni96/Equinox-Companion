using EquinoxCompanion;
using System.Runtime.CompilerServices;
using System.Text.Json;
internal static class GardenOptions330Tests
{
 [ModuleInitializer] internal static void Run(){
  void Check(string label,bool ok){if(!ok)throw new Exception(label);Console.WriteLine("PASS garden options "+label);}
  var old=JsonSerializer.Deserialize<Configuration>("{}")!;
  Check("older configuration keeps prior defaults",old.GardenAdventureTend&&!old.GardenAdventurePlant&&!old.GardenAdventureHarvest&&!old.GardenAdventureSwitch);
  for(var mask=0;mask<32;mask++){
   var settings=new Configuration{GardenAdventureTend=(mask&1)!=0,GardenAdventurePlant=(mask&2)!=0,GardenAdventureHarvest=(mask&4)!=0,GardenAdventureSwitch=(mask&8)!=0,GardenCareWalk=(mask&16)!=0};
   var snapshot=settings.Snapshot();
   var restored=JsonSerializer.Deserialize<Configuration>(JsonSerializer.Serialize(snapshot))!;
   Check("saved choices survive reload "+mask,restored.GardenAdventureTend==settings.GardenAdventureTend&&restored.GardenAdventurePlant==settings.GardenAdventurePlant&&restored.GardenAdventureHarvest==settings.GardenAdventureHarvest&&restored.GardenAdventureSwitch==settings.GardenAdventureSwitch&&restored.GardenCareWalk==settings.GardenCareWalk);
  }
 }
}
