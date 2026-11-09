using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Organizer517Tests
{
 [ModuleInitializer] internal static void Run(){
  foreach(uint storage in new uint[]{3200,3201,3202,3203,3205,3206,3207,3208,3209,3300,3400,3500})foreach(uint bag in new uint[]{0,1,2,3}){
   if(!LocalStoragePolicy.CanMove(storage,bag)||LocalStoragePolicy.CanMove(bag,storage))throw new Exception("Armoury withdrawals must be one-way into bags");
  }
  foreach(uint invalid in new uint[]{1000,3204,3210,3501})if(LocalStoragePolicy.CanMove(invalid,0))throw new Exception("Equipped or obsolete containers cannot be withdrawn");
  foreach(uint storage in new uint[]{4000,4001,4100,4101,27000,27001,27011,27200}){
   if(!LocalStoragePolicy.CanMove(storage,0)||!LocalStoragePolicy.CanMove(0,storage)||LocalStoragePolicy.CanMove(storage,4000))throw new Exception("Existing local-storage bounds changed");
  }
  Console.WriteLine("517 Armoury withdrawal boundaries and existing storage transfers passed.");
 }
}
