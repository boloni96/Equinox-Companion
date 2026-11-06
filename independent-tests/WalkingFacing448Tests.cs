using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class WalkingFacing448Tests
{
    [ModuleInitializer] internal static void Run()
    {
        if(WalkingSteering.ForwardAlignment(MathF.PI/2)!=0||WalkingSteering.ForwardAlignment(MathF.PI)!=0)throw new Exception("Standard navigation must turn before moving sideways or backward.");
        if(WalkingSteering.ForwardAlignment(0)!=1||WalkingSteering.ForwardAlignment(.1f)<=0||MathF.Abs(WalkingSteering.ForwardAlignment(.1f)-WalkingSteering.ForwardAlignment(-.1f))>.001f)throw new Exception("Forward speed must ramp symmetrically as facing aligns.");
        var facing=0f;var desired=MathF.PI/2;
        for(var n=0;n<180;n++){var next=WalkingSteering.SmoothHeading(facing,desired,1f/60);if(next<facing||next-facing>.041f)throw new Exception("Facing must turn continuously toward the route.");facing=next;}
        if(WalkingSteering.ForwardAlignment(desired-facing)<.99f)throw new Exception("Turning must converge to full forward movement.");
        Console.WriteLine("Forward-facing standard movement and continuous alignment passed.");
    }
}
