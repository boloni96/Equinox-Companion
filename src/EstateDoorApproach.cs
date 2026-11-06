using System.Numerics;
namespace EquinoxCompanion;
public static class EstateDoorApproach
{
    // An interaction marker is not a walkable floor point (estate doors are raised).
    public static Vector3? Find(IWalkingCollision collision,Vector3 start,Vector3 door,Func<Vector3,bool> visible)
    {
        var candidates=new List<Vector3>();
        if(Vector3.Distance(start,door)<=2.8f&&collision.Ground(start,out var standing)&&
            collision.Traverse(standing,standing)&&visible(standing))return standing;
        var facing=MathF.Atan2(start.Z-door.Z,start.X-door.X);
        foreach(var radius in new[]{2.25f,3f,1.5f})
        for(var i=0;i<12;i++){
            var angle=facing+i*MathF.PI/6;
            var near=door+new Vector3(MathF.Cos(angle)*radius,0,MathF.Sin(angle)*radius);
            foreach(var height in new[]{0f,-.6f,-1.2f,-1.8f,-2.4f,.6f}){
                if(!collision.Ground(near+new Vector3(0,height,0),out var floor)||
                    Vector3.Distance(floor,door)>2.8f||!collision.Traverse(floor,floor)||!visible(floor))continue;
                candidates.Add(floor);break;
            }
        }
        return candidates.Count==0?null:candidates.OrderBy(p=>Vector3.DistanceSquared(start,p)).First();
    }
}
