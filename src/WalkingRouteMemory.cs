using System.Numerics;
namespace EquinoxCompanion;
internal sealed record RememberedWalkingRoute(uint Territory,Vector3 Goal,float Range,Vector3[] Points);
internal static class VerifiedWalkingOpening
{
    public static Vector3[] ShortDirect(IWalkingCollision collision,Vector3 start,Vector3 goal,float maxDistance=4){
        var distance=CollisionRouteSearch.Horizontal(start,goal);
        if(distance>Math.Clamp(maxDistance,0,8)||MathF.Abs(start.Y-goal.Y)>1.2f)return [];
        var steps=Math.Max(1,(int)MathF.Ceiling(distance/.25f));
        var path=new List<Vector3>{start};
        for(var i=1;i<=steps;i++){
            var near=Vector3.Lerp(start,goal,(float)i/steps);near.Y=path[^1].Y;
            if(!collision.Ground(near,out var floor)||!collision.Traverse(path[^1],floor))return [];
            path.Add(floor);
        }
        return MathF.Abs(path[^1].Y-goal.Y)<=1.2f?path.ToArray():[];
    }
    // Bounded gate alignment: stay within one yalm of the forward corridor.
    // Every segment uses the same ground/body collision checks as direct walking.
    public static Vector3[] Entrance(IWalkingCollision collision,Vector3 start,Vector3 goal){
        var direct=ShortDirect(collision,start,goal);if(direct.Length>1)return direct;
        var delta=goal-start;delta.Y=0;
        if(delta.Length()<.1f||delta.Length()>4||MathF.Abs(start.Y-goal.Y)>1.2f)return [];
        var side=Vector3.Normalize(new Vector3(-delta.Z,0,delta.X));
        foreach(var offset in new[]{.45f,-.45f,.9f,-.9f}){
            var a=start+delta*.25f+side*offset;
            var b=start+delta*.75f+side*offset;
            var first=ShortDirect(collision,start,a);if(first.Length<2)continue;
            b.Y=first[^1].Y;
            var middle=ShortDirect(collision,first[^1],b);if(middle.Length<2)continue;
            var last=ShortDirect(collision,middle[^1],goal);if(last.Length<2)continue;
            return first.Concat(middle.Skip(1)).Concat(last.Skip(1)).ToArray();
        }
        return [];
    }
    public static Vector3[] Build(IWalkingCollision collision,Vector3 start,Vector3 goal,float range){
        var delta=goal-start;delta.Y=0;var length=delta.Length();
        // Different floors require the planned stair/ramp route, not a direct opening.
        if(length<range+8||MathF.Abs(goal.Y-start.Y)>1.2f)return [];
        var direction=delta/length;var path=new List<Vector3>{start};
        for(var i=1;i<=8;i++){
            var next=start+direction*i;next.Y=path[^1].Y;
            if(!collision.Ground(next,out var floor)||!collision.Traverse(path[^1],floor)||collision.WallPenalty(floor)>.1f)return [];
            path.Add(floor);
        }
        // Keep two checked yalms beyond the opening, so it does not end facing a wall.
        return path.Take(7).ToArray();
    }
}
internal static class WalkingRouteMemory
{
    // Split only connections near the obstruction. Other traversed sections
    // remain usable by the route network, including the opposite side.
    public static IEnumerable<RememberedWalkingRoute> Cut(RememberedWalkingRoute route,Vector3 blocked){
        var chunk=new List<Vector3>();
        for(var i=0;i<route.Points.Length;i++){
            var p=route.Points[i];
            var safePoint=Vector3.Distance(p,blocked)>.8f;
            var safeEdge=i==0||AvoidStalledTargets.ClearOfBlocked(route.Points[i-1],p,[blocked]);
            if(!safePoint||!safeEdge){
                if(chunk.Count>=2)yield return new(route.Territory,chunk[^1],.25f,chunk.ToArray());
                chunk.Clear();
            }
            if(safePoint)chunk.Add(p);
        }
        if(chunk.Count>=2)yield return new(route.Territory,chunk[^1],.25f,chunk.ToArray());
    }
    public static Vector3[]? Prefix(IEnumerable<RememberedWalkingRoute> routes,uint territory,Vector3 start,Vector3 goal,Func<Vector3,Vector3,bool> clear){
        Vector3[]? best=null;var bestRemaining=Vector3.Distance(start,goal)-6;var checks=32;
        foreach(var route in routes.Reverse().Where(r=>r.Territory==territory)){
            for(var i=0;i<route.Points.Length;i++){
                if(Vector3.Distance(start,route.Points[i])>2.2f)continue;
                if(checks--<=0)return best;
                if(!clear(start,route.Points[i]))continue;
                foreach(var direction in new[]{1,-1}){
                    for(var end=i+direction;end>=0&&end<route.Points.Length;end+=direction){
                        // Do not bridge a discontinuity in observed movement.
                        if(Vector3.Distance(route.Points[end-direction],route.Points[end])>2.9f)break;
                        var remaining=Vector3.Distance(route.Points[end],goal);
                        if(remaining>=bestRemaining)continue;
                        bestRemaining=remaining;
                        best=new[]{start}.Concat(Enumerable.Range(0,Math.Abs(end-i)+1).Select(n=>route.Points[i+n*direction])).ToArray();
                    }
                }
                break;
            }
        }
        return best;
    }
    public static Vector3[]? Join(IEnumerable<RememberedWalkingRoute> routes,uint territory,Vector3 position,Vector3 goal,float range,Func<Vector3,Vector3,bool> clear){
        foreach(var route in routes.Reverse()){
            if(route.Territory!=territory||Vector3.Distance(route.Goal,goal)>.1f||MathF.Abs(route.Range-range)>.1f||route.Points.Length<2)continue;
            var joins=route.Points.Select((p,i)=>(Point:p,Index:i,Distance:Vector3.Distance(position,p)))
                .Where(p=>p.Distance<=2.2f).OrderBy(p=>p.Distance);
            foreach(var join in joins){
                if(!clear(position,join.Point))continue;
                return new[]{position}.Concat(route.Points.Skip(join.Index)).ToArray();
            }
        }
        return null;
    }
}

