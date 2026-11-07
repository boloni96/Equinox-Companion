using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class GenericRoad480Tests
{
    internal static void Run(){
        foreach(var rotated in new[]{false,true}){
            const int size=512;var rgba=new byte[size*size*4];
            for(var z=0;z<size;z++)for(var x=0;x<size;x++){
                var world=new Vector3((x+.5f)/2-128,0,(z+.5f)/2-128);
                if(rotated)world=new(world.Z,0,-world.X);
                var road=MathF.Abs(world.X)<=42&&MathF.Abs(world.Z)<=2||MathF.Abs(world.X)<=42&&MathF.Abs(world.Z-20)<=2||MathF.Abs(MathF.Abs(world.X)-40)<=2&&world.Z>=0&&world.Z<=20;
                var i=(z*size+x)*4;rgba[i]=road?(byte)214:(byte)100;rgba[i+1]=road?(byte)200:(byte)100;rgba[i+2]=road?(byte)154:(byte)100;rgba[i+3]=255;
            }
            Vector3 Turn(Vector3 p)=>rotated?new(-p.Z,0,p.X):p;
            var start=Turn(new(-39,0,1));var goal=Turn(new(39,0,1));
            var map=NavigationRoadMap.FromRgba(rgba,size,size,800,0,0)??throw new Exception("Generic road fixture missing.");
            var roadGuide=map.BuildRoute(start,goal)??throw new Exception("Generic initial guide missing.");
            var collision=new PlotBoundary(rotated);
            var search=new CollisionRouteSearch(collision,start,goal,.25f,roads:roadGuide);
            var prefixes=new List<Vector3[]>();
            var deadline=DateTime.UtcNow.AddSeconds(15);
            while(!search.Finished&&DateTime.UtcNow<deadline){
                search.Advance(100,1);
                if(search.TakeVerifiedPrefix() is {} prefix)prefixes.Add(prefix);
                // Allow the managed map worker to run between simulated frames.
                Thread.Sleep(1);
            }
            if(search.Path==null)throw new Exception("Generic plot-boundary route failed: "+search.Error);
            if(search.RoadDetours==0||search.CompletedRoadDetours==0)throw new Exception($"A blocked map line must rebuild its guide, not repeat the same shortcut: {search.RoadDetours}/{search.CompletedRoadDetours}; {search.Expanded} nodes; {search.DiagnosticSnapshot().RoadSeedStop}.");
            if(prefixes.Count<2)throw new Exception("Walking must receive a checked section while remaining streets are being calculated.");
            foreach(var path in prefixes.Append(search.Path))for(var i=1;i<path.Length;i++)
                if(!collision.Traverse(path[i-1],path[i]))throw new Exception("Generic road detour crossed a plot boundary.");
            Console.WriteLine($"480 generic {(rotated?"rotated":"original")} plot-boundary route: {search.CompletedRoadDetours} map detours, {prefixes.Count} walking sections, {search.Expanded} expansions.");
        }
    }
    private sealed class PlotBoundary(bool rotated):IWalkingCollision {
        private Vector3 Local(Vector3 p)=>rotated?new(p.Z,p.Y,-p.X):p;
        public bool Ground(Vector3 p,out Vector3 floor){floor=p with{Y=0};var q=Local(p);return MathF.Abs(q.Y)<.7f&&MathF.Abs(q.X)<=45&&MathF.Abs(q.Z)<=24&&!(MathF.Abs(q.X)<4&&MathF.Abs(q.Z)<8);}
        public bool Traverse(Vector3 a,Vector3 b){
            if(Vector3.Distance(a,b)>2.9f)return false;
            for(var i=0;i<=32;i++)if(!Ground(Vector3.Lerp(a,b,i/32f),out _))return false;
            return true;
        }
    }
}
