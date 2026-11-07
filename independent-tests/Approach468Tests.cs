using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Approach468Tests
{
    [ModuleInitializer] internal static void Run()
    {
        var disconnected=new Surface(true);
        if(EstateDoorApproach.HasIncomingConnection(disconnected,new(-10,0,0),Vector3.Zero))throw new Exception("Standing room on an isolated point is not an incoming walking connection.");
        if(!EstateDoorApproach.HasIncomingConnection(new Surface(false),new(-10,0,0),Vector3.Zero))throw new Exception("Connected floor rejected.");
        if(NativeAetheryteApproach.Ground(disconnected,Vector3.Zero,new(-10,0,0),out _))throw new Exception("Aetheryte approach must reject isolated support.");
        var selected=EstateDoorApproach.Find(disconnected,new(-10,0,0),new(0,1,0),_=>true,4.5f);
        if(selected!=null)throw new Exception("Plot approach must reject all disconnected candidate points.");
        var open=new CountingFloor();var search=new CollisionRouteSearch(open,Vector3.Zero,new(48,0,0),.4f,bidirectional:false);
        for(var i=0;i<1000&&!search.Finished;i++)search.Advance(100,1000);
        if(search.Path==null||search.Expanded>30)throw new Exception("Open road search should use longer verified connectors.");
        for(var i=1;i<search.Path.Length;i++)if(Vector3.Distance(search.Path[i-1],search.Path[i])>2.9f)throw new Exception("Search exceeded swept connector limit.");
        var narrow=new CountingFloor{ShortOnly=true};var fine=new CollisionRouteSearch(narrow,Vector3.Zero,new(12,0,0),.4f);
        for(var i=0;i<1000&&!fine.Finished;i++)fine.Advance(100,1000);
        if(fine.Path==null)throw new Exception("Unit connectors must remain available when long connectors are rejected.");
        Console.WriteLine($"Incoming approach connections and mixed-length swept routing passed ({search.Expanded} open-road areas).");
    }
    private sealed class Surface(bool disconnected):IWalkingCollision
    {
        public bool Ground(Vector3 p,out Vector3 q){q=p with{Y=0};return Math.Abs(p.Y)<.7f;}
        public bool Traverse(Vector3 a,Vector3 b)=>!disconnected||Vector3.Distance(a,b)<.001f;
    }
    private sealed class CountingFloor:IWalkingCollision
    {
        public bool ShortOnly;
        public bool Ground(Vector3 p,out Vector3 q){q=p with{Y=0};return true;}
        public bool Traverse(Vector3 a,Vector3 b)=>Vector3.Distance(a,b)<=(ShortOnly?1.01f:2.9f);
    }
}
