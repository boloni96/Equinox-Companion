using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class HousingElevation476Tests
{
    [ModuleInitializer] internal static void Run(){
        // Coordinates from the stage-7 wait in the 022044 live export.
        var player=new Vector3(17.585922f,5.9999895f,130.11188f);
        var shard=new Vector3(-4.5625f,9.994568f,96.94055f);
        var edge=NativeAetheryteApproach.InteractionEdge(player,shard,3.2f);
        if(!NativeAetheryteApproach.Ground(new Platform(10),edge,player,out var floor)||MathF.Abs(floor.Y-10)>.01f)
            throw new Exception("Higher shard platform must be found from the reported lower-floor position.");
        if(CollisionRouteSearch.Horizontal(floor,shard)<2||Vector3.Distance(floor,shard)>3.2f)
            throw new Exception("Approach should retain interaction stand-off, not collapse onto the shard center.");
        // A floor below the destination must not win merely because it matches
        // the starting character's height.
        if(!NativeAetheryteApproach.Ground(new Platform(10,true),edge,player,out floor)||floor.Y!=10)
            throw new Exception("Destination platform must be preferred over a lower overlapping floor.");
        var below=NativeAetheryteApproach.InteractionEdge(player with{Y=14},shard,3.2f);
        if(!NativeAetheryteApproach.Ground(new Platform(10),below,player with{Y=14},out floor)||floor.Y!=10)
            throw new Exception("Descending toward a shard must also preserve its destination height.");
        if(NativeAetheryteApproach.Ground(new Platform(null),edge,player,out _))
            throw new Exception("Height hints cannot invent support when geometry is missing.");
        Console.WriteLine("476 reported housing stage-7 elevation, lower-floor isolation and missing support tests passed.");
    }
    private sealed class Platform(float? height,bool lower=false):IWalkingCollision {
        public bool Ground(Vector3 p,out Vector3 q){
            var y=lower&&MathF.Abs(p.Y-6)<.7f?6:height;
            q=p with{Y=y??0};return y.HasValue&&MathF.Abs(p.Y-y.Value)<.7f;
        }
        public bool Traverse(Vector3 a,Vector3 b)=>height.HasValue&&MathF.Abs(a.Y-b.Y)<.01f;
    }
}
