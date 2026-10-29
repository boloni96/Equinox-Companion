using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class OrganizerSelection288Tests
{
 [ModuleInitializer] internal static void Run()
 {
  void Check(string label,bool ok){if(!ok)throw new Exception(label);Console.WriteLine("PASS organizer288 "+label);}
  Check("tradable normal stack",WithdrawalTradePolicy.Allows(true,true,false,0,false));
  Check("untradable type excluded",!WithdrawalTradePolicy.Allows(true,true,true,0,false));
  Check("bound gear excluded",!WithdrawalTradePolicy.Allows(true,true,false,1,false));
  Check("collectibles excluded",!WithdrawalTradePolicy.Allows(true,true,false,0,true));
  Check("unknown item excluded",!WithdrawalTradePolicy.Allows(true,false,false,0,false));
  Check("filter off preserves existing withdrawal",WithdrawalTradePolicy.Allows(false,true,true,100,true));
  var entries=InventoryBrowseSelection.Entries([new(100,"Ingot",1),new(100,"Ingot",1),new(200,"Cloth",1)],new HashSet<uint>{100},5);
  var withdraw=InventoryBrowseSelection.Plan(entries,false,RetrievalQuality.HqOnly);
  Check("temporary list retains quantity and quality without recipe expansion",withdraw.Mode==RetrievalMode.CompletedItems&&withdraw.Items.Length==1&&withdraw.Items[0].Quantity==5&&withdraw.Quality==RetrievalQuality.HqOnly&&withdraw.Entrust==null);
  var entrust=InventoryBrowseSelection.Plan(entries,true,RetrievalQuality.Any);
  var amounts=entrust.Entrust!.Amounts(entries.Select(x=>x.ItemId),[new(0,0,100,12,false,99),new(0,1,200,7,false,99)],RetrievalQuality.Any,_=>1,[]);
  Check("temporary entrust moves selected bag items only with zero keep",amounts.Count==1&&amounts[100]==12);
 }
}
