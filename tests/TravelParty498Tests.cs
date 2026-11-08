using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class TravelParty498Tests
{
 [ModuleInitializer] internal static void Run(){
  if(NativeTravelPolicy.PartyBlocksTravel(0,false,false,false))throw new Exception("Solo world travel blocked");
  if(!NativeTravelPolicy.PartyBlocksTravel(2,true,false,false)||!NativeTravelPolicy.PartyBlocksTravel(1,true,false,false))throw new Exception("Local party must clear");
  if(NativeTravelPolicy.PartyBlocksTravel(8,true,true,false))throw new Exception("Cross-world party should survive same-DC travel");
  if(!NativeTravelPolicy.PartyBlocksTravel(0,false,true,true))throw new Exception("DC travel must leave cross-world party");
  var warning="Travel to Marilith?\nPlease be advised you must be playing solo or in a cross-world party for your transfer to be processed.";
  if(!NativeTravelPolicy.WorldPrompt(warning,"Marilith")||NativeTravelPolicy.WorldPrompt(warning,"Raiden")||NativeTravelPolicy.WorldPrompt(warning+" Different action", "Marilith"))throw new Exception("Travel confirmation must match exact destination and known warning");
  if(NativeTravelPolicy.LeavePartyPrompt("Disband party?")||NativeTravelPolicy.LeavePartyPrompt("Travel to Marilith?")||!NativeTravelPolicy.LeavePartyPrompt("Leave party?"))throw new Exception("Do not approve unrelated party dialogs");
  Console.WriteLine("498 party travel gates and scoped confirmation checks passed.");
 }
}
