using System.Numerics;
namespace EquinoxCompanion;

// A territory graph of previously traversed routes, not a speculative full-zone mesh.
// Shared samples join routes without inventing edges through walls or between floors.
internal static class WalkingRouteNetwork
{
    private readonly record struct Key(int X,int Y,int Z);
    private static Key Cell(Vector3 p)=>new((int)MathF.Floor(p.X/2),(int)MathF.Floor(p.Y),(int)MathF.Floor(p.Z/2));
    internal static Vector3[]? Find(IEnumerable<RememberedWalkingRoute> routes,uint territory,Vector3 start,Vector3 goal,float range,Func<Vector3,Vector3,bool> clear)
    {
        var points=new List<Vector3>();var edges=new List<List<int>>();var buckets=new Dictionary<Key,List<int>>();
        foreach(var route in routes.Where(r=>r.Territory==territory).TakeLast(256)){
            var previous=-1;
            foreach(var point in route.Points){
                if(points.Count>=24000)break;
                var key=Cell(point);if(!buckets.TryGetValue(key,out var bucket))buckets[key]=bucket=[];
                // Only effectively identical route samples may merge without a new connector check.
                var index=bucket.FirstOrDefault(i=>Vector3.DistanceSquared(points[i],point)<.0001f,-1);
                if(index<0){index=points.Count;points.Add(point);edges.Add([]);bucket.Add(index);}
                if(previous>=0&&previous!=index&&Vector3.Distance(points[previous],point)<=2.9f){edges[previous].Add(index);edges[index].Add(previous);}
                previous=index;
            }
        }
        IEnumerable<int> Near(Vector3 p){var k=Cell(p);for(var x=-1;x<=1;x++)for(var y=-1;y<=1;y++)for(var z=-1;z<=1;z++)if(buckets.TryGetValue(new(k.X+x,k.Y+y,k.Z+z),out var b))foreach(var i in b)if(Vector3.Distance(p,points[i])<=2.2f)yield return i;}
        var open=new PriorityQueue<int,float>();var costs=new Dictionary<int,float>();var parents=new Dictionary<int,int>();var closed=new HashSet<int>();
        foreach(var i in Near(start).OrderBy(i=>Vector3.DistanceSquared(start,points[i])).Take(8))if(clear(start,points[i])){costs[i]=Vector3.Distance(start,points[i]);parents[i]=-1;open.Enqueue(i,costs[i]+Vector3.Distance(points[i],goal));}
        var connectorBudget=96;
        while(open.TryDequeue(out var current,out _)){
            if(!closed.Add(current))continue;
            var p=points[current];
            // Interaction routes end at their accepted stopping range, not at
            // the object's centre. Reuse that observed endpoint on return trips.
            var inRange=CollisionRouteSearch.Horizontal(p,goal)<=range&&MathF.Abs(p.Y-goal.Y)<=1.2f;
            if(inRange||Vector3.Distance(p,goal)<=2.2f&&clear(p,goal)){
                var path=new List<Vector3>();if(!inRange)path.Add(goal);for(var i=current;i>=0;i=parents[i])path.Add(points[i]);path.Add(start);path.Reverse();return path.ToArray();
            }
            var next=new List<int>(edges[current]);
            if(connectorBudget>0)foreach(var i in Near(p).Where(i=>i!=current&&!next.Contains(i)&&!closed.Contains(i)).OrderBy(i=>Vector3.DistanceSquared(points[i],p)).Take(3)){
                if(connectorBudget--<=0)break;if(clear(p,points[i]))next.Add(i);
            }
            foreach(var i in next){if(closed.Contains(i))continue;var cost=costs[current]+Vector3.Distance(p,points[i]);if(costs.TryGetValue(i,out var old)&&old<=cost)continue;costs[i]=cost;parents[i]=current;open.Enqueue(i,cost+Vector3.Distance(points[i],goal));}
        }
        return null;
    }
}
internal sealed class WalkingRecoveryBudget
{
    private int failures;private Vector3 lastRecovery;private bool seen;
    public void Reset(){failures=0;seen=false;}
    public bool Try(Vector3 position){if(seen&&Vector3.Distance(position,lastRecovery)>=8)failures=0;seen=true;lastRecovery=position;return ++failures<=4;}
}
