using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class InteractionDistance454Tests
{
    [ModuleInitializer] internal static void Run()
    {
        foreach(var distance in new[]{4f,8f,10f,13f,30f})
            if(InteractionApproachPolicy.WithinDistance(Vector3.Zero,new(distance,0,0),false))throw new Exception("Distant door/NPC must not stop approach or authorize interaction.");
        if(!InteractionApproachPolicy.WithinDistance(Vector3.Zero,new(2.5f,0,0),false))throw new Exception("Nearby door may stop.");
        if(InteractionApproachPolicy.WithinDistance(Vector3.Zero,new(1,4,0),false))throw new Exception("Vertical distance must count.");
        if(InteractionApproachPolicy.WithinDistance(Vector3.Zero,new(float.NaN,0,0),false))throw new Exception("Invalid positions must be rejected.");
        if(InteractionApproachPolicy.WithinDistance(Vector3.Zero,new(10,0,0),true))throw new Exception("Distant crystal must not signal early arrival.");
        for(var i=0;i<3;i++)if(NativeAetheryteApproach.Range(i,100)>8)throw new Exception("Large hitboxes cannot authorize distant interaction.");
        Console.WriteLine("Distant door, NPC, crystal and invalid-position interaction guards passed.");
    }
}
