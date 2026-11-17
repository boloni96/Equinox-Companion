using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class WorkshopRoute579Tests
{
    [ModuleInitializer] internal static void Run()
    {
        var goal=new Vector3(0,.23f,-5);var route=new RememberedWalkingRoute(653,goal,4,[new(0,0,5),new(0,0,1),new(0,-2,-3)]);
        var path=WorkshopRouteReuse.Find([route],653,new(0,0,4.5f),goal,4,(_,_)=>true);
        if(path==null||path[^1]!=route.Points[^1])throw new Exception("Verified lowered-floor workshop route should be reusable.");
        if(WorkshopRouteReuse.Find([route],1,route.Points[0],goal,4,(_,_)=>true)!=null)throw new Exception("Route cannot cross territories.");
        if(WorkshopRouteReuse.Find([route],653,route.Points[0],goal,4,(_,_)=>false)!=null)throw new Exception("Blocked entry must force replanning.");
        if(WorkshopRouteReuse.Find([route],653,route.Points[0],goal,1,(_,_)=>true)!=null)throw new Exception("Cached endpoint must satisfy current interaction distance.");
        Console.WriteLine("PASS workshop route reuse, floor and collision checks");
    }
}
