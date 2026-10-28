using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class BackgroundStorage283Tests
{
 [ModuleInitializer] internal static void Run(){
  void Check(string label,bool ok){if(!ok)throw new Exception(label);Console.WriteLine("PASS background/storage "+label);}
  var now=DateTimeOffset.Parse("2026-10-28T21:00:00Z");var actor=new Actor("123","Test Actor",410,410);
  SyncEvent Storage(int n,int amount)=>new(n.ToString("x32"),"storage.observed",now.AddSeconds(n),actor,null,Storage:new("bag:4000","Chocobo saddlebag 1",amount>0?[100u]:[],amount>0?[new(100,amount,false,"Coke",[new(1,amount)])]:[]));
  var rows=Enumerable.Range(0,52000).Select(i=>Storage(i,i)).ToList();var index=new DiscoveryReadIndex();index.Refresh(rows);
  Check("latest storage is indexed",index.Last("storage.observed",actor.ContentId,storage:"bag:4000")==rows[^1]);
  var scanned=index.IndexedRecords;for(var i=0;i<1000;i++)index.Refresh(rows);
  Check("unchanged history is not rescanned",index.IndexedRecords==scanned);
  rows.Add(Storage(52000,3));index.Refresh(rows);Check("append indexes only the new observation",index.IndexedRecords==scanned+1);
  rows.RemoveAt(0);rows.Add(Storage(52001,4));index.Refresh(rows);Check("same-count prune and append rebuilds safely",index.Kind("storage.observed").Count==rows.Count&&index.Last("storage.observed",actor.ContentId,storage:"bag:4000")==rows[^1]);
  rows.Clear();index.Refresh(rows);Check("pruned storage does not linger",index.Kind("storage.observed").Count==0&&index.Last("storage.observed",actor.ContentId,storage:"bag:4000")==null);
  var address=new Address("0000000000000001",410,641,1,1,0,false,false);
  rows.Add(new("e1","garden.empty",now,actor,address,1,2));rows.Add(new("e0","garden.empty",now.AddDays(-1),actor,address,1,2));index.Refresh(rows);
  Check("empty index retains newest evidence, not insertion order",index.EmptyAt(address.HouseId,1,2)==now&&index.EmptyAt(address.HouseId,2,2)==DateTimeOffset.MinValue);
  var gate=new StorageCaptureGate();StorageSlotValue[] slots=[new(100,5,false),new(0,0,false)];
  Check("first loaded saddlebag needs capture",gate.Needs(actor,"bag:4000",slots,now));gate.Remember(actor,"bag:4000",slots,now);
  Check("unchanged loaded pages skip expensive processing",!gate.Needs(actor,"bag:4000",slots,now.AddSeconds(1)));
  Check("quantity and quality changes captured",gate.Needs(actor,"bag:4000",[new(100,4,false),new(0,0,false)],now)&&gate.Needs(actor,"bag:4000",[new(100,5,true),new(0,0,false)],now));
  Check("position changes captured",gate.Needs(actor,"bag:4000",[new(0,0,false),new(100,5,false)],now));
  Check("premium and second pages independent",gate.Needs(actor,"bag:4100",slots,now)&&gate.Needs(actor,"bag:4001",slots,now));
  Check("new character cannot inherit capture gate",gate.Needs(actor with{ContentId="456"},"bag:4000",slots,now));
  Check("world changes and periodic refresh retained",gate.Needs(actor with{CurrentWorldId=403},"bag:4000",slots,now)&&gate.Needs(actor,"bag:4000",slots,now.AddMinutes(30)));
  Check("capture gate owns its slot snapshot",!gate.Needs(actor,"bag:4000",slots,now));slots[0]=new(100,9,false);Check("mutated source cannot change gate snapshot",gate.Needs(actor,"bag:4000",slots,now));
  var event1=Storage(1,5);var event2=Storage(2,0);var character=new SharedCharacter("c","Test Actor","Rafflesia","Dynamis","NA","Main",[],ContentId:"123");
  var roster=new SharedRoster(1,now,[new("p","Person",[character])],ItemLocations:[new("c","Test Actor","bag:4000","Saddlebag",now,[100],[new(100,1,false,"Coke")])]);
  var search=ItemSearchIndex.Build(roster,[event1],new Dictionary<uint,string>{{100,"Coke"}});
  Check("local capture replaces older paired saddlebag",search.Search("coke",null).Single().Stack.Quantity==5);
  search=ItemSearchIndex.Build(roster,[event1,event2],new Dictionary<uint,string>{{100,"Coke"}});
  Check("confirmed empty page clears old paired item",search.Search("coke",null).Length==0);
  var pending=Enumerable.Range(1,100).Select(i=>Storage(i,5)).ToArray();
  var prepared=UploadPreparation.Build(pending,[],now.AddHours(1),25,false,_=>100);
  Check("background upload keeps recent and historical fairness",prepared.Events.Length==50&&prepared.Events.Contains(pending[0])&&prepared.Events.Contains(pending[^1]));
  Check("preparation does not mutate durable source",pending.Length==100&&pending[0].Storage!.Stacks![0].Quantity==5);
  Check("old server retains unsupported storage",UploadPreparation.Build(pending,[],now.AddHours(1),17,false,_=>100) is {Unsupported:100,Events.Length:0});
  Check("invalid evidence held rather than sent",UploadPreparation.Build([event1 with{Actor=actor with{ContentId=""}}],[],now.AddHours(1),25,false,_=>100) is {Held.Length:1,Events.Length:0});
  var irrelevant=Storage(1,5);Check("lottery merge excludes inventory history",!HousingLotteryNotices.WithShared([irrelevant],null).Any());
 }
}
