using System.Numerics;
namespace EquinoxCompanion;
public sealed class FollowBoundaryMotion
{
    private Vector3 direction;
    private DateTimeOffset movedAt;
    public void Reset(){direction=default;movedAt=default;}
    public void Observe(Vector3 delta,DateTimeOffset now){
        delta.Y=0;
        if(!float.IsFinite(delta.X)||!float.IsFinite(delta.Z)||delta.LengthSquared()>=25){Reset();return;}
        if(delta.LengthSquared()>.0025f){direction=Vector3.Normalize(delta);movedAt=now;}
    }
    public bool TryDirection(DateTimeOffset now,out Vector3 result){
        result=direction;
        return movedAt!=default&&now>=movedAt&&now-movedAt<=TimeSpan.FromMilliseconds(1500)&&direction.LengthSquared()>.5f;
    }
}
