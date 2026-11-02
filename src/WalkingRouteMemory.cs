using System.Numerics;
namespace EquinoxCompanion;
internal sealed record RememberedWalkingRoute(uint Territory,Vector3 Goal,float Range,Vector3[] Points);
internal static class VerifiedWalkingOpening
{
    public static Vector3[] Build(IWalkingCollision collision,Vector3 start,Vector3 goal,float range){
        var delta=goal-start;delta.Y=0;var length=delta.Length();
        if(length<range+4)return [];
        var direction=delta/length;var path=new List<Vector3>{start};
        for(var i=1;i<=6&&i<length-range;i++){
            var next=start+direction*i;next.Y=path[^1].Y;
            if(!collision.Ground(next,out var floor)||!collision.Traverse(path[^1],floor))break;
            path.Add(floor);
        }
        return path.Count>=5?path.ToArray():[];
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