internal enum EntranceProgress { Walking, Arrived, Blocked, OutsideCorridor, Expired }
// A small normal-input attempt at a verified estate landing. The game still
// applies its collision and movement gates; never writes position or jumps.
internal sealed class EntranceForwardProgress
{
    private readonly Vector3 start,direction;
    private readonly float length;
    private readonly DateTimeOffset began;
    private DateTimeOffset progressed;
    private float best;
    public float Heading=>MathF.Atan2(direction.X,direction.Z);
    public EntranceForwardProgress(Vector3 start,Vector3 goal,DateTimeOffset now){
        var delta=goal-start;delta.Y=0;length=delta.Length();
        if(!Finite(start)||!Finite(goal)||length<.3f||length>3.1f||MathF.Abs(goal.Y-start.Y)>.1f)
            throw new InvalidOperationException("Forward entrance attempt must be a short, level-requested segment.");
        this.start=start;direction=delta/length;began=progressed=now;
    }
    private static bool Finite(Vector3 p)=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z);
    public EntranceProgress Observe(Vector3 current,DateTimeOffset now){
        if(!Finite(current))return EntranceProgress.OutsideCorridor;
        var delta=current-start;var along=delta.X*direction.X+delta.Z*direction.Z;
        var sideways=new Vector2(delta.X-direction.X*along,delta.Z-direction.Z*along).Length();
        if(sideways>.75f||along<-.5f||along>length+1||delta.Y>2.5f||delta.Y<-.7f)return EntranceProgress.OutsideCorridor;
        if(new Vector2(delta.X-direction.X*length,delta.Z-direction.Z*length).Length()<=.3f)return EntranceProgress.Arrived;
        if(along>=length)return EntranceProgress.OutsideCorridor;
        if(now-began>=TimeSpan.FromSeconds(8))return EntranceProgress.Expired;
        if(along>=best+.1f){best=along;progressed=now;}
        return now-progressed>=TimeSpan.FromSeconds(1.5)?EntranceProgress.Blocked:EntranceProgress.Walking;
    }
}
