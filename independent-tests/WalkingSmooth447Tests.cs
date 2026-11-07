using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class WalkingSmooth447Tests
{
    [ModuleInitializer] internal static void Run()
    {
        foreach(var speed in new[]{0f,.5f,1f})
        for(var heading=-MathF.PI;heading<=MathF.PI;heading+=.01f){
            var axes=WalkingSteering.HeadingAxes(heading,2.8f,speed);
            if(MathF.Abs(axes.Length()-speed)>.0001f)throw new Exception("Heading changes must preserve requested speed, including entrance steps and game-gate pauses.");
        }
        var a=WalkingSteering.HeadingAxes(MathF.PI-.001f,0,1);
        var b=WalkingSteering.HeadingAxes(-MathF.PI+.001f,0,1);
        if(System.Numerics.Vector2.Distance(a,b)>.003f)throw new Exception("Heading wrap must not interrupt movement.");
        Console.WriteLine("470 constant speed across route headings and angle wrap passed.");
    }
}
