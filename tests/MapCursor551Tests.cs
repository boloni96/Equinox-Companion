using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class MapCursor551Tests
{
    [ModuleInitializer] internal static void Run()
    {
        // Tuliyollal map 855 / marker range 620; mouse digits from the .550 user report.
        var samples=new[]{(935,498,(ushort)11,(ushort)3,(ushort)6,(ushort)7),
            (777,908,(ushort)9,(ushort)7,(ushort)10,(ushort)9),
            (1243,298,(ushort)14,(ushort)7,(ushort)4,(ushort)4),
            (844,1255,(ushort)10,(ushort)3,(ushort)14,(ushort)9)};
        foreach(var (mx,my,xi,xd,yi,yd) in samples){
            if(!MapTravelPolicy.Hit(mx,my,180,MapTravelPolicy.CursorCoordinate(xi,xd),MapTravelPolicy.CursorCoordinate(yi,yd)))throw new Exception("Recorded Tuliyollal shard click missed");
            if(MapTravelPolicy.Hit(mx,my,180,xi+xd/100f,yi+yd/100f))throw new Exception("Fixture must reproduce .550 coordinate failure");
        }
        if(MapTravelPolicy.Hit(935,498,180,16.6f,11.7f))throw new Exception("Summoning bell click matched shard");
        if(!float.IsNaN(MapTravelPolicy.CursorCoordinate(11,30)))throw new Exception("Unknown cursor encoding accepted");
        if(MapTravelPolicy.TooltipName("Tuliyollal\r(100\ue049)")!="Tuliyollal")throw new Exception("Teleport cost contaminated destination name");
    }
}
