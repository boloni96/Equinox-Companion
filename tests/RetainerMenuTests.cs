using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class RetainerMenuTests
{
    [ModuleInitializer] internal static void Run(){
        void Check(string name,bool ok){if(!ok)throw new Exception(name);Console.WriteLine("PASS retainer menu "+name);}
        Check("trailing punctuation",RetainerMenuPolicy.Matches("Entrust or withdraw items", "Entrust or withdraw items."));
        Check("spacing",RetainerMenuPolicy.Matches(" Entrust  or withdraw items. ","Entrust or withdraw items."));
        Check("gil is not items",!RetainerMenuPolicy.Matches("Entrust or withdraw gil.","Entrust or withdraw items."));
        Check("no substring action",!RetainerMenuPolicy.Matches("Withdraw and sell","Withdraw"));
        Check("empty labels rejected",!RetainerMenuPolicy.Matches("",""));
        Check("localized exact",RetainerMenuPolicy.Matches("アイテムを預ける。","アイテムを預ける"));
    }
}
