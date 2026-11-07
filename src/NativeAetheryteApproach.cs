using System.Numerics;
namespace EquinoxCompanion;
public static class NativeAetheryteApproach
{
    public static float Range(int attempt,float hitbox)=>attempt<=0?8:attempt==1?7:6;
    public static Vector3 InteractionEdge(Vector3 player,Vector3 crystal,float range)
    {
        if(Vector3.Distance(player,crystal)<=range)return player;
        // Leave room for the walker's arrival tolerance without walking to the
        // pedestal. Account for the crystal's raised origin in the 3D range.
        var radius=MathF.Max(0,range-1);
        // A different platform is not a raised marker on our current floor.
        // Preserve its elevation as a ground-search hint instead of collapsing
        // the horizontal approach to the crystal center at the player's height.
        var elevation=MathF.Abs(crystal.Y-player.Y)>radius?crystal.Y:player.Y;
        var height=crystal.Y-elevation;
        var horizontal=MathF.Sqrt(MathF.Max(0,radius*radius-height*height));
        var offset=player-crystal;offset.Y=0;
        if(offset.LengthSquared()<.01f)return new(crystal.X,elevation,crystal.Z);
        var point=crystal+Vector3.Normalize(offset)*horizontal;point.Y=elevation;return point;
    }
    public static bool Ground(IWalkingCollision collision,Vector3 approach,Vector3 player,out Vector3 floor)
    {
        // Prefer the destination elevation when it differs. Checking only the
        // player's lower floor cannot discover an elevated residential shard.
        if(MathF.Abs(approach.Y-player.Y)<=.7f&&collision.Ground(new Vector3(approach.X,player.Y,approach.Z),out floor)&&Connected(collision,player,floor))return true;
        for(var drop=0f;drop<=4;drop+=.5f)
            if(collision.Ground(approach-new Vector3(0,drop,0),out floor)&&Connected(collision,player,floor))return true;
        floor=default;return false;
    }
    private static bool Connected(IWalkingCollision collision,Vector3 player,Vector3 floor)=>
        Vector3.DistanceSquared(player,floor)<.25f||EstateDoorApproach.HasIncomingConnection(collision,player,floor);
}
