using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class SearchBudget462Tests
{
    [ModuleInitializer] internal static void Run()
    {
        // Emulate native query costs by allowing only two new probes per slice.
        var collision=new SlicedFloor();var search=new CollisionRouteSearch(collision,Vector3.Zero,new(10.3f,0,0),.25f);
        for(var i=0;i<5000&&!search.Finished;i++)search.Advance(100,192);
        if(search.Path==null||search.BudgetYields==0||collision.Peak>2||collision.InSlice)throw new Exception("Interrupted edge/final connector must resume, respect query quota and release its slice.");
        if(Vector3.Distance(search.Path[^1],new(10.3f,0,0))>.25f)throw new Exception("Yielded search cannot claim arrival without finishing the final connector.");
        var slow=new CollisionRouteSearch(new CostlyFloor(),Vector3.Zero,new(48,0,10),.4f,heuristicWeight:1.25f);
        var fast=new CollisionRouteSearch(new CostlyFloor(),Vector3.Zero,new(48,0,10),.4f);
        for(var i=0;i<1000&&(!slow.Finished||!fast.Finished);i++){slow.Advance(100,1000);fast.Advance(100,1000);}
        if(slow.Path==null||fast.Path==null||fast.Expanded>=slow.Expanded/2)throw new Exception("Goal-focused search must avoid broad expansion under map/clearance costs.");
        Console.WriteLine($"Resumable collision probes and final connectors passed; focused search {fast.Expanded} versus {slow.Expanded} nodes.");
    }
    private sealed class CostlyFloor:IWalkingCollision
    {
        public bool Ground(Vector3 p,out Vector3 floor){floor=p with{Y=0};return true;}
        public bool Traverse(Vector3 a,Vector3 b)=>true;
        public float RoutePenalty(Vector3 point)=>1;
    }
    private sealed class SlicedFloor:IWalkingCollision
    {
        private readonly HashSet<(Vector3,Vector3,int)> cached=[];private int queries;
        public bool InSlice;public int Peak;
        public void BeginSlice(double milliseconds){InSlice=true;queries=0;}
        public void EndSlice(){Peak=Math.Max(Peak,queries);InSlice=false;}
        private void Probe(Vector3 a,Vector3 b,int step){
            var key=(a,b,step);if(cached.Contains(key))return;
            if(InSlice&&queries>=2)throw new NavigationSliceExpiredException();
            if(InSlice)queries++;cached.Add(key);
        }
        public bool Ground(Vector3 p,out Vector3 floor){for(var i=0;i<3;i++)Probe(p,p,i);floor=p with{Y=0};return true;}
        public bool Traverse(Vector3 a,Vector3 b){for(var i=0;i<9;i++)Probe(a,b,i);return true;}
    }
}
