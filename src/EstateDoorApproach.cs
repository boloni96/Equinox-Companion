using System.Numerics;
namespace EquinoxCompanion;
public static class EstateDoorApproach
{
    // An interaction marker is not a walkable floor point (estate doors are raised).
    public static Vector3? Find(IWalkingCollision collision,Vector3 start,Vector3 door,Func<Vector3,bool> visible,float limit=3.2f)
    {
        var candidates=new List<Vector3>();
        if(Vector3.Distance(start,door)<=limit-.4f&&collision.Ground(start,out var standing)&&
            collision.Traverse(standing,standing)&&visible(standing))return standing;
        var facing=MathF.Atan2(start.Z-door.Z,start.X-door.X);
        foreach(var radius in new[]{MathF.Max(1,limit-.6f),2.25f,1.5f})
        for(var i=0;i<12;i++){
            var angle=facing+i*MathF.PI/6;
            var near=door+new Vector3(MathF.Cos(angle)*radius,0,MathF.Sin(angle)*radius);
            foreach(var height in new[]{0f,-.6f,-1.2f,-1.8f,-2.4f,.6f}){
                if(!collision.Ground(near+new Vector3(0,height,0),out var floor)||
                    Vector3.Distance(floor,door)>limit-.4f||!collision.Traverse(floor,floor)||!visible(floor))continue;
                candidates.Add(floor);break;
            }
        }
        foreach(var candidate in candidates.OrderBy(p=>Vector3.DistanceSquared(start,p)))
            if(HasIncomingConnection(collision,start,candidate))return candidate;
        return null;
    }
    // A standing point alone can be a disconnected ledge or decoration. Verify
    // an incoming edge on the same lattice used by the route search.
    public static bool HasIncomingConnection(IWalkingCollision collision,Vector3 start,Vector3 point)
    {
        var x=MathF.Round(point.X-start.X);var z=MathF.Round(point.Z-start.Z);
        for(var dz=-1;dz<=1;dz++)for(var dx=-1;dx<=1;dx++){
            if(dx==0&&dz==0)continue;
            var near=new Vector3(start.X+x+dx,point.Y,start.Z+z+dz);
            if(!collision.Ground(near,out var floor)&&!collision.Ground(near+new Vector3(0,.45f,0),out floor)&&!collision.Ground(near-new Vector3(0,.45f,0),out floor))continue;
            if(collision.Traverse(floor,point))return true;
        }
        return false;
    }
}
