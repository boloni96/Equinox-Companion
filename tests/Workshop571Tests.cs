using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class Workshop571Tests
{
    [ModuleInitializer] internal static void Run()
    {
        void Check(string name,bool ok){if(!ok)throw new Exception(name);Console.WriteLine("PASS workshop restock "+name);}
        var row=new WorkshopSupply(1,"Material",10,3);
        Check("withdraw remaining phase sets",WorkshopPolicy.Restock(row,8,2,22,0,0)==(false,22));
        Check("mixed remnants cannot form set",WorkshopPolicy.Restock(row,8,2,0,0,0)==null);
        Check("HQ fallback uses whole sets",WorkshopPolicy.Restock(row,8,2,0,18,0)==(true,18));
        Check("NQ filter excludes HQ",WorkshopPolicy.Restock(row,8,2,0,18,1)==null);
        Check("HQ filter excludes NQ",WorkshopPolicy.Restock(row,8,2,22,0,2)==null);
        Check("only remaining deliveries",WorkshopPolicy.Restock(row with{SetsDelivered=2},8,0,100,0,0)==(false,2));
        Check("completed row does not withdraw",WorkshopPolicy.Restock(row with{SetsDelivered=3},0,0,100,100,0)==null);
        Check("unknown quality rejected",WorkshopPolicy.Restock(row,0,0,100,100,3)==null);
    }
}
