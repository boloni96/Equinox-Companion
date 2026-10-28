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
        var menu=new[]{"Entrust or withdraw items.","Entrust or withdraw gil.","Quit."};
        Check("menu proves identity before inventory exists",RetainerMenuPolicy.SelectedMenu(1,1,1,true,menu,menu[0],menu[1]));
        Check("stale selected retainer rejected",!RetainerMenuPolicy.SelectedMenu(2,1,1,true,menu,menu[0],menu[1]));
        Check("different active retainer rejected",!RetainerMenuPolicy.SelectedMenu(1,1,2,true,menu,menu[0],menu[1]));
        Check("unrelated menu rejected",!RetainerMenuPolicy.SelectedMenu(1,1,1,true,["Travel","Quit"],menu[0],menu[1]));
        Check("outside bell rejected",!RetainerMenuPolicy.SelectedMenu(1,1,1,false,menu,menu[0],menu[1]));
    }
}
