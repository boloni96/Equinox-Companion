using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class TravelCity455Tests
{
    [ModuleInitializer] internal static void Run()
    {
        if(TravelCityPolicy.Preferred(2,0,false)!=2)throw new Exception("Ordinary travel must use Gridania even with Limsa shopping.");
        if(TravelCityPolicy.Preferred(2,0,true)!=8)throw new Exception("Shopping must retain Limsa independently.");
        if(TravelCityPolicy.Preferred(1,2,false)!=8||TravelCityPolicy.Preferred(1,2,true)!=9)throw new Exception("Changing shopping preference must not change ordinary travel.");
        if(TravelCityPolicy.Select(new uint[]{8,2,9},2)!=2)throw new Exception("Prefer selected unlocked gateway.");
        if(TravelCityPolicy.Select(new uint[]{8,9},2)!=8)throw new Exception("Locked preference falls back to an unlocked city.");
        if(TravelCityPolicy.Select(new uint[]{99,9,8},0)!=9)throw new Exception("Automatic preserves first available main-city behavior.");
        if(TravelCityPolicy.Select(new uint[]{99},8)!=0)throw new Exception("Never select a locked city.");
        Console.WriteLine("Separate ordinary/shopping city preference and unlocked fallback passed.");
    }
}
