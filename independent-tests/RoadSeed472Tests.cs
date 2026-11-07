using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class RoadSeed472Tests
{
    [ModuleInitializer] internal static void Run(){
        Vector3[] road=[Vector3.Zero,new(0,0,10),new(10,0,10)];
        var floor=new Ramp();var seed=new WalkingRoadSeed(floor,Vector3.Zero,road);
        for(var i=0;i<500&&!seed.Finished;i++)seed.Step();
        if(!seed.Finished||Vector3.Distance(seed.Points[^1],new(10,5,10))>.01f)throw new Exception("Map seed must acquire real ramp height and follow the road bend.");
        for(var i=1;i<seed.Points.Length;i++)if(!floor.Traverse(seed.Points[i-1],seed.Points[i]))throw new Exception("Seed includes unchecked edge.");
        var wall=new Ramp{Wall=true};var stopped=new WalkingRoadSeed(wall,Vector3.Zero,road);
        for(var i=0;i<500&&!stopped.Finished;i++)stopped.Step();
        if(!stopped.Finished||stopped.Points.Any(p=>p.Z>=4))throw new Exception("Road art must not permit crossing a wall.");
        var sliced=new Ramp{Yield=true};var resumed=new WalkingRoadSeed(sliced,Vector3.Zero,road);
        for(var i=0;i<1000&&!resumed.Finished;i++)try{resumed.Step();}catch(NavigationSliceExpiredException){}
        if(!resumed.Finished||Vector3.Distance(resumed.Points[^1],new(10,5,10))>.01f)throw new Exception("Interrupted validation must resume without losing steps.");
        Console.WriteLine("472 road-guided ramp, bend, obstacle and interrupted-validation tests passed.");
    }
    private sealed class Ramp:IWalkingCollision {
        public bool Wall,Yield;private bool yielded;
        public bool Ground(Vector3 p,out Vector3 floor){floor=p with{Y=p.Z*.5f};return true;}
        public bool Traverse(Vector3 a,Vector3 b){
            if(Yield&&!yielded){yielded=true;throw new NavigationSliceExpiredException();}yielded=false;
            return Vector3.Distance(a,b)<=.6f&&(!Wall||b.Z<4);
        }
    }
}
