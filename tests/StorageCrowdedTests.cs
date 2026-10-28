using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class StorageCrowdedTests
{
 [ModuleInitializer] internal static void Run(){
  var counts=new[]{175,175,175,175,170,172,175,175,168,172};
  var inventories=new Dictionary<ulong,ChestStack[]>();
  for(var r=0;r<10;r++)inventories[(ulong)(r+1)]=Enumerable.Range(0,175).Select(i=>new ChestStack((uint)(10000+i/25),i%25,i==0?100u:i<counts[r]?(uint)(10000+r*175+i):0,i==0?50:i<counts[r]?1:0,false,i==0?99:1)).ToArray();
  inventories[0]=[new(0,0,0,0,false,0)];
  var before=inventories.Values.SelectMany(x=>x).Where(s=>s.Item>0).GroupBy(s=>(s.Item,s.Hq)).ToDictionary(g=>g.Key,g=>g.Sum(s=>s.Quantity));
  var plan=new StorageRoutingPlan("Crowded",1,[new(StorageRouting.AllSources,StorageAutoRouting.AllRetainers,[],[],AllItems:true)],0);
  var result=StorageRouting.Build(plan,inventories,_=>1,includeExistingBags:false);
  var state=inventories.ToDictionary(x=>x.Key,x=>x.Value.ToList());
  foreach(var step in result.Moves){
   var from=step.Internal?step.Retainer:step.Withdraw?step.Retainer:0;var to=step.Withdraw?0:step.Retainer;
   var m=step.Move;var si=state[from].FindIndex(x=>x.Container==m.Source.Container&&x.Slot==m.Source.Slot);var di=state[to].FindIndex(x=>x.Container==m.Destination.Container&&x.Slot==m.Destination.Slot);
   if(state[from][si]!=m.Source||state[to][di]!=m.Destination)throw new Exception("crowded move precondition mismatch");
   var left=m.Source.Quantity-m.Quantity;state[from][si]=left==0?m.Source with{Item=0,Quantity=0,Hq=false,Limit=0}:m.Source with{Quantity=left};
   state[to][di]=m.Destination with{Item=m.Source.Item,Hq=m.Source.Hq,Quantity=m.Destination.Quantity+m.Quantity,Limit=m.Source.Limit};
   if(state[to][di].Quantity>state[to][di].Limit)throw new Exception("crowded move exceeded stack capacity");
  }
  var after=state.Values.SelectMany(x=>x).Where(s=>s.Item>0).GroupBy(s=>(s.Item,s.Hq)).ToDictionary(g=>g.Key,g=>g.Sum(s=>s.Quantity));
  if(before.Count!=after.Count||before.Any(x=>after.GetValueOrDefault(x.Key)!=x.Value))throw new Exception("crowded inventory conservation");
  if(state[0].Any(x=>x.Item>0)){Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{remaining=result.Remaining,bag=state[0],last=result.Moves.TakeLast(10),stocks=state.Where(x=>x.Key!=0).Select(x=>new{id=x.Key,items=x.Value.Where(s=>s.Item==100).ToArray()})}));throw new Exception("crowded organiser stranded cargo");}
  var free=state.Where(x=>x.Key!=0).SelectMany(x=>x.Value).Count(x=>x.Item==0);
  if(free<=18)throw new Exception("crowded organiser failed to consolidate");
  Console.WriteLine($"PASS crowded ten-retainer run conserves items, empties staging bag and frees {free-18} slots");
 }
}
