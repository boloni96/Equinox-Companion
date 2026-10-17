using System.Runtime.CompilerServices;
namespace EquinoxCompanion.Tests;
public static class Adventure158Tests
{
 [ModuleInitializer] public static void Run()
 {
  var house=new SharedHouse("h","game","Private house","Home","Siren","Mist",1,1,"Small","Owner","","",null,false);
  var owner=new SharedCharacter("owner","Owner","Siren","Aether","NA","Main",[house]);
  var visitor=new SharedCharacter("visitor","Visitor","Siren","Aether","NA","Main",[house]);
  var legacy=new AdventureTask("0:{\"multiHouse\":\"h\"}:1","Housing","Housing",false,false);
  if(AdventurePolicy.TaskCharacterId(legacy,[visitor,owner])!="owner")throw new Exception("House belongs under owner, not first visitor");
  if(!AdventurePolicy.IsHouseVisit(legacy))throw new Exception("Legacy house visit must retain its address label");
  var inspect=legacy with {Rule=new("inspecthouse","h"),Label="Inspect garden beds"};
  if(AdventurePolicy.IsHouseVisit(inspect))throw new Exception("Inspection must not become Enter house");
  var fashion=legacy with {Id="0:{\"multiFashion\":\"visitor\"}:0",Group="Fashion",Rule=null};
  if(AdventurePolicy.TaskCharacterId(fashion,[owner,visitor])!="visitor")throw new Exception("Fashion belongs under matching character");
  var explicitOwner=legacy with {Rule=new("visit","h",Cid:"visitor")};
  if(AdventurePolicy.TaskCharacterId(explicitOwner,[owner,visitor])!="visitor")throw new Exception("Explicit task assignment must win");
  if(AdventurePolicy.TaskCharacterId(legacy,[])!="")throw new Exception("Unmatched shared tasks must remain visible");
  Console.WriteLine("PASS 158 character task grouping, legacy housing ownership and inspection labels");
 }
}
