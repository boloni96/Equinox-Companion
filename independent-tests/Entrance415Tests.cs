using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Entrance415Tests
{
    private sealed class Gate(bool solid):IWalkingCollision
    {
        public bool Ground(Vector3 p,out Vector3 floor){floor=new(p.X,0,p.Z);return MathF.Abs(p.Y)<.7f;}
        public bool Traverse(Vector3 a,Vector3 b){
            for(var i=0;i<=20;i++){var p=Vector3.Lerp(a,b,i/20f);
                if(p.X>.9f&&p.X<2.1f&&(solid||MathF.Abs(p.Z)<.3f))return false;}
            return true;
        }
    }
    [ModuleInitializer] internal static void Run(){
        var c=new Gate(false);var start=Vector3.Zero;var goal=new Vector3(3,0,0);
        if(VerifiedWalkingOpening.ShortDirect(c,start,goal).Length!=0)throw new Exception("Direct gate obstruction must be detected.");
        var path=VerifiedWalkingOpening.Entrance(c,start,goal);
        if(path.Length<2||path[^1]!=goal||path.Any(p=>MathF.Abs(p.Z)>1||p.X<0||p.X>3))
            throw new Exception("Gate alignment must reach goal inside the bounded corridor.");
        for(var i=1;i<path.Length;i++)if(!c.Traverse(path[i-1],path[i]))throw new Exception("Every alignment segment must be collision checked.");
        if(VerifiedWalkingOpening.Entrance(new Gate(true),start,goal).Length!=0)
            throw new Exception("A solid gate cannot be bypassed.");
        if(VerifiedWalkingOpening.Entrance(c,start,new(10,0,0)).Length!=0)
            throw new Exception("Entrance alignment must not become an area search.");
        Console.WriteLine("Bounded entrance alignment passed.");
    }
}
