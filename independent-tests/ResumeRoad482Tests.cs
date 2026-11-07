using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class ResumeRoad482Tests
{
    [ModuleInitializer] internal static void Run(){
        Vector3[] street=[Vector3.Zero,new(0,0,20),new(10,5,30),new(20,5,30)];
        var tail=NavigationStreetCorridors.Resume(street,new(0,0,15));
        if(tail==null||tail[0].Z<14.99f||tail.Any(p=>p.Z<14.99f))throw new Exception("A mid-street resume must not return to its starting point.");
        if(NavigationStreetCorridors.Resume(street,new(0,5,15))!=null)throw new Exception("Do not join a street on the wrong floor.");
        var mist=NavigationStreetCorridors.Guide(339,new(-685.4134f,15.3988285f,-659.2977f),new(-662.0286f,24.99856f,-653.7531f));
        if(mist==null||mist[0].Z>-650||!mist.Any(p=>p.Y>=24))throw new Exception("Reported Mist mid-street origin must continue toward its stairs, not the shard.");
        const int size=256;var rgba=new byte[size*size*4];
        for(var z=0;z<size;z++)for(var x=0;x<size;x++){
            var road=z>=124&&z<132;var i=(z*size+x)*4;
            rgba[i]=road?(byte)214:(byte)100;rgba[i+1]=road?(byte)200:(byte)100;rgba[i+2]=road?(byte)154:(byte)100;rgba[i+3]=255;
        }
        var map=NavigationRoadMap.FromRgba(rgba,size,size,800,0,0)!;
        var old=map.BuildRoute(new(-39,0,1),new(39,0,1))!;
        var search=new CollisionRouteSearch(new Flat(),new(16,0,1),new(39,0,1),.25f,roads:old,mapDetourWorker:work=>Task.FromResult(work()));
        for(var i=0;i<1000&&!search.Finished;i++)search.Advance(100,100);
        if(search.Path==null||search.Path.Any(p=>p.X<15.99f))throw new Exception("Reusing a map after a cached opening must not send movement backwards.");
        if(search.RoadDetours!=0||search.CompletedRoadDetours!=0)throw new Exception("Rebasing must not consume the obstacle-detour budget.");
        Console.WriteLine("482 mid-route map rebasing, street resume, floor isolation and reported Mist-origin checks passed.");
    }
    private sealed class Flat:IWalkingCollision {
        public bool Ground(Vector3 p,out Vector3 floor){floor=p with{Y=0};return MathF.Abs(p.Y)<.7f;}
        public bool Traverse(Vector3 a,Vector3 b)=>Vector3.Distance(a,b)<=2.9f&&MathF.Abs(a.Y-b.Y)<.01f;
    }
}
