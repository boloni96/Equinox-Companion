using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class WalkingShortcut483Tests
{
    [ModuleInitializer] internal static void Run(){
        Vector3[] grid=[new(0,0,0),new(.5f,0,0),new(1,0,0),new(1,0,.5f),new(1,0,1),new(1.5f,0,1),new(2,0,1),new(2,0,1.5f),new(2,0,2)];
        var next=WalkingSteering.AdvanceWaypoint(Vector3.Zero,grid,1,(_,_)=>true);
        if(next<4)throw new Exception("Clear grid staircase must join an onward point instead of visiting every bend.");
        // A shortcut cannot cross a wall just because its endpoints are nearby.
        next=WalkingSteering.AdvanceWaypoint(Vector3.Zero,grid,1,(a,b)=>b.Z==0);
        if(grid[next].Z!=0)throw new Exception("Blocked corner was skipped.");
        // After a rounded turn, consume the passed corner rather than turn back.
        Vector3[] bend=[new(0,0,0),new(0,0,2),new(2,0,2),new(4,0,2)];
        next=WalkingSteering.AdvanceWaypoint(new(1.2f,0,.8f),bend,1,(_,_)=>true);
        if(next!=2)throw new Exception("A clear rounded-corner connector should advance beyond the old corner.");
        next=WalkingSteering.AdvanceWaypoint(new(1.2f,0,.8f),bend,1,(_,_)=>false);
        if(next!=1)throw new Exception("Do not consume a corner without a checked reconnection.");
        Vector3[] stacked=[Vector3.Zero,new(0,0,1),new(0,4,1),new(0,4,0)];
        next=WalkingSteering.AdvanceWaypoint(Vector3.Zero,stacked,1,(_,_)=>true);
        if(next!=1)throw new Exception("Do not skip to a different floor.");
        var observed=Enumerable.Range(0,21).Select(i=>new Vector3(i,0,0)).ToArray();
        RememberedWalkingRoute[] routes=[new(1,observed[^1],1,observed)];
        var prefix=WalkingRouteMemory.Prefix(routes,1,new(20,0,0),new(-10,0,0),(_,_)=>true);
        if(prefix==null||prefix[^1].X!=0)throw new Exception("Return trips should reuse observed sections in reverse.");
        var reused=WalkingRouteNetwork.Find(routes,1,new(20,0,0),new(-3,0,0),4,(_,_)=>true);
        if(reused==null||Vector3.Distance(reused[^1],new(-3,0,0))>4)throw new Exception("A remembered interaction endpoint inside arrival range must be reusable.");
        if(WalkingRouteNetwork.Find(routes,1,new(20,0,0),new(-3,5,0),4,(_,_)=>false)!=null)throw new Exception("Arrival range cannot join different floors.");
        Console.WriteLine("483 checked grid shortcuts, rounded-corner progress, walls and floor separation passed.");
    }
}
