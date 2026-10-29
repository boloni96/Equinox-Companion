using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class StorageBlocked304Tests
{
 [ModuleInitializer] internal static void Run(){
    void Check(string label,bool ok){if(!ok)throw new Exception(label);Console.WriteLine("PASS blocked organizer "+label);}
    ChestStack S(uint item,int slot=0)=>new(10000,slot,item,item==0?0:1,false,1);
    var stock=new Dictionary<ulong,ChestStack[]>{
        [1]=[S(100)],[2]=[S(0)],[3]=[S(200)],[4]=[S(300)],[5]=[S(400)],
        [0]=[new(0,0,0,0,false,0),new(0,1,0,0,false,0),new(0,2,777,1,false,1)]
    };
    RetainerAssignment[] homes=[new(2,[],[100]),new(4,[],[200]),new(5,[],[300])];
    RoutingResult Run(ulong from){
        var rules=StorageAssignments.Apply([new(from,StorageAutoRouting.AllRetainers,[],[],AllItems:true)],homes,stock,id=>1);
        return StorageRouting.Build(new("Test",1,rules,1),stock,id=>1,includeExistingBags:false);
    }
    var selected=Run(1);var all=Run(StorageRouting.AllSources);
    Check("single-retainer transfer is feasible",selected.Moves.Count(x=>!x.Withdraw&&!x.Internal&&x.Move.Source.Item==100)==1);
    Check("all-retainer preview retains independent feasible transfer",all.Moves.Any(x=>x.Retainer==2&&!x.Withdraw&&!x.Internal&&x.Move.Source.Item==100));
    Check("blocked chain remains reported",all.Remaining==2);
    Check("stranded withdrawals are never returned",all.Moves.All(x=>x.Move.Source.Item==100));
    Check("existing bags are untouched",all.Moves.All(x=>x.Move.Source.Item!=777));
    Check("input snapshot is not mutated",stock[1][0].Item==100&&stock[2][0].Item==0);
    var tools=all.Moves.Where(x=>x.Move.Source.Item==100).ToArray();
    Check("cargo is fully delivered",tools.Where(x=>x.Withdraw).Sum(x=>x.Move.Quantity)==tools.Where(x=>!x.Withdraw&&!x.Internal).Sum(x=>x.Move.Quantity));
    var partial=new Dictionary<ulong,ChestStack[]>{
        [1]=[S(100),S(100,1)],[2]=[S(300),S(0,1)],[3]=[S(400)],
        [0]=[new(0,0,0,0,false,0),new(0,1,0,0,false,0)]
    };
    var partialRules=new[]{new StorageRoute(1,2,[],[100]),new StorageRoute(2,3,[],[300])};
    var partialResult=StorageRouting.Build(new("Partial",1,partialRules,1),partial,id=>1,includeExistingBags:false);
    Check("retry preserves an already delivered prefix of a subsequently blocked item",partialResult.Moves.Count(x=>!x.Withdraw&&!x.Internal&&x.Move.Source.Item==100)==1&&partialResult.Remaining==2);
    var chain=new Dictionary<ulong,ChestStack[]>{
        [1]=[S(100)],[2]=[S(200)],[3]=[S(300)],[4]=[S(0)],
        [0]=[new(0,0,0,0,false,0),new(0,1,0,0,false,0)]
    };
    var chainRules=new[]{new StorageRoute(1,2,[],[100]),new StorageRoute(2,3,[],[200]),new StorageRoute(3,4,[],[300])};
    var continued=StorageRouting.Build(new("Continue",1,chainRules,1),chain,id=>1,includeExistingBags:false);
    Check("later retainer frees capacity and previously blocked transfers are retried",continued.Remaining==0&&continued.Moves.Count(x=>!x.Withdraw&&!x.Internal)==3);
    Check("continued chain leaves no staged cargo",continued.Moves.Where(x=>x.Withdraw).Sum(x=>x.Move.Quantity)==continued.Moves.Where(x=>!x.Withdraw&&!x.Internal).Sum(x=>x.Move.Quantity));
    var blockedOnly=StorageAssignments.Apply([new(StorageRouting.AllSources,StorageAutoRouting.AllRetainers,[],[200,300])],homes,stock,id=>1);
    var none=StorageRouting.Build(new("Blocked",1,blockedOnly,1),stock,id=>1,includeExistingBags:false);
    Check("fully blocked plans remain safely empty",none.Moves.Length==0&&none.Remaining==2);
 }
}
