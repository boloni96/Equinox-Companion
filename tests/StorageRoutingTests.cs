using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class StorageRoutingTests
{
    [ModuleInitializer] internal static void Run()
    {
        void Check(string label,bool ok){if(!ok)throw new Exception(label);Console.WriteLine("PASS routing "+label);}
        ChestStack Empty(uint type,int slot)=>new(type,slot,0,0,false,0);
        var bags=Enumerable.Range(0,50).Select(i=>Empty(0,i)).ToArray();
        var source=Enumerable.Range(0,60).Select(i=>new ChestStack(10000,i,(uint)(100+i),1,false,1)).ToArray();
        var destination=Enumerable.Range(0,70).Select(i=>Empty(10000,i)).ToArray();
        var plan=new StorageRoutingPlan("Move",1,[new(1,2,[],[],AllItems:true)],1);
        var result=StorageRouting.Build(plan,new Dictionary<ulong,ChestStack[]>{{0,bags},{1,source},{2,destination}},_=>58,includeExistingBags:false);
        Check("49 slots filled before first delivery",result.Moves.TakeWhile(x=>x.Withdraw).Count()==49);
        Check("continues until all sixty arrive",result.Remaining==0&&result.Moves.Count(x=>!x.Withdraw&&!x.Internal)==60);
        result=StorageRouting.Build(plan with{ReserveSlots=0},new Dictionary<ulong,ChestStack[]>{{0,bags},{1,source},{2,destination}},_=>58,includeExistingBags:false);
        Check("zero reserve fills all fifty",result.Moves.TakeWhile(x=>x.Withdraw).Count()==50);
        var full=new Dictionary<ulong,ChestStack[]>{{0,[Empty(0,0),Empty(0,1)]},{1,[new(10000,0,100,10,false,99)]},{2,[new(10000,0,200,10,false,99)]}};
        result=StorageRouting.Build(new("Swap",1,[new(1,2,[],[100]),new(2,1,[],[200])],1),full,_=>0,includeExistingBags:false);
        Check("full retainers can exchange misplaced stacks",result.Remaining==0&&result.Moves.Count(x=>!x.Withdraw)==2);
        result=StorageRouting.Build(plan,full,_=>0,includeExistingBags:false);
        Check("full destination without outgoing route is left alone",result.Moves.Length==0&&result.Remaining==10);
        var send=new StorageRoutingPlan("Send five",1,[new(0,2,[],[100],Amounts:new(){{100,5}})]);
        result=StorageRouting.Build(send,new Dictionary<ulong,ChestStack[]>{{0,[new(0,0,100,30,false,99)]},{2,[Empty(10000,0)]}},_=>0,true);
        Check("send plan honours requested quantity",result.Moves.Sum(x=>x.Move.Quantity)==5);
        var merge=new Dictionary<ulong,ChestStack[]>{{0,[Empty(0,0)]},{2,[new(10000,0,100,40,false,99),new(10001,0,100,30,false,99)]}};
        result=StorageRouting.Build(new("Merge",1,[new(StorageRouting.AllSources,2,[],[],AllItems:true)]),merge,_=>0,includeExistingBags:false);
        Check("already correct retainer consolidates partial stacks",result.Moves.Length==1&&result.Moves[0].Internal&&result.Moves[0].Move.Quantity==30);
        result=StorageRouting.Build(new("Materia",1,[new(1,2,[58],[])]),new Dictionary<ulong,ChestStack[]>{{0,bags},{1,source},{2,destination}},id=>id==100?58u:1u,includeExistingBags:false);
        Check("category routing leaves other types alone",result.Moves.Count(x=>x.Withdraw)==1&&result.Moves[0].Move.Source.Item==100);
        var gear=new Dictionary<ulong,ChestStack[]>{{0,[new(0,0,500,1,false,1),Empty(0,1),Empty(0,2)]},{1,[new(10000,0,500,1,false,1)]},{2,[Empty(10000,0)]}};
        result=StorageRouting.Build(plan,gear,_=>1,includeExistingBags:false);
        Check("organizer does not substitute pre-existing gear",result.Moves.Where(x=>!x.Withdraw).All(x=>x.Move.Source.Slot!=0));
        var chained=new Dictionary<ulong,ChestStack[]>{{0,bags},{1,[new(10000,0,500,10,false,99)]},{2,[new(10000,0,500,10,false,99)]},{3,[Empty(10000,0)]}};
        result=StorageRouting.Build(new("Explicit sources",1,[new(1,2,[],[500]),new(2,3,[],[500])]),chained,_=>1,includeExistingBags:false);
        Check("delivered cargo is not picked up again",result.Moves.Where(x=>x.Withdraw).Sum(x=>x.Move.Quantity)==20&&result.Remaining==0);
        var fragmented=new Dictionary<ulong,ChestStack[]>{{0,[Empty(0,0),Empty(0,1)]},{1,[new(10000,0,500,10,false,99)]},{2,[new(10000,0,600,10,false,99),new(10000,1,600,10,false,99)]}};
        result=StorageRouting.Build(new("Free a slot",1,[new(StorageRouting.AllSources,2,[],[],AllItems:true)]),fragmented,_=>0,includeExistingBags:false);
        Check("merge frees destination space before collecting",result.Remaining==0&&result.Moves[0].Internal&&result.Moves.Any(x=>x.Withdraw));
        var preset=new RetrievalPreset("Send",[],DestinationRetainer:22,SendCategories:[58]);
        var imported=RetainerRetrieval.Import(RetainerRetrieval.Export(preset),_=>null);
        Check("send destination and categories survive export",imported.DestinationRetainer==22&&imported.SendCategories!.Single()==58);

    }
}
