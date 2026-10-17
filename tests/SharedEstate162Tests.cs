using System.Runtime.CompilerServices;
using EquinoxCompanion;
public static class SharedEstate162Tests
{
 [ModuleInitializer] public static void Run()
 {
  void Check(bool value,string name){if(!value)throw new Exception("Shared estates: "+name);}
  var now=DateTimeOffset.UtcNow;var gate=new SharedEstateObservationGate();
  Check(!gate.Ready("1","house",true,now),"first sample waits");
  Check(!gate.Ready("1","house",true,now.AddSeconds(15)),"second early sample waits");
  Check(gate.Ready("1","house",true,now.AddSeconds(30)),"stable loaded snapshot accepted");
  Check(!gate.Ready("2","house",true,now.AddSeconds(31)),"character switch resets");
  Check(!gate.Ready("2","house",false,now.AddSeconds(32)),"unavailable resets");
  Check(!gate.Ready("2","house",true,now.AddSeconds(63)),"loading time does not count");
  var actor=new Actor("123","Gardener",57,57);var home=new Address("0039015500000001",57,341,1,2,0,false,false,"Siren","The Goblet");
  var e=new SyncEvent(Guid.NewGuid().ToString("N"),"house.shared-estates",now,actor,null,SharedEstates:new("shared-estate-slots",[home]));
  Check(SyncValidation.CanSend(e,now),"valid tenant observation");
  Check(SyncValidation.CanSend(e with {SharedEstates=new("shared-estate-slots",[])},now),"confirmed empty slots");
  Check(!SyncValidation.CanSend(e with {SharedEstates=new("shared-estate-slots",[home,home])},now),"duplicate invalid");
  Check(!SyncValidation.CanSend(e with {SharedEstates=new("shared-estate-slots",[home with {WorldId=58}])},now),"foreign world invalid");
  Check(!SyncValidation.SupportedByWebsite(e.Kind,20)&&SyncValidation.SupportedByWebsite(e.Kind,21),"deployment protocol gate");
 }
}
