using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class StorageFlexibleTests
{
 [ModuleInitializer] internal static void Run(){
  void Check(string text,bool ok){if(!ok)throw new Exception(text);Console.WriteLine("PASS flexible storage "+text);}
  ChestStack S(uint item,int quantity=10)=>new(10000,0,item,quantity,false,99);
  ChestStack E()=>new(10000,0,0,0,false,0);
  ChestStack[] bags=[new(0,0,0,0,false,0),new(0,1,0,0,false,0)];
  var full=new Dictionary<ulong,ChestStack[]>{{0,bags},{1,[S(100)]},{2,[S(200)]},{3,[S(300)]}};
  var deadlock=new StorageRoutingPlan("Blocked",1,[new(1,2,[],[100]),new(2,3,[],[200])],1);
  var blocked=StorageRouting.Build(deadlock,full,_=>0,includeExistingBags:false);
  Check("blocked chain never executes a stranded withdrawal",blocked.Moves.Length==0&&blocked.Remaining==20);
  var cycle=new StorageRoutingPlan("Cycle",1,[new(1,2,[],[100]),new(2,3,[],[200]),new(3,1,[],[300])],1);
  var routed=StorageRouting.Build(cycle,full,_=>0,includeExistingBags:false);
  Check("three full retainers can exchange through staging space",routed.Remaining==0&&routed.Moves.Where(m=>m.Withdraw).Sum(m=>m.Move.Quantity)==routed.Moves.Where(m=>!m.Withdraw&&!m.Internal).Sum(m=>m.Move.Quantity));
  var all=StorageAutoRouting.AllRetainers;
  var saved=new RetrievalPreset("Materials",[new(100,"Item",600)],PlanId:"stable");
  RetainerAssignment[] assignments=[new(2,[],[],["stable"]),new(3,[9],[])];
  StorageRoute[] request=[new(StorageRouting.AllSources,all,[],[],AllItems:true)];
  var resolved=StorageAssignments.Apply(request,assignments,full,_=>9,[saved]);
  Check("plan item homes override broad categories",resolved.Single(r=>r.Items.Contains(100u)).Destinations!.SequenceEqual(new ulong[]{2}));
  resolved=StorageAssignments.Apply(request,assignments,full,_=>9,[saved with{Name="Renamed",Items=[new(200,"New item",1)]}]);
  Check("plan rename and edits follow stable identity",resolved.Single(r=>r.Items.Contains(200u)).Destinations!.SequenceEqual(new ulong[]{2})&&resolved.Single(r=>r.Items.Contains(100u)).Destinations!.SequenceEqual(new ulong[]{3}));
  resolved=StorageAssignments.Apply(request,assignments,full,_=>9,[]);
  Check("deleted plan contributes no stale item homes",resolved.All(r=>r.Destinations!.SequenceEqual(new ulong[]{3})));
  var flexible=full.ToDictionary(x=>x.Key,x=>x.Value);flexible[4]=[E()];
  var rules=new StorageRoute[]{new(StorageRouting.AllSources,all,[],[100],Destinations:[2,4],Flexible:true),new(2,3,[],[200])};
  routed=StorageRouting.Build(new("Available alternative",1,rules,1),flexible,_=>0,includeExistingBags:false);
  Check("uses open eligible home even if first home has blocked outgoing work",routed.Moves.Any(m=>!m.Withdraw&&m.Retainer==4&&m.Move.Source.Item==100)&&routed.Moves.Where(m=>m.Withdraw).Sum(m=>m.Move.Quantity)==routed.Moves.Where(m=>!m.Withdraw&&!m.Internal).Sum(m=>m.Move.Quantity));
  var valid=new Dictionary<ulong,ChestStack[]>{{0,bags},{1,[S(100,99)]},{2,[S(100,99)]}};
  routed=StorageRouting.Build(new("Allowed homes",1,[new(StorageRouting.AllSources,all,[],[100],Destinations:[1,2],Flexible:true)],1),valid,_=>0,includeExistingBags:false);
  Check("already suitable full stacks stay put without priority churn",routed.Moves.Length==0&&routed.Remaining==0);
  valid[1]=[S(100,30)];valid[2]=[S(100,20)];
  routed=StorageRouting.Build(new("Consolidate suitable homes",1,[new(StorageRouting.AllSources,all,[],[100],Destinations:[1,2],Flexible:true)],1),valid,_=>0,includeExistingBags:false);
  Check("compatible partial stacks still consolidate",routed.Moves.Any(m=>m.Withdraw&&m.Retainer==2)&&routed.Remaining==0);
 }
}
