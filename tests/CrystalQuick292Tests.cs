using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class CrystalQuick292Tests
{
 [ModuleInitializer] internal static void Run(){
  void Check(string label,bool ok){if(!ok)throw new Exception(label);Console.WriteLine("PASS quick crystals292 "+label);}
  Check("all eighteen types available without plan",CrystalTransferSelection.Items(0,0).SequenceEqual(Enumerable.Range(2,18).Select(x=>(uint)x)));
  Check("shards only",CrystalTransferSelection.Items(1,0).SequenceEqual(new uint[]{2,3,4,5,6,7}));
  Check("fire crystals only",CrystalTransferSelection.Items(2,1).SequenceEqual(new uint[]{8}));
  Check("all water types",CrystalTransferSelection.Items(0,6).SequenceEqual(new uint[]{7,13,19}));
  ChestStack[] bags=[new(2001,6,8,9900,false,9999),new(2001,7,9,50,false,9999)];
  var withdraw=CrystalTransferSelection.Goals(true,2,1,200,0,bags);Check("extra withdrawal capped to capacity",withdraw[8]==9999&&withdraw.Count==1);
  var entrust=CrystalTransferSelection.Goals(false,2,0,9999,100,bags);Check("entrust preserves keep per type",entrust[8]==9800&&entrust[9]==0);
  var move=ChestTransferPlanner.Send(bags,[new(22001,6,8,0,false,9999)],entrust,RetrievalQuality.Any);Check("direct entrust works into empty chest crystal slot",move?.Quantity==9800);
  var groups=StorageCategoryGroups.Build([new(59,"Crystal",6,1),new(60,"Catalyst",6,2)]);Check("crystals are distinct from catalysts",groups.Single(g=>g.Name=="Crystals").Categories.Single().Id==59);
 }
}
