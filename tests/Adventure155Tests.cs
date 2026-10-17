using System.Runtime.CompilerServices;
namespace EquinoxCompanion.Tests;
public static class Adventure155Tests
{
 [ModuleInitializer] public static void Run()
 {
  var automatic=new AdventureTask("f","Fashion Report","Test",true,true,"100/100",true);
  if(AdventurePolicy.CanEdit(automatic))throw new Exception("Automatic completion must be read-only");
  if(!AdventurePolicy.CanEdit(automatic with {ServerConfirmed=false}))throw new Exception("Manual checks remain editable");
  var board=new AdventureBoard("a","g","2026-10-17","garden","Garden",[new("House","Siren","h",1,[automatic])],0,1,Protocol:2);
  var config=new Configuration{CachedAdventures=[board],AdventurePairingHash="hash",SelectedAdventure="a"};
  var snapshot=config.Snapshot();
  if(snapshot.CachedAdventures!=config.CachedAdventures||snapshot.SelectedAdventure!="a")throw new Exception("Immutable board cache must survive snapshot without per-frame serialization");
  Console.WriteLine("PASS 155 automatic check protection and saved adventure snapshot");
 }
}
