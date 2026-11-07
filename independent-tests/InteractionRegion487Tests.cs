using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class InteractionRegion487Tests
{
    [ModuleInitializer] internal static void Run(){
        foreach(var sign in new[]{-1,1}){
            var collision=new Bed();var start=new Vector3(-6,0,0);var target=Vector3.Zero;
            bool Arrived(Vector3 p)=>Vector3.Distance(p,target)<=2.95f&&p.Z*sign>=1.5f;
            var search=new CollisionRouteSearch(collision,start,target,2.95f,arrival:Arrived);
            for(var i=0;i<300&&!search.Finished;i++)search.Advance(100,192);
            if(search.Path is not {} path||!Arrived(path[^1]))throw new Exception("Garden route must reach the visible interaction side, not the blocked target centre.");
            for(var i=1;i<path.Length;i++)if(!collision.Traverse(path[i-1],path[i]))throw new Exception("Garden route crossed the patch.");
        }
        var entry=new OutdoorPlotEntry(Vector3.UnitZ,new Vector3(0,0,8));
        if(!entry.GeometryRoute||entry.Goal!=new Vector3(0,0,8))throw new Exception("Ward entrance must use the interior route destination.");
        if(entry.Confirm(Vector3.Zero,true)||entry.Confirm(new(0,0,1),true))throw new Exception("Identity at the street boundary must not finish entry.");
        if(entry.Confirm(new(4,0,0),false))throw new Exception("Detour must still match the requested estate.");
        if(!entry.Confirm(new(4,0,0),true))throw new Exception("Verified route may clear the boundary around either side, not only forward.");
        Console.WriteLine("487 garden interaction regions and collision-routed entry handoff passed.");
    }
    private sealed class Bed:IWalkingCollision {
        public bool Ground(Vector3 p,out Vector3 floor){floor=p with{Y=0};return MathF.Abs(p.X)<9&&MathF.Abs(p.Z)<9&&MathF.Abs(p.Y)<.7f;}
        public bool Traverse(Vector3 a,Vector3 b){
            if(Vector3.Distance(a,b)>2.9f)return false;
            for(var i=0;i<=24;i++){
                var p=Vector3.Lerp(a,b,i/24f);
                if(!Ground(p,out _)||MathF.Abs(p.X)<1.2f&&MathF.Abs(p.Z)<1.2f)return false;
            }
            return true;
        }
    }
}
