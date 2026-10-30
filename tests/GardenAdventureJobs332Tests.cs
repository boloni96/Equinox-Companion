using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class GardenAdventureJobs332Tests
{
 [ModuleInitializer] internal static void Run(){
  void Check(bool value,string message){if(!value)throw new Exception(message);}
  GardenTender[] account=[new("main",2),new("owner",2),new("eu",3),new("jp",1)];
  Check(GardenAdventureJobs.Tender("main","owner",2,account)=="main","Tending keeps starting main for same-region houses");
  Check(GardenAdventureJobs.Tender("main","owner",3,account)=="eu","EU care chooses this account's EU character");
  Check(GardenAdventureJobs.Tender("main","owner",1,account)=="jp","Japan care chooses this account's Japan character");
  Check(GardenAdventureJobs.Tender("main","outside-account",4,account)=="","No eligible region cannot leave account");
  Check(GardenAdventureJobs.Tender("main","eu",3,[account[0]])=="","Switch disabled cannot select another character");
  Check(GardenAdventureJobs.Tender("main","owner",0,account)=="","Unknown world cannot guess travel");
  Check(GardenAdventureJobs.AllowsTending(new(Kind:"care")),"Legacy care task");
  Check(GardenAdventureJobs.AllowsTending(new(Kind:"garden",Jobs:["care","harvest"])),"Mixed gardening care task");
  Check(GardenAdventureJobs.AllowsTending(new(Kind:"growingcycle",Phase:"waiting")),"Waiting route still needs care");
  Check(!GardenAdventureJobs.AllowsTending(new(Kind:"harvest")),"Harvest-only task does not become visitor care");
  Check(!GardenAdventureJobs.AllowsTending(new(Kind:"garden",Jobs:["plant"])),"Plant-only task stays assigned");
  var now=DateTimeOffset.UtcNow;var actor=new Actor("1","Visitor",410,410);
  var address=new Address("game-house",410,641,15,15,0,false,false,"Rafflesia","Shirogane");
  var target=new GardenSnapshot(now,actor,address,"123","Crop",null,null,false,[],new(2003757,123,"EventObj",1,2,3,55));
  var menu=new GardenMenu(1,now,target,"Garden",["Tend Crop","Quit"]);
  var resolve=GardenTargetMap.CreateResolver([new("game-house","Rafflesia","Shirogane",15,15,2,3,55,1,2,3,now)]);
  var intent=TendIntent.From(menu,"Tend Crop",now,resolve);
  Check(intent is {Patch:2,Bed:3},"Visitor tending uses verified saved bed identity");
  Check(intent!.Confirm(4017,now.AddSeconds(1),target)!=null,"Visitor care still requires real game receipt");
  Check(TendIntent.From(menu,"Tend Crop",now,GardenTargetMap.CreateResolver([]))==null,"Unmapped visitor care cannot invent bed numbers");
  Check(TendIntent.From(menu with{Target=target with{Address=address with{Plot=16}}},"Tend Crop",now,resolve)==null,"Neighbouring plot cannot reuse mapping");
  Check(TendIntent.From(menu,"Harvest Crop",now,resolve)==null,"Visitor mapping cannot authorize harvesting");
  Console.WriteLine("PASS gardening tender account and task scope");
 }
}
