using System.Diagnostics;
using System.Numerics;
namespace EquinoxCompanion;

public sealed class NavigationSliceExpiredException:Exception { }
public sealed record NavigationSearchPoint(float X,float Y,float Z)
{
    public static NavigationSearchPoint From(Vector3 p)=>new(p.X,p.Y,p.Z);
}
public sealed record NavigationSearchDiagnostic(NavigationSearchPoint Origin,NavigationSearchPoint Goal,int Expanded,int Frontier,int BudgetYields,int GroundRejected,int TraverseRejected,int AcceptedEdges,long NativeQueries,long CacheHits,NavigationSearchPoint[] Explored,NavigationSearchPoint[] Rejected,NavigationSearchPoint[] ClosestPath);

public interface IWalkingCollision
{
    long NativeQueries=>0;
    long CacheHits=>0;
    void BeginSlice(double milliseconds) { }
    void EndSlice() { }
    bool Ground(Vector3 near,out Vector3 ground);
    bool Traverse(Vector3 from,Vector3 to);
    float WallPenalty(Vector3 point)=>0;
    float RoutePenalty(Vector3 point)=>0;
}
public sealed class CollisionRouteSearch
{
    private readonly record struct Key(int X,int Y,int Z);
    private sealed record Node(Key Key,Vector3 Point,float Cost,Node? Previous);
    private readonly IWalkingCollision collision;
    private readonly Vector3 origin,goal;
    private readonly float range,spacing,heuristicWeight;
    private readonly PriorityQueue<Node,float> open=new();
    private readonly Dictionary<Key,float> costs=[];
    private readonly HashSet<Key> closed=[];
    private Node? expanding,closest;
    private readonly List<Vector3> explored=[];
    private readonly Queue<Vector3> rejected=[];
    private int groundRejected,traverseRejected,acceptedEdges;
    public NavigationSearchDiagnostic DiagnosticSnapshot(){
        var path=new List<Vector3>();for(var n=closest;n!=null;n=n.Previous)path.Add(n.Point);path.Reverse();
        var stride=Math.Max(1,(int)Math.Ceiling(explored.Count/512d));
        return new(NavigationSearchPoint.From(origin),NavigationSearchPoint.From(goal),Expanded,open.Count,BudgetYields,groundRejected,traverseRejected,acceptedEdges,collision.NativeQueries,collision.CacheHits,
            explored.Where((_,i)=>i%stride==0).Select(NavigationSearchPoint.From).ToArray(),rejected.Select(NavigationSearchPoint.From).ToArray(),path.Take(256).Select(NavigationSearchPoint.From).ToArray());
    }
    private void Rejected(Vector3 point){rejected.Enqueue(point);if(rejected.Count>128)rejected.Dequeue();}
    private int neighbor;
    private static readonly (int X,int Z)[] Directions=[(1,0),(-1,0),(0,1),(0,-1),(1,1),(1,-1),(-1,1),(-1,-1)];
    public string Error { get; private set; }="";
    public Vector3[]? Path { get; private set; }
    public int Expanded=>closed.Count;
    public int BudgetYields { get; private set; }
    public bool Finished=>Path!=null||Error.Length>0;
    public CollisionRouteSearch(IWalkingCollision collision,Vector3 start,Vector3 goal,float range,float spacing=1f,float heuristicWeight=4f)
    {
        this.collision=collision;this.goal=goal;this.range=Math.Clamp(range,.25f,5f);this.spacing=spacing;this.heuristicWeight=heuristicWeight;
        if(!float.IsFinite(heuristicWeight)||heuristicWeight is <1 or >8)throw new ArgumentException("Invalid search priority");
        if(!Finite(start)||!Finite(goal)||!float.IsFinite(spacing)||spacing<.25f||spacing>2)throw new ArgumentException("Invalid navigation coordinates");
        if(Vector3.Distance(start,goal)>2048)throw new InvalidOperationException("Local route exceeds 2048 yalms; use travel first.");
        if(!collision.Ground(start,out origin)){Error="No walkable ground beneath the character.";return;}
        var first=new Node(Address(origin),origin,0,null);costs[first.Key]=0;open.Enqueue(first,Heuristic(origin));
    }
    public void Advance(double milliseconds=1.5,int edgeBudget=24,CancellationToken cancellation=default)
    {
        if(Finished)return;
        var clock=Stopwatch.StartNew();collision.BeginSlice(milliseconds);
        try {
            while(edgeBudget-->0&&clock.Elapsed.TotalMilliseconds<milliseconds){
                cancellation.ThrowIfCancellationRequested();
                if(expanding==null){
                    while(open.TryDequeue(out var next,out _)){
                        if(closed.Contains(next.Key)||next.Cost>costs[next.Key])continue;
                        expanding=next;neighbor=-1;closed.Add(next.Key);explored.Add(next.Point);
                        if(closest==null||Vector3.DistanceSquared(next.Point,goal)<Vector3.DistanceSquared(closest.Point,goal))closest=next;
                        break;
                    }
                    if(expanding==null){Error="No connected walkable route found.";return;}
                    if(closed.Count>96000){Error="Local navigation search limit reached.";return;}
                }
                // Keep the exact node/edge pending until all its native queries finish.
                // Cached queries allow a long edge to resume without repeating native work.
                if(neighbor<0){
                    var next=expanding;Vector3? endpoint=null;
                    var arrived=Horizontal(next.Point,goal)<=range&&MathF.Abs(next.Point.Y-goal.Y)<=1.2f;
                    if(!arrived&&range<1&&Horizontal(next.Point,goal)<=2.6f&&collision.Ground(goal,out var floor)&&MathF.Abs(floor.Y-goal.Y)<=1.2f&&collision.Traverse(next.Point,floor))endpoint=floor;
                    if(arrived||endpoint.HasValue){
                        var path=new List<Vector3>();for(var n=next;n!=null;n=n.Previous)path.Add(n.Point);
                        path.Reverse();if(endpoint is {} end)path.Add(end);Path=path.ToArray();return;
                    }
                    neighbor=0;
                }
                ExpandEdge(expanding,Directions[neighbor]);
                if(++neighbor==Directions.Length)expanding=null;
            }
        }catch(NavigationSliceExpiredException){BudgetYields++;}
        finally{collision.EndSlice();}
    }
    private void ExpandEdge(Node parent,(int X,int Z) direction)
    {
        var candidate=parent.Point+new Vector3(direction.X*spacing,0,direction.Z*spacing);
        if(Horizontal(candidate,origin)>2048||Horizontal(candidate,goal)>Horizontal(origin,goal)+35)return;
        if(!collision.Ground(candidate,out var point)&&
           !collision.Ground(candidate+new Vector3(0,.45f,0),out point)&&
           !collision.Ground(candidate-new Vector3(0,.45f,0),out point)){groundRejected++;Rejected(candidate);return;}
        var key=Address(point);if(closed.Contains(key))return;
        var cost=parent.Cost+Vector3.Distance(parent.Point,point)*(1+6*collision.WallPenalty(point)+3*collision.RoutePenalty(point));
        if(costs.TryGetValue(key,out var known)&&cost>=known)return;
        if(!collision.Traverse(parent.Point,point)){traverseRejected++;Rejected(point);return;}
        acceptedEdges++;
        costs[key]=cost;open.Enqueue(new(key,point,cost,parent),cost+heuristicWeight*Heuristic(point));
    }
    private Key Address(Vector3 p)=>new((int)MathF.Round((p.X-origin.X)/spacing),(int)MathF.Round(p.Y*4),(int)MathF.Round((p.Z-origin.Z)/spacing));
    // Arrival needs both horizontal reach and the correct floor. A point directly
    // beneath the destination must not look like a zero-distance destination.
    internal static float RemainingDistance(Vector3 p,Vector3 goal,float range)
    {
        var horizontal=MathF.Max(0,Horizontal(p,goal)-range);
        var vertical=MathF.Max(0,MathF.Abs(p.Y-goal.Y)-1.2f);
        return MathF.Sqrt(horizontal*horizontal+vertical*vertical);
    }
    private float Heuristic(Vector3 p)=>RemainingDistance(p,goal,range);
    public static float Horizontal(Vector3 a,Vector3 b)=>new Vector2(a.X-b.X,a.Z-b.Z).Length();
    private static bool Finite(Vector3 p)=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z);
}

internal sealed class WalkingGroundReadiness
{
    private DateTimeOffset? since;
    private Vector3 position;
    private uint territory;
    private ulong character;
    public void Reset()=>since=null;
    public bool MayWait(Vector3 current,uint area,ulong actor,DateTimeOffset now)
    {
        if(since==null||area!=territory||actor!=character||Vector3.DistanceSquared(position,current)>.25f)
        {since=now;position=current;territory=area;character=actor;}
        return now-since.Value<TimeSpan.FromSeconds(10);
    }
}
