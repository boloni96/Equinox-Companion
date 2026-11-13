using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class InteractionSearch553Tests
{
    [ModuleInitializer] internal static void Run()
    {
        foreach(var sign in new[]{-1,1}){
            var geometry=new EnclosedBed();
            var target=new Vector3(20,0,0);
            bool Arrived(Vector3 p)=>Vector3.Distance(p,target)<=2.95f&&p.Z*sign>=1.6f;
            var search=new CollisionRouteSearch(geometry,Vector3.Zero,target,2.95f,arrival:Arrived);
            for(var tick=0;tick<10000&&!search.Finished;tick++)search.Advance(100,8);
            if(search.Path is not {} path||!Arrived(path[^1]))throw new Exception("Bidirectional approach did not reach a validated interaction spot");
            if(search.DiagnosticSnapshot().ReverseExpanded==0)throw new Exception("Interaction approach did not search from the target side");
            for(var i=1;i<path.Length;i++)if(!geometry.Traverse(path[i-1],path[i]))throw new Exception("Interaction shortcut crossed a bed or wall");
        }
        var locked=new EnclosedBed();
        var unreachable=new CollisionRouteSearch(locked,Vector3.Zero,new(20,0,0),2.95f,arrival:_=>false);
        for(var i=0;i<10000&&!unreachable.Finished;i++)unreachable.Advance(100,64);
        if(unreachable.Path!=null)throw new Exception("Unverified interaction position became a destination");
        Console.WriteLine("553 bidirectional interaction approach and validated endpoints passed.");
    }
    private sealed class EnclosedBed:IWalkingCollision
    {
        public bool Ground(Vector3 p,out Vector3 floor){floor=p with{Y=0};return p.X>=-2&&p.X<=24&&MathF.Abs(p.Z)<=8&&MathF.Abs(p.Y)<.7f;}
        public bool Traverse(Vector3 a,Vector3 b){
            if(Vector3.Distance(a,b)>2.9f)return false;
            for(var i=0;i<=32;i++){
                var p=Vector3.Lerp(a,b,i/32f);
                if(!Ground(p,out _)||(p.X>16&&p.X<18&&MathF.Abs(p.Z)<3)||(MathF.Abs(p.X-20)<1.2f&&MathF.Abs(p.Z)<1.2f))return false;
            }
            return true;
        }
    }
}
