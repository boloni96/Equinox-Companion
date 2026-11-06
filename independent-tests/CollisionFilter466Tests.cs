using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class CollisionFilter466Tests
{
    [ModuleInitializer] internal static void Run()
    {
        // Reproduce the recorded wall plane 0.53 yalms ahead of the actor.
        var start=new Vector3(155.64343f,-25.999994f,-19.400673f);
        var target=new Vector3(155.64343f,-25.999994f,-20.382042f);
        const float wallZ=-19.931623f;
        bool Wall(Vector3 a,Vector3 b,int mask)=>WalkingCollisionFilter.Matches(0x2004,mask)&&
            ((a.Z<wallZ)!=(b.Z<wallZ))&&MathF.Min(a.Y,b.Y)<-24;
        var ground=new Floor();
        if(!WalkingGroundTraversal.Check(ground,start,target,(a,b)=>Wall(a,b,WalkingCollisionFilter.Support),out _))throw new Exception("Fixture must reproduce the old support-only false clear.");
        if(WalkingGroundTraversal.Check(ground,start,target,(a,b)=>Wall(a,b,WalkingCollisionFilter.Movement),out var obstruction)||obstruction==null)throw new Exception("Reported 0x2004 corner wall must reject the approach.");
        if(!WalkingGroundTraversal.Check(ground,start,start+Vector3.UnitZ,(a,b)=>Wall(a,b,WalkingCollisionFilter.Movement),out _))throw new Exception("Clear road away from wall must remain traversable.");
        if(!WalkingCollisionFilter.Matches(0x4000,WalkingCollisionFilter.Movement)||WalkingCollisionFilter.Matches(0x1000,WalkingCollisionFilter.Movement)||WalkingCollisionFilter.Matches(0x2004,WalkingCollisionFilter.Support))throw new Exception("Movement must include support and observed barrier categories without changing support or adding unrelated 0x1000 geometry.");
        Console.WriteLine("Recorded Goblet wall material rejects false-clear path; support and clear-road behavior preserved.");
    }
    private sealed class Floor:IWalkingCollision
    {
        public bool Ground(Vector3 p,out Vector3 q){q=p with{Y=-25.999994f};return true;}
        public bool Traverse(Vector3 a,Vector3 b)=>true;
    }
}
