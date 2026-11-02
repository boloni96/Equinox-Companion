using System.Numerics;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using GameFramework=FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;
namespace EquinoxCompanion;

// All native queries run on the framework thread. No native pointers enter a worker.
internal sealed class GameWalkingCollision:IWalkingCollision
{
    private const float Radius=.32f,Height=1.85f;
    private static unsafe void Ready()
    {
        var f=GameFramework.Instance();var c=f==null?null:f->BGCollisionModule;
        if(c==null||c->ShuttingDown||c->LoadInProgressCounter>0)throw new InvalidOperationException("Waiting for collision geometry to finish loading.");
    }
    private static bool Hit(Vector3 origin,Vector3 direction,float distance,out RaycastHit hit)=>BGCollisionModule.RaycastMaterialFilter(origin,direction,out hit,distance);
    public bool Ground(Vector3 near,out Vector3 ground)
    {
        Ready();ground=default;
        if(!Hit(near+new Vector3(0,.7f,0),-Vector3.UnitY,1.5f,out var hit))return false;
        // Triangle cross product is available even when a collider omits its normal.
        var normal=hit.Normal;if(normal.LengthSquared()<.1f)normal=Vector3.Cross(hit.V2-hit.V1,hit.V3-hit.V1);
        if(normal.LengthSquared()<.0001f||MathF.Abs(Vector3.Normalize(normal).Y)<.65f)return false;
        if(hit.Point.Y-near.Y>.65f||near.Y-hit.Point.Y>.7f)return false;
        ground=hit.Point;
        if(Hit(ground+new Vector3(0,.12f,0),Vector3.UnitY,Height-.12f,out _))return false;
        return true;
    }
    public bool Traverse(Vector3 from,Vector3 to)
    {
        Ready();var horizontal=CollisionRouteSearch.Horizontal(from,to);
        if(horizontal>2.9f||MathF.Abs(from.Y-to.Y)>.65f)return false;
        var steps=Math.Max(1,(int)MathF.Ceiling(horizontal/.3f));var previous=from;
        var delta=to-from;var side=horizontal>.01f?new Vector3(-delta.Z,0,delta.X)/horizontal*Radius:Vector3.UnitX*Radius;
        for(var i=1;i<=steps;i++)
        {
            var point=Vector3.Lerp(from,to,(float)i/steps);point.Y=previous.Y;
            if(!Ground(point,out var floor)||MathF.Abs(floor.Y-previous.Y)>.45f)return false;
            // Keep both sides supported; don't squeeze along an edge or cut a corner.
            foreach(var offset in new[]{-side,Vector3.Zero,side})
            {
                if(!Ground(floor+offset,out var support)||MathF.Abs(support.Y-floor.Y)>.4f)return false;
                foreach(var height in new[]{.5f,1.05f,Height})
                {
                    var a=previous+offset+new Vector3(0,height,0);var b=floor+offset+new Vector3(0,height,0);
                    var direction=b-a;var length=direction.Length();
                    if(length>.001f&&Hit(a,direction/length,length,out _))return false;
                }
            }
            previous=floor;
        }
        return MathF.Abs(previous.Y-to.Y)<.25f;
    }
}
