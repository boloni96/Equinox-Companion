using System.Runtime.CompilerServices;
using EquinoxCompanion;
public static class Adventure163Tests
{
 [ModuleInitializer]public static void Run()
 {
  void Check(bool value,string why){if(!value)throw new Exception("Adventure location: "+why);}
  var task=new AdventureTask("t","Inspect","",false,false,Rule:new("inspecthouse","h2"));
  var board=new AdventureBoard("garden:p","g","2026-10-17","garden","Garden",[new("A","","h1",1,[task with {Rule=null}],"Siren",["owner"]),new("B","","h2",1,[task],"Siren",["other"])],0,1);
  Check(AdventurePolicy.HouseIndex(board,"h2","Siren")==1,"exact house beats different owner/current stop");
  Check(AdventurePolicy.HouseIndex(board,"h2","Sophia") is null,"wrong world rejected");
  Check(AdventurePolicy.HouseIndex(board,"missing","Siren") is null,"outside active adventure rejected");
  Check(AdventurePolicy.HouseIndex(board with {Stops=[board.Stops[0],board.Stops[1] with {Tasks=[task with {Done=true}]}]},"h2","Siren")==1,"completed house can be reviewed");
  Check(AdventurePolicy.HouseIndex(board with {Kind="multi",Stops=[board.Stops[1] with {HouseId=""}]},"h2","Siren")==0,"multi house task identifies stop");
  var house=new SharedHouse("h2","id","Private house","B","Siren","The Goblet",1,2,"Small","Owner","","",null,false);
  var address=new Address("id",57,341,1,2,0,false,false,"Siren","Goblet");
  Check(AdventurePolicy.CurrentHouse(address,57,[house,house])=="h2","same shared property deduplicated");
  Check(AdventurePolicy.CurrentHouse(address,58,[house])=="","stale world address rejected");
  Check(AdventurePolicy.CurrentHouse(address with {Plot=3},57,[house])=="","exact plot required");
  Check(AdventurePolicy.CurrentHouse(address,57,[house,house with {Id="duplicate"}])=="","ambiguous property rejected");
  var follow=new AdventureLocationSession();var now=DateTimeOffset.UtcNow;
  Check(!follow.Observe(true,"actor/world","h1",now).Changed,"location settles");
  Check(follow.Observe(true,"actor/world","h1",now.AddSeconds(1)).ContextChanged,"initial world follow");
  Check(!follow.Observe(true,"actor/world","h1",now.AddSeconds(5)).Changed,"poll/manual navigation does not snap back");
  Check(!follow.Observe(true,"actor/world","h2",now.AddSeconds(6)).Changed,"new plot settles");
  var next=follow.Observe(true,"actor/world","h2",now.AddSeconds(7));Check(next.Changed&&!next.ContextChanged,"same-world plot movement detected");
  follow.Observe(false,"actor/world","h2",now.AddSeconds(8));Check(!follow.Observe(true,"actor/world","h2",now.AddSeconds(9)).Changed,"loading resets stability");
 }
}
