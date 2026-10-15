using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Bid142Tests
{
 [ModuleInitializer]internal static void Run(){
  void Check(bool ok,string label){if(!ok)throw new Exception("142: "+label);}
  var state=BidSearch.NextSort(-1,false,4);Check(state==(4,false),"first click ascending");
  state=BidSearch.NextSort(state.Column,state.Descending,4);Check(state==(4,true),"second click descending");
  state=BidSearch.NextSort(state.Column,state.Descending,4);Check(state==(-1,false),"third click default");
  Check(BidSearch.NextSort(4,true,5)==(5,false),"different column starts ascending");
  var p=new BidPlot(86,"Mist",1,2,0,3000000,4,1,null,5,0,0);
  var rows=new[]{p with{District="Empyreum"},p with{Ward=10},p with{Ward=2,Plot=10},p with{Ward=2,Plot=2},p};
  var sorted=BidSearch.Sort(rows,-1,false);
  Check(sorted.Select(x=>$"{x.District}:{x.Ward}:{x.Plot}").SequenceEqual(new[]{"Mist:1:2","Mist:2:2","Mist:2:10","Mist:10:2","Empyreum:1:2"}),"district order and numeric ward/plot order");
  Check(BidSearch.Sort(rows,4,true).SequenceEqual(sorted),"equal values use district/address tie break");
  Console.WriteLine("PASS 142 three-state sort and numeric default district/address order");
 }
}
