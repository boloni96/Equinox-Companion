using System.Numerics;
namespace EquinoxCompanion;
internal static class WalkingGroundTraversal
{
    private const float Radius=.6f,Height=1.85f;
    private static bool Finite(Vector3 p)=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z);
    public static bool Check(IWalkingCollision collision,Vector3 from,Vector3 to,Func<Vector3,Vector3,bool> blocked,out Vector3? blockedAt,Func<Vector3,bool>? bodyRoom=null,Action<string>? rejected=null)
    {
        var sweep=new Sweep(collision,from,to,blocked,bodyRoom);
        var result=sweep.Run();blockedAt=sweep.BlockedAt;
        if(sweep.Failure is {} failure)rejected?.Invoke(failure);
        return result;
    }
    // Retain completed short sections across frame-budget interruptions. Native
    // ray caches resume the unfinished section; completed sections need no replay.
    internal sealed class Sweep(IWalkingCollision collision,Vector3 from,Vector3 to,Func<Vector3,Vector3,bool> blocked,Func<Vector3,bool>? bodyRoom=null)
    {
        private bool initialized;
        private Vector3 previous,side;
        private int steps,next=1,room;
        private readonly List<Vector3> floors=[];
        internal Vector3? BlockedAt { get; private set; }
        internal string? Failure { get; private set; }
        private bool Reject(string reason,Vector3 point){Failure=reason;BlockedAt=point;return false;}
        internal bool Run()
        {
            if(!initialized){
                var horizontal=CollisionRouteSearch.Horizontal(from,to);
                if(horizontal>2.9f||MathF.Abs(from.Y-to.Y)>1.2f)return Reject("Connector exceeds length or elevation limit",to);
                if(!collision.Ground(from,out var origin)||!Finite(origin)||!Finite(from)||!Finite(to)||
                   CollisionRouteSearch.Horizontal(from,origin)>.01f||origin.Y-from.Y>.65f||from.Y-origin.Y>.7f)
                    return Reject("Origin support unavailable or mismatched",from);
                from=origin;previous=from;
                steps=Math.Max(1,(int)MathF.Ceiling(horizontal/.3f));
                var delta=to-from;
                side=horizontal>.01f?new Vector3(-delta.Z,0,delta.X)/horizontal*Radius:Vector3.UnitX*Radius;
                initialized=true;
            }
            // Reject a wall anywhere along this short edge before paying for all
            // cross-footprint probes. Successful edges still pass every check.
            for(;next<=steps;next++){
                var point=Vector3.Lerp(from,to,(float)next/steps);point.Y=previous.Y;
                if(!collision.Ground(point,out var floor)||MathF.Abs(floor.Y-previous.Y)>.45f)
                    return Reject("Center support missing or step too high",point);
                foreach(var offset in (ReadOnlySpan<Vector3>)[-side,Vector3.Zero,side]){
                    if(!SupportedOffset(collision,floor,offset,out var support))
                        return Reject("Lateral support missing or step too high",floor+offset);
                    var previousSupport=previous+offset;
                    if(SupportedOffset(collision,previous,offset,out var oldSupport))previousSupport=oldSupport;
                    foreach(var height in (ReadOnlySpan<float>)[.2f,.5f,1.05f,Height]){
                        var a=previous+offset;var b=floor+offset;
                        a.Y=b.Y=MathF.Max(MathF.Max(previous.Y,floor.Y),MathF.Max(previousSupport.Y,support.Y))+height;
                        if(Vector3.DistanceSquared(a,b)>.000001f&&blocked(a,b))
                            return Reject("Longitudinal body ray blocked",floor);
                    }
                }
                floors.Add(floor);previous=floor;
            }
            if(!(MathF.Abs(previous.Y-to.Y)<.25f))return Reject("Destination support height mismatch",to);
            for(;room<floors.Count;room++){
                var floor=floors[room];
                if(!(bodyRoom?.Invoke(floor)??TerrainBodyRoom(collision,floor,blocked)))
                    return Reject("Cross-body clearance or footprint support blocked",floor);
            }
            return true;
        }
    }
    private static bool SupportedOffset(IWalkingCollision collision,Vector3 center,Vector3 offset,out Vector3 support)
    {
        support=center;
        var steps=(int)MathF.Ceiling(offset.Length()/.3f);
        for(var i=1;i<=steps;i++){
            var probe=center+offset*((float)i/steps);probe.Y=support.Y;
            if(!collision.Ground(probe,out var floor)||MathF.Abs(floor.Y-support.Y)>.45f)return false;
            support=floor;
        }
        return true;
    }
    internal static bool TerrainBodyRoom(IWalkingCollision collision,Vector3 floor,Func<Vector3,Vector3,bool> blocked)=>
        WalkingClearance.BodyRoom(floor,Radius,blocked,p=>SupportedOffset(collision,floor,p-floor,out var support)?support:null);

}

// A planning collision instance belongs to one route on one loaded territory.
// Cache only completed checks at exact coordinates; live steering stays uncached.
internal sealed class WalkingTraversalCache
{
    private readonly Dictionary<(Vector3 From,Vector3 To),(bool Clear,Vector3? Blocked,string? Failure)> edges=[];
    private readonly Dictionary<Vector3,bool> rooms=[];
    private readonly Dictionary<(Vector3 From,Vector3 To),WalkingGroundTraversal.Sweep> pending=[];
    public string? Failure { get; private set; }
    public bool Check(IWalkingCollision collision,Vector3 from,Vector3 to,Func<Vector3,Vector3,bool> blocked,out Vector3? blockedAt,Func<string,string>? describeFailure=null)
    {
        if(edges.TryGetValue((from,to),out var saved)){blockedAt=saved.Blocked;Failure=saved.Failure;return saved.Clear;}
        bool Room(Vector3 floor){
            if(rooms.TryGetValue(floor,out var clear))return clear;
            clear=WalkingGroundTraversal.TerrainBodyRoom(collision,floor,blocked);
            if(rooms.Count<100000)rooms[floor]=clear;
            return clear;
        }
        Failure=null;
        if(!pending.TryGetValue((from,to),out var sweep)){
            sweep=new(collision,from,to,blocked,Room);pending[(from,to)]=sweep;
        }
        var result=sweep.Run();blockedAt=sweep.BlockedAt;Failure=sweep.Failure is {} reason?(describeFailure?.Invoke(reason)??reason):null;
        pending.Remove((from,to));
        // Exceptions (including slice expiry) never save an unfinished result.
        if(edges.Count<100000)edges[(from,to)]=(result,blockedAt,Failure);
        return result;
    }
}

// Ground support and movement obstacles are different material categories.
// Live Goblet corner probes found material 0x2004: invisible to support-only 0x4000.
internal static class WalkingCollisionFilter
{
    public const int Support=0x4000;
    public const int Movement=0x6000;
    public static bool Matches(ulong material,int mask)=>(material&(ulong)mask)!=0;
}

// Triangle normals encode surface area as well as direction. Small paving
// triangles must obey the same slope limit as larger triangles.
internal static class WalkingSupportNormal
{
    internal static bool Walkable(Vector3 normal)
    {
        if(!float.IsFinite(normal.X)||!float.IsFinite(normal.Y)||!float.IsFinite(normal.Z))return false;
        var scale=MathF.Max(MathF.Abs(normal.X),MathF.Max(MathF.Abs(normal.Y),MathF.Abs(normal.Z)));
        if(scale==0)return false;
        // Scale first to avoid underflow/overflow when normalizing tiny/large triangles.
        var direction=normal/scale;
        return Vector3.Normalize(direction).Y>=.65f;
    }
}
