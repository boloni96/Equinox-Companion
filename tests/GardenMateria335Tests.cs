using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class GardenMateria335Tests
{
 [ModuleInitializer] internal static void Run(){
  void Check(bool value,string message){if(!value)throw new Exception(message);}
  for(var home=1;home<=4;home++)for(var destination=1;destination<=4;destination++)
   Check(GardenAdventureJobs.Reachable(home,destination)==(home==destination||destination==4),$"Travel matrix {home} -> {destination}");
  foreach(var invalid in new[]{-1,0,5,99}){
   Check(!GardenAdventureJobs.Reachable(invalid,4),"Unknown home region must not authorize travel");
   Check(!GardenAdventureJobs.Reachable(2,invalid),"Unknown destination must not authorize travel");
  }
  GardenTender[] eligible=[new("main",2),new("sophia",4),new("eu",3),new("jp",1)];
  Check(GardenAdventureJobs.Tender("main","sophia",4,eligible)=="main","Main travels from Dynamis to Sophia instead of switching");
  Check(GardenAdventureJobs.Tender("main","sophia",4,[eligible[0]])=="main","Inspection and tending to Materia do not require switching");
  Check(GardenAdventureJobs.Tender("main","eu",3,eligible)=="eu","EU work switches to eligible EU character");
  Check(GardenAdventureJobs.Tender("main","jp",1,eligible)=="jp","Japan work switches to eligible Japan character");
  Check(GardenAdventureJobs.Tender("main","eu",3,[eligible[0]])=="","No direct NA to EU");
  Check(GardenAdventureJobs.Tender("main","jp",1,[eligible[0]])=="","No direct NA to Japan");
  Check(GardenAdventureJobs.Tender("sophia","outside-login",2,[eligible[1]])=="","Materia native cannot travel to NA or select outside-login character");
  Check(GardenAdventureJobs.Tender("main","sophia",4,[])=="","No eligible login candidates remain blocked");
  Check(GardenAdventureJobs.Tender("main","main",2,[eligible[0]])=="main","Home-region eligibility retained for returning NA visitor");
  Console.WriteLine("PASS garden Materia reachability and regional character selection");
 }
}
