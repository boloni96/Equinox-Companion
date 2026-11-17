using System.Numerics;
namespace EquinoxCompanion;
internal static class WorkshopRouteReuse
{
    internal static Vector3[]? Find(IEnumerable<RememberedWalkingRoute> routes,uint territory,Vector3 start,Vector3 goal,float range,Func<Vector3,Vector3,bool> connector)
    {
        foreach(var route in routes.Reverse()){
            if(route.Territory!=territory||route.Points.Length<2||Vector3.DistanceSquared(route.Goal,goal)>1||!InteractionApproachPolicy.WithinDistance(route.Points[^1],goal,range))continue;
            var candidates=Enumerable.Range(0,route.Points.Length).Where(i=>Vector3.DistanceSquared(start,route.Points[i])<=9&&MathF.Abs(start.Y-route.Points[i].Y)<.5f).OrderBy(i=>Vector3.DistanceSquared(start,route.Points[i]));
            foreach(var i in candidates){if(!connector(start,route.Points[i]))continue;return new[]{start}.Concat(route.Points.Skip(i)).ToArray();}
        }
        return null;
    }
}
