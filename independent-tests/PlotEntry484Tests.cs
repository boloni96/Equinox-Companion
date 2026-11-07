using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class PlotEntry484Tests
{
    [ModuleInitializer] internal static void Run(){
        // A destination inside a fenced yard is not reachable by a straight
        // line from the road. Both mirrored layouts must enter through the gap.
        foreach(var sign in new[]{-1,1}){
            var ground=new Yard(sign);var start=new Vector3(0,0,-3);var landing=new Vector3(0,0,6);
            if(ground.Traverse(start,landing))throw new Exception("Fixture must block the direct fence crossing.");
            var search=new CollisionRouteSearch(ground,start,landing,.3f);
            for(var i=0;i<300&&!search.Finished;i++)search.Advance(100,192);
            if(search.Path is not {} path)throw new Exception("Geometry-only plot entry must find the gate in either direction.");
            if(!path.Any(p=>p.X*sign>=3.5f))throw new Exception("Entry must use the opening rather than cross the fence.");
            for(var i=1;i<path.Length;i++)if(!ground.Traverse(path[i-1],path[i]))throw new Exception("Plot route contains an unwalkable connection.");
        }
        Console.WriteLine("484 geometry-only fenced plot entry through left/right gates passed.");
    }
    private sealed class Yard(int sign):IWalkingCollision {
        public bool Ground(Vector3 p,out Vector3 floor){floor=p with{Y=0};return MathF.Abs(p.Y)<.7f&&MathF.Abs(p.X)<=9&&p.Z>=-5&&p.Z<=9;}
        public bool Traverse(Vector3 a,Vector3 b){
            if(Vector3.Distance(a,b)>2.9f||!Ground(a,out _)||!Ground(b,out _))return false;
            // Inflate the fence thickness; only its open gate is traversable.
            for(var i=0;i<=24;i++){
                var p=Vector3.Lerp(a,b,i/24f);
                if(MathF.Abs(p.Z)<=.4f&&(p.X*sign<3.5f||p.X*sign>6.5f))return false;
            }
            return true;
        }
    }
}
