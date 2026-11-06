using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class NativeHousing458Tests
{
    [ModuleInitializer] internal static void Run()
    {
        foreach(var name in new[]{"Mist","Lavender Beds","The Lavender Beds","The Goblet","Goblet","Shirogane","Empyreum"})
            if(NativeHousingPolicy.District(name)==null)throw new Exception("Known district missing.");
        if(NativeHousingPolicy.District("unknown")!=null)throw new Exception("Unknown district accepted.");
        if(!NativeHousingPolicy.Address(1,1)||!NativeHousingPolicy.Address(30,60)||NativeHousingPolicy.Address(31,1)||NativeHousingPolicy.Address(1,61)||NativeHousingPolicy.Address(0,1)||NativeHousingPolicy.Address(1,0))throw new Exception("Housing address bounds failed.");
        if(!NativeHousingPolicy.WardPrompt("Travel to 15th Ward, Shirogane?",15,"Shirogane")||
            NativeHousingPolicy.WardPrompt("Travel to 15th Ward, Shirogane?",5,"Shirogane")||
            NativeHousingPolicy.WardPrompt("Travel to 15th Ward, Mist?",15,"Shirogane")||
            NativeHousingPolicy.WardPrompt("Cancel travel to 15th Ward, Shirogane?",15,"Shirogane")||
            NativeHousingPolicy.WardPrompt("Travel to Shirogane?",15,"Shirogane"))throw new Exception("Ward confirmation must verify action, full ward number and district.");
        if(NativeHousingPolicy.Position(new(float.NaN,0,0))||NativeHousingPolicy.Position(new(0,float.PositiveInfinity,0)))throw new Exception("Invalid housing geometry accepted.");
        Console.WriteLine("Native housing district, address bounds and exact ward confirmation guards passed.");
    }
}
