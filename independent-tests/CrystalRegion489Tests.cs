using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class CrystalRegion489Tests
{
    [ModuleInitializer] internal static void Run(){
        // Floating marker has no walkable floor at its own coordinates. A
        // supported street approach is sufficient if range and visibility pass.
        var ground=new Street();var target=new Vector3(24,5,0);
        foreach(var side in new[]{-1,1}){
            bool Reach(Vector3 p)=>InteractionApproachPolicy.WithinDistance(p,target,10.25f)&&p.Z*side>=2;
            var search=new CollisionRouteSearch(ground,Vector3.Zero,target,5,arrival:Reach);
            for(var i=0;i<10000&&!search.Finished;i++){search.Advance(100,4);search.TakeVerifiedPrefix();}
            if(search.Path is not {} path||!Reach(path[^1]))throw new Exception("Crystal approach must find a reachable interaction region without grounding the floating object or selecting one radial point.");
            for(var i=1;i<path.Length;i++)if(!ground.Traverse(path[i-1],path[i]))throw new Exception("Crystal approach left the supported street.");
        }
        Console.WriteLine("489 floating crystal and alternate visible street-side interaction regions passed.");
    }
    private sealed class Street:IWalkingCollision {
        public bool Ground(Vector3 p,out Vector3 floor){floor=p with{Y=0};return MathF.Abs(p.Y)<.7f&&p.X>=-2&&p.X<=22&&MathF.Abs(p.Z)<=8;}
        public bool Traverse(Vector3 a,Vector3 b)=>Vector3.Distance(a,b)<=2.9f&&Ground(a,out _)&&Ground(b,out _);
    }
}
