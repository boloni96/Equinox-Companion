using System.Numerics;
namespace EquinoxCompanion;
internal static class WalkingSteering
{
    // Facing is visual; velocity stays on the collision-checked world heading.
    public static Vector2 HeadingAxes(float heading,float reference,float speed)=>
        new(MathF.Sin(heading-reference)*speed,MathF.Cos(heading-reference)*speed);
    public static float SmoothHeading(float current,float desired,float seconds){
        var difference=MathF.IEEERemainder(desired-current,2*MathF.PI);
        var dt=Math.Clamp(seconds,0,.05f);
        return current+Math.Clamp(difference*(1-MathF.Exp(-8*dt)),-2.4f*dt,2.4f*dt);
    }
    public static int AdvanceWaypoint(Vector3 position,Vector3[] path,int next,Func<Vector3,Vector3,bool> clear,Vector3? previous=null)
    {
        // A reached waypoint is consumed even if extra wall space is unavailable.
        // Pursuit still validates every step toward the following waypoint.
        while(next<path.Length-1&&ReachedWaypoint(position,previous,path[next]))next++;
        // Recover forward progress after a rounded corner or a slow frame.
        // Only join a nearby onward segment through a checked connector.
        var bestDistance=float.MaxValue;var best=next;
        for(var i=next;i<path.Length&&i<=next+12;i++){
            var start=path[Math.Max(0,i-1)];var edge=path[i]-start;
            var t=edge.LengthSquared()<.0001f?1:Math.Clamp(Vector3.Dot(position-start,edge)/edge.LengthSquared(),0,1);
            var projection=Vector3.Lerp(start,path[i],t);var distance=Vector3.Distance(position,projection);
            if(distance>.9f||distance>bestDistance+.02f)continue;
            var candidate=t>=.95f&&i<path.Length-1?i+1:i;
            var connector=BoundedTarget(position,path[candidate]);
            if(!clear(position,connector))continue;
            bestDistance=distance;best=candidate;
        }
        next=Math.Min(best,path.Length-1);
        return next;
    }
    private static bool ReachedWaypoint(Vector3 position,Vector3? previous,Vector3 point){
        if(Vector3.Distance(position,point)<=.25f)return true;
        if(previous is not {} before||Vector3.DistanceSquared(before,position)>6.25f)return false;
        var motion=position-before;var length=motion.LengthSquared();
        if(length<.000001f)return false;
        var t=Math.Clamp(Vector3.Dot(point-before,motion)/length,0,1);
        return Vector3.DistanceSquared(point,before+motion*t)<=.0625f;
    }
    public static Vector3[]? JoinContinuation(Vector3[] path,Vector3[] continuation){
        if(path.Length==0||continuation.Length<2)return null;
        // A ready full route can repeat the verified prefix already being walked.
        // Merge only an exact shared suffix/prefix, keeping the live waypoint index.
        for(var start=0;start<path.Length;start++){
            var overlap=path.Length-start;if(overlap>continuation.Length)continue;
            var matches=true;
            for(var i=0;i<overlap;i++)if(Vector3.DistanceSquared(path[start+i],continuation[i])>.000001f){matches=false;break;}
            if(matches)return path.Concat(continuation.Skip(overlap)).ToArray();
        }
        return null;
    }
    public static Vector3 PursuitTarget(Vector3 position,Vector3[] path,int next,Func<Vector3,Vector3,bool> clear,Func<Vector3,Vector3,bool>? traversable=null)
    {
        // Dense support samples are not steering instructions. Aim along a
        // straight run until a real bend; the immediate chord is checked below.
        var runEnd=StraightRunEnd(path,Math.Max(0,next-1));
        if(runEnd>next){
            var along=BoundedTarget(position,path[runEnd]);
            if(clear(position,along))return along;
            if(traversable!=null&&traversable(position,along))return along;
        }
        var start=path[Math.Max(0,next-1)];var segment=path[next]-start;
        var length=segment.LengthSquared();var t=length<.0001f?1:Math.Clamp(Vector3.Dot(position-start,segment)/length,0,1);
        var point=Vector3.Lerp(start,path[next],t);var remaining=2.2f;
        for(var i=next;i<path.Length;i++){
            var distance=Vector3.Distance(point,path[i]);
            if(distance>=remaining){point=Vector3.Lerp(point,path[i],remaining/MathF.Max(distance,.001f));break;}
            point=path[i];remaining-=distance;
        }
        point=BoundedTarget(position,point);
        if(clear(position,point))return point;
        // A three-yalm wall-space preference must not reduce a clear corridor
        // to half-yalm steering targets. Keep pursuit when physical checks pass.
        if(traversable!=null&&traversable(position,point))return point;
        var fallback=BoundedTarget(position,path[next]);
        if(clear(position,fallback))return fallback;
        // Extra wall space is a preference, not an impassable obstacle. When a
        // shortcut loses that space, follow the next route point with the same
        // physical checks as planning. Shorten steep stair connectors rather than
        // rejecting a whole staircase because a lookahead spans too much rise.
        if(traversable!=null){
            if(traversable(position,fallback))return fallback;
            if(SideDetour(position,fallback,traversable) is {} detour)return detour;
            for(var distance=Vector3.Distance(position,fallback);distance>=.2f;distance*=.5f){
                var step=Vector3.Lerp(position,fallback,distance/MathF.Max(.001f,Vector3.Distance(position,fallback)));
                if(traversable(position,step))return step;
            }
        }
        return position;
    }
    // Check both legs of a short detour; never take a blind sidestep.
    internal static Vector3? SideDetour(Vector3 position,Vector3 onward,Func<Vector3,Vector3,bool> traversable)
    {
        var edge=onward-position;var horizontal=new Vector3(edge.X,0,edge.Z);
        if(horizontal.LengthSquared()<.04f)return null;
        var side=Vector3.Normalize(new Vector3(-edge.Z,0,edge.X));
        foreach(var width in (ReadOnlySpan<float>)[.75f,1.25f]){
            foreach(var sign in (ReadOnlySpan<float>)[1,-1]){
                var candidate=Vector3.Lerp(position,onward,.5f)+side*(width*sign);
                if(Vector3.Distance(position,candidate)>2.2f||Vector3.Distance(candidate,onward)>2.2f)continue;
                if(traversable(position,candidate)&&traversable(candidate,onward))return candidate;
            }
        }
        return null;
    }
    internal static int StraightRunEnd(Vector3[] path,int start)
    {
        var end=Math.Min(start+1,path.Length-1);
        // Bound CPU work and preserve height changes as well as horizontal bends.
        for(var candidate=end+1;candidate<path.Length&&candidate<=start+128;candidate++){
            var edge=path[candidate]-path[start];var length=edge.LengthSquared();
            if(length<.0001f)break;
            var previous=0f;var straight=true;
            for(var i=start+1;i<candidate;i++){
                var t=Vector3.Dot(path[i]-path[start],edge)/length;
                if(t<previous||t>1||Vector3.DistanceSquared(path[i],path[start]+edge*t)>.0025f){straight=false;break;}
                previous=t;
            }
            if(!straight)break;
            end=candidate;
        }
        return end;
    }
    public static Vector3 BoundedTarget(Vector3 position,Vector3 target){
        var distance=Vector3.Distance(position,target);
        return Vector3.Lerp(position,target,MathF.Min(1,2.2f/MathF.Max(distance,.001f)));
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
    public long NativeQueries=>inner.NativeQueries;
    public long CacheHits=>inner.CacheHits;
    public void BeginSlice(double milliseconds)=>inner.BeginSlice(milliseconds);
    public void EndSlice()=>inner.EndSlice();
    public bool Ground(Vector3 near,out Vector3 ground)=>inner.Ground(near,out ground);
    public float WallPenalty(Vector3 point){
        var result=inner.WallPenalty(point);
        foreach(var p in blocked)if(MathF.Abs(p.Y-point.Y)<1)result=MathF.Max(result,Math.Clamp(1-CollisionRouteSearch.Horizontal(p,point)/3,0,1));
        return result;
    }
    public float RoutePenalty(Vector3 point)=>inner.RoutePenalty(point);
    public string? TraversalFailure { get; private set; }
    public bool Traverse(Vector3 from,Vector3 to){
        if(!ClearOfBlocked(from,to,blocked)){TraversalFailure="Previously stalled obstacle exclusion";return false;}
        var result=inner.Traverse(from,to);TraversalFailure=inner.TraversalFailure;return result;
    }
    public static bool ClearOfBlocked(Vector3 from,Vector3 to,IEnumerable<Vector3> blocked)
    {
        var edge=new Vector2(to.X-from.X,to.Z-from.Z);
        foreach(var p in blocked){
            var offset=new Vector2(p.X-from.X,p.Z-from.Z);
            var t=edge.LengthSquared()<.0001f?0:Math.Clamp(Vector2.Dot(offset,edge)/edge.LengthSquared(),0,1);
            var nearest=Vector3.Lerp(from,to,t);
            if(MathF.Abs(p.Y-nearest.Y)>=1||CollisionRouteSearch.Horizontal(p,nearest)>=.65f)continue;
            // A failed step can leave the player inside the exclusion. Permit escape
            // away from it, but never a shortcut across it or toward its centre.
            if(offset.Length()<.65f&&Vector2.Dot(offset,edge)<=0&&CollisionRouteSearch.Horizontal(p,to)>offset.Length()+.01f)continue;
            return false;
        }
        return true;
    }
}

// Cross the full footprint, not just three parallel tracks, to catch narrow posts.
internal static class WalkingClearance
{
    public static bool SegmentBlocked(Vector3 from,Vector3 to,Func<Vector3,Vector3,bool> cast)=>cast(from,to)||cast(to,from);
    public static bool PreservesRoom(Vector3 from,Vector3 to,Func<Vector3,float> penalty){
        var allowed=MathF.Max(penalty(from),penalty(to))+.1f;
        for(var i=1;i<4;i++)if(penalty(Vector3.Lerp(from,to,i/4f))>allowed)return false;
        return true;
    }
    public static bool BodyRoom(Vector3 floor,float radius,Func<Vector3,Vector3,bool> blocked,Func<Vector3,Vector3?>? support=null)
    {
        foreach(var axis in (ReadOnlySpan<Vector3>)[Vector3.UnitX,Vector3.UnitZ,new(.707107f,0,.707107f),new(.707107f,0,-.707107f)]){
            var low=floor-axis*radius;var high=floor+axis*radius;
            if(support!=null){
                if(support(low) is not {} a||support(high) is not {} b)return false;
                low=a;high=b;
            }
            // A cross-body ray must clear the ground at both ends. Using the
            // center's height casts the low ray into an otherwise walkable slope.
            foreach(var height in (ReadOnlySpan<float>)[.5f,1.05f,1.75f]){
                var a=low;var b=high;a.Y=b.Y=MathF.Max(floor.Y,MathF.Max(low.Y,high.Y))+height;
                if(blocked(a,b))return false;
            }
        }
        return true;
    }
}
