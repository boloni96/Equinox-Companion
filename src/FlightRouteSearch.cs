using System.Diagnostics;
using System.Numerics;
namespace EquinoxCompanion;
internal interface IFlightCollision { bool Clear(Vector3 from,Vector3 to); }
internal sealed class FlightRouteSearch
{
    private readonly record struct Cell(int X,int Y,int Z);
    private sealed record Node(Cell Cell,Vector3 Point,float Cost,Node? Previous);
    private readonly IFlightCollision collision;private readonly Vector3 start,goal;
    private readonly PriorityQueue<Node,float> open=new();private readonly Dictionary<Cell,float> costs=[];private readonly HashSet<Cell> closed=[];
    private static readonly Vector3[] directions=(from x in new[]{-1,0,1} from y in new[]{-1,0,1} from z in new[]{-1,0,1} where x!=0||y!=0||z!=0 select new Vector3(x,y,z)*4).ToArray();
    private Node? expanding;private int neighbor;
    public Vector3[]? Path {get;private set;}
    public string Error {get;private set;}="";
    public int Expanded=>closed.Count;
    public FlightRouteSearch(IFlightCollision collision,Vector3 start,Vector3 goal){
        this.collision=collision;this.start=start;this.goal=goal;
        if(!Finite(start)||!Finite(goal)||Vector3.Distance(start,goal)>2048)throw new InvalidOperationException("Flight destination is invalid or exceeds 2048 yalms.");
        var n=new Node(new(0,0,0),start,0,null);open.Enqueue(n,Vector3.Distance(start,goal));costs[n.Cell]=0;
    }
    private static bool Finite(Vector3 p)=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z);
    public void Advance(double milliseconds=4,int budget=64){
        if(Path!=null||Error.Length>0)return;var clock=Stopwatch.StartNew();
        while(budget-->0&&clock.Elapsed.TotalMilliseconds<milliseconds){
            if(expanding==null){
                Node? n=null;while(open.TryDequeue(out var candidate,out _))if(!closed.Contains(candidate.Cell)&&candidate.Cost<=costs[candidate.Cell]){n=candidate;break;}
                if(n==null){Error="No clear flight corridor found.";return;}
                closed.Add(n.Cell);if(closed.Count>48000){Error="Flight search limit reached.";return;}
                if(Vector3.Distance(n.Point,goal)<=7&&collision.Clear(n.Point,goal)){
                    var path=new List<Vector3>{goal};for(var p=n;p!=null;p=p.Previous)path.Add(p.Point);path.Reverse();Path=path.ToArray();return;
                }
                expanding=n;neighbor=0;
            }
            var parent=expanding;var offset=directions[neighbor++];if(neighbor==directions.Length)expanding=null;
            var point=parent.Point+offset;
            if(point.Y<MathF.Min(start.Y,goal.Y)-32||point.Y>MathF.Max(start.Y,goal.Y)+128||Vector3.Distance(point,start)>2048||Vector3.Distance(point,goal)>Vector3.Distance(start,goal)+128)continue;
            var d=(point-start)/4;var cell=new Cell((int)MathF.Round(d.X),(int)MathF.Round(d.Y),(int)MathF.Round(d.Z));
            if(closed.Contains(cell))continue;var cost=parent.Cost+offset.Length();
            if(costs.TryGetValue(cell,out var old)&&old<=cost||!collision.Clear(parent.Point,point))continue;
            costs[cell]=cost;open.Enqueue(new(cell,point,cost,parent),cost+Vector3.Distance(point,goal));
        }
    }
}
internal static class FlightNavigationPolicy
{
    public static bool Prefer(bool allowed,bool enabled,bool requested,float distance)=>allowed&&enabled&&(requested||distance>=80);
    public static bool Arrived(Vector3 position,Vector3 goal,float range)=>CollisionRouteSearch.Horizontal(position,goal)<=MathF.Max(.5f,range)&&MathF.Abs(position.Y-goal.Y)<=1;
    public static bool CanSupply(float forward,float left,float up,float turn)=>float.IsFinite(forward)&&float.IsFinite(left)&&float.IsFinite(up)&&float.IsFinite(turn)&&forward==0&&left==0&&up==0&&turn==0;
}
