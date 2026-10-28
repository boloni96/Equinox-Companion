using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class PreviewSearch282Tests
{
 [ModuleInitializer] internal static void Run(){
  void Check(string label,bool ok){if(!ok)throw new Exception(label);Console.WriteLine("PASS preview/search "+label);}
  ChestStack I(uint c,int slot,uint id,int q,bool hq=false)=>new(c,slot,id,q,hq,99);
  ChestStack E(uint c,int slot)=>new(c,slot,0,0,false,0);
  var inventory=new Dictionary<ulong,ChestStack[]>{{0,[E(0,0),E(0,1)]},{1,[I(10000,0,100,95),E(10000,1)]},{2,[I(10000,0,100,10)]}};
  var route=new StorageRoutingPlan("Preview",1,[new(StorageRouting.AllSources,StorageAutoRouting.AllRetainers,[],[],AllItems:true)],0);
  var moves=StorageRouting.Build(route,inventory,_=>0,includeExistingBags:false);
  var rows=StoragePreview.Describe(moves.Moves);
  Check("preview uses actual movement quantities and retainer origin",rows.Any(r=>r.From==2&&r.To==1&&r.Item==100&&r.Quantity==10&&!r.Merge)&&rows.All(r=>r.From!=0&&r.To!=0));
  Check("preview needs no separate retriever stock list",rows.Length>0);
  var blocked=new Dictionary<ulong,ChestStack[]>{{0,[I(0,0,100,20)]},{1,[I(10000,0,100,95)]},{2,[I(10000,0,100,10)]}};
  moves=StorageRouting.Build(route,blocked,_=>0,includeExistingBags:false);
  Check("blocked preview has no invented movements",StoragePreview.Describe(moves.Moves).Length==0&&moves.Remaining>0);
  Check("internal merge is labelled separately",StoragePreview.Describe([new(1,new(I(10000,0,100,10),I(10000,1,100,20),10),false,true)]).Single().Merge);
  var now=DateTimeOffset.UtcNow;
  var character=new SharedCharacter("c","Character","Raiden","Light","EU","Main",[],ContentId:"1",FcId:"fc1",FcMember:true);
  var roster=new SharedRoster(1,now,[new("p","Person",[character])],ItemLocations:[new("c","Character","bag:0","Inventory",now.AddMinutes(-1),[100],[new(100,1,false,"old")])]);
  var actor=new Actor("1","Character",403,403);
  var observations=Enumerable.Range(0,50000).Select(i=>new SyncEvent(i.ToString(),"storage.observed",now.AddSeconds(-50000+i),actor,null,Storage:new("bag:0","Inventory",[100],[new(100,i,false,"Coke alias")]))).Append(new("new","storage.observed",now,actor,null,Storage:new("bag:0","Inventory",[100],[new(100,198,false,"Coke alias")]))).ToArray();
  var watch=System.Diagnostics.Stopwatch.StartNew();var index=ItemSearchIndex.Build(roster,observations,new Dictionary<uint,string>{{100,"Coke"},{200,"Coke"}});var elapsed=watch.ElapsedMilliseconds;
  Check("history reduces to latest inventory",index.LocationCount==1&&index.Search("coke",null).Single().Stack.Quantity==198);
  Check("recorded alias still searchable",index.Search("alias",null).Length==1);
  Check("exact IDs do not match same-name items",index.Search("Coke",200).Length==0&&index.Search("100",null).Length==1);
  Check("empty search is empty",index.Search("",null).Length==0);
  var empty=ItemSearchIndex.Build(roster,[new("empty","storage.observed",now.AddSeconds(1),actor,null,Storage:new("bag:0","Inventory",[],[]))],new Dictionary<uint,string>());
  Check("new empty observation clears old inventory",empty.Search("old",null).Length==0);
  var fc=ItemSearchIndex.Build(roster with{People=[new("p","Person",[character with{FcMember=false}])]},[new("fc","storage.observed",now,actor,null,Storage:new("fc:fc1:20000","FC chest",[100],[new(100,3,false,"Coke")]))],new Dictionary<uint,string>());
  Check("stale local FC membership cannot inject chest stock",fc.LocationCount==1);
  watch.Restart();for(var i=0;i<100;i++)index.Search("coke",null);Console.WriteLine($"Search benchmark: 50,001 observations indexed in {elapsed} ms; 100 cached searches in {watch.ElapsedMilliseconds} ms.");
 }
}
