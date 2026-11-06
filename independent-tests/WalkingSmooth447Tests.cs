using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class WalkingSmooth447Tests
{
    [ModuleInitializer] internal static void Run()
    {
        if(MathF.Abs(WalkingSteering.TurnSpeed(.499f)-WalkingSteering.TurnSpeed(.501f))>.01f)throw new Exception("Small heading changes must not jump between slow and full movement.");
        var previous=1f;
        for(var angle=0f;angle<=MathF.PI;angle+=.01f){var speed=WalkingSteering.TurnSpeed(angle);if(speed<.34f||speed>previous+.001f)throw new Exception("Clear turns must slow continuously without alternating stop/start.");previous=speed;}
        if(WalkingSteering.TurnSpeed(0)!=1)throw new Exception("Straight route should retain full movement.");
        Console.WriteLine("Continuous clear-turn movement speed passed.");
    }
}
