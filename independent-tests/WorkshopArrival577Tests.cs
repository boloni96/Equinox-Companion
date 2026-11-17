using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class WorkshopArrival577Tests
{
    [ModuleInitializer] internal static void Run()
    {
        // Workshop floor descends beneath the fabrication station's object origin.
        var collision=new WorkshopFloor();var start=new Vector3(.54f,0,4.88f);
        var station=new Vector3(.045f,.228f,-5.23f);
        var search=new CollisionRouteSearch(collision,start,station,4f,arrival:p=>InteractionApproachPolicy.WithinDistance(p,station,4f),narrowApproach:true);
        for(var i=0;i<100&&!search.Finished;i++)search.Advance(20,192);
        if(search.Path is not {Length:>1} path||path[^1].Y> -1.2f||!InteractionApproachPolicy.WithinDistance(path[^1],station,4f))
            throw new Exception("Workshop arrival must reach the lower floor without seeking the station object's height.");
        for(var i=1;i<path.Length;i++)if(!collision.Traverse(path[i-1],path[i]))throw new Exception("Workshop route bypassed a validated stair segment.");
        Console.WriteLine("PASS workshop lower-floor route: "+path.Length+" points, "+search.Expanded+" expanded nodes");
    }
    private sealed class WorkshopFloor:IWalkingCollision
    {
        public bool Ground(Vector3 p,out Vector3 floor){floor=new(p.X,Math.Clamp(p.Z-1,-2,0),p.Z);return MathF.Abs(p.X)<3;}
        public bool Traverse(Vector3 a,Vector3 b)=>MathF.Abs(a.Y-b.Y)<=.55f&&MathF.Abs(a.X)<3&&MathF.Abs(b.X)<3;
        public float WallPenalty(Vector3 p)=>0;
    }
}
