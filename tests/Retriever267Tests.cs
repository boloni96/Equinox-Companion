using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class Retriever267Tests
{
    [ModuleInitializer] internal static void Run()
    {
        void Check(string label,bool ok){if(!ok)throw new Exception(label);Console.WriteLine("PASS retriever 267 "+label);}
        var projects=new Dictionary<uint,RetrievalEntry[]>{{100,[new(1,"Ingot",10),new(2,"Lumber",5)]},{101,[new(1,"Ingot",20)]},{102,[new(2,"Lumber",15)]},{103,[new(3,"Rivet",30)]}};
        var parts=new[]{new RetrievalEntry(100,"Bow",1),new RetrievalEntry(101,"Bridge",1),new RetrievalEntry(102,"Hull",1),new RetrievalEntry(103,"Stern",1)};
        var turnins=RetainerRetrieval.WorkshopTurnIns(parts,projects);
        Check("four selected parts become combined workshop supplies",turnins.Length==3&&turnins.Single(x=>x.ItemId==1).Quantity==30&&turnins.Single(x=>x.ItemId==2).Quantity==20);
        var four=RetainerRetrieval.WorkshopTurnIns(parts.Select(e=>e with{Quantity=4}),projects);
        Check("four submarines scale every part requirement",four.Single(x=>x.ItemId==3).Quantity==120);
        Check("already expanded turn-ins stay unchanged",RetainerRetrieval.WorkshopTurnIns(turnins,projects).SequenceEqual(turnins));
        Check("mixed ordinary items preserved",RetainerRetrieval.WorkshopTurnIns(parts.Append(new(9,"Potion",7)),projects).Single(x=>x.ItemId==9).Quantity==7);
        var recipes=new Dictionary<uint,RetrievalRecipe[]>{{1,[new(10,1,1,[new(8,"Ore",2)],"Smith")]}};
        var raw=RetainerRetrieval.PlanCrafts(turnins,recipes);
        Check("workshop supplies expand into crafting ingredients",raw.Materials.Single(x=>x.ItemId==8).Quantity==60&&!raw.Materials.Any(x=>x.ItemId==100));
    }
}
