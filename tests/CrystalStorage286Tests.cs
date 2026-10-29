using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class CrystalStorage286Tests
{
 [ModuleInitializer] internal static void Run()
 {
  void Check(string label,bool ok){if(!ok)throw new Exception(label);Console.WriteLine("PASS crystals286 "+label);}
  var bag=CrystalStorage.Normalize(new ChestStack(2001,6,8,9990,false,9999));
  var retainer=CrystalStorage.Normalize(new ChestStack(12001,6,8,500,false,9999));
  var chest=CrystalStorage.Normalize(new ChestStack(22001,6,8,500,false,9999));
  var emptyBag=CrystalStorage.Normalize(new ChestStack(2001,6,0,0,false,0));
  var emptyRetainer=CrystalStorage.Normalize(new ChestStack(12001,6,0,0,false,0));
  var emptyChest=CrystalStorage.Normalize(new ChestStack(22001,6,0,0,false,0));
  var fullBag=new ChestStack(0,0,100,99,false,99);
  var goals=new Dictionary<uint,int>{{8,9999}};
  var move=ChestTransferPlanner.Withdraw([chest],[fullBag,bag],goals,RetrievalQuality.Any)!;
  Check("FC withdrawal uses dedicated capacity despite full ordinary bags",move.Quantity==9&&move.Destination.Container==2001);
  Check("retainer withdrawal capacity is independent of empty bag slots",CrystalStorage.Room(8,[fullBag,bag])==9);
  Check("empty dedicated slots retain their crystal identity",emptyBag.Item==8&&emptyRetainer.Item==8&&emptyChest.Item==8);
  move=ChestTransferPlanner.Withdraw([chest],[emptyBag],new Dictionary<uint,int>{{8,30}},RetrievalQuality.Any)!;
  Check("FC withdrawal into empty crystal slot is exact",move.Quantity==30&&move.Destination.Slot==6);
  foreach(var target in new[]{emptyRetainer,emptyChest}){
   move=ChestTransferPlanner.Send([bag],[target],new Dictionary<uint,int>{{8,100}},RetrievalQuality.Any)!;
   Check("entrust uses the matching dedicated slot "+target.Container,move.Quantity==100&&move.Destination.Container==target.Container);
  }
  Check("crystals cannot spill into empty ordinary slots",ChestTransferPlanner.Send([bag],[new(10000,0,0,0,false,0)],goals,RetrievalQuality.Any)==null);
  Check("crystals cannot use another element slot",ChestTransferPlanner.Send([bag],[emptyChest with{Item=9,Slot=7}],goals,RetrievalQuality.Any)==null);
  Check("ordinary items cannot use crystal slots",ChestTransferPlanner.Send([fullBag],[emptyChest with{Item=0}],new Dictionary<uint,int>{{100,99}},RetrievalQuality.Any)==null);
  Check("full crystal inventory offers zero space",CrystalStorage.Room(8,[bag with{Quantity=9999}])==0);
  Check("HQ-only does not substitute NQ crystals",ChestTransferPlanner.Withdraw([chest],[emptyBag],goals,RetrievalQuality.HqOnly)==null);
  var invalid=false;try{CrystalStorage.Normalize(chest with{Item=9});}catch(InvalidOperationException){invalid=true;}
  Check("mismatched crystal identities stop instead of moving the wrong type",invalid);
  var plan=new StorageRoutingPlan("Crystals",1,[new(0,1,[],[8],Amounts:new(){{8,100}})],ReserveSlots:1);
  var routing=StorageRouting.Build(plan,new Dictionary<ulong,ChestStack[]>{{0,[fullBag,bag]},{1,[emptyRetainer]}},_=>59,true);
  Check("retainer entrust plan needs no ordinary empty slot",routing.Moves.Length==1&&routing.Moves[0].Move.Quantity==100&&routing.Moves[0].Move.Destination.Container==12001);
 }
}
