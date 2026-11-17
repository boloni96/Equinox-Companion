using EquinoxCompanion;
using System.Runtime.CompilerServices;
using System.Text.Json;
internal static class WorkshopArtisan583Tests
{
    [ModuleInitializer] internal static void Run()
    {
        void Check(string name,bool ok){if(!ok)throw new Exception(name);Console.WriteLine("PASS workshop Artisan: "+name);}
        var outputs=Enumerable.Range(0,26).Select(i=>new RetrievalEntry((uint)(100+i),"Output "+i,6)).ToArray();
        var recipes=outputs.ToDictionary(e=>e.ItemId,e=>new[]{new RetrievalRecipe(e.ItemId+100,e.ItemId,3,[new(e.ItemId+1000,"Raw "+e.ItemId,2)],"Smith")});
        var stock=outputs.Take(3).ToDictionary(e=>e.ItemId,e=>6);
        var missing=CraftingPreparation.Build(outputs,recipes,stock,new Dictionary<uint,int>(),true);
        Check("26 requested recipes become 23 only when three are covered",missing.Steps.Length==23);
        var full=CraftingPreparation.ArtisanPlan(missing,outputs,recipes,false);
        Check("full mode restores all 26 recipes including covered outputs",full.Steps.Length==26&&full.Steps.Select(e=>e.ItemId).ToHashSet().SetEquals(outputs.Select(e=>e.ItemId)));
        Check("missing mode preserves exact stock-adjusted plan",ReferenceEquals(CraftingPreparation.ArtisanPlan(missing,outputs,recipes,true),missing));
        using var json=JsonDocument.Parse(RetainerRetrieval.ExportArtisan("Full",full.Steps,true));
        Check("clipboard JSON contains every full recipe with craft quantities",json.RootElement.GetProperty("Recipes").GetArrayLength()==26&&json.RootElement.GetProperty("Recipes").EnumerateArray().All(e=>e.GetProperty("Quantity").GetInt32()==2));
        Check("literal targets preserve direct plus ingredient requirements",json.RootElement.GetProperty("SkipLiteral").GetBoolean());
        using var reduced=JsonDocument.Parse(RetainerRetrieval.ExportArtisan("Missing",missing.Steps));
        Check("missing clipboard disables second stock subtraction",reduced.RootElement.GetProperty("Recipes").GetArrayLength()==23&&!reduced.RootElement.GetProperty("SkipIfEnough").GetBoolean());
        var owned=CraftingPreparation.Build(outputs,recipes,outputs.ToDictionary(e=>e.ItemId,e=>e.Quantity),new Dictionary<uint,int>(),true);
        Check("fully owned queue can still export a full recipe list",owned.Steps.Length==0&&CraftingPreparation.ArtisanPlan(owned,outputs,recipes,false).Steps.Length==26);
        var chain=new Dictionary<uint,RetrievalRecipe[]>{[1]=[new(11,1,1,[new(2,"Subcraft",2)],"A")],[2]=[new(12,2,1,[new(3,"Raw",3)],"A")]};
        var partial=CraftingPreparation.Build([new(1,"Final",1)],chain,new Dictionary<uint,int>{{2,2}},new Dictionary<uint,int>(),true);
        var fullChain=CraftingPreparation.ArtisanPlan(partial,[new(1,"Final",1)],chain,false);
        Check("full mode restores covered subcraft before the parent",partial.Steps.Length==1&&fullChain.Steps.Select(e=>e.ItemId).SequenceEqual([2u,1u])&&fullChain.Missing.Single().Quantity==6);
    }
}
