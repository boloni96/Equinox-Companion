using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class EstateApproach412Tests
{
    private sealed class Floor:IWalkingCollision
    {
        public bool Ground(Vector3 p,out Vector3 floor){floor=new(p.X,0,p.Z);return MathF.Abs(p.Y)<=.7f&&p.X<=5;}
        public bool Traverse(Vector3 a,Vector3 b)=>a.X<=5&&b.X<=5&&MathF.Abs(a.Y)<.01f&&MathF.Abs(b.Y)<.01f;
    }
    [ModuleInitializer] internal static void Run()
    {
        var c=new Floor();var start=Vector3.Zero;var marker=new Vector3(5,1.25f,0);
        var approach=EstateDoorApproach.Find(c,start,marker,_=>true);
        if(approach is not {} point||point.Y!=0||Vector3.Distance(point,marker)>3.2f)
            throw new Exception("Raised door needs an interaction-range standing point on the floor.");
        var search=new CollisionRouteSearch(c,start,point,.35f);
        for(var i=0;i<30&&!search.Finished;i++)search.Advance(100,100);
        if(search.Path==null)throw new Exception("Raised-door approach should have a short reachable route.");
        if(EstateDoorApproach.Find(c,start,marker,_=>false)!=null)
            throw new Exception("Occluded entrances must not get an interaction point.");
        var near=new Vector3(3,0,0);
        if(EstateDoorApproach.Find(c,near,marker,_=>true)!=near)
            throw new Exception("Already in interaction range should keep the current standing point.");
        Console.WriteLine("Estate door floor approach passed.");
    }
}
