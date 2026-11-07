using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class WalkingConnector469Tests
{
    [ModuleInitializer] internal static void Run()
    {
        var position=new Vector3(127.02366f,-25.999998f,-19.895237f);
        Vector3[] route=[new(128.12811f,-26,-18.454803f),new(128.22739f,-26,-19.318748f),new(128.44185f,-25.891405f,-20.21749f)];
        var target=WalkingSteering.PursuitTarget(position,route,1,(_,_)=>false,(_,_)=>true);
        if(Vector3.Distance(target,route[1])>.001f)throw new Exception("A soft wall-space rejection must follow the physical route waypoint, not stop or cut the corner.");
        target=WalkingSteering.PursuitTarget(position,route,1,(_,_)=>false,(_,_)=>false);
        if(target!=position)throw new Exception("Physical obstruction must still stop movement.");
        Vector3[] stairs=[Vector3.Zero,new(0,1.8f,2)];
        target=WalkingSteering.PursuitTarget(Vector3.Zero,stairs,1,(_,_)=>false,(a,b)=>b.Y-a.Y<=1.2f);
        if(target==Vector3.Zero||target.Y>1.2f)throw new Exception("Steep lookahead must shorten to a physically checked stair step.");
        var comfortable=WalkingSteering.PursuitTarget(Vector3.Zero,[Vector3.Zero,new(0,0,1),new(0,0,3)],1,(_,_)=>true,(_,_)=>throw new Exception("No fallback needed"));
        if(MathF.Abs(comfortable.Z-2.2f)>.001f)throw new Exception("Open-road pursuit remains smooth.");
        Console.WriteLine("469 connector checks passed: soft clearance, physical blockage, stairs and open-road pursuit.");
    }
}
