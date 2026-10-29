using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class RetainerCrystals293Tests
{
 [ModuleInitializer] internal static void Run(){
  void Check(string label,bool ok){if(!ok)throw new Exception(label);Console.WriteLine("PASS retainer crystals293 "+label);}
  var source=new ChestStack(12001,6,8,100,false,9999);var target=new ChestStack(2001,6,8,9900,false,9999);
  Check("withdraw uses retainer retrieve command",RetainerCrystalTransferPolicy.Command(new(source,target,99))==0);
  Check("entrust uses retainer entrust command",RetainerCrystalTransferPolicy.Command(new(target,source,99))==1);
  Check("single crystal still uses normal crystal command",RetainerCrystalTransferPolicy.Command(new(source,target,1))==0);
  bool Rejected(ChestMove move){try{RetainerCrystalTransferPolicy.Command(move);return false;}catch(InvalidOperationException){return true;}}
  Check("capacity changes stop",Rejected(new(source,target,100)));
  Check("wrong element stops",Rejected(new(source,target with{Item=9},1)));
  Check("wrong dedicated slot stops",Rejected(new(source,target with{Slot=7},1)));
  Check("FC chest does not use retainer commands",Rejected(new(source,target with{Container=22001},1)));
  Check("quantity dialog cannot silently reduce amount",RetainerCrystalTransferPolicy.AllowsQuantity(99,99)&&!RetainerCrystalTransferPolicy.AllowsQuantity(100,99)&&!RetainerCrystalTransferPolicy.AllowsQuantity(0,99));
 }
}
