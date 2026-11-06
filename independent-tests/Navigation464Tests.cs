using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Navigation464Tests
{
    [ModuleInitializer] internal static void Run()
    {
        var goal=new Vector3(112.62f,-15,-24.39f);
        if(CollisionRouteSearch.RemainingDistance(goal-new Vector3(0,11,0),goal,.4f)<9)throw new Exception("Lower floor cannot have zero remaining route distance.");
        if(CollisionRouteSearch.RemainingDistance(goal,goal,.4f)!=0)throw new Exception("Arrival must have zero remaining distance.");
        var floor=new Floor();var cache=new WalkingTraversalCache();var probes=0;
        bool Clear(Vector3 a,Vector3 b){probes++;return false;}
        if(!cache.Check(floor,Vector3.Zero,Vector3.UnitX,Clear,out _))throw new Exception("Open edge rejected.");
        var initial=probes;
        if(!cache.Check(floor,Vector3.Zero,Vector3.UnitX,Clear,out _)||probes!=initial)throw new Exception("Completed edge must reuse exact checks.");
        var interrupted=new WalkingTraversalCache();var first=true;
        bool Interrupt(Vector3 a,Vector3 b){if(first){first=false;throw new NavigationSliceExpiredException();}return false;}
        try{interrupted.Check(floor,Vector3.Zero,Vector3.UnitX,Interrupt,out _);throw new Exception("Expected yield.");}catch(NavigationSliceExpiredException){}
        if(!interrupted.Check(floor,Vector3.Zero,Vector3.UnitX,Interrupt,out _))throw new Exception("Interrupted checks must resume without false failure.");
        var blocked=new WalkingTraversalCache();
        if(blocked.Check(floor,Vector3.Zero,Vector3.UnitX,(_,_)=>true,out var obstruction)||obstruction==null)throw new Exception("Obstacle checks must remain effective.");
        // The next route has fresh geometry, even at the same coordinates.
        if(new WalkingTraversalCache().Check(floor,Vector3.Zero,Vector3.UnitX,(_,_)=>true,out _))throw new Exception("Cache leaked between routes.");
        var search=new CollisionRouteSearch(new Ramp(),Vector3.Zero,new(12,6,0),.4f);
        for(var i=0;i<1000&&!search.Finished;i++)search.Advance(100,1000);
        if(search.Path==null||Math.Abs(search.Path[^1].Y-6)>1.2f)throw new Exception("Elevation-aware search must climb a connected ramp.");
        Console.WriteLine($"Elevation-aware routing and completed clearance caching passed ({initial} probes avoided on repeated edge).");
    }
    private sealed class Floor:IWalkingCollision
    {
        public bool Ground(Vector3 p,out Vector3 q){q=p with{Y=0};return true;}
        public bool Traverse(Vector3 a,Vector3 b)=>true;
    }
    private sealed class Ramp:IWalkingCollision
    {
        public bool Ground(Vector3 p,out Vector3 q){q=p with{Y=p.X*.5f};return p.X>=0&&p.X<=12&&Math.Abs(p.Z)<2;}
        public bool Traverse(Vector3 a,Vector3 b)=>Math.Abs(a.Y-b.Y)<=.6f;
    }
}
