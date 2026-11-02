using System.Numerics;
namespace EquinoxCompanion;
internal static class WalkingSteering
{
    public static float SmoothHeading(float current,float desired,float seconds){
        var difference=MathF.IEEERemainder(desired-current,2*MathF.PI);
        return current+Math.Clamp(difference,-6*Math.Clamp(seconds,0,.05f),6*Math.Clamp(seconds,0,.05f));
    }
    public static Vector2 Axes(Vector3 from,Vector3 target,float referenceHeading)
    {
        var delta=target-from;
        if(delta.X*delta.X+delta.Z*delta.Z<.0001f)return Vector2.Zero;
        var angle=MathF.Atan2(delta.X,delta.Z)-referenceHeading;
        return new(MathF.Sin(angle),MathF.Cos(angle));
    }
    public static int LookAhead(Vector3 position,Vector3[] path,int next,Func<Vector3,Vector3,bool> clear)
    {
        // Skip grid zigzags only where the live collision test approves the chord.
        var best=next;
        for(var i=next+1;i<path.Length&&i<=next+4;i++){
            if(Vector3.Distance(position,path[i])>2.6f)break;
            if(clear(position,path[i]))best=i;
        }
        return best;
    }
}
internal sealed class NavigationPhaseTimer
{
    private DateTimeOffset started;
    private bool planning;
    public void Begin(DateTimeOffset now){started=now;planning=true;}
    public void SetPlanning(bool value,DateTimeOffset now){if(value==planning)return;planning=value;started=now;}
    public bool Expired(DateTimeOffset now)=>now-started>TimeSpan.FromSeconds(90);
}
internal sealed class AvoidStalledTargets(IWalkingCollision inner,Vector3[] blocked):IWalkingCollision
{
    public bool Ground(Vector3 near,out Vector3 ground)=>inner.Ground(near,out ground);
    public bool Traverse(Vector3 from,Vector3 to)=>
        !blocked.Any(p=>CollisionRouteSearch.Horizontal(p,to)<.65f&&MathF.Abs(p.Y-to.Y)<1)&&inner.Traverse(from,to);
}

// Cross the full footprint, not just three parallel tracks, to catch narrow posts.
internal static class WalkingClearance
{
    public static bool BodyRoom(Vector3 floor,float radius,Func<Vector3,Vector3,bool> blocked)
    {
        foreach(var height in (ReadOnlySpan<float>)[.5f,1.05f,1.75f])
        foreach(var axis in (ReadOnlySpan<Vector3>)[Vector3.UnitX,Vector3.UnitZ,new(.707107f,0,.707107f),new(.707107f,0,-.707107f)]){
            var center=floor+new Vector3(0,height,0);
            if(blocked(center-axis*radius,center+axis*radius))return false;
        }
        return true;
    }
}
