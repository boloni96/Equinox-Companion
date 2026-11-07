using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class TerrainClearance477Tests
{
    [ModuleInitializer] internal static void Run(){
        // A ray at center-floor + .2 cuts into the uphill side of this ramp.
        var sideways=new Terrain(p=>p.X*.6f);
        Check(sideways,Vector3.Zero,Vector3.UnitZ,"cross-slope rail height");
        // The old center-height cross-body ray clips a valid rising surface.
        var rising=new Terrain(p=>p.X*.9f);
        Check(rising,Vector3.Zero,new(1,.9f,0),"rising body footprint");
        Check(rising,new(1,.9f,0),Vector3.Zero,"descending body footprint");
        var steps=new Terrain(p=>MathF.Floor(p.X/.3f)*.3f);
        Check(steps,new(.05f,0,0),new(.95f,.9f,0),"consecutive legal risers under footprint");
        var route=new CollisionRouteSearch(sideways,Vector3.Zero,new(0,0,12),.4f);
        for(var i=0;i<500&&!route.Finished;i++)route.Advance(100,1000);
        if(route.Path==null||route.Expanded>20)throw new Exception("An open cross-slope road must plan directly instead of exploring rejected terrain rays.");
        for(var i=1;i<route.Path.Length;i++)Check(sideways,route.Path[i-1],route.Path[i],"planned slope connector");
        var wall=new Terrain(p=>p.X*.6f){Wall=true};
        if(wall.Traverse(Vector3.Zero,Vector3.UnitZ))throw new Exception("Terrain-aware clearance bypassed a wall across a slope.");
        var cliff=new Terrain(p=>p.X>.2f?2:0);
        if(cliff.Traverse(Vector3.Zero,new(0,0,1)))throw new Exception("Footprint support must not climb an excessive side ledge.");
        var post=new Terrain(_=>0){Post=true};
        if(post.Traverse(Vector3.Zero,Vector3.UnitZ))throw new Exception("Terrain-aware footprint missed an off-center thin post.");
        var cache=new WalkingTraversalCache();
        if(cache.Check(wall,Vector3.Zero,Vector3.UnitZ,wall.Blocked,out _)||string.IsNullOrEmpty(cache.Failure))throw new Exception("Rejected sweep must retain its failure phase.");
        var failure=cache.Failure;
        if(cache.Check(wall,Vector3.Zero,Vector3.UnitZ,wall.Blocked,out _)||cache.Failure!=failure)throw new Exception("Cached failure lost its diagnostic cause.");
        Console.WriteLine("477 terrain-height rails, slope/step footprints, walls, ledges, thin posts and cached failure reasons passed.");
    }
    private static void Check(Terrain t,Vector3 a,Vector3 b,string label){
        if(!t.Traverse(a,b))throw new Exception("Walkable terrain rejected: "+label+" / "+t.Failure);
    }
    private sealed class Terrain(Func<Vector3,float> height):IWalkingCollision {
        public bool Wall,Post;public string? Failure;
        public bool Ground(Vector3 p,out Vector3 floor){floor=p with{Y=height(p)};return MathF.Abs(p.Y-floor.Y)<=.7f;}
        public bool Blocked(Vector3 a,Vector3 b){
            for(var i=0;i<=100;i++){
                var p=Vector3.Lerp(a,b,i/100f);
                if(p.Y<=height(p)+.001f)return true;
                if(Wall&&MathF.Abs(p.Z-.5f)<.03f&&p.Y<4)return true;
                if(Post&&MathF.Abs(p.X-.24f)<.045f&&MathF.Abs(p.Z-.5f)<.045f&&p.Y<1.6f)return true;
            }
            return false;
        }
        public bool Traverse(Vector3 a,Vector3 b){Failure=null;return WalkingGroundTraversal.Check(this,a,b,Blocked,out _,rejected:r=>Failure=r);}
    }
}
