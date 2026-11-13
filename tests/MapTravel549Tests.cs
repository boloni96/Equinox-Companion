using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class MapTravel549Tests
{
    [ModuleInitializer] internal static void Run()
    {
        if(!MapTravelPolicy.CanSelectTown("Bayside Bevy Marketplace","Bayside Bevy Marketplace",false,false,false))throw new Exception("Valid open map destination rejected");
        if(MapTravelPolicy.CanSelectTown("Bayside Bevy Marketplace","The For'ard Cabins",false,false,false)||MapTravelPolicy.CanSelectTown("Bayside Bevy Marketplace","Bayside Bevy Marketplace",true,false,false)||MapTravelPolicy.CanSelectTown("Bayside Bevy Marketplace","Bayside Bevy Marketplace",false,true,false)||MapTravelPolicy.CanSelectTown("Bayside Bevy Marketplace","Bayside Bevy Marketplace",false,false,true))throw new Exception("Invalid native map selection accepted");
        if(!MapTravelPolicy.Hit(1024,1024,100,21.5f,21.5f))throw new Exception("Centered marker missed");
        if(!MapTravelPolicy.Hit(1024,1024,200,11.25f,11.25f))throw new Exception("Scaled map marker missed");
        if(MapTravelPolicy.Hit(1024,1024,100,11.25f,11.25f))throw new Exception("Wrong map scale accepted");
        if(MapTravelPolicy.Hit(1024,1024,100,23,21.5f))throw new Exception("Unrelated marker accepted");
        if(MapTravelPolicy.Hit(1024,1024,0,21.5f,21.5f)||MapTravelPolicy.Hit(1024,1024,100,float.NaN,21.5f))throw new Exception("Invalid map coordinates accepted");
    }
}
