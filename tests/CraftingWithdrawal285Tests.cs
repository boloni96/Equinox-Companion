using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class CraftingWithdrawal285Tests
{
 [ModuleInitializer] internal static void Run()
 {
  void Check(string label,bool ok){if(!ok)throw new Exception(label);Console.WriteLine("PASS crafting285 "+label);}
  var recipes=new Dictionary<uint,RetrievalRecipe[]>{
   [100]=[new(1,100,1,[new(200,"Cloth",3),new(300,"Leather",1)],"Weaver")],
   [200]=[new(2,200,1,[new(400,"Thread",2)],"Weaver")],
   [400]=[new(3,400,1,[new(500,"Fibre",4)],"Weaver")]
  };
  RetainerRetrieval.CraftPlan Build(RetrievalPreset p)=>RetainerRetrieval.BuildPlan(p.Items,p.Multiplier,p.IncludesSubcrafts,p.Mode,recipes,new Dictionary<uint,RetrievalEntry[]>());
  var direct=CraftingWithdrawal.Create(100,"Slacks",false);var raw=CraftingWithdrawal.Create(100,"Slacks",true);
  var d=Build(direct);var r=Build(raw);
  Check("recipe-menu direct action requests finished cloth, not slacks or thread",d.Materials.Single(x=>x.ItemId==200).Quantity==3&&!d.Materials.Any(x=>x.ItemId is 100 or 400 or 500));
  Check("raw action expands nested subcrafts and retains purchased materials",r.Materials.Single(x=>x.ItemId==500).Quantity==24&&r.Materials.Single(x=>x.ItemId==300).Quantity==1&&!r.Materials.Any(x=>x.ItemId is 100 or 200 or 400));
  Check("changing quantity scales ingredients without double expansion",Build(direct with{Multiplier=4}).Materials.Single(x=>x.ItemId==200).Quantity==12);
  Check("quick action is independent of Entrust/Artisan list flags",direct.Entrust==null&&!direct.IncludesSubcrafts&&!direct.Items[0].ReadyMaterial);
 }
}
