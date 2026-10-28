using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class InventoryBrowseSelectionTests
{
 [ModuleInitializer] internal static void Run(){
  void Check(string label,bool ok){if(!ok)throw new Exception(label);Console.WriteLine("PASS inventory selection "+label);}
  Check("search and category intersect",InventoryBrowseSelection.Matches("Wooden Table",77,"wood",new HashSet<uint>{77},false)&&!InventoryBrowseSelection.Matches("Wooden Table",77,"wood",new HashSet<uint>{58},false));
  Check("cleared categories mean no matches",!InventoryBrowseSelection.Matches("Table",77,"",new HashSet<uint>(),false));
  Check("all categories include unknown categories",InventoryBrowseSelection.Matches("New Item",999,"",new HashSet<uint>(),true));
  var stock=Enumerable.Range(1,450).Select(i=>new RetrievalEntry((uint)i,"Item "+i,1)).ToArray();var selection=stock.Select(x=>x.ItemId).ToHashSet();
  var rows=InventoryBrowseSelection.Entries(stock.Concat(stock),selection,7);
  Check("select all includes beyond previous 200 limit and deduplicates holders and quality",rows.Length==450&&rows.All(x=>x.Quantity==7));
  var withdraw=InventoryBrowseSelection.Plan(rows,false,RetrievalQuality.HqOnly);
  Check("new withdrawal uses exact listed quantities",withdraw.Mode==RetrievalMode.CompletedItems&&withdraw.Items.Length==450&&withdraw.Items[0].Quantity==7&&withdraw.Quality==RetrievalQuality.HqOnly&&withdraw.Entrust==null);
  var entrust=InventoryBrowseSelection.Plan(rows,true,RetrievalQuality.Any);
  Check("new entrust uses selection semantics",entrust.Entrust!=null&&entrust.DestinationRetainer==StorageAutoRouting.AllRetainers);
  selection.Clear();Check("clear selection gives empty request",InventoryBrowseSelection.Entries(stock,selection,1).Length==0);
 }
}
