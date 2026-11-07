using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Navigation471Tests
{
    [ModuleInitializer] internal static void Run()
    {
        var route=new RememberedWalkingRoute(341,new(20,0,0),.4f,Enumerable.Range(0,21).Select(x=>new Vector3(x,0,0)).ToArray());
        var split=WalkingRouteMemory.Cut(route,new(10,0,0)).ToArray();
        if(split.Length!=2||split[0].Points[^1].X!=9||split[1].Points[0].X!=11)throw new Exception("Only the failed connection should be removed; retain both useful sections.");
        foreach(var r in split)for(var i=1;i<r.Points.Length;i++)if(!AvoidStalledTargets.ClearOfBlocked(r.Points[i-1],r.Points[i],[new(10,0,0)]))throw new Exception("Cache retained blocked edge.");
        var prefix=WalkingRouteMemory.Prefix(split,341,Vector3.Zero,new(20,0,0),(a,b)=>Vector3.Distance(a,b)<.1f);
        if(prefix==null||prefix[^1].X!=9)throw new Exception("Retry must reuse the observed prefix while planning the remaining section.");
        if(WalkingRouteMemory.Prefix(split,339,Vector3.Zero,new(20,0,0),(_,_)=>true)!=null||WalkingRouteMemory.Prefix(split,341,Vector3.Zero,new(20,0,0),(_,_)=>false)!=null)throw new Exception("Cache must respect territory and blocked entry.");
        if(WalkingRouteMemory.Prefix(split,341,new(0,9,0),new(20,9,0),(_,_)=>true)!=null)throw new Exception("Cache cannot join another floor.");
        var adjusted=WalkingRouteNetwork.Find([route],341,Vector3.Zero,new(20.8f,0,0),.4f,(a,b)=>Vector3.Distance(a,b)<=2.2f);
        if(adjusted==null||adjusted[^1]!=new Vector3(20.8f,0,0))throw new Exception("A slightly different approach point should reuse the route plus a checked final connector.");
        if(WalkingRouteNetwork.Find([route],341,Vector3.Zero,new(20.8f,0,0),.4f,(a,b)=>b.X<=20)!=null)throw new Exception("A changed destination may not bypass a blocked final connector.");
        var clock=new WalkingGroundReadiness();var now=DateTimeOffset.UtcNow;
        if(!clock.MayWait(Vector3.Zero,341,1,now,2)||clock.MayWait(Vector3.Zero,341,1,now.AddSeconds(2),2))throw new Exception("Missing support recovery must have a bounded deadline.");
        var collision=new OpenRoad();var search=new CollisionRouteSearch(collision,Vector3.Zero,new(48,0,0),.4f);
        for(var i=0;i<500&&!search.Finished;i++)search.Advance(100,2000);
        if(search.Path==null||collision.WallReads>search.Expanded*3)throw new Exception("Clearance queries must scale with selected nodes, not every neighbor.");
        Console.WriteLine($"471 cache split/prefix, floor isolation and bounded support retry passed; {collision.WallReads} wall reads for {search.Expanded} expanded nodes.");
    }
    private sealed class OpenRoad:IWalkingCollision {
        public int WallReads;
        public bool Ground(Vector3 p,out Vector3 floor){floor=p;return true;}
        public bool Traverse(Vector3 a,Vector3 b)=>true;
        public float WallPenalty(Vector3 point){WallReads++;return 0;}
    }
}
