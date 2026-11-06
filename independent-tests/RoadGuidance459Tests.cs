using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class RoadGuidance459Tests
{
    [ModuleInitializer] internal static void Run()
    {
        // Observed game map colours: road vs parchment, water and plot fill.
        if(!NavigationRoadMap.RoadColour(214,200,154,255)||NavigationRoadMap.RoadColour(206,190,123,255)||NavigationRoadMap.RoadColour(100,150,180,255)||NavigationRoadMap.RoadColour(214,200,154,0))throw new Exception("Game road colour confidence failed.");
        const int size=256;var data=new byte[size*size*4];
        for(var y=0;y<size;y++)for(var x=0;x<size;x++){
            var i=(y*size+x)*4;var road=x>=120&&x<136;
            data[i]=road?(byte)214:(byte)100;data[i+1]=road?(byte)200:(byte)100;data[i+2]=road?(byte)154:(byte)100;data[i+3]=255;
        }
        var map=NavigationRoadMap.FromRgba(data,size,size,200,700,700)??throw new Exception("Expected recognized road mask.");
        if(map.Penalty(new(-700,0,-700))>=map.Penalty(new(-600,0,-700)))throw new Exception("Subdivision offset must align road guidance with game coordinates.");
        if(map.Penalty(new(99999,0,99999))!=0)throw new Exception("Map boundaries cannot wrap into another road.");
        if(NavigationRoadMap.FromRgba(new byte[4],256,256,200,0,0)!=null)throw new Exception("Invalid texture must fall back to geometry.");
        var path=new[]{Vector3.Zero,new Vector3(2,0,0),new Vector3(2,0,2)};
        if(WalkingSteering.PursuitTarget(Vector3.Zero,path,1,(_,_)=>false)!=Vector3.Zero)throw new Exception("Blocked corner cannot return an unchecked fallback movement.");
        if(WalkingClearance.PreservesRoom(Vector3.Zero,new(2,0,0),p=>p.X is >.4f and <1.6f?1:0))throw new Exception("Shortcut cannot discard open-road clearance.");
        if(!WalkingClearance.PreservesRoom(Vector3.Zero,new(2,0,0),_=>1))throw new Exception("Narrow gate must remain eligible when physically clear.");
        var blocked=new AvoidStalledTargets(new OpenFloor(),[new(3,0,0)]);
        if(blocked.WallPenalty(new(2,0,0))<=blocked.WallPenalty(new(-1,0,0)))throw new Exception("Recovery must prefer clearance around observed stalls.");
        Console.WriteLine("Map coordinate alignment, road preferences, invalid-map fallback and corner steering passed.");
    }
    private sealed class OpenFloor:IWalkingCollision {public bool Ground(Vector3 p,out Vector3 floor){floor=p;return true;}public bool Traverse(Vector3 a,Vector3 b)=>true;}
}
