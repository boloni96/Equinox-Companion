using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Performance501Tests
{
 [ModuleInitializer] internal static void Run(){
  var now=DateTimeOffset.Parse("2026-11-08T03:00:00Z");var actor=new Actor("1","Test",1,1);
  SyncEvent E(string id,string kind,int seconds=0,string? box=null)=>new(id,kind,now.AddSeconds(seconds),actor,null,Storage:box is null?null:new(box,box,[]));
  var rows=Enumerable.Range(0,90000).Select(i=>E(i.ToString(),"collection.observed")).ToList();
  rows.Add(E("fashion","fashion.observed"));rows.Add(E("garden","garden.mapped"));
  rows.Add(E("new","storage.observed",10,"retainer:1:10000"));rows.Add(E("old","storage.observed",0,"retainer:1:10000"));
  var index=new DiscoveryReadIndex();index.Refresh(rows);var revision=index.GardenRevision;var scanned=index.IndexedRecords;
  for(var i=0;i<100;i++){index.Refresh(rows);if(index.Kind("fashion.observed").Count!=1)throw new Exception("Fashion index");}
  if(index.IndexedRecords!=scanned)throw new Exception("Unchanged history rescanned");
  if(index.StorageFor("1").Single().Id!="new"||index.StorageFor("2").Any())throw new Exception("Storage date/owner isolation");
  rows.Add(E("other","collection.observed"));index.Refresh(rows);
  if(index.GardenRevision!=revision)throw new Exception("Unrelated event invalidated garden");
  rows.Add(E("tend","garden.tended"));index.Refresh(rows);
  if(index.GardenRevision==revision||index.GardenEvents.Count!=2)throw new Exception("Garden update not visible");
  rows.Add(E("tie","storage.other",10,"retainer:1:10000"));index.Refresh(rows);
  if(index.StorageFor("1").Single().Id!="new")throw new Exception("Stable equal-time storage");
  index.Refresh([E("replacement","garden.empty")]);
  if(index.StorageFor("1").Any()||index.Kind("fashion.observed").Count!=0||index.GardenEvents.Count!=1)throw new Exception("Replacement retained stale data");
  var history=new PerformanceHitchHistory();history.Record(1,"inner",3000,1,now,null);history.Record(1,"outer",3010,1,now,null);
  for(var i=2;i<400;i++)history.Record(i,"draw",20,i,now,null);
  if(history.Snapshot(400).Any(x=>x.Frame==1)||history.WorstSnapshot(400).First().Milliseconds!=3010||history.WorstSnapshot(400).Count(x=>x.Frame==1)!=1)throw new Exception("Major freeze lost or nested duplicates retained");
  if(history.WorstSnapshot(1001).Length!=0)throw new Exception("Expired freezes retained");
  history.Clear();if(history.WorstSnapshot(1).Length!=0)throw new Exception("Reset retained freezes");
  Console.WriteLine("501 indexed history, relevant invalidation and major-freeze retention passed.");
 }
}
