using System.Diagnostics;
using System.Numerics;
namespace EquinoxCompanion;

public interface IWalkingCollision
{
    bool Ground(Vector3 near,out Vector3 ground);
    bool Traverse(Vector3 from,Vector3 to);
}
public sealed class CollisionRouteSearch
{
    private readonly record struct Key(int X,int Y,int Z);
    private sealed record Node(Key Key,Vector3 Point,float Cost,Node? Previous);
    private readonly IWalkingCollision collision;
    private readonly Vector3 origin,goal;
    private readonly float range,spacing;
    private readonly PriorityQueue<Node,float> open=new();
    private readonly Dictionary<Key,float> costs=[];
    private readonly HashSet<Key> closed=[];
    private Node? expanding;
    private int neighbor;
    private static readonly (int X,int Z)[] Directions=[(1,0),(-1,0),(0,1),(0,-1),(1,1),(1,-1),(-1,1),(-1,-1)];
    public string Error { get; private set; }="";
    public Vector3[]? Path { get; private set; }
    public int Expanded=>closed.Count;
    public bool Finished=>Path!=null||Error.Length>0;
    public CollisionRouteSearch(IWalkingCollision collision,Vector3 start,Vector3 goal,float range,float spacing=1f)
    {
        this.collision=collision;this.goal=goal;this.range=Math.Clamp(range,.25f,5f);this.spacing=spacing;
        if(!Finite(start)||!Finite(goal)||!float.IsFinite(spacing)||spacing<.25f||spacing>2)throw new ArgumentException("Invalid navigation coordinates");
        if(Vector3.Distance(start,goal)>160)throw new InvalidOperationException("Local route exceeds 160 yalms; use travel first.");
        if(!collision.Ground(start,out origin)){Error="No walkable ground beneath the character.";return;}
        var first=new Node(Address(origin),origin,0,null);costs[first.Key]=0;open.Enqueue(first,Heuristic(origin));
    }
    public void Advance(double milliseconds=1.5,int edgeBudget=24,CancellationToken cancellation=default)
    {
        if(Finished)return;
        var clock=Stopwatch.StartNew();
        while(edgeBudget-->0&&clock.Elapsed.TotalMilliseconds<milliseconds)
        {
            cancellation.ThrowIfCancellationRequested();
            if(expanding==null)
            {
                Node? next=null;
                while(open.TryDequeue(out var n,out _)){if(!closed.Contains(n.Key)&&n.Cost<=costs[n.Key]){next=n;break;}}
                if(next==null){Error="No connected walkable route found.";return;}
                closed.Add(next.Key);
                if(closed.Count>12000){Error="Local navigation search limit reached.";return;}
                if(Horizontal(next.Point,goal)<=range&&MathF.Abs(next.Point.Y-goal.Y)<=1.2f)
                {
                    var path=new List<Vector3>();for(var n=next;n!=null;n=n.Previous)path.Add(n.Point);
                    path.Reverse();Path=path.ToArray();return;
                }
                expanding=next;neighbor=0;
            }
            var parent=expanding;var d=Directions[neighbor++];if(neighbor==Directions.Length)expanding=null;
            var candidate=parent.Point+new Vector3(d.X*spacing,0,d.Z*spacing);
            if(Horizontal(candidate,origin)>160||Horizontal(candidate,goal)>Horizontal(origin,goal)+35)continue;
            if(!collision.Ground(candidate,out var point))continue;
            var key=Address(point);if(closed.Contains(key))continue;
            var cost=parent.Cost+Vector3.Distance(parent.Point,point);
            if(costs.TryGetValue(key,out var known)&&cost>=known)continue;
            if(!collision.Traverse(parent.Point,point))continue;
            costs[key]=cost;open.Enqueue(new(key,point,cost,parent),cost+1.25f*Heuristic(point));
        }
    }
    private Key Address(Vector3 p)=>new((int)MathF.Round((p.X-origin.X)/spacing),(int)MathF.Round(p.Y*4),(int)MathF.Round((p.Z-origin.Z)/spacing));
    private float Heuristic(Vector3 p)=>MathF.Max(0,Horizontal(p,goal)-range);
    public static float Horizontal(Vector3 a,Vector3 b)=>new Vector2(a.X-b.X,a.Z-b.Z).Length();
    private static bool Finite(Vector3 p)=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z);
}
