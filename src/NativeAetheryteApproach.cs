using System.Numerics;
namespace EquinoxCompanion;
public static class NativeAetheryteApproach
{
    public static bool Ground(IWalkingCollision collision,Vector3 approach,Vector3 player,out Vector3 floor)
    {
        if(collision.Ground(new Vector3(approach.X,player.Y,approach.Z),out floor))return true;
        for(var drop=0f;drop<=4;drop+=.5f)
            if(collision.Ground(approach-new Vector3(0,drop,0),out floor))return true;
        floor=default;return false;
    }
}
