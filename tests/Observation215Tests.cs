using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Observation215Tests
{
 [ModuleInitializer]internal static void Run()
 {
    var now=DateTimeOffset.Parse("2026-10-20T04:00:00Z");
    var actor=new Actor("123","Garden Owner",57,57,"Siren","Siren");
    var home=new Address("0000000000000001",57,339,1,1,0,false,false,"Siren","Mist");
    var houses=new List<HouseObservation>();var tends=new List<TendingRecord>();var plants=new List<PlantingRecord>();
    var history=Enumerable.Range(0,30000).Select(i=>new SyncEvent(i.ToString("x32"),"garden.empty",now.AddSeconds(-30000+i),actor,home,1,1)).ToList();
    var sent=history.Select(e=>e.Id).ToList();var index=new ObservationIndex();
    void Refresh()=>index.Refresh(houses,tends,plants,history,sent,"pair-a");
    void Check(bool good,string message){if(!good)throw new Exception("Observation215: "+message);}
    Refresh();Check(index.PendingCount==0,"acknowledged history is separate from pending");var read=index.IndexedRecords;
    for(var i=0;i<100;i++)Refresh();Check(index.IndexedRecords==read,"idle updates never scan old history");
    var fresh=new SyncEvent("fresh","garden.empty",now,actor,home,2,8);history.Add(fresh);Refresh();
    Check(index.IndexedRecords==read+1&&index.Pending.Single()==fresh,"new batch2 bed8 action indexed immediately");
    Check(index.LastDiscovery(fresh)==fresh&&index.LastDiscovery(fresh with {Bed=7}) is null,"dedup identity includes exact bed");
    var replanted=new PlantingRecord("plant",now.AddSeconds(1),actor,home,2,8,new(1,"Seed",2,"Soil"));plants.Add(replanted);Refresh();
    Check(index.PlantedAfter(fresh,now)&&!index.PlantedAfter(fresh with {Bed=7},now),"replant invalidation remains bed-specific");
    sent.Add("fresh");Refresh();Check(index.Pending.Single().Id=="plant","partial receipt removes only accepted actions");
    var restart=new ObservationIndex();restart.Refresh(houses,tends,plants,history,sent,"pair-a");Check(restart.Pending.Select(e=>e.Id).SequenceEqual(index.Pending.Select(e=>e.Id)),"restart reconstructs exact pending state");
    sent.Clear();index.Refresh(houses,tends,plants,history,sent,"pair-b");Check(index.PendingCount==30002,"new pairing replays retained records");
    var collection=fresh with {Id="segment1",Kind="collection.observed",Collection=new("mount",[1],[1],[],1)};
    history.Add(collection);history.Add(collection with {Id="segment2",Collection=collection.Collection! with {Segment=2}});index.Refresh(houses,tends,plants,history,sent,"pair-b");
    Check(index.LastDiscovery(collection)?.Id=="segment1","collection segments preserve separate current state");
    var old=now.AddDays(-100);var oldRows=new[]{fresh with {Id="old",At=old},fresh with {Id="newest",At=old.AddDays(1)},fresh with {Id="unsent",At=old.AddDays(-1)},fresh with {Id="other-bed",At=old,Bed=7}};
    var prune=ObservationRetention.Plan([],[],[],oldRows,["old","newest","other-bed"],now.AddDays(-60));
    Check(prune.Discoveries.SetEquals(["old"]),"retention keeps current state, every unsent action, and independent beds");
    var sameTime=new[]{fresh with {Id="z",Kind="garden.tended"},fresh with {Id="a",Kind="garden.planted"},fresh with {Id="m",Kind="garden.empty"}};
    var ordered=SyncQueue.Select(sameTime,_=>1).Select(e=>e.Id).ToArray();
    Check(ordered.SequenceEqual(SyncQueue.Select(sameTime.Reverse(),_=>1).Select(e=>e.Id)),"equal timestamps use existing deterministic ID order independent of index insertion");
    Console.WriteLine("PASS215 indexed outbox: 30000 history records, incremental updates, partial receipts, pairing/restart and garden retention");
 }
}
