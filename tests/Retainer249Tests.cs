using EquinoxCompanion;
using System.Runtime.CompilerServices;
using System.Text.Json;
internal static class Retainer249Tests
{
 [ModuleInitializer] internal static void Run(){
  static void Check(string name,bool ok){if(!ok)throw new Exception("Retainers: "+name);Console.WriteLine("PASS 249 "+name);}
  var now=DateTimeOffset.UtcNow;var actor=new Actor("12345","Market Tester",410,410,"Rafflesia","Rafflesia");
  var items=Enumerable.Range(0,20).Select(i=>new RetainerMarketItem(i,10,new string('界',100),true,9999,999999999,now.AddDays(-10))).ToArray();
  var rows=Enumerable.Range(1,10).Select(i=>new RetainerMarketRow(i.ToString(),"Retainer "+i,i-1,100,now,now.AddDays(-6),items)).ToArray();
  var details=new RetainerMarketDetails(rows);var outbox=new RetainerMarketOutbox();
  Check("observed retainer payload is valid",RetainerMarketSnapshot.Valid(details,now));
  Check("first snapshot queues",outbox.Put(actor,details,now));Check("one envelope per retainer",outbox.Pending.Count()==10);
  var json=new JsonSerializerOptions(JsonSerializerDefaults.Web);
  Check("Unicode maximum inventory fits upload budget",outbox.Pending.All(e=>JsonSerializer.SerializeToUtf8Bytes(new{events=new[]{e}},json).Length<60000));
  Check("snapshot does not repeat unchanged data",!outbox.Put(actor,details,now.AddMinutes(1))&&outbox.Pending.Count()==10);
  var first=outbox.Pending.First();Check("upload receipt acknowledges only current snapshot",outbox.Acknowledge(first.Id)&&outbox.Pending.Count()==9);
  var saved=outbox.Snapshot();var updated=details with {Retainers=[rows[0] with {Items=[],ObservedAt=now.AddSeconds(1),LastVisit=now}]};
  Check("one changed retainer replaces its snapshot",outbox.Put(actor,updated,now.AddSeconds(1))&&outbox.Pending.Count()==10&&outbox.Snapshots.Count==10);
  Check("late acknowledgement cannot erase replacement",!outbox.Acknowledge(first.Id)&&outbox.Pending.Count()==10);
  Check("configuration snapshot containers are independent",saved.Pending.Count()==9&&saved.Snapshots.Values.First().Market!.Retainers[0].Items.Length==20);
  foreach(var e in outbox.Pending.ToArray())outbox.Acknowledge(e.Id);
  Check("all accepted leaves no pending market uploads",!outbox.Pending.Any());
  Check("pairing change resends snapshots to new journal",outbox.Scope("new")&&outbox.Pending.Count()==10);
  Check("same pairing keeps receipts",!outbox.Scope("new"));
  Check("old Journal holds retainer events",!SyncValidation.SupportedByWebsite(first,24)&&SyncValidation.SupportedByWebsite(first,25));
  Check("retainer event needs no estate",SyncValidation.CanSend(first,now));
  Check("invalid duplicate slots rejected",!RetainerMarketSnapshot.Valid(new([rows[0] with {Items=[items[0],items[0]]}]),now));
  Check("invalid future observation rejected",!RetainerMarketSnapshot.Valid(new([rows[0] with {ObservedAt=now.AddHours(1)}]),now));
  var gameplay=Enumerable.Range(20,50).Select(i=>new SyncEvent(i.ToString("x32"),"house.entered",now.AddMinutes(-i),actor,null)).ToArray();
  var selected=SyncQueue.Select(gameplay.Concat(outbox.Pending),_=>500);
  Check("gameplay fills batch before market snapshots",selected.Length==50&&selected.All(e=>e.Kind!="market.retainers"));
  selected=SyncQueue.Select(gameplay.Take(3).Concat(outbox.Pending),x=>x[0].Kind=="market.retainers"?10000:1000);
  Check("market only uses remaining bytes and slots",selected.Take(3).All(e=>e.Kind!="market.retainers")&&selected.Length==8);
  Check("empty gameplay queue permits market sync",SyncQueue.Select(outbox.Pending,_=>1000).Length==10);
 }
}
