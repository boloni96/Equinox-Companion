using System.Numerics;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using GameFramework=FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;
namespace EquinoxCompanion;

// All native queries run on the framework thread. No native pointers enter a worker.
internal sealed class GameWalkingCollision:IWalkingCollision
{
    private readonly Dictionary<Vector3,(bool Found,Vector3 Ground)>? groundCache;
    internal GameWalkingCollision(bool cacheGround=false){if(cacheGround)groundCache=new();}
    private const float Radius=.45f,Height=1.85f;
    private readonly Dictionary<Vector3,float> wallCache=[];
    public float WallPenalty(Vector3 point){
        if(groundCache!=null&&wallCache.TryGetValue(point,out var cached))return cached;
        Ready();const float preferred=3.5f;var nearest=preferred;
        for(var i=0;i<8;i++){
            var angle=i*MathF.PI/4;var direction=new Vector3(MathF.Cos(angle),0,MathF.Sin(angle));
            if(Hit(point+new Vector3(0,.9f,0),direction,preferred,out var hit))nearest=MathF.Min(nearest,CollisionRouteSearch.Horizontal(point,hit.Point));
        }
        var penalty=Math.Clamp((preferred-nearest)/(preferred-Radius),0,1);
        if(groundCache!=null&&wallCache.Count<100000)wallCache[point]=penalty;
        return penalty;
    }
    public bool ComfortableTraverse(Vector3 from,Vector3 to)=>Traverse(from,to)&&WalkingClearance.PreservesRoom(from,to,WallPenalty);
    internal Vector3? LastBlockedAt { get; private set; }
    private bool Reject(Vector3 point){LastBlockedAt=point;return false;}
    private static bool Blocked(Vector3 a,Vector3 b){var delta=b-a;var length=delta.Length();return length>.001f&&Hit(a,delta/length,length,out _);}
    internal static unsafe bool Available(){
        var f=GameFramework.Instance();var c=f==null?null:f->BGCollisionModule;
        return c!=null&&!c->ShuttingDown&&c->LoadInProgressCounter==0;
    }
    private static unsafe void Ready()
    {
        var f=GameFramework.Instance();var c=f==null?null:f->BGCollisionModule;
        if(c==null||c->ShuttingDown||c->LoadInProgressCounter>0)throw new InvalidOperationException("Waiting for collision geometry to finish loading.");
    }
    private static bool Hit(Vector3 origin,Vector3 direction,float distance,out RaycastHit hit)=>BGCollisionModule.RaycastMaterialFilter(origin,direction,out hit,distance);
    public bool Ground(Vector3 near,out Vector3 ground)
    {
        if(groundCache!=null&&groundCache.TryGetValue(near,out var cached)){ground=cached.Ground;return cached.Found;}
        var found=ReadGround(near,out ground);
        if(groundCache!=null&&groundCache.Count<100000)groundCache[near]=(found,ground);
        return found;
    }
    private bool ReadGround(Vector3 near,out Vector3 ground)
    {
        Ready();ground=default;
        if(!Hit(near+new Vector3(0,.7f,0),-Vector3.UnitY,1.5f,out var hit))return false;
        // Triangle cross product is available even when a collider omits its normal.
        var normal=hit.Normal;if(normal.LengthSquared()<.1f)normal=Vector3.Cross(hit.V2-hit.V1,hit.V3-hit.V1);
        if(normal.LengthSquared()<.0001f||Vector3.Normalize(normal).Y<.65f)return false;
        if(hit.Point.Y-near.Y>.65f||near.Y-hit.Point.Y>.7f)return false;
        ground=hit.Point;
        if(Hit(ground+new Vector3(0,.12f,0),Vector3.UnitY,Height-.12f,out _))return false;
        return true;
    }
    public bool Traverse(Vector3 from,Vector3 to)
    {
        Ready();LastBlockedAt=null;var horizontal=CollisionRouteSearch.Horizontal(from,to);
        if(horizontal>2.9f||MathF.Abs(from.Y-to.Y)>.65f)return Reject(to);
        var steps=Math.Max(1,(int)MathF.Ceiling(horizontal/.3f));var previous=from;
        var delta=to-from;var side=horizontal>.01f?new Vector3(-delta.Z,0,delta.X)/horizontal*Radius:Vector3.UnitX*Radius;
        for(var i=1;i<=steps;i++)
        {
            var point=Vector3.Lerp(from,to,(float)i/steps);point.Y=previous.Y;
            if(!Ground(point,out var floor)||MathF.Abs(floor.Y-previous.Y)>.45f)return Reject(point);
            // Keep both sides supported; don't squeeze along an edge or cut a corner.
            foreach(var offset in (ReadOnlySpan<Vector3>)[-side,Vector3.Zero,side])
            {
                if(!Ground(floor+offset,out var support)||MathF.Abs(support.Y-floor.Y)>.4f)return Reject(floor+offset);
                foreach(var height in (ReadOnlySpan<float>)[.2f,.5f,1.05f,Height])
                {
                    // Test above the higher support: a legal stair riser is not a body obstacle.
                    var a=previous+offset;var b=floor+offset;
                    a.Y=b.Y=MathF.Max(previous.Y,floor.Y)+height;
                    var direction=b-a;var length=direction.Length();
                    if(length>.001f&&Hit(a,direction/length,length,out _))return Reject(floor);
                }
            }
            if(!WalkingClearance.BodyRoom(floor,Radius,Blocked))return Reject(floor);
            previous=floor;
        }
        return MathF.Abs(previous.Y-to.Y)<.25f||Reject(to);
    }
}
