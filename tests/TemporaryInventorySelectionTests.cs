using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class TemporaryInventorySelectionTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        static void Check(bool ok,string name){if(!ok)throw new Exception("Temporary inventory: "+name);}
        ChestStack[] stock=[new(0,0,100,10,false,999),new(1,1,100,5,true,999),new(0,2,200,99,false,999),new(2001,0,2,40,false,9999),new(10000,0,100,900,false,999)];
        var selected=new Dictionary<uint,int>{{100,12},{2,80},{300,8}};
        var all=TemporaryInventorySelection.Amounts(stock,selected,RetrievalQuality.Any,_=>true);
        Check(all.Count==2&&all[100]==12&&all[2]==40,"selected IDs only; quantity capped at live bag stock");
        var nq=TemporaryInventorySelection.Amounts(stock,selected,RetrievalQuality.NqOnly,_=>true);
        Check(nq[100]==10,"retainer stock and HQ excluded");
        var filtered=TemporaryInventorySelection.Amounts(stock,selected,RetrievalQuality.Any,id=>id==2);
        Check(filtered.Count==1&&filtered.ContainsKey(2),"category/search filter narrows selection");
        Check(TemporaryInventorySelection.Amounts(stock,new Dictionary<uint,int>(),RetrievalQuality.Any,_=>true).Count==0,"cleared selection cannot entrust");
        var rules=PlanItemScope.Restrict([new(0,StorageAutoRouting.AllRetainers,[],all.Keys.ToArray(),Amounts:all)],all.Keys,_=>1);
        var inventories=new Dictionary<ulong,ChestStack[]>{{0,stock.Where(s=>s.Container<4||s.Container==2001).ToArray()},{1,[new(10000,0,0,0,false,999),new(10000,1,0,0,false,999),new(12001,0,2,0,false,9999)]}};
        var assigned=StorageAssignments.Apply(rules,[new(1,[1],[])],inventories,_=>1);
        var moves=StorageRouting.Build(new("Temporary",1,assigned,0),inventories,_=>1,true,true).Moves;
        Check(moves.Length>0&&moves.All(m=>all.ContainsKey(m.Move.Source.Item)),"broad assignment cannot add unselected items");
        Check(moves.Where(m=>m.Move.Source.Item==100).Sum(m=>m.Move.Quantity)==12,"combined HQ/NQ quantity respected");
        Check(selected[2]==80&&selected.Count==3,"calculating does not mutate temporary selection");
        Console.WriteLine("PASS temporary inventory selection, filters, quantities, crystal and assignment routing");
    }
}
