using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Chest516Tests
{
 [ModuleInitializer] internal static void Run(){
  static void Check(bool ok,string why){if(!ok)throw new Exception("Chest 516: "+why);}
  ChestStack[] chest=[new(22001,0,2,100,false,9999),new(20000,0,100,5,false,999)];
  ChestStack[] bags=[new(2001,0,2,50,false,9999),new(0,0,100,10,false,999)];
  var ordinary=ChestTransferPlanner.Duplicates(chest,bags,2,false);
  Check(ordinary?.Source.Item==100&&ordinary.Quantity==8,"exclude crystals but retain keep amount for ordinary duplicates");
  Check(ChestTransferPlanner.Duplicates(chest,bags,2,true)?.Source.Item==2,"explicit inclusion allows crystals");
  Check(ChestTransferPlanner.Duplicates(chest,[bags[0]],0,false)==null,"crystal-only bags cannot leak into excluded duplicates");
  Check(ChestTransferPlanner.Send([bags[0]],chest,new Dictionary<uint,int>{{2,20}},RetrievalQuality.Any)?.Quantity==20,"explicit crystal entrust remains supported");
  var gate=new StorageCaptureGate();var actor=new Actor("123","Test",410,410);var now=DateTimeOffset.UtcNow;StorageSlotValue[] slots=[new(100,5,false)];
  gate.Remember(actor,"fc:123:20000",slots,now);
  Check(!gate.Needs(actor,"fc:123:20000",slots,now.AddSeconds(1)),"unchanged FC slots skip reconstruction");
  Check(gate.Needs(actor,"fc:123:20000",[new(100,6,false)],now.AddSeconds(1)),"changed quantity captured immediately");
  Check(gate.Needs(actor,"fc:456:20000",slots,now.AddSeconds(1)),"different FC does not reuse previous capture");
  Check(gate.Needs(actor,"fc:123:20000",slots,now.AddMinutes(30)),"periodic refresh preserved");
  Check(EstateTravelCommand.Parse("ws")==EstateTravelCommand.Parse("workshop"),"workshop alias matches");
  Check(EstateTravelCommand.Parse(" WS ")?.Mode==3,"case and whitespace accepted");
  Console.WriteLine("516 chest crystal exclusion, capture and workshop checks passed.");
 }
}
