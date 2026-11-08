using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Storage505Tests
{
 [ModuleInitializer] internal static void Run(){
  static void Check(bool v,string why){if(!v)throw new Exception("Storage505: "+why);}
  foreach(var t in new uint[]{4000,4001,4100,4101,27000,27001,27011,27200})Check(LocalStoragePolicy.CanMove(0,t)&&LocalStoragePolicy.CanMove(t,3),"supported storage must connect to bags");
  foreach(var t in new uint[]{1000,25000,25001,25003,25200,10000,20000})Check(!LocalStoragePolicy.CanMove(0,t)&&!LocalStoragePolicy.CanMove(t,0),"reject equipped, placed, fixtures and unrelated container routes");
  Check(!LocalStoragePolicy.CanMove(4000,27000),"no cross-storage move without bag staging");
  var unknown=new StorageStack(100,5,false,"Test");
  Check(LocalStoragePolicy.Filter(unknown,0)&&!LocalStoragePolicy.Filter(unknown,1)&&!LocalStoragePolicy.Filter(unknown,2)&&!LocalStoragePolicy.Filter(unknown,3)&&!LocalStoragePolicy.Filter(unknown,4),"unknown metadata cannot masquerade as a known copy state");
  var mixed=new StorageStack(100,7,false,"Test",[new(1,3,true,false),new(2,4,false,true)]);
  var copies=LocalStoragePolicy.Copies(mixed).ToArray();
  Check(copies.Length==2&&copies.Sum(s=>s.Quantity)==7&&copies.Where(s=>LocalStoragePolicy.Filter(s,3)).Sum(s=>s.Quantity)==3,"mixed binding copies must remain distinguishable without duplicating sync item keys");
  Check(WithdrawalTradePolicy.Allows(3,true,false,1,false)&&!WithdrawalTradePolicy.Allows(3,true,false,100,true),"collectability is not binding");
  Check(WithdrawalTradePolicy.Allows(4,true,true,0,false)&&!WithdrawalTradePolicy.Allows(1,true,true,0,false),"unbound does not imply tradable");
  var original=new StorageDetails("bag:0","Inventory",[100],[mixed]);
  Check(!StorageObservationEquality.Same(original,original with{Stacks=[mixed with{Positions=[new(1,3,false,true),new(2,4,false,true)]}]}),"binding-only changes must be observed");
  Check(LocalStoragePolicy.View("bag:4101",3)&&LocalStoragePolicy.View("house:ABC:27001",4)&&!LocalStoragePolicy.View("bag:0",3),"storage views distinguish sources");
  Console.WriteLine("505 storage routing boundaries, binding/collectible filters and mixed-copy observations passed.");
 }
}
