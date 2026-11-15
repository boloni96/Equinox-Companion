using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class OffsetGate556Tests
{
    [ModuleInitializer] internal static void Run()
    {
        foreach(var blocked in new[]{false,true})foreach(var reverse in new[]{false,true}){
            var geometry=new OffsetGate { Blocked=blocked };
            var start=reverse?new Vector3(14,0,1):Vector3.Zero;
            var goal=reverse?Vector3.Zero:new Vector3(14,0,1);
            var search=new CollisionRouteSearch(geometry,start,goal,.25f);
            for(var frame=0;frame<10000&&!search.Finished;frame++)search.Advance(100,32);
            if(blocked){if(search.Path!=null)throw new Exception("556 wider join bypassed the closed gate");continue;}
            if(search.Path is not {} path)throw new Exception("556 offset approaches failed to join across a supported connector");
            if(!geometry.GateChecked)throw new Exception("556 connector was not collision checked");
            for(var i=1;i<path.Length;i++)if(!geometry.Traverse(path[i-1],path[i]))throw new Exception("556 returned an unchecked/blocked edge");
        }
        Console.WriteLine("556 offset gate connections in both directions and closed gate rejection passed.");
    }
    // Two grid-aligned approaches linked by an offset, verified passage. The
    // connector has no intermediate coarse-grid node and exceeds the old 1.5
    // join radius. Ground/Traverse model allowed actor-center positions.
    private sealed class OffsetGate:IWalkingCollision
    {
        public bool Blocked,GateChecked;
        public bool Ground(Vector3 p,out Vector3 floor){
            floor=p with{Y=0};
            if(MathF.Abs(p.Y)>.7f||p.X<0||p.X>14)return false;
            var z=p.X<=6?0:p.X>=8?1:(p.X-6)/2;
            return MathF.Abs(p.Z-z)<.05f;
        }
        public bool Traverse(Vector3 a,Vector3 b){
            if(CollisionRouteSearch.Horizontal(a,b)>2.9f)return false;
            for(var i=0;i<=64;i++){
                var p=Vector3.Lerp(a,b,i/64f);
                if(!Ground(p,out _))return false;
                if(p.X>6.1f&&p.X<7.9f){GateChecked=true;if(Blocked)return false;}
            }
            return true;
        }
    }
}
