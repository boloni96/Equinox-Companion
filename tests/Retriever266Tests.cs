using EquinoxCompanion;
using System.Runtime.CompilerServices;
using System.Text.Json;
internal static class Retriever266Tests
{
    [ModuleInitializer] internal static void Run()
    {
        void Check(string name,bool ok){if(!ok)throw new Exception("Retriever 266: "+name);Console.WriteLine("PASS retriever 266 "+name);}
        var ingot=new RetrievalEntry(1,"Ingot",10,10);var rivet=new RetrievalEntry(2,"Rivets",5,20);var ore=new RetrievalEntry(3,"Ore",1);
        var recipes=new Dictionary<uint,RetrievalRecipe[]>{{1,[new(10,1,1,[ore with{Quantity=2}],"Smith")]},{2,[new(20,2,1,[ingot with{Quantity=2}],"Smith")]}};
        var exact=RetainerRetrieval.Requested([ingot,rivet],1,false,recipes);
        Check("workshop turn-ins preserve every required item",exact.Length==2&&exact[0].Quantity==10);
        var goals=RetainerRetrieval.Requested([ingot,rivet],1,true,recipes);
        Check("expanded list removes consumed subcrafts",goals.Length==1&&goals[0].ItemId==2&&goals[0].Quantity==5);
        var plan=RetainerRetrieval.PlanCrafts(goals,recipes);
        Check("ingredients do not double count listed intermediate",plan.Materials.Single().ItemId==3&&plan.Materials.Single().Quantity==20);
        Check("subcrafts precede final outputs",plan.Steps.Select(s=>s.ItemId).SequenceEqual(new uint[]{1,2}));
        goals=RetainerRetrieval.Requested([ingot with{Quantity=12},rivet],1,true,recipes);
        Check("extra requested intermediate preserved",goals.First(g=>g.ItemId==1).Quantity==2);
        plan=RetainerRetrieval.PlanCrafts(goals,recipes);
        Check("duplicate recipes combined for Artisan",plan.Steps.Count(s=>s.ItemId==1)==1&&plan.Steps.First(s=>s.ItemId==1).Crafts==12);
        var scaled=RetainerRetrieval.Requested([ingot,rivet],4,false,recipes);
        Check("multiplier applies to actual rows",scaled[0].Quantity==40&&scaled[1].Quantity==20);
        using var json=JsonDocument.Parse(RetainerRetrieval.ExportArtisan("Workshop",plan.Steps));
        Check("Artisan export uses recipe IDs and craft counts",json.RootElement.GetProperty("Recipes")[0].GetProperty("ID").GetUInt32()==10&&json.RootElement.GetProperty("Recipes")[0].GetProperty("Quantity").GetInt32()==12);
        var preset=new RetrievalPreset("4x is just a name",[ingot],1,true,true);
        var saved=RetainerRetrieval.Import(RetainerRetrieval.Export(preset),id=>id=="1"?ingot:null);
        Check("preset keeps explicit interpretation",saved.IncludesSubcrafts&&saved.ArtisanPreset&&saved.Multiplier==1);
    }
}
