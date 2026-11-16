using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class BringWard559Tests
{
 [ModuleInitializer] internal static void Run(){
  var ward=BringWardDestination.Capture(979,28,true)??throw new Exception("Empyreum street rejected");
  if(ward.District!="Empyreum"||!ward.Matches(979,28,true)||ward.Matches(979,27,true)||ward.Matches(641,28,true)||ward.Matches(979,28,false))throw new Exception("Incorrect Bring ward arrival");
  foreach(var territory in new uint[]{339,340,341,641,979})if(BringWardDestination.Capture(territory,1,true)==null||BringWardDestination.Capture(territory,30,true)==null)throw new Exception("Supported district rejected");
  if(BringWardDestination.Capture(979,0,true)!=null||BringWardDestination.Capture(979,31,true)!=null||BringWardDestination.Capture(979,28,false)!=null||BringWardDestination.Capture(9999,28,true)!=null)throw new Exception("Unverified ward accepted");
  Console.WriteLine("559 street-only Bring destinations, exact ward arrival, five districts and invalid addresses passed.");
 }
}
