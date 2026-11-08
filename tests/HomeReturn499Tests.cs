using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class HomeReturn499Tests
{
 [ModuleInitializer] internal static void Run(){
  const string success="Successfully returned to Home World.";
  foreach(var stage in Enum.GetValues<NativeTravelStage>())foreach(var returning in new[]{false,true})foreach(var committed in new[]{false,true}){
   var actual=NativeTravelPolicy.HomeReturnSucceeded(stage,returning,committed,410,410,success);
   if(actual!=(stage==NativeTravelStage.TransferWait&&returning&&committed))throw new Exception("Home success must belong to a committed return at TransferWait");
  }
  foreach(var text in new[]{"Unable to return to Home World. [1000]","Transfer successful",success+" Retry?",""})
   if(NativeTravelPolicy.HomeReturnSucceeded(NativeTravelStage.TransferWait,true,true,410,410,text))throw new Exception("Do not accept unrelated or failed return messages");
  if(NativeTravelPolicy.HomeReturnSucceeded(NativeTravelStage.TransferWait,true,true,35,410,success)||NativeTravelPolicy.HomeReturnSucceeded(NativeTravelStage.TransferWait,true,true,0,0,success))throw new Exception("Home destination must match");
  if(!NativeTravelPolicy.ContinueDcFromHome(true,410,410,35,9,8))throw new Exception("Continue next DC from home without an extra login");
  if(NativeTravelPolicy.Arrival(1,1,410,405,false))throw new Exception("Stale visiting world is not home arrival");
  Console.WriteLine("499 owned home-return success, rejection and onward DC checks passed.");
 }
}
