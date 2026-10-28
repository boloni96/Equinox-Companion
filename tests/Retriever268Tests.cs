using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class Retriever268Tests
{
    [ModuleInitializer] internal static void Run()
    {
        void Check(string label,bool ok){if(!ok)throw new Exception(label);Console.WriteLine("PASS retriever 268 "+label);}
        void Reject(string label,Action action){try{action();}catch(ArgumentException){Check(label,true);return;}throw new Exception("Expected rejection: "+label);}
        var gear=new RetrievalEntry(1,"Crafted armour",2);var ingot=new RetrievalEntry(2,"Ingot",3);var ore=new RetrievalEntry(3,"Ore",2);var crystal=new RetrievalEntry(4,"Crystal",1);
        var recipes=new Dictionary<uint,RetrievalRecipe[]>{{1,[new(10,1,1,[ingot,crystal],"Armourer")]},{2,[new(20,2,1,[ore],"Smith")]}};
        var projects=new Dictionary<uint,RetrievalEntry[]>{{100,[new(2,"Ingot",5)]}};
        RetainerRetrieval.CraftPlan Plan(RetrievalEntry[] list,RetrievalMode mode,int multiple=1)=>RetainerRetrieval.BuildPlan(list,multiple,false,mode,recipes,projects);
        var direct=Plan([gear],RetrievalMode.DirectIngredients);
        Check("gear selected as item shows direct recipe ingredients",direct.Materials.Single(x=>x.ItemId==2).Quantity==6&&!direct.Materials.Any(x=>x.ItemId==1));
        var raw=Plan([gear],RetrievalMode.AllMaterials);
        Check("gear expands through subcraft with crystals",raw.Materials.Single(x=>x.ItemId==3).Quantity==12&&raw.Materials.Single(x=>x.ItemId==4).Quantity==2);
        Check("exact item mode keeps gear",Plan([gear],RetrievalMode.CompletedItems).Materials.Single()==gear);
        Check("600 finished ingots exact mode",Plan([ingot with{Quantity=600}],RetrievalMode.CompletedItems).Materials.Single().Quantity==600);
        var mixed=Plan([gear,new(100,"Workshop part",2)],RetrievalMode.DirectIngredients);
        Check("mixed gear and workshop share materials once",mixed.Materials.Single(x=>x.ItemId==2).Quantity==16);
        Check("quantity and multiplier apply once",Plan([gear],RetrievalMode.DirectIngredients,4).Materials.Single(x=>x.ItemId==2).Quantity==24);
        Check("workshop source ready-made supplies do not expand twice",Plan([ingot with{ReadyMaterial=true}],RetrievalMode.DirectIngredients).Materials.Single().ItemId==2);
        Check("raw mode expands ready-made supplies",Plan([ingot with{ReadyMaterial=true}],RetrievalMode.AllMaterials).Materials.Single().Quantity==6);
        Check("noncraftable item preserved without invented recipe",Plan([new(99,"Vendor item",5)],RetrievalMode.DirectIngredients).Materials.Single().Quantity==5);
        Check("empty selected list produces empty plan",Plan([],RetrievalMode.DirectIngredients).Materials.Length==0);
        recipes[1]=[new(10,1,3,[ingot],"A")];Check("direct mode respects recipe yield",Plan([gear],RetrievalMode.DirectIngredients).Materials.Single().Quantity==3);
        recipes[1]=[new(10,1,1,[ingot],"A"),new(11,1,1,[ore],"B")];
        Reject("ambiguous direct recipe does not invent requirements",()=>Plan([gear],RetrievalMode.DirectIngredients));
        Check("explicit alternate recipe controls preview",Plan([gear with{RecipeId=11}],RetrievalMode.DirectIngredients).Materials.Single().ItemId==3);
        Reject("direct quantity overflow rejected",()=>Plan([gear with{Quantity=1_000_000}],RetrievalMode.DirectIngredients,999));
        var p=new RetrievalPreset("Materials",[ingot with{ReadyMaterial=true}]);var round=RetainerRetrieval.Import(RetainerRetrieval.Export(p),_=>ingot);
        Check("ready-material provenance survives save/export",round.Items.Single().ReadyMaterial);
        p=p with{Mode=RetrievalMode.AllMaterials,RecipeChoices=new(){{1,11}}};round=RetainerRetrieval.Import(RetainerRetrieval.Export(p),_=>ingot);
        Check("saved list remembers mode and recipe choices",round.Mode==RetrievalMode.AllMaterials&&round.RecipeChoices![1]==11);
        Reject("removed recipe is not treated as noncraftable",()=>Plan([new(99,"Old item",1,999)],RetrievalMode.DirectIngredients));
    }
}
