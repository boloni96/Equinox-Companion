using System.Numerics;
namespace EquinoxCompanion;
internal static class WalkingGroundTraversal
{
    private const float Radius=.6f,Height=1.85f;
    private static bool Finite(Vector3 p)=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z);
    public static bool Check(IWalkingCollision collision,Vector3 from,Vector3 to,Func<Vector3,Vector3,bool> blocked,out Vector3? blockedAt,Func<Vector3,bool>? bodyRoom=null)
    {
        blockedAt=null;var horizontal=CollisionRouteSearch.Horizontal(from,to);
        if(horizontal>2.9f||MathF.Abs(from.Y-to.Y)>1.2f){blockedAt=to;return false;}
        // Actor coordinates and raycast support can differ on uneven estate ground.
        // Use the same grounded origin as route planning, without widening step limits.
        if(!collision.Ground(from,out var origin)||!Finite(origin)||!Finite(from)||!Finite(to)||
           CollisionRouteSearch.Horizontal(from,origin)>.01f||origin.Y-from.Y>.65f||from.Y-origin.Y>.7f)
        {blockedAt=from;return false;}
        from=origin;
        var steps=Math.Max(1,(int)MathF.Ceiling(horizontal/.3f));var previous=from;
        var delta=to-from;var side=horizontal>.01f?new Vector3(-delta.Z,0,delta.X)/horizontal*Radius:Vector3.UnitX*Radius;
        for(var i=1;i<=steps;i++)
        {
            var point=Vector3.Lerp(from,to,(float)i/steps);point.Y=previous.Y;
            if(!collision.Ground(point,out var floor)||MathF.Abs(floor.Y-previous.Y)>.45f){blockedAt=point;return false;}
            // Keep both sides supported; don't squeeze along an edge or cut a corner.
            foreach(var offset in (ReadOnlySpan<Vector3>)[-side,Vector3.Zero,side])
            {
                if(!collision.Ground(floor+offset,out var support)||MathF.Abs(support.Y-floor.Y)>.4f){blockedAt=floor+offset;return false;}
                foreach(var height in (ReadOnlySpan<float>)[.2f,.5f,1.05f,Height])
                {
                    // Test above the higher support: a legal stair riser is not a body obstacle.
                    var a=previous+offset;var b=floor+offset;
                    a.Y=b.Y=MathF.Max(previous.Y,floor.Y)+height;
                    var direction=b-a;var length=direction.Length();
                    if(length>.001f&&blocked(a,b)){blockedAt=floor;return false;}
                }
            }
            if(!(bodyRoom?.Invoke(floor)??WalkingClearance.BodyRoom(floor,Radius,blocked))){blockedAt=floor;return false;}
            previous=floor;
        }
        if(MathF.Abs(previous.Y-to.Y)<.25f)return true;
        blockedAt=to;return false;
    }
}

// A planning collision instance belongs to one route on one loaded territory.
// Cache only completed checks at exact coordinates; live steering stays uncached.
internal sealed class WalkingTraversalCache
{
    private readonly Dictionary<(Vector3 From,Vector3 To),(bool Clear,Vector3? Blocked)> edges=[];
    private readonly Dictionary<Vector3,bool> rooms=[];
    public bool Check(IWalkingCollision collision,Vector3 from,Vector3 to,Func<Vector3,Vector3,bool> blocked,out Vector3? blockedAt)
    {
        if(edges.TryGetValue((from,to),out var saved)){blockedAt=saved.Blocked;return saved.Clear;}
        bool Room(Vector3 floor){
            if(rooms.TryGetValue(floor,out var clear))return clear;
            clear=WalkingClearance.BodyRoom(floor,.6f,blocked);
            if(rooms.Count<100000)rooms[floor]=clear;
            return clear;
        }
        var result=WalkingGroundTraversal.Check(collision,from,to,blocked,out blockedAt,Room);
        // Exceptions (including slice expiry) never save an unfinished result.
        if(edges.Count<100000)edges[(from,to)]=(result,blockedAt);
        return result;
    }
}
