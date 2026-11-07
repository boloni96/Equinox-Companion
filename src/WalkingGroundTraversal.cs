using System.Numerics;
namespace EquinoxCompanion;
internal static class WalkingGroundTraversal
{
    private const float Radius=.6f,Height=1.85f;
    private static bool Finite(Vector3 p)=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z);
    public static bool Check(IWalkingCollision collision,Vector3 from,Vector3 to,Func<Vector3,Vector3,bool> blocked,out Vector3? blockedAt,Func<Vector3,bool>? bodyRoom=null,Action<string>? rejected=null)
    {
        blockedAt=null;var horizontal=CollisionRouteSearch.Horizontal(from,to);
        if(horizontal>2.9f||MathF.Abs(from.Y-to.Y)>1.2f){rejected?.Invoke("Connector exceeds length or elevation limit");blockedAt=to;return false;}
        // Actor coordinates and raycast support can differ on uneven estate ground.
        // Use the same grounded origin as route planning, without widening step limits.
        if(!collision.Ground(from,out var origin)||!Finite(origin)||!Finite(from)||!Finite(to)||
           CollisionRouteSearch.Horizontal(from,origin)>.01f||origin.Y-from.Y>.65f||from.Y-origin.Y>.7f)
        {rejected?.Invoke("Origin support unavailable or mismatched");blockedAt=from;return false;}
        from=origin;
        var steps=Math.Max(1,(int)MathF.Ceiling(horizontal/.3f));var previous=from;
        var delta=to-from;var side=horizontal>.01f?new Vector3(-delta.Z,0,delta.X)/horizontal*Radius:Vector3.UnitX*Radius;
        for(var i=1;i<=steps;i++)
        {
            var point=Vector3.Lerp(from,to,(float)i/steps);point.Y=previous.Y;
            if(!collision.Ground(point,out var floor)||MathF.Abs(floor.Y-previous.Y)>.45f){rejected?.Invoke("Center support missing or step too high");blockedAt=point;return false;}
            // Keep both sides supported; don't squeeze along an edge or cut a corner.
            foreach(var offset in (ReadOnlySpan<Vector3>)[-side,Vector3.Zero,side])
            {
                if(!SupportedOffset(collision,floor,offset,out var support)){rejected?.Invoke("Lateral support missing or step too high");blockedAt=floor+offset;return false;}
                var previousSupport=previous+offset;
                if(SupportedOffset(collision,previous,offset,out var oldSupport))previousSupport=oldSupport;
                foreach(var height in (ReadOnlySpan<float>)[.2f,.5f,1.05f,Height])
                {
                    // Test above the higher support: a legal stair riser is not a body obstacle.
                    var a=previous+offset;var b=floor+offset;
                    a.Y=b.Y=MathF.Max(MathF.Max(previous.Y,floor.Y),MathF.Max(previousSupport.Y,support.Y))+height;
                    var direction=b-a;var length=direction.Length();
                    if(length>.001f&&blocked(a,b)){rejected?.Invoke("Longitudinal body ray blocked");blockedAt=floor;return false;}
                }
            }
            if(!(bodyRoom?.Invoke(floor)??TerrainBodyRoom(collision,floor,blocked))){rejected?.Invoke("Cross-body clearance or footprint support blocked");blockedAt=floor;return false;}
            previous=floor;
        }
        if(MathF.Abs(previous.Y-to.Y)<.25f)return true;
        rejected?.Invoke("Destination support height mismatch");blockedAt=to;return false;
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
    public string? Failure { get; private set; }
    public bool Check(IWalkingCollision collision,Vector3 from,Vector3 to,Func<Vector3,Vector3,bool> blocked,out Vector3? blockedAt)
    {
        if(edges.TryGetValue((from,to),out var saved)){blockedAt=saved.Blocked;Failure=saved.Failure;return saved.Clear;}
        bool Room(Vector3 floor){
            if(rooms.TryGetValue(floor,out var clear))return clear;
            clear=WalkingGroundTraversal.TerrainBodyRoom(collision,floor,blocked);
            if(rooms.Count<100000)rooms[floor]=clear;
            return clear;
        }
        Failure=null;
        var result=WalkingGroundTraversal.Check(collision,from,to,blocked,out blockedAt,Room,reason=>Failure=reason);
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
