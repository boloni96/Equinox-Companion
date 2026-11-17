using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class Chest584Tests
{
    [ModuleInitializer] internal static void Run()
    {
        void Check(string name,bool ok){if(!ok)throw new Exception(name);Console.WriteLine("PASS chest584: "+name);}
        var now=DateTimeOffset.Parse("2026-11-17T18:42:00Z");var gate=new ChestSnapshotGate();
        ChestStack[] Page(int count)=>[new(22001,0,2,count,false,9999,true)];
        Check("first loaded sample is not sufficient",!gate.Observe(1,10,20,22001,Page(8647),now));
        Check("wait for settled contents",!gate.Observe(1,10,20,22001,Page(8647),now.AddMilliseconds(500)));
        Check("stable page can be read",gate.Observe(1,10,20,22001,Page(8647),now.AddSeconds(1)));
        Check("withdrawal resets readiness",!gate.Observe(1,10,20,22001,Page(0),now.AddSeconds(2)));
        Check("confirmed zero is authoritative",gate.Observe(1,10,20,22001,Page(0),now.AddSeconds(3)));
        gate.Reset();Check("busy transition needs new stable samples",!gate.Observe(1,10,20,22001,Page(0),now.AddSeconds(4)));
        Check("other FC cannot reuse readiness",!gate.Observe(1,11,20,22001,Page(0),now.AddSeconds(5)));
        Check("character change cannot reuse readiness",!gate.Observe(2,11,20,22001,Page(0),now.AddSeconds(6)));
        Check("page change cannot reuse readiness",!gate.Observe(2,11,20,20000,Page(0),now.AddSeconds(7)));
        var outputs=new[]{new RetrievalEntry(2,"Fire Shard",22136)};var recipes=new Dictionary<uint,RetrievalRecipe[]>();
        int Missing(int bags,int chest)=>CraftingPreparation.Build(outputs,recipes,new Dictionary<uint,int>{{2,bags}},new Dictionary<uint,int>{{2,chest}},true).Missing.Single().Quantity;
        Check("moving shards between bags and chest preserves shortfall",Missing(8647,0)==13489&&Missing(0,8647)==13489);
        Check("517 stale stock would produce the reported incorrect shortage",Missing(0,517)==21619);
    }
}
