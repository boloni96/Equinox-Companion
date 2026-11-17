using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class Crafting565Tests
{
    [ModuleInitializer] internal static void Run()
    {
        void Check(string name,bool ok){if(!ok)throw new Exception("Crafting565: "+name);Console.WriteLine("PASS crafting565 "+name);}
        var recipes=new Dictionary<uint,RetrievalRecipe[]>{[100]=[new(1000,100,1,[new(200,"Ingot",3)],"Smith")],[101]=[new(1001,101,1,[new(200,"Ingot",2)],"Smith")],[200]=[new(2000,200,3,[new(300,"Ore",2)],"Smith")]};
        PreparationPlan Plan(RetrievalEntry[] e,Dictionary<uint,int> b,Dictionary<uint,int> r,bool complete=true)=>CraftingPreparation.Build(e,recipes,b,r,complete);
        var p=Plan([new(100,"A",2),new(101,"B",1)],new(){[100]=1,[200]=2},new(){[200]=1,[300]=2});
        Check("owned final output reduces subcraft demand",p.Rows.Single(x=>x.ItemId==200).Required==5);
        Check("shared subcraft stock allocated once",p.Rows.Single(x=>x.ItemId==200).Bags==2&&p.Rows.Single(x=>x.ItemId==200).Retainers==1);
        Check("recipe yield rounds once",p.Steps.Single(x=>x.ItemId==200).Crafts==1);
        Check("raw retainer stock makes both outputs craftable",p.Missing.Length==0&&p.Rows.Single(x=>x.ItemId==100).Coverage==PreparationCoverage.Craftable);
        Check("ingredients precede outputs",Array.FindIndex(p.Steps,s=>s.ItemId==200)<Array.FindIndex(p.Steps,s=>s.ItemId==100));
        Check("used for includes both parents",p.Rows.Single(x=>x.ItemId==200).UsedFor.Length==2);
        p=Plan([new(100,"A",1),new(101,"B",1)],[],[]);
        Check("shared demand ceil once",p.Steps.Single(s=>s.ItemId==200).Crafts==2&&p.Missing.Single().Quantity==4);
        Check("missing propagates to parent",p.Rows.Single(r=>r.ItemId==100).Coverage==PreparationCoverage.Missing);
        p=Plan([new(100,"A",1)],[],[],false);Check("partial stock explicit",p.Rows.Single(r=>r.ItemId==300).Coverage==PreparationCoverage.Incomplete&&p.Missing.Single().Quantity==2);
        Check("known lower bound covers requirement",new RetrievalPreview(1,"Steel",420,0,null,439).Missing==0);
        Check("unknown remainder stays unknown",new RetrievalPreview(1,"Steel",500,0,null,439).Missing==null);
        Check("no retainer data needed when in bags",new RetrievalPreview(1,"Steel",10,10,null).Missing==0);
        Check("10HQ20NQ full sets",WorkshopPolicy.CompleteSets(20,10,10)==3);
        Check("8HQ22NQ only two sets",WorkshopPolicy.CompleteSets(22,8,10)==2);
        Check("supplement either quality correctly",WorkshopPolicy.Supplements(22,8,10,3)==(8,2));
        Check("choose HQ when NQ insufficient",WorkshopPolicy.ChooseHq(2,16,6)==true);
        Check("never mix quality",WorkshopPolicy.ChooseHq(4,2,6)==null);
        var row=new WorkshopSupply(1,"Lumber",3,3,2);Check("live remaining one set",row.RemainingSets==1&&row.RemainingQuantity==3);
        Check("bonus phase with fewer sets",new WorkshopSupply(1,"Lumber",10,2,1).RemainingQuantity==10);
        Check("invalid native phase rejected",!WorkshopPolicy.Valid(new(1,3,3,[row]))&&!WorkshopPolicy.Valid(new(1,0,3,[row with{SetsDelivered=4}])));
        var q=new WorkshopQueueSave{Items=[new(1,"Bow",4),new(2,"Hull",4),new(3,"Stern",4),new(4,"Bridge",4)],Completed=4};
        Check("4 sharks means 16 parts",q.Items.Sum(x=>x.Quantity)==16&&WorkshopPolicy.Next(q)?.ItemId==2);
        q.Completed=16;Check("queue ends after receipt count",WorkshopPolicy.Next(q)==null);
        var cycle=new Dictionary<uint,RetrievalRecipe[]>{[1]=[new(1,1,1,[new(2,"B",1)],"A")],[2]=[new(2,2,1,[new(1,"A",1)],"A")]};
        try{CraftingPreparation.Build([new(1,"A",1)],cycle,new Dictionary<uint,int>(),new Dictionary<uint,int>(),true);throw new Exception("Cycle was accepted");}catch(ArgumentException){Check("cycle detected",true);}
    }
}
