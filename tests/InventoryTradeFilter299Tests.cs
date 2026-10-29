using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class InventoryTradeFilter299Tests
{
    [ModuleInitializer] internal static void Run()
    {
        void Check(bool ok,string name){if(!ok)throw new Exception("Inventory trade filter: "+name);}
        ChestStack tradable=new(0,0,100,5,false,99),bound=new(0,1,100,5,false,99);
        ChestStack[] destination=[new(10000,0,0,0,false,99)];
        foreach(var eligible in new[]{tradable,bound}){
            // Both directions of the copy filter retain only the matching source slot.
            ChestStack[] filtered=[eligible];
            var amounts=TemporaryInventorySelection.Amounts(filtered,new Dictionary<uint,int>{{100,20}},RetrievalQuality.Any,_=>true);
            var inventories=new Dictionary<ulong,ChestStack[]>{{0,filtered},{1,destination}};
            var rules=new StorageRoute[]{new(0,StorageAutoRouting.AllRetainers,[],[100],Amounts:amounts)};
            var assigned=StorageAssignments.Apply(rules,[new(1,[1],[])],inventories,_=>1);
            var moves=StorageRouting.Build(new("Filtered copies",1,assigned,0),inventories,_=>1,true,true).Moves;
            Check(moves.Sum(x=>x.Move.Quantity)==5&&moves.All(x=>x.Move.Source.Slot==eligible.Slot),"assignments preserve matching copies and quantities");
            var rebound=StorageVisit.Bind(moves,filtered,destination);
            Check(rebound.All(x=>x.Move.Source.Slot==eligible.Slot),"live slot binding preserves filter");
            var rejected=false;try{StorageVisit.Bind(moves,[],destination);}catch(InvalidOperationException){rejected=true;}
            Check(rejected,"missing matching copy cannot be replaced by an excluded one");
        }
        Console.WriteLine("PASS inventory trade filter routing and slot binding");
    }
}
