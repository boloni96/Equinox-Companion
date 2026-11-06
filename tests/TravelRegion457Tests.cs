using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class TravelRegion457Tests
{
    [ModuleInitializer] internal static void Run()
    {
        var allowed=new HashSet<(uint,uint)>{(1,1),(2,2),(3,3),(4,4),(1,4),(2,4),(3,4)};
        for(uint home=0;home<=5;home++)for(uint destination=0;destination<=5;destination++)
            if(NativeTravelPolicy.RegionReachable(home,destination)!=allowed.Contains((home,destination)))
                throw new Exception($"Incorrect regional travel eligibility {home} -> {destination}");
        // Rafflesia -> Raiden is unavailable even if the NA character visits Materia first.
        if(NativeTravelPolicy.RegionReachable(2,3))throw new Exception("NA character cannot travel to EU.");
        if(!NativeTravelPolicy.RegionReachable(2,2)||!NativeTravelPolicy.RegionReachable(3,3))throw new Exception("Visitors must retain access to their home region.");
        Console.WriteLine("Regional preflight: blocked NA/EU/JP crossings, Materia visits and home returns passed.");
    }
}
