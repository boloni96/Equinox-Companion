using System.Numerics;
namespace EquinoxCompanion;
internal sealed record RememberedWalkingRoute(uint Territory,Vector3 Goal,float Range,Vector3[] Points);
internal static class VerifiedWalkingOpening
{
    public static Vector3[] ShortDirect(IWalkingCollision collision,Vector3 start,Vector3 goal){
        var distance=CollisionRouteSearch.Horizontal(start,goal);
        if(distance>4||MathF.Abs(start.Y-goal.Y)>1.2f)return [];
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
