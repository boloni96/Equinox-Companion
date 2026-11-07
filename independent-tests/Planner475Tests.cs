using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Planner475Tests
{
    [ModuleInitializer] internal static void Run(){
        var geometry=new StairTurn();
        var search=new CollisionRouteSearch(geometry,Vector3.Zero,new(12,2,4),.25f);
        for(var i=0;i<2000&&!search.Finished;i++)search.Advance(100,2000);
        if(search.Path==null)throw new Exception($"Planner missed a supported stair turn halfway between whole-yalm grid lines: {search.Error}, {search.Expanded}");
        if(!search.Path.Any(p=>MathF.Abs(p.X-4.5f)<.01f&&p.Y is >.1f and <1.9f))throw new Exception("Route must use the stairs rather than jump floors.");
        for(var i=1;i<search.Path.Length;i++)if(!geometry.Traverse(search.Path[i-1],search.Path[i]))throw new Exception("Fine steps or nearby joins bypassed collision validation.");
        var blocked=new CollisionRouteSearch(new StairTurn{Wall=true},Vector3.Zero,new(12,2,4),.25f);
        for(var i=0;i<2000&&!blocked.Finished;i++)blocked.Advance(100,2000);
        if(!blocked.Finished||blocked.Path!=null)throw new Exception("Nearby search joins crossed a wall.");
        var seed=new WalkingRoadSeed(new StairTurn{Wall=true},Vector3.Zero,[new(4.5f,0,0),new(4.5f,2,4),new(12,2,4)]);
        for(var i=0;i<200&&!seed.Finished;i++)seed.Step();
        if(seed.StopReason==null||!seed.StopReason.StartsWith("Connection blocked"))throw new Exception("A rejected road must retain the exact failed connector.");
        Console.WriteLine($"475 half-yalm stair turn, collision-checked joins and blocked-guide diagnostics passed ({search.Expanded} nodes).");
    }
    // These are valid actor-center corridors after footprint clearance. The stair
    // turns at x=4.5; neither a direct shortcut nor a whole-yalm lattice can use it.
    private sealed class StairTurn:IWalkingCollision {
        public bool Wall;
        public bool Ground(Vector3 p,out Vector3 q){
            q=p;
            if(MathF.Abs(p.X-4.5f)<.08f&&p.Z>=0&&p.Z<=4)q.Y=p.Z*.5f;
            else if(p.X>=0&&p.X<=4.5f&&MathF.Abs(p.Z)<.08f)q.Y=0;
            else if(p.X>=4.5f&&p.X<=12&&MathF.Abs(p.Z-4)<.08f)q.Y=2;
            else return false;
            return MathF.Abs(q.Y-p.Y)<=.7f;
        }
        public bool Traverse(Vector3 a,Vector3 b){
            if(CollisionRouteSearch.Horizontal(a,b)>2.9f||MathF.Abs(a.Y-b.Y)>1.2f)return false;
            if(Wall&&(a.Z<2)!=(b.Z<2))return false;
            for(var i=0;i<=30;i++){
                var p=Vector3.Lerp(a,b,i/30f);
                if(!Ground(p,out var q)||MathF.Abs(q.Y-p.Y)>.02f)return false;
            }
            return true;
        }
    }
}
