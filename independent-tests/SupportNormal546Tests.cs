using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class SupportNormal546Tests
{
    [ModuleInitializer] internal static void Run()
    {
        // Captured at the estate entrance in report 20261111-072346.
        var paving=new Vector3(-.005030011f,.0066459104f,.0039250418f);
        foreach(var scale in new[]{1f,.00001f,100000f}){
            if(!WalkingSupportNormal.Walkable(paving*scale))throw new Exception("Walkable estate paving rejected because of triangle size");
            if(!WalkingSupportNormal.Walkable(Vector3.UnitY*scale))throw new Exception("Flat support rejected");
            foreach(var blocked in new[]{Vector3.UnitX,-Vector3.UnitY,new Vector3(1,.5f,0)})
                if(WalkingSupportNormal.Walkable(blocked*scale))throw new Exception("Wall, underside or steep slope accepted");
        }
        foreach(var invalid in new[]{Vector3.Zero,new Vector3(float.NaN,1,0),new Vector3(0,float.PositiveInfinity,0)})
            if(WalkingSupportNormal.Walkable(invalid))throw new Exception("Invalid support normal accepted");
        if(!WalkingSupportNormal.Walkable(new(1,.9f,0))||WalkingSupportNormal.Walkable(new(1,.8f,0)))throw new Exception("Existing slope limit changed");
        Console.WriteLine("546 estate support normals: scale invariant, invalid/steep surfaces rejected.");
    }
}
