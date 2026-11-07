using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class MistCorridor474Tests
{
    [ModuleInitializer] internal static void Run(){
        var start=new Vector3(-689.0164f,18.000723f,-619.28986f);var goal=new Vector3(-662.0299f,24.998558f,-653.74646f);
        var guide=NavigationStreetCorridors.Guide(339,start,goal)??throw new Exception("Reported Mist approach must select the street corridor.");
        if(!guide.Any(p=>p.Y<17)||!guide.Any(p=>p.Z<-669))throw new Exception("The upper street requires the downhill and staircase detour.");
        if(NavigationStreetCorridors.Guide(341,start,goal)!=null||NavigationStreetCorridors.Guide(339,start+new Vector3(0,15,0),goal)!=null||NavigationStreetCorridors.Guide(339,start,goal+new Vector3(30,0,0))!=null)throw new Exception("Corridor must not apply to another district, floor or plot area.");
        var geometry=new CorridorFloor(guide);
        var search=new CollisionRouteSearch(geometry,guide[0],goal,.4f,streetGuide:guide);
        for(var i=0;i<2000&&!search.Finished;i++)search.Advance(100,2000);
        if(search.Path==null||search.DiagnosticSnapshot().RoadSeedPoints<140||search.Expanded>10)throw new Exception("The verified street should avoid broad grid expansion.");
        for(var i=1;i<search.Path.Length;i++)if(!geometry.Traverse(search.Path[i-1],search.Path[i]))throw new Exception("Street hint produced an unchecked edge.");
        var blocked=new CollisionRouteSearch(new Blocked(),guide[0],goal,.4f,streetGuide:guide);
        for(var i=0;i<1000&&!blocked.Finished;i++)blocked.Advance(100,2000);
        if(blocked.Path!=null)throw new Exception("A known corridor cannot bypass physical blockage.");
        Console.WriteLine($"474 Mist street corridor passed: {search.DiagnosticSnapshot().RoadSeedPoints} checked points, {search.Expanded} grid nodes (synthetic geometry).");
    }
    private sealed class CorridorFloor(Vector3[] guide):IWalkingCollision {
        public bool Ground(Vector3 p,out Vector3 floor){
            floor=default;var best=float.MaxValue;
            for(var i=1;i<guide.Length;i++){
                var a=guide[i-1];var b=guide[i];var edge=new Vector2(b.X-a.X,b.Z-a.Z);var v=new Vector2(p.X-a.X,p.Z-a.Z);
                var t=edge.LengthSquared()<.00001f?0:Math.Clamp(Vector2.Dot(v,edge)/edge.LengthSquared(),0,1);
                var q=Vector3.Lerp(a,b,t);var distance=CollisionRouteSearch.Horizontal(p,q);
                if(distance>=best)continue;best=distance;floor=new(p.X,q.Y,p.Z);
            }
            return best<.05f&&MathF.Abs(floor.Y-p.Y)<.7f;
        }
        public bool Traverse(Vector3 a,Vector3 b)=>Vector3.Distance(a,b)<=.75f&&Ground(b,out var floor)&&MathF.Abs(floor.Y-b.Y)<.01f;
    }
    private sealed class Blocked:IWalkingCollision {
        public bool Ground(Vector3 p,out Vector3 floor){floor=p;return true;}
        public bool Traverse(Vector3 a,Vector3 b)=>false;
    }
}
