using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class MaterialColors583Tests
{
    [ModuleInitializer] internal static void Run()
    {
        void Check(string name,bool ok){if(!ok)throw new Exception(name);Console.WriteLine("PASS material colors: "+name);}
        var recipes=new Dictionary<uint,RetrievalRecipe[]>{
            [100]=[new(1,100,1,[new(200,"Ingot",2)],"Smith")],
            [200]=[new(2,200,1,[new(300,"Ore",3),new(2,"Fire Shard",1)],"Smith")]};
        PreparationPlan Plan(Dictionary<uint,int> stock)=>CraftingPreparation.Build([new(100,"Rivet",3)],recipes,new Dictionary<uint,int>(),stock,true);
        var missing=Plan(new(){{300,18},{2,5}});
        var rivet=missing.Rows.Single(r=>r.ItemId==100);
        Check("missing zero still red when a raw ingredient is short",rivet.Missing==0&&rivet.Coverage==PreparationCoverage.Missing&&rivet.MissingIngredients.SequenceEqual(["Fire Shard"]));
        Check("covered ore stays gold despite unrelated shard shortage",missing.Rows.Single(r=>r.ItemId==300).Coverage==PreparationCoverage.Retainers);
        var ready=Plan(new(){{300,18},{2,6}});
        Check("blue requires all ingredients for all remaining crafts",ready.Rows.Single(r=>r.ItemId==100).Coverage==PreparationCoverage.Craftable&&ready.Rows.Single(r=>r.ItemId==100).MissingIngredients.Length==0);
        var covered=Plan(new(){{100,3}});
        Check("owned turn-ins have no crafting blockers",covered.Rows.Length==1&&covered.Rows[0].Coverage==PreparationCoverage.Retainers&&covered.Rows[0].MissingIngredients.Length==0);
        Check("turn-in view excludes ore and subcraft",missing.Rows.Where(r=>r.MatchesView(1)).Select(r=>r.ItemId).SequenceEqual([100u]));
        Check("ingredients view includes raw and intermediate crafts",missing.Rows.Where(r=>r.MatchesView(2)).Select(r=>r.ItemId).ToHashSet().SetEquals([200u,300u,2u]));
        var shared=CraftingPreparation.Build([new(100,"Rivet",1),new(200,"Ingot",1)],recipes,new Dictionary<uint,int>(),new Dictionary<uint,int>(),true).Rows.Single(r=>r.ItemId==200);
        Check("shared turn-in ingredient appears in both without double count",shared.MatchesView(1)&&shared.MatchesView(2)&&shared.Required==3&&shared.RequestedQuantity==1);
        var outputs=new[]{new RetrievalEntry(100,"Rivet",1),new RetrievalEntry(200,"Ingot",1)};
        var reserved=new Dictionary<uint,PreparationStockAllocation>{{100,new(0,0)},{200,new(1,0)}};
        var surplus=CraftingPreparation.Build(outputs,recipes,new Dictionary<uint,int>{{200,3}},new Dictionary<uint,int>(),true,reserved);
        Check("surplus turn-in bags still cover ingredient demand",surplus.Rows.Single(r=>r.ItemId==200).Bags==3&&surplus.Rows.Single(r=>r.ItemId==200).CraftQuantity==0&&surplus.Rows.All(r=>r.ItemId!=2));
        reserved[200]=new(0,1);
        surplus=CraftingPreparation.Build(outputs,recipes,new Dictionary<uint,int>(),new Dictionary<uint,int>{{200,3}},true,reserved);
        Check("surplus stored turn-ins prevent extra crafts and shards",surplus.Rows.Single(r=>r.ItemId==200).Retainers==3&&surplus.Missing.Length==0);
        Check("ingredient withdrawal retains surplus of shared turn-in items",CraftingPreparation.IngredientWithdrawals(surplus,[new(200,"Ingot",1)]).Single().Quantity==2);
        // Two NQ and eight HQ cannot deliver one ten-item set. The spare HQ can
        // nevertheless supply the two ingots used to craft a rivet.
        var qualityOutputs=new[]{new RetrievalEntry(100,"Rivet",1),new RetrievalEntry(200,"Ingot",10)};
        reserved[200]=new(2,0);
        var quality=CraftingPreparation.Build(qualityOutputs,recipes,new Dictionary<uint,int>{{200,10}},new Dictionary<uint,int>{{300,24},{2,8}},true,reserved);
        var ingot=quality.Rows.Single(r=>r.ItemId==200);
        Check("incompatible quality cannot satisfy direct delivery",ingot.Required==12&&ingot.Bags==4&&ingot.CraftQuantity==8);
        Check("surplus quality can supply other crafts without extra shard demand",quality.Rows.Single(r=>r.ItemId==2).Required==8&&quality.Missing.Length==0);
        var changed=false;try{CraftingPreparation.Build(qualityOutputs,recipes,new Dictionary<uint,int>{{200,1}},new Dictionary<uint,int>(),true,reserved);}catch(InvalidOperationException){changed=true;}
        Check("stock changes cannot create negative shortages",changed);


    }
}
