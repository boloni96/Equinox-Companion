using EquinoxCompanion;
using System.Runtime.CompilerServices;
using System.Text.Json;
internal static class Artisan572Tests
{
    [ModuleInitializer] internal static void Run()
    {
        void Check(string name,bool ok){if(!ok)throw new Exception(name);Console.WriteLine("PASS Artisan target "+name);}
        var p=new PreparationPlan([new(1,"Cobalt Ingot",604,0,288,316,0,true,[],PreparationCoverage.Craftable)],[new(99,1,"Cobalt Ingot",316,1)],[],[]);
        var target=CraftingPreparation.ArtisanTargets(p);
        Check("604 target rather than 316 shortage",target.Single().Crafts==604&&p.Steps.Single().Crafts==316);
        using var json=JsonDocument.Parse(RetainerRetrieval.ExportArtisan("Required",target,true));
        Check("Artisan owns stock subtraction",json.RootElement.GetProperty("SkipIfEnough").GetBoolean()&&json.RootElement.GetProperty("Recipes")[0].GetProperty("Quantity").GetInt32()==604);
        using var exact=JsonDocument.Parse(RetainerRetrieval.ExportArtisan("Missing",p.Steps));
        Check("missing export disables second subtraction",!exact.RootElement.GetProperty("SkipIfEnough").GetBoolean());
        var multi=p with{Steps=[new(99,1,"Cobalt Ingot",106,3)]};
        Check("recipe yield rounds target crafts up",CraftingPreparation.ArtisanTargets(multi).Single().Crafts==202);
    }
}
