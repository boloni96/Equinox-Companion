using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class NativeApproach442Tests
{
    private sealed class Floor(float? height):IWalkingCollision
    {
        public bool Ground(Vector3 p,out Vector3 floor){floor=new(p.X,height??0,p.Z);return height is {} y&&MathF.Abs(p.Y-y)<=.7f;}
        public bool Traverse(Vector3 a,Vector3 b)=>height!=null;
    }
    [ModuleInitializer] internal static void Run()
    {
        var raised=new Vector3(4,3.2f,0);
        if(!NativeAetheryteApproach.Ground(new Floor(0),raised,Vector3.Zero,out var p)||p.Y!=0)throw new Exception("Floating aetheryte origins must use a supported floor approach.");
        if(!NativeAetheryteApproach.Ground(new Floor(1.7f),raised,Vector3.Zero,out p)||p.Y!=1.7f)throw new Exception("Raised platforms need bounded floor probes when player height differs.");
        if(NativeAetheryteApproach.Ground(new Floor(null),raised,Vector3.Zero,out _))throw new Exception("Missing collision must not invent a walkable floor.");
        Console.WriteLine("Native aetheryte raised-origin and missing-floor regressions passed.");
    }
}
