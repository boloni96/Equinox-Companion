using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class Workshop582Tests
{
    [ModuleInitializer] internal static void Run()
    {
        void Check(string name,bool ok){if(!ok)throw new Exception(name);Console.WriteLine("PASS workshop582: "+name);}
        var recipes=new Dictionary<uint,RetrievalRecipe[]>{
            [100]=[new(1000,100,1,[new(200,"Ingot",3),new(2,"Fire Shard",2)],"Smith")],
            [101]=[new(1001,101,1,[new(200,"Ingot",2),new(2,"Fire Shard",1)],"Smith")],
            [200]=[new(2000,200,3,[new(2,"Fire Shard",4)],"Smith")]};
        var p=CraftingPreparation.Build([new(100,"Part A",2),new(101,"Part B",1),new(2,"Fire Shard",10)],recipes,
            new Dictionary<uint,int>{{200,2},{2,3}},new Dictionary<uint,int>{{200,1},{2,5}},true);
        var shards=p.Rows.Single(r=>r.ItemId==2);
        Check("shared ingot crafts round once",p.Steps.Single(s=>s.ItemId==200).Crafts==2);
        Check("shards include direct outputs and every subcraft once",shards.Required==23&&shards.Bags==3&&shards.Retainers==5&&shards.Missing==15);
        Check("demand explanation reconciles with total",p.Rows.All(r=>r.DemandSources.Sum(s=>s.Quantity)==r.Required));
        Check("no raw ingredients requested when outputs are already owned",CraftingPreparation.Build([new(100,"Part A",2)],recipes,new Dictionary<uint,int>{{100,2}},new Dictionary<uint,int>(),true).Missing.Length==0);
        var large=CraftingPreparation.Build([new(200,"Ingot",18000)],recipes,new Dictionary<uint,int>(),new Dictionary<uint,int>{{2,9999}},true);
        Check("whole queue shards can exceed carry cap",large.Rows.Single(r=>r.ItemId==2).Required==24000&&large.Missing.Single().Quantity==14001);
        MarketShoppingRequest[] original=[new("Fire Shard",999),new("Unrelated item",5)];
        var merged=MarketShopping.WithRequirements(original,p.Missing.Select(e=>new MarketShoppingRequest(e.Name,e.Quantity)));
        Check("shopping uses missing quantity rather than gross need",merged.Single(e=>e.Name=="Fire Shard").Quantity==15);
        Check("other shopping items survive",merged.Single(e=>e.Name=="Unrelated item").Quantity==5&&original[0].Quantity==999);
        Check("repeated send never doubles quantities",MarketShopping.WithRequirements(merged,[new("fire shard",15)]).Length==2);
        Check("updated shortage replaces prior amount",MarketShopping.WithRequirements(merged,[new("Fire Shard",7)]).Single(e=>e.Name=="Fire Shard").Quantity==7);
        var failed=false;try{MarketShopping.WithRequirements(Enumerable.Range(0,100).Select(i=>new MarketShoppingRequest("Item "+i,1)),[new("One too many",1)]);}catch(ArgumentException){failed=true;}
        Check("oversized list rejects without partial mutation",failed);
    }
}
