using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class RoadRoute465Tests
{
    [ModuleInitializer] internal static void Run()
    {
        // U-shaped road: the connected guide should go around the central block.
        const int size=256;var rgba=new byte[size*size*4];
        for(var z=0;z<size;z++)for(var x=0;x<size;x++){
            var road=(x>=56&&x<72&&z>=56&&z<200)||(x>=184&&x<200&&z>=56&&z<200)||(z>=184&&z<200&&x>=56&&x<200);
            var i=(z*size+x)*4;rgba[i]=road?(byte)214:(byte)100;rgba[i+1]=road?(byte)200:(byte)100;rgba[i+2]=road?(byte)154:(byte)100;rgba[i+3]=255;
        }
        var map=NavigationRoadMap.FromRgba(rgba,size,size,800,0,0)??throw new Exception("Road map missing.");
        var roads=map.BuildRoute(new(-64,0,-64),new(64,0,-64))??throw new Exception("Road graph missing.");
        if(!roads.Points.Any(p=>p.Z>40))throw new Exception("Connected road route must take the U detour instead of the short off-road line.");
        if(map.BuildRoute(new(9999,0,0),Vector3.Zero)!=null)throw new Exception("Off-map coordinates cannot wrap.");
        var geometry=new Floors();var search=new CollisionRouteSearch(geometry,Vector3.Zero,new(8,12,0),.4f);
        for(var i=0;i<4000&&!search.Finished;i++)search.Advance(100,2000);
        if(search.Path==null)throw new Exception("Bidirectional search did not connect floors through the distant ramp: "+search.Error);
        if(!search.Path.Any(p=>p.Z>=26)||!search.Path.Any(p=>p.Y is >2 and <10))throw new Exception("Stacked floors must connect by the ramp, never a vertical shortcut.");
        for(var i=1;i<search.Path.Length;i++)if(!geometry.Traverse(search.Path[i-1],search.Path[i]))throw new Exception("Returned reverse-search edges must be valid in walking direction.");
        if(search.DiagnosticSnapshot().ReverseExpanded==0)throw new Exception("Upper approach must be investigated independently.");
        if(geometry.Probes>search.Expanded*2)throw new Exception("Selected-edge checking regressed to probing every branch.");
        var sliced=new YieldingFloors();var resumed=new CollisionRouteSearch(sliced,Vector3.Zero,new(8,12,0),.4f);
        for(var i=0;i<20000&&!resumed.Finished;i++)resumed.Advance(100,2000);
        if(resumed.Path==null||resumed.BudgetYields==0||sliced.InSlice)throw new Exception("Bidirectional validation must survive mid-edge slice expiry.");
        for(var i=1;i<resumed.Path.Length;i++)if(!new Floors().Traverse(resumed.Path[i-1],resumed.Path[i]))throw new Exception("Resumed join returned an unchecked segment.");
        // Reverse search must not turn a one-way descent into an ascent.
        var oneWay=new Floors{OnlyDown=true};var impossible=new CollisionRouteSearch(oneWay,Vector3.Zero,new(8,12,0),.4f);
        for(var i=0;i<4000&&!impossible.Finished;i++)impossible.Advance(100,2000);
        if(!impossible.Finished||impossible.Path!=null)throw new Exception("Reverse search accepted an illegal upward edge.");
        // Disagreeing map artwork cannot override an impassable geometry wall.
        var wall=new Wall();var guided=new CollisionRouteSearch(wall,new(-64,0,-64),new(64,0,-64),.4f,roads:roads);
        for(var i=0;i<4000&&!guided.Finished;i++)guided.Advance(100,2000);
        if(!guided.Finished||guided.Path!=null)throw new Exception("Map guidance invented a crossing through a wall.");
        Console.WriteLine($"Connected map roads, stacked-floor stair detour, directional joins and geometry authority passed ({search.Expanded} areas, {geometry.Probes} traversal checks).");
    }
    private sealed class Floors:IWalkingCollision
    {
        public bool OnlyDown;public int Probes;
        public bool Ground(Vector3 p,out Vector3 floor){
            var heights=new List<float>();
            if(p.X>=-2&&p.X<=24&&p.Z>=-2&&p.Z<=30)heights.Add(0);
            if(p.X>=0&&p.X<=24&&p.Z>=0&&p.Z<=4)heights.Add(12);
            if(p.X>=19&&p.X<=21&&p.Z>=4&&p.Z<=28)heights.Add((28-p.Z)*.5f);
            var y=heights.OrderBy(y=>Math.Abs(y-p.Y)).FirstOrDefault(float.NaN);floor=p with{Y=y};
            return float.IsFinite(y)&&Math.Abs(y-p.Y)<=.7f;
        }
        public bool Traverse(Vector3 a,Vector3 b){
            Probes++;if(Vector3.Distance(a,b)>2.9f||Math.Abs(a.Y-b.Y)>.7f||(OnlyDown&&b.Y>a.Y+.001f))return false;
            for(var i=0;i<=8;i++){var p=Vector3.Lerp(a,b,i/8f);if(!Ground(p,out var q)||Math.Abs(q.Y-p.Y)>.1f)return false;}
            return true;
        }
    }
    private sealed class YieldingFloors:IWalkingCollision
    {
        private readonly Floors inner=new();
        private readonly HashSet<(Vector3,Vector3,int)> cached=[];
        private int quota;public bool InSlice;
        public void BeginSlice(double milliseconds){quota=2;InSlice=true;}
        public void EndSlice()=>InSlice=false;
        private void Probe(Vector3 a,Vector3 b,int i){
            if(cached.Contains((a,b,i)))return;
            if(InSlice&&quota--<=0)throw new NavigationSliceExpiredException();
            cached.Add((a,b,i));
        }
        public bool Ground(Vector3 p,out Vector3 q){Probe(p,p,0);return inner.Ground(p,out q);}
        public bool Traverse(Vector3 a,Vector3 b){for(var i=1;i<=3;i++)Probe(a,b,i);return inner.Traverse(a,b);}
    }
    private sealed class Wall:IWalkingCollision
    {
        public bool Ground(Vector3 p,out Vector3 q){q=p with{Y=0};return Math.Abs(p.X)<=65&&Math.Abs(p.Z)<=65;}
        public bool Traverse(Vector3 a,Vector3 b)=>Vector3.Distance(a,b)<=2.9f&&(a.X<0)==(b.X<0);
    }
}
