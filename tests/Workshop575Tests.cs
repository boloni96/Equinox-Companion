using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class Workshop575Tests
{
    [ModuleInitializer] internal static void Run()
    {
        void Check(string name,bool ok){if(!ok)throw new Exception(name);Console.WriteLine("PASS workshop source "+name);}
        Check("no chest stock means retainer",!WorkshopPolicy.PreferRecordedChest(10,50,0,1,100));
        Check("complete closer retainer wins",!WorkshopPolicy.PreferRecordedChest(10,50,50,100,1));
        Check("complete closer chest wins",WorkshopPolicy.PreferRecordedChest(10,50,50,1,100));
        Check("chest supplies retainer shortage",WorkshopPolicy.PreferRecordedChest(10,2,8,100,1));
        Check("unavailable chest is not routed",!WorkshopPolicy.PreferRecordedChest(10,0,50,float.PositiveInfinity,1));
        Check("crystal source is explicit container",CrystalStorage.IsContainer(22001)&&CrystalStorage.IsContainer(12001));
    }
}
