using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class EntrustBatch537Tests
{
    [ModuleInitializer] internal static void Run()
    {
        ChestStack Bag(int slot,uint item,int qty)=>new(0,slot,item,qty,false,999);
        ChestStack Stored(int slot,uint item,int qty)=>new(10000,slot,item,qty,false,999);
        var bags=new[]{Bag(0,100,8),Bag(1,101,7),Bag(2,102,6)};
        var inventory=new Dictionary<ulong,ChestStack[]>{[0]=bags,[1]=[Stored(0,100,20),Stored(1,102,20)],[2]=[Stored(0,101,20)]};
        var plan=new StorageRoutingPlan("Duplicates",1,[new(0,StorageAutoRouting.AllRetainers,[],[],Duplicates:true)]);
        var result=StorageRouting.Build(plan,inventory,_=>0,true,true);
        if(!result.Moves.Select(m=>m.Retainer).SequenceEqual(new ulong[]{1,1,2})||result.Remaining!=0)throw new Exception("Entrust must finish the retainer across interleaved bag slots");
        var preferred=StorageAutoRouting.BatchEntrust(result.Moves,bags,2);
        if(!preferred.Select(m=>m.Retainer).SequenceEqual(new ulong[]{2,1,1}))throw new Exception("Open retainer must be first");
        if(preferred.Sum(m=>m.Move.Quantity)!=21||preferred.Any(m=>m.Withdraw||m.Internal))throw new Exception("Grouping must preserve deposit quantities and direction");
        var split=Bag(0,100,12);
        RoutedMove[] moves=[new(1,new(split,Stored(0,100,994),5),false),new(2,new(split with{Quantity=7},Stored(0,100,992),7),false)];
        var reordered=StorageAutoRouting.BatchEntrust(moves,[split],2);
        if(reordered[0].Move.Source.Quantity!=12||reordered[1].Move.Source.Quantity!=5||reordered.Sum(m=>m.Move.Quantity)!=12)throw new Exception("Split stack quantities must be rebased for the new order");
        if(reordered[0].Move.Destination!=moves[1].Move.Destination||reordered[1].Move.Destination!=moves[0].Move.Destination)throw new Exception("Grouping must preserve destination capacity reservations");
        var mixed=moves.Append(moves[0] with{Withdraw=true}).ToArray();
        if(!StorageAutoRouting.BatchEntrust(mixed,[split],2).SequenceEqual(mixed))throw new Exception("Never reorder withdrawal dependencies");
        var limited=plan with{Rules=[new(0,StorageAutoRouting.AllRetainers,[],[],AllItems:true,Amounts:new(){[100]=3},Destinations:[1])]};
        var selected=StorageRouting.Build(limited,inventory,_=>0,true,true);
        if(selected.Moves.Sum(m=>m.Move.Quantity)!=3||selected.Moves.Any(m=>m.Retainer!=1||m.Move.Source.Item!=100))throw new Exception("Entrust batching must respect quantities and assigned destinations");
        Console.WriteLine("PASS retainer Entrust batching, open-retainer priority, split stacks and scope");
    }
}
