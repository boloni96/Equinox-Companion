using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class EstateTravel518Tests {
 [ModuleInitializer] internal static void Run(){
  foreach(var alias in new[]{"priv","private","fc","shared","sha","shared2","sha2"}){
   var mode=EstateTravelCommand.Parse(alias)!.Value.Mode;
   if(!EstateTravelCommand.OutdoorTeleportArrived(mode,true)||EstateTravelCommand.OutdoorTeleportArrived(mode,false))throw new Exception("Outdoor travel must finish after teleport loading, without plot entry: "+alias);
  }
  foreach(var alias in new[]{"epriv","eprivate","efc","eshared","esha","esha2","workshop","ws"})if(EstateTravelCommand.OutdoorTeleportArrived(EstateTravelCommand.Parse(alias)!.Value.Mode,true))throw new Exception("Interior route finished outside: "+alias);
  Console.WriteLine("518 outdoor teleport completion preserves interior estate routes.");
 }
}
