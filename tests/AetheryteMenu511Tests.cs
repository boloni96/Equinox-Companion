using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class AetheryteMenu511Tests
{
 [ModuleInitializer] internal static void Run(){
  static void Check(bool ok,string why){if(!ok)throw new Exception("Aetheryte menu: "+why);}
  foreach(uint id in new uint[]{2,8,9})Check(NativeTravelPolicy.AdoptAetheryteMenu(NativeTravelStage.Gateway,true,id,10,1),"open city menu bypasses movement approach distance");
  Check(!NativeTravelPolicy.AdoptAetheryteMenu(NativeTravelStage.Gateway,false,2,2,1),"unrelated target rejected");
  Check(!NativeTravelPolicy.AdoptAetheryteMenu(NativeTravelStage.Gateway,true,70,2,1),"residential shard rejected");
  foreach(var count in new[]{0,2})Check(!NativeTravelPolicy.AdoptAetheryteMenu(NativeTravelStage.Gateway,true,2,2,count),"missing or ambiguous menu rejected");
  foreach(var distance in new[]{float.NaN,21,-1})Check(!NativeTravelPolicy.AdoptAetheryteMenu(NativeTravelStage.Gateway,true,2,distance,1),"invalid or remote target rejected");
  Check(!NativeTravelPolicy.AdoptAetheryteMenu(NativeTravelStage.WorldQueue,true,2,2,1),"queue does not restart a completed interaction");
  Console.WriteLine("511 aetheryte menu handoff regression checks passed.");
 }
}
