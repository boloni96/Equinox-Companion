using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class WorldDestination527Tests
{
    [ModuleInitializer] internal static void Run(){
        if(WorldDestinationPolicy.Available(0,2,"Test")||WorldDestinationPolicy.Available(99,0,"Test")||WorldDestinationPolicy.Available(99,5,"Test")||WorldDestinationPolicy.Available(11,2," "))throw new Exception("Unavailable destination accepted");
        foreach(var region in new uint[]{1,2,3,4})if(!WorldDestinationPolicy.Available(11,region,"Valid DC"))throw new Exception("Supported region omitted");
    }
}
