using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class InventoryReview302Tests
{
    [ModuleInitializer] internal static void Run()
    {
        void Check(bool ok,string name){if(!ok)throw new Exception("Inventory export: "+name);}
        var t=DateTimeOffset.Parse("2026-10-29T20:00:00Z");var a=new Actor("1","One",1,1,"World");var b=new Actor("2","Two",1,1,"World");
        SyncEvent E(Actor actor,string key,DateTimeOffset at,StorageStack[]? stacks,uint[]? ids=null)=>new(Guid.NewGuid().ToString(),"storage.observed",at,actor,null,Storage:new(key,key,ids??stacks?.Select(s=>s.ItemId).ToArray()??[],stacks));
        SyncEvent[] events=[E(a,"bag:0",t,[new(100,5,false,"Ore")]),E(a,"bag:0",t.AddMinutes(1),[]),E(b,"bag:0",t,[new(100,9,true,"Ore")]),E(a,"dresser",t,null,[200]),E(a,"fc:9:20000",t,[new(100,3,false,"Ore")]),E(b,"fc:9:20000",t.AddMinutes(1),[new(100,7,false,"Ore")]),E(a,"retainer:10:10000",t,[new(100,10,false,"Ore")])];
        RetainerInventoryRecord[] records=[new(1,10,t.AddMinutes(2),[new(10000,0,100,2,false,99),new(10000,1,0,0,false,0)])];
        var report=InventoryReview.Build(events,records,new Dictionary<ulong,string>{{10,"Materia"}},new Dictionary<uint,string>{{100,"Ore"}},new Dictionary<uint,uint>{{100,5}},new Dictionary<uint,string>{{5,"Materials"}});
        Check(report.Single(x=>x.CharacterId=="1"&&x.Key=="bag:0").Items.Length==0,"latest empty container replaces historical contents");
        Check(report.Single(x=>x.CharacterId=="2"&&x.Key=="bag:0").Items.Single().Quantity==9,"characters keep separate bags");
        var dresser=report.Single(x=>x.Key=="dresser").Items.Single();Check(dresser.Quantity==null&&dresser.Hq==null,"presence-only storage never invents quantities or quality");
        Check(report.Count(x=>x.Key=="fc:9:20000")==1&&report.Single(x=>x.Key=="fc:9:20000").Items.Single().Quantity==7,"shared chest snapshots not double counted");
        var retainer=report.Single(x=>x.Key=="retainer:10:10000");Check(retainer.Items.Single().Quantity==2&&retainer.CapacitySlots==2,"newer Organizer inventory wins");
        Check(retainer.Items.Single().Category=="Materials"&&retainer.Items.Single().StackCount==1,"category and occupied stack counts included");
        Console.WriteLine("PASS inventory review: newest records, shared locations, unknown quantities and categories");
    }
}
