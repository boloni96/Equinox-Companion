using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class WalkingProgress473Tests
{
    [ModuleInitializer] internal static void Run(){
        Vector3[] road=[Vector3.Zero,new(1,0,0),new(2,0,0),new(3,0,0)];
        // A slow frame crosses a waypoint without ending inside its radius.
        var next=WalkingSteering.AdvanceWaypoint(new(1.4f,0,0),road,1,(_,_)=>false,new(.6f,0,0));
        if(next!=2)throw new Exception("A crossed waypoint must not pull movement backwards.");
        if(WalkingSteering.PursuitTarget(new(1.4f,0,0),road,next,(_,_)=>false,(_,_)=>false)!=new Vector3(1.4f,0,0))throw new Exception("Consumed points never authorize blocked movement.");
        if(WalkingSteering.AdvanceWaypoint(new(1.4f,2,0),road,1,(_,_)=>false,new(.6f,2,0))!=1)throw new Exception("Crossing above a waypoint is not reaching its floor.");
        if(WalkingSteering.AdvanceWaypoint(new(4,0,0),road,1,(_,_)=>false,Vector3.Zero)!=1)throw new Exception("Large discontinuities cannot consume unwalked points.");
        var joined=WalkingSteering.JoinContinuation([Vector3.Zero,new(1,0,0)],[new(1,0,0),new(2,0,0),new(3,0,0)]);
        if(joined==null||joined.Length!=4)throw new Exception("Ready continuation must join without repeating the boundary point.");
        var target=WalkingSteering.PursuitTarget(new(.7f,0,0),joined,1,(_,_)=>true);
        if(target.X<=1)throw new Exception("Pursuit should continue beyond the old section end without a pause.");
        if(WalkingSteering.JoinContinuation(road,[new(3,1,0),new(4,1,0)])!=null)throw new Exception("Disconnected sections cannot merge.");
        Console.WriteLine("473 crossed-waypoint, floor isolation and continuous section handoff tests passed.");
    }
}
