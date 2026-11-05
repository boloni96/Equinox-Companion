using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Ground418Tests
{
    private sealed class Surface(Func<Vector3,float?> height,Func<Vector3,Vector3,bool>? blocked=null):IWalkingCollision
    {
        public bool Ground(Vector3 near,out Vector3 floor){
            var y=height(near);floor=new(near.X,y??0,near.Z);return y.HasValue;
        }
        public bool Traverse(Vector3 from,Vector3 to)=>
            WalkingGroundTraversal.Check(this,from,to,blocked??((a,b)=>false),out _);
    }
    [ModuleInitializer] internal static void Run(){
        void Check(bool ok,string label){if(!ok)throw new Exception("Ground traversal: "+label);}
        var flat=new Surface(p=>29f);
        var actor=new Vector3(-646.5992f,29.470238f,-606.4019f);
        var target=new Vector3(actor.X,29f,actor.Z-1);
        Check(flat.Traverse(actor,target),"recorded Mist actor offset must join floor route");
        var search=new CollisionRouteSearch(flat,actor,target,.35f);
        for(var i=0;i<100&&!search.Finished;i++)search.Advance(100,1000);
        Check(search.Path is { Length:>1 }&&flat.Traverse(actor,search.Path[1]),"planned first edge must be executable");
        Check(flat.Traverse(new(0,29,0),new(1,29,0)),"flat ground");
        Check(!flat.Traverse(new(0,30,0),new(1,29,0)),"excessive origin offset");
        Check(!new Surface(p=>null).Traverse(new(0,29,0),new(1,29,0)),"missing start support");
        Check(!new Surface(p=>p.X<.1f?29f:28.53f).Traverse(new(0,29,0),new(1,28.53f,0)),"real .47 drop stays blocked");
        Check(!new Surface(p=>p.X<.1f?29f:29.47f).Traverse(new(0,29,0),new(1,29.47f,0)),"real .47 riser stays blocked");
        Check(new Surface(p=>29f+MathF.Floor(p.X/.3f)*.1f).Traverse(new(0,29,0),new(.9f,29.3f,0)),"small stairs");
        Check(!new Surface(p=>MathF.Abs(p.Z)>.3f?null:29f).Traverse(new(0,29,0),new(1,29,0)),"unsupported edge");
        Check(!new Surface(p=>29f,(a,b)=>a.X<=.5f&&b.X>=.5f).Traverse(new(0,29.47f,0),new(1,29,0)),"wall remains blocking");
        Check(!flat.Traverse(new(0,29,0),new(4,29,0)),"long unchecked chord");
        Console.WriteLine("Ground origin and estate traversal regressions passed.");
    }
}
