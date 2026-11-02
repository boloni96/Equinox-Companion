using System.Numerics;
namespace EquinoxCompanion;

// Search core for a future collision-built mesh. It does NOT extract game geometry.
// Builder must supply convex, clearance-tested walkable cells and shared-edge portals.
public sealed class IndependentNavigationGraph
{
    public sealed record Cell(int Id,Vector3 Center);
    public sealed record Portal(int From,int To,Vector3 Position);
    private readonly Dictionary<int,Cell> cells;
    private readonly Dictionary<int,Portal[]> edges;
    public IndependentNavigationGraph(IEnumerable<Cell> cells,IEnumerable<Portal> portals)
    {
        this.cells=cells.ToDictionary(c=>c.Id);
        if(this.cells.Values.Any(c=>!Finite(c.Center)))throw new ArgumentException("Non-finite cell");
        var links=portals.ToArray();
        if(links.Any(p=>!this.cells.ContainsKey(p.From)||!this.cells.ContainsKey(p.To)||!Finite(p.Position)))
            throw new ArgumentException("Invalid portal");
        edges=links.GroupBy(p=>p.From).ToDictionary(g=>g.Key,g=>g.ToArray());
    }
    public Vector3[]? Find(int start,int goal,CancellationToken cancel=default,int expansionLimit=100000)
    {
        if(!cells.ContainsKey(start)||!cells.ContainsKey(goal))return null;
        var queue=new PriorityQueue<int,float>();var cost=new Dictionary<int,float>{{start,0}};
        var previous=new Dictionary<int,Portal>();var closed=new HashSet<int>();queue.Enqueue(start,0);
        while(queue.TryDequeue(out var current,out _))
        {
            cancel.ThrowIfCancellationRequested();
            if(!closed.Add(current))continue;
            if(closed.Count>expansionLimit)throw new InvalidOperationException("Navigation search budget exceeded");
            if(current==goal)
            {
                var result=new List<Vector3>{cells[goal].Center};
                while(current!=start){var p=previous[current];result.Add(p.Position);result.Add(cells[p.From].Center);current=p.From;}
                result.Reverse();return result.ToArray();
            }
            if(!edges.TryGetValue(current,out var next))continue;
            foreach(var p in next)
            {
                var candidate=cost[current]+Vector3.Distance(cells[current].Center,p.Position)+Vector3.Distance(p.Position,cells[p.To].Center);
                if(cost.TryGetValue(p.To,out var known)&&candidate>=known)continue;
                cost[p.To]=candidate;previous[p.To]=p;
                queue.Enqueue(p.To,candidate+Vector3.Distance(cells[p.To].Center,cells[goal].Center));
            }
        }
        return null; // Never replace a missing route with a straight line.
    }
    private static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
}
