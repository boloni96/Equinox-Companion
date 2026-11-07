using System.Diagnostics;
using System.Numerics;
namespace EquinoxCompanion;

public sealed class NavigationSliceExpiredException:Exception { }
public sealed record NavigationSearchPoint(float X,float Y,float Z)
{
    public static NavigationSearchPoint From(Vector3 p)=>new(p.X,p.Y,p.Z);
}
public sealed record NavigationSearchDiagnostic(NavigationSearchPoint Origin,NavigationSearchPoint Goal,int Expanded,int Frontier,int BudgetYields,int GroundRejected,int TraverseRejected,int AcceptedEdges,long NativeQueries,long CacheHits,NavigationSearchPoint[] Explored,NavigationSearchPoint[] Rejected,NavigationSearchPoint[] ClosestPath,int ReverseExpanded,NavigationSearchPoint[] ReverseExplored,bool RoadGuided,int RoadSeedPoints);

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
    private sealed record Node(Key Key,Vector3 Point,float Cost,Node? Previous,bool Priced=false);
    private sealed class Frontier(bool reverse)
    {
        public readonly bool Reverse=reverse;
        public readonly PriorityQueue<Node,float> Open=new();
        public readonly Dictionary<Key,Node> Closed=[];
        public Node? Pending;
        public int Neighbor=-3;
    }
    private readonly IWalkingCollision collision;
    private readonly Vector3 origin,goal;
    private readonly float range,spacing,heuristicWeight,searchRadius;
    private readonly NavigationRoadRoute? roads;
    private readonly Frontier forward=new(false);
    private Frontier? backward,active;
    private Node? closest;
    private WalkingRoadSeed? roadSeed;
    private int roadSeedPoints;
    private readonly List<Vector3> explored=[],reverseExplored=[];
    private readonly Queue<Vector3> rejected=[];
    private int groundRejected,traverseRejected,acceptedEdges;
    private static readonly (int X,int Z)[] Directions=[(2,0),(-2,0),(0,2),(0,-2),(2,2),(2,-2),(-2,2),(-2,-2),(1,0),(-1,0),(0,1),(0,-1),(1,1),(1,-1),(-1,1),(-1,-1)];
    public string Error { get; private set; }="";
    public Vector3[]? Path { get; private set; }
    public int Expanded=>forward.Closed.Count+(backward?.Closed.Count??0);
    public int BudgetYields { get; private set; }
    public bool Finished=>Path!=null||Error.Length>0;
    public NavigationSearchDiagnostic DiagnosticSnapshot(){
        var path=Trail(closest);var stride=Math.Max(1,(int)Math.Ceiling(explored.Count/512d));
        var reverseStride=Math.Max(1,(int)Math.Ceiling(reverseExplored.Count/512d));
        return new(NavigationSearchPoint.From(origin),NavigationSearchPoint.From(goal),Expanded,forward.Open.Count+(backward?.Open.Count??0),BudgetYields,groundRejected,traverseRejected,acceptedEdges,collision.NativeQueries,collision.CacheHits,
            explored.Where((_,i)=>i%stride==0).Select(NavigationSearchPoint.From).ToArray(),rejected.Select(NavigationSearchPoint.From).ToArray(),path.Take(256).Select(NavigationSearchPoint.From).ToArray(),
            backward?.Closed.Count??0,reverseExplored.Where((_,i)=>i%reverseStride==0).Select(NavigationSearchPoint.From).ToArray(),roads!=null,roadSeedPoints);
    }
    private static List<Vector3> Trail(Node? node){var p=new List<Vector3>();for(var n=node;n!=null;n=n.Previous)p.Add(n.Point);p.Reverse();return p;}
    private void Rejected(Vector3 point){rejected.Enqueue(point);if(rejected.Count>128)rejected.Dequeue();}
    public CollisionRouteSearch(IWalkingCollision collision,Vector3 start,Vector3 goal,float range,float spacing=1f,float heuristicWeight=4f,NavigationRoadRoute? roads=null,bool bidirectional=true)
    {
        this.collision=collision;this.goal=goal;this.range=Math.Clamp(range,.25f,5f);this.spacing=spacing;this.heuristicWeight=heuristicWeight;this.roads=roads;
        searchRadius=MathF.Max(Horizontal(start,goal)+35,roads?.Points.Max(p=>Horizontal(p,goal))+16??0);
        if(!float.IsFinite(heuristicWeight)||heuristicWeight is <1 or >8)throw new ArgumentException("Invalid search priority");
        if(!Finite(start)||!Finite(goal)||!float.IsFinite(spacing)||spacing<.25f||spacing>2)throw new ArgumentException("Invalid navigation coordinates");
        if(Vector3.Distance(start,goal)>2048)throw new InvalidOperationException("Local route exceeds 2048 yalms; use travel first.");
        if(!collision.Ground(start,out origin)){Error="No walkable ground beneath the character.";return;}
        forward.Open.Enqueue(new(Address(origin),origin,0,null),0);
        // Start from the actual upper/lower destination floor too. Reverse edges
        // are validated in the direction the character will eventually walk.
        if(bidirectional&&Vector3.Distance(origin,goal)>=12&&collision.Ground(goal,out var end)&&MathF.Abs(end.Y-goal.Y)<=1.2f){
            backward=new(true);backward.Open.Enqueue(new(Address(end),end,0,null),0);
        }
        if(roads is not null&&Vector3.Distance(origin,goal)>=12)roadSeed=new(collision,origin,roads.Points);
        active=forward;
    }
    public void Advance(double milliseconds=1.5,int edgeBudget=24,CancellationToken cancellation=default)
    {
        if(Finished)return;
        var clock=Stopwatch.StartNew();collision.BeginSlice(milliseconds);
        try{
            while(edgeBudget-->0&&clock.Elapsed.TotalMilliseconds<milliseconds){
                cancellation.ThrowIfCancellationRequested();
                if(roadSeed is {} seed){
                    seed.Step();roadSeedPoints=seed.Count;
                    if(seed.Finished){
                        var points=seed.Points;Node? parent=null;var cost=0f;
                        foreach(var point in points){
                            if(parent!=null)cost+=Vector3.Distance(parent.Point,point);
                            parent=new(Address(point),point,cost,parent,true);
                        }
                        if(points.Length>2&&parent!=null)forward.Open.Enqueue(parent,cost+heuristicWeight*Heuristic(parent.Point,false));
                        roadSeed=null;
                    }
                    continue;
                }
                var side=active??forward;
                if(side.Pending==null){
                    while(side.Open.TryDequeue(out var next,out _)){
                        if(side.Closed.ContainsKey(next.Key))continue;
                        side.Pending=next;side.Neighbor=-3;break;
                    }
                    if(side.Pending==null){
                        var other=side.Reverse?forward:backward;
                        if(other==null||(other.Pending==null&&other.Open.Count==0)){Error="No connected walkable route found.";return;}
                        active=other;continue;
                    }
                }
                var current=side.Pending!;
                if(side.Neighbor==-3){
                    // Price wall clearance only for promising candidates, instead
                    // of probing eight directions around every unused neighbor.
                    if(!current.Priced&&current.Previous is {} priceParent){
                        var cost=current.Cost+Vector3.Distance(priceParent.Point,current.Point)*6*collision.WallPenalty(current.Point);
                        var priced=current with{Cost=cost,Priced=true};
                        side.Open.Enqueue(priced,cost+heuristicWeight*Heuristic(priced.Point,side.Reverse));
                        side.Pending=null;continue;
                    }
                    // Lazy edge validation: expensive swept-body probes run only
                    // when a candidate is selected, not for every unused branch.
                    if(current.Previous is {} parent&&!collision.Traverse(side.Reverse?current.Point:parent.Point,side.Reverse?parent.Point:current.Point)){
                        traverseRejected++;Rejected(current.Point);side.Pending=null;continue;
                    }
                    if(current.Previous!=null)acceptedEdges++;
                    side.Neighbor=-2;
                }
                if(side.Neighbor==-2){
                    side.Closed.Add(current.Key,current);(side.Reverse?reverseExplored:explored).Add(current.Point);
                    if(!side.Reverse&&(closest==null||Vector3.DistanceSquared(current.Point,goal)<Vector3.DistanceSquared(closest.Point,goal)))closest=current;
                    if(Expanded>96000){Error="Local navigation search limit reached.";return;}
                    side.Neighbor=-1;
                }
                if(side.Neighbor==-1){
                    if(TryJoin(side,current))return;
                    if(!side.Reverse){
                        Vector3? endpoint=null;
                        var arrived=Horizontal(current.Point,goal)<=range&&MathF.Abs(current.Point.Y-goal.Y)<=1.2f;
                        if(!arrived&&range<1&&Horizontal(current.Point,goal)<=2.6f&&collision.Ground(goal,out var floor)&&MathF.Abs(floor.Y-goal.Y)<=1.2f&&collision.Traverse(current.Point,floor))endpoint=floor;
                        if(arrived||endpoint.HasValue){var path=Trail(current);if(endpoint is {} end)path.Add(end);Path=path.ToArray();return;}
                    }
                    side.Neighbor=0;
                }
                ExpandEdge(side,current,Directions[side.Neighbor]);
                if(++side.Neighbor==Directions.Length){side.Pending=null;active=side.Reverse?forward:backward??forward;}
            }
        }catch(NavigationSliceExpiredException){BudgetYields++;}
        finally{collision.EndSlice();}
    }
    private bool TryJoin(Frontier side,Node current)
    {
        var other=side.Reverse?forward:backward;if(other==null)return false;
        for(var dy=-1;dy<=1;dy++){
            var key=current.Key with{Y=current.Key.Y+dy};
            if(!other.Closed.TryGetValue(key,out var match)||Vector3.Distance(current.Point,match.Point)>.5f)continue;
            var a=side.Reverse?match:current;var b=side.Reverse?current:match;
            if(Vector3.DistanceSquared(a.Point,b.Point)>.000001f&&!collision.Traverse(a.Point,b.Point))continue;
            var path=Trail(a);for(var n=b;n!=null;n=n.Previous)if(Vector3.DistanceSquared(path[^1],n.Point)>.000001f)path.Add(n.Point);
            Path=path.ToArray();return true;
        }
        return false;
    }
    private void ExpandEdge(Frontier side,Node parent,(int X,int Z) direction)
    {
        // Both searches use the same horizontal lattice; retain each real floor height.
        var cell=Address(parent.Point);
        var candidate=new Vector3(origin.X+(cell.X+direction.X)*spacing,parent.Point.Y,origin.Z+(cell.Z+direction.Z)*spacing);
        // Longer connectors reduce expansion on open roads; unit steps remain
        // available at every node for narrow gates and stairs. All are swept.
        if(Horizontal(candidate,parent.Point)>2.9f||Horizontal(candidate,origin)>2048||Horizontal(candidate,goal)>searchRadius)return;
        if(!collision.Ground(candidate,out var point)&&!collision.Ground(candidate+new Vector3(0,.45f,0),out point)&&!collision.Ground(candidate-new Vector3(0,.45f,0),out point)){
            groundRejected++;Rejected(candidate);return;
        }
        var key=Address(point);if(side.Closed.ContainsKey(key))return;
        var cost=parent.Cost+Vector3.Distance(parent.Point,point)*(1+3*collision.RoutePenalty(point));
        side.Open.Enqueue(new(key,point,cost,parent),cost+heuristicWeight*Heuristic(point,side.Reverse));
    }
    private Key Address(Vector3 p)=>new((int)MathF.Round((p.X-origin.X)/spacing),(int)MathF.Round(p.Y*4),(int)MathF.Round((p.Z-origin.Z)/spacing));
    internal static float RemainingDistance(Vector3 p,Vector3 goal,float range){
        var horizontal=MathF.Max(0,Horizontal(p,goal)-range);var vertical=MathF.Max(0,MathF.Abs(p.Y-goal.Y)-1.2f);
        return MathF.Sqrt(horizontal*horizontal+vertical*vertical);
    }
    private float Heuristic(Vector3 p,bool reverse){
        var direct=RemainingDistance(p,reverse?origin:goal,reverse?0:range);
        return MathF.Max(direct,roads?.Remaining(p,reverse)??0);
    }
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
    public bool MayWait(Vector3 current,uint area,ulong actor,DateTimeOffset now,double seconds=10)
    {
        if(since==null||area!=territory||actor!=character||Vector3.DistanceSquared(position,current)>.25f)
        {since=now;position=current;territory=area;character=actor;}
        return now-since.Value<TimeSpan.FromSeconds(seconds);
    }
}
