using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class Retriever269Tests
{
    [ModuleInitializer] internal static void Run()
    {
        void Check(string label,bool ok){if(!ok)throw new Exception(label);Console.WriteLine("PASS retriever 269 "+label);}
        var containers=new ulong[]{0,2001,10000,10006,11000,12001,12002,20000,25000};
        Check("owned storage filter includes retainer bags and crystals only",containers.Where(RetainerRetrieval.IsRetainerStorage).SequenceEqual(new ulong[]{10000,10006,12001}));
        var exchange=new RetrievalEntry(50,"Exchange-only equipment",1);
        foreach(var mode in new[]{RetrievalMode.DirectIngredients,RetrievalMode.AllMaterials,RetrievalMode.CompletedItems}){
            var plan=RetainerRetrieval.BuildPlan([exchange],1,false,mode,new Dictionary<uint,RetrievalRecipe[]>(),new Dictionary<uint,RetrievalEntry[]>());
            Check("no recipe is invented in "+mode,plan.Materials.Single()==exchange&&plan.Steps.Length==0);
        }
    }
}
