using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Shortcut490Tests
{
    [ModuleInitializer] internal static void Run(){
        Vector3[] guide=[new(0,0,0),new(-3,0,0),new(-3,0,4),new(4,0,4),new(7,0,4)];
        foreach(var blocked in new[]{false,true}){
            var collision=new Floor(blocked);var seed=new WalkingRoadSeed(collision,guide[0],guide);
            for(var i=0;i<2000&&!seed.Finished;i++)seed.Step();
            var path=seed.Points;
            if(seed.StopReason!="Guide completed")throw new Exception("A valid original detour must remain available when its shortcut is blocked.");
            for(var i=1;i<path.Length;i++)if(!collision.Traverse(path[i-1],path[i]))throw new Exception("Shortcut crossed an obstacle.");
            if(!blocked&&path.Any(p=>p.X<-.01f))throw new Exception("Open-floor guide must not walk backward through the hairpin.");
            if(blocked&&!path.Any(p=>p.X<-2))throw new Exception("Blocked straight line must preserve the necessary detour.");
        }
        // A dense local loop can exceed sixteen samples. Advancing still requires
        // a complete short collision-approved connector, never just proximity.
        Vector3[] loop=Enumerable.Range(0,41).Select(i=>new Vector3(MathF.Sin(i*MathF.PI/20)*2,0,2-MathF.Cos(i*MathF.PI/20)*2)).ToArray();
        var joined=WalkingSteering.AdvanceWaypoint(Vector3.Zero,loop,1,(_,_)=>true);
        if(joined<20)throw new Exception("Shared steering should look past a dense nearby loop.");
        if(WalkingSteering.AdvanceWaypoint(Vector3.Zero,loop,1,(_,_)=>false)>1)throw new Exception("Blocked shortcut must not advance route progress.");
        Console.WriteLine("490 general road hairpin shortcuts, obstacle fallback and dense-loop steering passed.");
    }
    private sealed class Floor(bool obstacle):IWalkingCollision {
        public bool Ground(Vector3 p,out Vector3 floor){floor=p with{Y=0};return MathF.Abs(p.Y)<.7f;}
        public bool Traverse(Vector3 a,Vector3 b){
            if(Vector3.Distance(a,b)>2.9f)return false;
            for(var i=0;i<=24;i++){
                var p=Vector3.Lerp(a,b,i/24f);
                if(obstacle&&p.X>-1&&p.X<3&&p.Z>.5f&&p.Z<3)return false;
            }
            return true;
        }
    }
}
