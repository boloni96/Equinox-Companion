using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class WalkingContinuity478Tests
{
    [ModuleInitializer] internal static void Run(){
        var road=Enumerable.Range(0,71).Select(i=>new Vector3(i*.5f,0,0)).ToArray();
        if(WalkingSteering.StraightRunEnd(road,0)!=70)throw new Exception("70 support points should form one straight steering run.");
        var next=1;Vector3? previous=null;
        for(float x=0;x<32;x+=.13f){
            var position=new Vector3(x,0,0);
            next=WalkingSteering.AdvanceWaypoint(position,road,next,(_,_)=>true,previous);
            var aim=WalkingSteering.PursuitTarget(position,road,next,(_,_)=>false,(_,_)=>true);
            if(aim.X-position.X<2.19f||MathF.Abs(aim.Z)>.001f)throw new Exception("Narrow straight corridor must keep full forward pursuit, not half-yalm steps or reversals.");
            previous=position;
        }
        Vector3[] corner=[Vector3.Zero,new(1,0,0),new(2,0,0),new(2,0,1),new(2,0,2)];
        if(WalkingSteering.StraightRunEnd(corner,0)!=2)throw new Exception("A real corner must end the straight run.");
        bool Clear(Vector3 a,Vector3 b)=>b.Z==0;
        var target=WalkingSteering.PursuitTarget(Vector3.Zero,corner,1,(_,_)=>false,Clear);
        if(target!=new Vector3(2,0,0))throw new Exception("Aim for the clear corner without crossing the obstacle.");
        if(WalkingSteering.PursuitTarget(Vector3.Zero,corner,1,(_,_)=>false,(_,_)=>false)!=Vector3.Zero)throw new Exception("No checked connector means no movement.");
        Vector3[] crest=[Vector3.Zero,new(1,1,0),new(2,2,0),new(3,1,0),new(4,0,0)];
        if(WalkingSteering.StraightRunEnd(crest,0)!=2)throw new Exception("Do not flatten height changes or cut through a crest.");
        Vector3[] reverse=[Vector3.Zero,new(1,0,0),new(.5f,0,0)];
        if(WalkingSteering.StraightRunEnd(reverse,0)!=1)throw new Exception("Do not fold a route reversal into a straight run.");
        var forward=new Vector3(0,0,2);
        // The direct line/right side are blocked; the left dogleg is clear.
        bool Left(Vector3 a,Vector3 b)=>a.X<-.1f||b.X<-.1f;
        var detour=WalkingSteering.SideDetour(Vector3.Zero,forward,Left);
        if(detour is not {} left||left.X>=0)throw new Exception("Use the checked left side when the right is blocked.");
        bool Right(Vector3 a,Vector3 b)=>a.X>.1f||b.X>.1f;
        detour=WalkingSteering.SideDetour(Vector3.Zero,forward,Right);
        if(detour is not {} right||right.X<=0)throw new Exception("Use the checked right side when the left is blocked.");
        if(WalkingSteering.SideDetour(Vector3.Zero,forward,(a,b)=>a==Vector3.Zero)!=null)throw new Exception("A clear first leg with a blocked reconnection is not a detour.");
        if(WalkingSteering.SideDetour(Vector3.Zero,forward,(_,_)=>false)!=null)throw new Exception("Do not sidestep when both sides are blocked.");
        Console.WriteLine("478 continuous 70-point corridor, physical obstacle, corner, crest and reversal checks passed.");
    }
}
