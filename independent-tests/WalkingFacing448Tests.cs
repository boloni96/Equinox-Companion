using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class WalkingFacing448Tests
{
    [ModuleInitializer] internal static void Run()
    {
        // Standard controls: changing facing must not stall or steer off the
        // already checked path. Reconstruct world velocity from local axes.
        foreach(var desired in new[]{.24f,.26f,MathF.PI/2,-MathF.PI/2,MathF.PI}){
            var facing=0f;
            for(var n=0;n<180;n++){
                var next=WalkingSteering.SmoothHeading(facing,desired,1f/60);
                if(MathF.Abs(MathF.IEEERemainder(next-facing,2*MathF.PI))>.041f)throw new Exception("Facing turn must remain bounded.");
                facing=next;
                var axes=WalkingSteering.HeadingAxes(desired,facing,1);
                var world=new Vector2(axes.X*MathF.Cos(facing)+axes.Y*MathF.Sin(facing),-axes.X*MathF.Sin(facing)+axes.Y*MathF.Cos(facing));
                if(MathF.Abs(axes.Length()-1)>.0001f||Vector2.Distance(world,new(MathF.Sin(desired),MathF.Cos(desired)))>.0001f)
                    throw new Exception("Turning must retain full speed exactly along the checked world direction.");
            }
        }
        Console.WriteLine("470 continuous Standard movement during smooth facing passed.");
    }
}
