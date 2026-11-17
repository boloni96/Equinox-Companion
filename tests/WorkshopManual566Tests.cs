using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class WorkshopManual566Tests
{
    [ModuleInitializer] internal static void Run()
    {
        void Check(string name,bool ok){if(!ok)throw new Exception("Workshop manual: "+name);Console.WriteLine("PASS workshop manual "+name);}
        var p=new WorkshopPhase(21794,1,3,[new(5361,"Cedar lumber",3,3,1),new(5056,"Mythril plate",6,3,0)]);
        Check("clicked second row accepted",WorkshopPolicy.ManualRequestMatches(p,21794,1,5056,6));
        Check("different project rejected",!WorkshopPolicy.ManualRequestMatches(p,21792,1,5056,6));
        Check("wrong item rejected",!WorkshopPolicy.ManualRequestMatches(p,21794,1,5361,6));
        Check("wrong set quantity rejected",!WorkshopPolicy.ManualRequestMatches(p,21794,1,5056,3));
        Check("another row's request rejected",!WorkshopPolicy.ManualRequestMatches(p,21794,0,5056,6));
        Check("missing selection rejected",!WorkshopPolicy.ManualRequestMatches(p,21794,255,5056,6));
        Check("completed row rejected",!WorkshopPolicy.ManualRequestMatches(p with{Rows=[new(5056,"Mythril plate",6,3,3)]},21794,0,5056,6));
        Check("NQ preferred for manual request",WorkshopPolicy.ChooseHq(6,12,6)==false);
        Check("HQ selected when only HQ has full set",WorkshopPolicy.ChooseHq(2,6,6)==true);
        Check("mixed partial stacks not submitted",WorkshopPolicy.ChooseHq(2,4,6)==null);
    }
}
