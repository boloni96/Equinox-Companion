using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class GardenSupplyRefresh327Tests
{
 [ModuleInitializer] internal static void Run(){
  void Check(string label,bool ok){if(!ok)throw new Exception(label);Console.WriteLine("PASS garden supplies "+label);}
  var now=DateTimeOffset.Parse("2026-10-30T16:00:00Z");var actor=new Actor("123","Gardener",410,410);
  var gate=new StorageCaptureGate();StorageSlotValue[] slots=[new(100,9,false)];
  gate.Remember(actor,"bag:0",slots,now);gate.Remember(actor,"bag:4000",slots,now);
  Check("unchanged bags renew before scheduler 15-minute expiry",gate.Needs(actor,"bag:0",slots,now.AddMinutes(10)));
  Check("normal frame still uses cache",!gate.Needs(actor,"bag:0",slots,now.AddSeconds(1)));
  Check("saddlebags retain normal interval",!gate.Needs(actor,"bag:4000",slots,now.AddMinutes(10)));
  gate.InvalidateBags();
  Check("explicit refresh bypasses unchanged bag cache",gate.Needs(actor,"bag:0",slots,now.AddSeconds(1)));
  Check("bag refresh does not rescan saddlebag",!gate.Needs(actor,"bag:4000",slots,now.AddSeconds(1)));
  Check("explicit refresh bypasses second observation dedup",StorageCaptureGate.FreshSnapshotRequested("bag:0",now,now.AddSeconds(1)));
  Check("one snapshot satisfies request",!StorageCaptureGate.FreshSnapshotRequested("bag:0",now.AddSeconds(2),now.AddSeconds(1)));
  Check("not every storage container is force-uploaded",!StorageCaptureGate.FreshSnapshotRequested("bag:4000",now,now.AddSeconds(1)));
  Check("no implicit forced uploads",!StorageCaptureGate.FreshSnapshotRequested("bag:0",now,default));
 }
}
