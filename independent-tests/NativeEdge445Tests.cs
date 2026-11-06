using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class NativeEdge445Tests
{
    private sealed class Pedestal: IWalkingCollision
    {
        public bool Blocked;
        public bool Ground(Vector3 p,out Vector3 floor){floor=new(p.X,0,p.Z);return MathF.Abs(p.Y)<=.7f&&CollisionRouteSearch.Horizontal(p,Vector3.Zero)>5;}
        public bool Traverse(Vector3 a,Vector3 b)=>!Blocked&&Ground(a,out _)&&Ground(b,out _);
    }
    [ModuleInitializer] internal static void Run()
    {
        if(NativeAetheryteApproach.Range(0,1)!=8||NativeAetheryteApproach.Range(1,1)!=7||NativeAetheryteApproach.Range(2,1)!=6)throw new Exception("Only attempt interaction at bounded close distances.");
        var player=new Vector3(18,0,0);var crystal=new Vector3(0,4,0);var map=new Pedestal();
        var edge=NativeAetheryteApproach.InteractionEdge(player,crystal,13);
        if(Vector3.Distance(edge,crystal)>12.01f||edge.X<10||edge.Y!=0)throw new Exception("Approach must stop inside interaction range on the player's floor, not beside the pedestal.");
        if(!NativeAetheryteApproach.Ground(map,edge,player,out var floor))throw new Exception("Interaction edge must be supported even when pedestal ground is blocked.");
        if(VerifiedWalkingOpening.ShortDirect(map,player,floor,8).Length<2)throw new Exception("Nearby clear aetheryte approach must avoid area search.");
        map.Blocked=true;if(VerifiedWalkingOpening.ShortDirect(map,player,floor,8).Length!=0)throw new Exception("Blocked approach must retain collision checks and fall back to navigation.");
        var near=new Vector3(10,0,0);if(NativeAetheryteApproach.InteractionEdge(near,crystal,13)!=near)throw new Exception("Already in interaction range must not walk.");
        var closer=NativeAetheryteApproach.InteractionEdge(player,crystal,8);if(Vector3.Distance(closer,crystal)>7.01f||Vector3.Distance(edge,closer)<4)throw new Exception("Closer fallback must choose a new destination inside the smaller range.");
        Console.WriteLine("Aetheryte boundary, blocked pedestal and short direct approach passed.");
    }
}
