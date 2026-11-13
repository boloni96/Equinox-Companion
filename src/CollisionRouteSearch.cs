using System.Diagnostics;
using System.Numerics;
namespace EquinoxCompanion;

public sealed class NavigationSliceExpiredException:Exception { }
public sealed record NavigationSearchPoint(float X,float Y,float Z)
{
    public static NavigationSearchPoint From(Vector3 p)=>new(p.X,p.Y,p.Z);
}
public sealed record NavigationRejectedConnection(NavigationSearchPoint From,NavigationSearchPoint To,string Reason);
public sealed record NavigationSearchDiagnostic(NavigationSearchPoint Origin,NavigationSearchPoint Goal,int Expanded,int Frontier,int BudgetYields,int GroundRejected,int TraverseRejected,int AcceptedEdges,long NativeQueries,long CacheHits,NavigationSearchPoint[] Explored,NavigationSearchPoint[] Rejected,NavigationSearchPoint[] ClosestPath,int ReverseExpanded,NavigationSearchPoint[] ReverseExplored,bool RoadGuided,int RoadSeedPoints,string? RoadSeedStop,NavigationRejectedConnection[] RejectedConnections,int RoadDetours,int CompletedRoadDetours,NavigationSearchPoint[] RoadObstacles,int DirectPoints,string? DirectStopReason);

