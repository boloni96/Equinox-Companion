using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class Retriever269MoreTests
{
    [ModuleInitializer] internal static void Run()
    {
        void Check(string label,bool ok){if(!ok)throw new Exception(label);Console.WriteLine("PASS retriever 269 "+label);}
        var ore=new RetrievalEntry(1000,"Ore",4);var ingot=new RetrievalEntry(2000,"Ingot",1);var jacket=new RetrievalEntry(3000,"Jacket",1);
        var recipes=new Dictionary<uint,RetrievalRecipe[]>{{2000,[new(20,2000,1,[ore,new(8,"Fire crystal",7)],"Smith"),new(21,2000,1,[ore,new(9,"Ice crystal",7)],"Armourer")]},{3000,[new(30,3000,1,[ingot],"Leatherworker")]}};
        var raw=RetainerRetrieval.PlanCrafts([jacket],recipes);
        Check("job alternatives differing only in crystals do not block subcraft",raw.Materials.Single(x=>x.ItemId==1000).Quantity==4);
        Check("chosen recipe includes its own crystal type",raw.Materials.Any(x=>x.ItemId==8)&&!raw.Materials.Any(x=>x.ItemId==9));
        var alternate=RetainerRetrieval.PlanCrafts([ingot with{RecipeId=21}],recipes);
        Check("explicit job choice overrides automatic alternate",alternate.Materials.Any(x=>x.ItemId==9)&&!alternate.Materials.Any(x=>x.ItemId==8));
        var outfits=new Dictionary<uint,RetrievalEntry[]>{{4000,[jacket,new(5000,"Noncraftable piece",1)]}};
        var plan=RetainerRetrieval.BuildPlan([new(4000,"Attire",2)],1,false,RetrievalMode.DirectIngredients,recipes,new Dictionary<uint,RetrievalEntry[]>(),outfits);
        Check("outfit resolves pieces without inventing recipes for noncraftable members",plan.Materials.Single(x=>x.ItemId==2000).Quantity==2&&plan.Materials.Single(x=>x.ItemId==5000).Quantity==2);
        var exact=RetainerRetrieval.BuildPlan([new(4000,"Attire",2)],1,false,RetrievalMode.CompletedItems,recipes,new Dictionary<uint,RetrievalEntry[]>(),outfits);
        Check("exact mode retains outfit item",exact.Materials.Single().ItemId==4000);
        var slots=new[]{new RetrievalSlot(1,50,10000,0,false),new RetrievalSlot(1,3,10000,1,true)};
        Check("HQ-only excludes larger NQ stack",slots.Where(s=>RetainerRetrieval.AllowsQuality(s.Hq,RetrievalQuality.HqOnly)).Sum(s=>s.Quantity)==3);
        Check("NQ-only excludes HQ stack",slots.Where(s=>RetainerRetrieval.AllowsQuality(s.Hq,RetrievalQuality.NqOnly)).Sum(s=>s.Quantity)==50);
        Check("any quality combines both",slots.Where(s=>RetainerRetrieval.AllowsQuality(s.Hq,RetrievalQuality.Any)).Sum(s=>s.Quantity)==53);
        var p=new RetrievalPreset("HQ",[ingot],Quality:RetrievalQuality.HqOnly);var round=RetainerRetrieval.Import(RetainerRetrieval.Export(p),_=>ingot);
        Check("quality survives preset export",round.Quality==RetrievalQuality.HqOnly);
    }
}