public interface IWalkingCollision
{
    string? TraversalFailure=>null;
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
    private readonly Func<Vector3,bool>? arrival;
    private readonly Vector3 origin,goal;
    private readonly float range,spacing,heuristicWeight;
    private float searchRadius;
    private NavigationRoadRoute? roads;
    private readonly Frontier forward=new(false);
    private Frontier? backward,active;
    private Node? closest;
    private WalkingRoadSeed? roadSeed;
    private WalkingDirectApproach? directApproach;
    private int directPoints;
    private string? directStopReason;
    private Vector3[]? verifiedPrefix;
    private Vector3 reverseGoal;
    private Node? committedRoadEnd;
    private Task<NavigationRoadRoute?>? roadDetourTask;
    private bool rebasingRoadGuide;
    private readonly Func<Func<NavigationRoadRoute?>,Task<NavigationRoadRoute?>> roadWorker;
    private readonly List<Vector3> roadObstacles=[];
    private int roadDetours,priorExpanded;
    private readonly List<Vector3> reverseRoots=[];
    private bool seedInteractionReverse;
    private int interactionSeedIndex;
    public int CompletedRoadDetours { get; private set; }
    public int RoadDetours=>roadDetours;
    // A completed, collision-verified guide section can be walked while this
    // search continues from its endpoint. Ownership is transferred only once.
    public Vector3[]? TakeVerifiedPrefix(){
        if(verifiedPrefix==null&&!Finished&&directApproach==null&&roadSeed==null&&roadDetourTask==null)StreamGeometryPrefix();
        var result=verifiedPrefix;verifiedPrefix=null;return result;
    }
    private void StreamGeometryPrefix(){
        if(closest==null)return;
        var nodes=new List<Node>();for(var n=closest;n!=null;n=n.Previous)nodes.Add(n);nodes.Reverse();
        var start=committedRoadEnd?.Point??origin;
        var index=nodes.FindLastIndex(n=>Vector3.DistanceSquared(n.Point,start)<.000001f);
        if(index<0)return;
        var length=0f;
        for(var i=index+1;i<nodes.Count;i++){
            length+=Vector3.Distance(nodes[i-1].Point,nodes[i].Point);
            if(length>12)return;
            if(length<3||Horizontal(start,nodes[i].Point)<2||
                RemainingDistance(start,goal,range)-RemainingDistance(nodes[i].Point,goal,range)<2)continue;
            // Every edge in this closed-node ancestry has passed collision checks.
            // Commit the walked prefix so subsequent routes cannot switch back to
            // an unrelated branch at the original start.
            verifiedPrefix=nodes.Skip(index).Take(i-index+1).Select(n=>n.Point).ToArray();
            CommitPrefix(nodes[i]);return;
        }
    }
    private void CommitPrefix(Node end){
        reverseGoal=end.Point;committedRoadEnd=end with{Cost=0};closest=null;
        priorExpanded+=forward.Closed.Count+(backward?.Closed.Count??0);
        forward.Open.Clear();forward.Closed.Clear();forward.Pending=null;
        forward.Open.Enqueue(committedRoadEnd,0);testedJoins.Clear();
        if(backward!=null){
            backward.Open.Clear();backward.Closed.Clear();backward.Pending=null;
            foreach(var root in reverseRoots)backward.Open.Enqueue(new(Address(root),root,0,null),heuristicWeight*RemainingDistance(root,reverseGoal,0));
        }
        active=forward;
    }
    private int roadSeedPoints;
    private string? roadSeedStop;
    private readonly HashSet<(Key Forward,Key Backward)> testedJoins=[];
    private readonly List<Vector3> explored=[],reverseExplored=[];
    private readonly Queue<Vector3> rejected=[];
    private readonly Queue<NavigationRejectedConnection> rejectedConnections=[];
    private int groundRejected,traverseRejected,acceptedEdges;
    private static readonly (int X,int Z)[] Directions=[(4,0),(-4,0),(0,4),(0,-4),(4,4),(4,-4),(-4,4),(-4,-4),(2,0),(-2,0),(0,2),(0,-2),(2,2),(2,-2),(-2,2),(-2,-2)];
    public string Error { get; private set; }="";
    public Vector3[]? Path { get; private set; }
    public int Expanded=>priorExpanded+forward.Closed.Count+(backward?.Closed.Count??0);
    public int BudgetYields { get; private set; }
    public bool Finished=>Path!=null||Error.Length>0;
    public NavigationSearchDiagnostic DiagnosticSnapshot(){
        var path=Trail(closest);var stride=Math.Max(1,(int)Math.Ceiling(explored.Count/512d));
        var reverseStride=Math.Max(1,(int)Math.Ceiling(reverseExplored.Count/512d));
        return new(NavigationSearchPoint.From(origin),NavigationSearchPoint.From(goal),Expanded,forward.Open.Count+(backward?.Open.Count??0),BudgetYields,groundRejected,traverseRejected,acceptedEdges,collision.NativeQueries,collision.CacheHits,
            explored.Where((_,i)=>i%stride==0).Select(NavigationSearchPoint.From).ToArray(),rejected.Select(NavigationSearchPoint.From).ToArray(),path.Take(256).Select(NavigationSearchPoint.From).ToArray(),
            backward?.Closed.Count??0,reverseExplored.Where((_,i)=>i%reverseStride==0).Select(NavigationSearchPoint.From).ToArray(),roads!=null,roadSeedPoints,roadSeedStop,rejectedConnections.ToArray(),roadDetours,CompletedRoadDetours,roadObstacles.Select(NavigationSearchPoint.From).ToArray(),directApproach?.Points.Length??directPoints,directApproach?.Reason??directStopReason);
    }
    private static List<Vector3> Trail(Node? node){var p=new List<Vector3>();for(var n=node;n!=null;n=n.Previous)p.Add(n.Point);p.Reverse();return p;}
    private void Rejected(Vector3 point){rejected.Enqueue(point);if(rejected.Count>128)rejected.Dequeue();}
    public CollisionRouteSearch(IWalkingCollision collision,Vector3 start,Vector3 goal,float range,float spacing=1f,float heuristicWeight=4f,NavigationRoadRoute? roads=null,bool bidirectional=true,IReadOnlyList<Vector3>? streetGuide=null,Func<Func<NavigationRoadRoute?>,Task<NavigationRoadRoute?>>? mapDetourWorker=null,Func<Vector3,bool>? arrival=null)
    {
        this.arrival=arrival;
        roadWorker=mapDetourWorker??(work=>Task.Run(work));
        this.collision=collision;this.goal=goal;this.range=Math.Clamp(range,.25f,5f);this.spacing=spacing/2;this.heuristicWeight=heuristicWeight;this.roads=roads;
        searchRadius=MathF.Max(Horizontal(start,goal)+35,roads?.SearchRadius(goal)??0);
        if(!float.IsFinite(heuristicWeight)||heuristicWeight is <1 or >8)throw new ArgumentException("Invalid search priority");
        if(!Finite(start)||!Finite(goal)||!float.IsFinite(spacing)||spacing<.25f||spacing>2)throw new ArgumentException("Invalid navigation coordinates");
        if(Vector3.Distance(start,goal)>2048)throw new InvalidOperationException("Local route exceeds 2048 yalms; use travel first.");
        if(!collision.Ground(start,out origin)){Error="No walkable ground beneath the character.";return;}
        reverseGoal=origin;forward.Open.Enqueue(new(Address(origin),origin,0,null),0);
        // Start from the actual upper/lower destination floor too. Reverse edges
        // are validated in the direction the character will eventually walk.
        if(arrival==null&&bidirectional&&Vector3.Distance(origin,goal)>=12&&collision.Ground(goal,out var end)&&MathF.Abs(end.Y-goal.Y)<=1.2f){
            reverseRoots.Add(end);backward=new(true);backward.Open.Enqueue(new(Address(end),end,0,null),0);
        }
        if(arrival!=null){directApproach=new(collision,origin,goal,arrival);seedInteractionReverse=bidirectional;}
        if(arrival==null&&Vector3.Distance(origin,goal)>=12){
            if(streetGuide!=null)roadSeed=new(collision,origin,streetGuide);
            else if(roads!=null){
                if(roads.Points.Length>0&&Horizontal(origin,roads.Points[0])<=1)
                    roadSeed=new(collision,origin,roads.Points);
                else{
                    // A cached opening changes the search origin. Never walk
                    // back to the old map route's start to reuse its directions.
                    var mapRoute=roads;var from=origin;var to=goal;
                    rebasingRoadGuide=true;roadDetourTask=roadWorker(()=>mapRoute.Detour(from,to,[]));
                }
            }
        }
        active=forward;
    }
    public void Advance(double milliseconds=1.5,int edgeBudget=24,CancellationToken cancellation=default)
    {
        if(Finished)return;
        var clock=Stopwatch.StartNew();collision.BeginSlice(milliseconds);
        try{
            while(edgeBudget-->0&&clock.Elapsed.TotalMilliseconds<milliseconds){
                cancellation.ThrowIfCancellationRequested();
                if(directApproach is {} direct){
                    direct.Step();directPoints=direct.Points.Length;directStopReason=direct.Reason;
                    if(direct.Arrived){Path=direct.Points;directApproach=null;return;}
                    if(direct.TakePrefix() is {} section)
                        verifiedPrefix=verifiedPrefix==null?section:verifiedPrefix.Concat(section.Skip(1)).ToArray();
                    if(direct.Finished){
                        var points=direct.Points;Node? end=null;
                        foreach(var point in points)end=new(Address(point),point,0,end,true);
                        CommitPrefix(end!);directApproach=null;
                        if(direct.RejectedPoint is {} rejectedPoint){
                            rejectedConnections.Enqueue(new(NavigationSearchPoint.From(points[^1]),NavigationSearchPoint.From(rejectedPoint),direct.Reason??"Direct approach rejected"));
                            traverseRejected++;
                        }
                        // Near an interaction target, search locally around the
                        // actual obstruction instead of committing another map loop.
                        if(Horizontal(points[^1],goal)<=range+12){roads=null;roadSeed=null;}
                        else if(roads is {} map){
                            var from=points[^1];rebasingRoadGuide=true;
                            roadDetourTask=roadWorker(()=>map.Detour(from,goal,[]));
                        }
                    }
                    continue;
                }
                if(seedInteractionReverse){
                    SeedInteractionReverse();
                    continue;
                }
                if(roadDetourTask?.IsCompleted==true){
                    var task=roadDetourTask;roadDetourTask=null;
                    if(task.IsCompletedSuccessfully&&task.Result is {} detour){
                        if(!rebasingRoadGuide)CompletedRoadDetours++;roads=detour;searchRadius=MathF.Max(searchRadius,detour.SearchRadius(goal));roadSeed=new(collision,committedRoadEnd?.Point??origin,detour.Points);
                    }else if(task.IsFaulted){_ = task.Exception;}
                    rebasingRoadGuide=false;
                }
                if(roadSeed is {} seed){
                    seed.Step();roadSeedPoints=seed.Count;roadSeedStop=seed.StopReason;
                    if(seed.Finished){
                        var points=seed.Points;Node? parent=committedRoadEnd;var cost=0f;
                        foreach(var point in points){
                            if(parent!=null&&Vector3.DistanceSquared(parent.Point,point)<.000001f)continue;
                            if(parent!=null)cost+=Vector3.Distance(parent.Point,point);
                            parent=new(Address(point),point,cost,parent,true);
                        }
                        if(points.Length>=5&&parent!=null&&Horizontal(points[0],points[^1])>=2){
                            // Commit the checked prefix instead of leaving a long
                            // stair detour behind cheaper, unproductive origin nodes.
                            // Keep its chain for the complete returned path, but
                            // price the remaining search from the new walking end.
                            // Append if two sections finish inside the same frame.
                            verifiedPrefix=verifiedPrefix==null?points:verifiedPrefix.Concat(points.Skip(1)).ToArray();
                            CommitPrefix(parent);
                        }
                        else if(points.Length>2&&parent!=null)forward.Open.Enqueue(parent,cost+heuristicWeight*Heuristic(parent.Point,false));
                        roadSeed=null;
                        if(seed.BlockedPoint is {} block&&roads!=null&&roadDetours<4){
                            roadObstacles.Add(block);roadDetours++;
                            var guide=roads;var restart=committedRoadEnd?.Point??origin;var destination=goal;var obstacles=roadObstacles.ToArray();
                            // Only immutable managed map data goes to the worker.
                            // Native collision checks and movement remain on Update.
                            roadDetourTask=roadWorker(()=>guide.Detour(restart,destination,obstacles));
                        }
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
                        if(other==null||(other.Pending==null&&other.Open.Count==0)){
                            if(roadDetourTask!=null)return;
                            Error="No connected walkable route found.";return;
                        }
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
                        // Refine only a rejected unit-length edge. Open roads keep
                        // their coarse stride instead of quadrupling the search area.
                        var reason=collision.TraversalFailure??"Swept connection rejected";
                        RefineEdge(side,parent,current.Point);
                        rejectedConnections.Enqueue(new(NavigationSearchPoint.From(side.Reverse?current.Point:parent.Point),NavigationSearchPoint.From(side.Reverse?parent.Point:current.Point),reason));
                        if(rejectedConnections.Count>64)rejectedConnections.Dequeue();
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
                        var arrived=arrival!=null?arrival(current.Point):Horizontal(current.Point,goal)<=range&&MathF.Abs(current.Point.Y-goal.Y)<=1.2f;
                        if(arrival==null&&!arrived&&range<1&&Horizontal(current.Point,goal)<=2.6f&&collision.Ground(goal,out var floor)&&MathF.Abs(floor.Y-goal.Y)<=1.2f&&collision.Traverse(current.Point,floor))endpoint=floor;
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
    // Seed a second search from supported, visible interaction positions, never
    // from the garden/crystal collider itself. Run within the same frame budget;
    // an interrupted native query retries the same sample on the next slice.
    private void SeedInteractionReverse()
    {
        const int directions=8;
        if(interactionSeedIndex>=directions*2){seedInteractionReverse=false;return;}
        var angle=(interactionSeedIndex%directions)*MathF.PI/4;
        var point=goal+new Vector3(MathF.Cos(angle),0,MathF.Sin(angle))*(range*.9f);
        if(interactionSeedIndex>=directions)point.Y=origin.Y;
        if(collision.Ground(point,out var floor)&&arrival!(floor)&&
            !reverseRoots.Any(p=>Address(p)==Address(floor))){
            reverseRoots.Add(floor);backward??=new(true);
            backward.Open.Enqueue(new(Address(floor),floor,0,null),heuristicWeight*RemainingDistance(floor,reverseGoal,0));
        }
        interactionSeedIndex++;
    }
    private bool TryJoin(Frontier side,Node current)
    {
        var other=side.Reverse?forward:backward;if(other==null)return false;
        // Two valid frontiers need not land on an identical lattice point.
        // Nearby joins still use the complete directional swept-body check.
        var candidates=new List<Node>();
        var cells=(int)MathF.Ceiling(1.5f/spacing);
        for(var dx=-cells;dx<=cells;dx++)for(var dz=-cells;dz<=cells;dz++)for(var dy=-4;dy<=4;dy++){
            var key=current.Key with{X=current.Key.X+dx,Y=current.Key.Y+dy,Z=current.Key.Z+dz};
            if(other.Closed.TryGetValue(key,out var match)&&Horizontal(current.Point,match.Point)<=1.5f&&MathF.Abs(current.Point.Y-match.Point.Y)<=1f)candidates.Add(match);
        }
        foreach(var match in candidates.OrderBy(n=>Vector3.DistanceSquared(n.Point,current.Point)).Take(4)){
            var a=side.Reverse?match:current;var b=side.Reverse?current:match;
            if(testedJoins.Contains((a.Key,b.Key)))continue;
            var clear=Vector3.DistanceSquared(a.Point,b.Point)<=.000001f||collision.Traverse(a.Point,b.Point);
            // Never remember an interrupted check as a failed connection.
            testedJoins.Add((a.Key,b.Key));
            if(!clear)continue;
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
            if(Math.Abs(direction.X)<=2&&Math.Abs(direction.Z)<=2)RefineEdge(side,parent,candidate);
            groundRejected++;Rejected(candidate);return;
        }
        var key=Address(point);if(side.Closed.ContainsKey(key))return;
        var cost=parent.Cost+Vector3.Distance(parent.Point,point)*(1+3*collision.RoutePenalty(point));
        side.Open.Enqueue(new(key,point,cost,parent),cost+heuristicWeight*Heuristic(point,side.Reverse));
    }
    private void RefineEdge(Frontier side,Node parent,Vector3 destination)
    {
        var from=Address(parent.Point);var to=Address(destination);
        var dx=Math.Abs(from.X-to.X);var dz=Math.Abs(from.Z-to.Z);
        // Half of a one-yalm connector; never recursively subdivide below the
        // half-yalm lattice (quarter-yalm in existing close recovery searches).
        if(Math.Max(dx,dz)!=2)return;
        var candidate=(parent.Point+destination)/2;candidate.Y=parent.Point.Y;
        if(!collision.Ground(candidate,out var point)&&!collision.Ground(candidate+new Vector3(0,.45f,0),out point)&&!collision.Ground(candidate-new Vector3(0,.45f,0),out point))return;
        var key=Address(point);if(side.Closed.ContainsKey(key))return;
        // Prefer an available coarse route. A fixed fallback cost avoids filling
        // open floors with shifted half-grid lanes while retaining tight turns.
        var cost=parent.Cost+24+Vector3.Distance(parent.Point,point)*(1+3*collision.RoutePenalty(point));
        side.Open.Enqueue(new(key,point,cost,parent),cost+heuristicWeight*Heuristic(point,side.Reverse));
    }
    private Key Address(Vector3 p)=>new((int)MathF.Round((p.X-origin.X)/spacing),(int)MathF.Round(p.Y*4),(int)MathF.Round((p.Z-origin.Z)/spacing));
    internal static float RemainingDistance(Vector3 p,Vector3 goal,float range){
        var horizontal=MathF.Max(0,Horizontal(p,goal)-range);var vertical=MathF.Max(0,MathF.Abs(p.Y-goal.Y)-1.2f);
        return MathF.Sqrt(horizontal*horizontal+vertical*vertical);
    }
    private float Heuristic(Vector3 p,bool reverse){
        var direct=RemainingDistance(p,reverse?reverseGoal:goal,reverse?0:range);
        return reverse&&(reverseGoal!=origin||roads!=null&&(roads.Points.Length==0||Horizontal(roads.Points[0],reverseGoal)>1))?direct:MathF.Max(direct,roads?.Remaining(p,reverse)??0);
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
