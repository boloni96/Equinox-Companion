using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;

internal static class ResumableSweep554Tests
{
    [ModuleInitializer] internal static void Run()
    {
        // A blocked far end must not spend native queries on footprints that
        // cannot make this connector usable. No wall or floor rule is relaxed.
        var wall=new Terrain { Wall=true };
        var rooms=0;
        if(WalkingGroundTraversal.Check(wall,Vector3.Zero,new(2,0,0),wall.Blocked,out _,
            p=>{rooms++;return true;}))throw new Exception("554 longitudinal wall bypassed");
        if(rooms!=0)throw new Exception("554 blocked edge performed unnecessary footprint sweeps");

        var invalid=new Terrain { InvalidFloor=true };
        if(invalid.Traverse(Vector3.Zero,new(2,0,0)))throw new Exception("554 non-finite support accepted");

        foreach(var slope in new[]{0f,.3f}){
            var direct=new Terrain { Slope=slope };
            var destination=new Vector3(2,2*slope,0);
            if(!WalkingGroundTraversal.Check(direct,Vector3.Zero,destination,direct.Blocked,out _))
                throw new Exception("554 uninterrupted walkable sweep rejected");
            foreach(var budget in new[]{80,110,150}){
                var sliced=new Terrain { Slope=slope };
                var cache=new WalkingTraversalCache();var finished=false;var interruptions=0;
                for(var frame=0;frame<30&&!finished;frame++){
                    sliced.Remaining=budget;
                    try{
                        if(!cache.Check(sliced,Vector3.Zero,destination,sliced.Blocked,out _))
                            throw new Exception("554 resumed sweep changed result");
                        finished=true;
                    }catch(NavigationSliceExpiredException){interruptions++;}
                }
                if(!finished||interruptions==0)throw new Exception("554 sweep failed to resume within bounded frames");
                if(sliced.Calls>direct.Calls*2)throw new Exception("554 completed sections replayed excessively");
                var before=sliced.Calls;sliced.Remaining=0;
                if(!cache.Check(sliced,Vector3.Zero,destination,sliced.Blocked,out _)||before!=sliced.Calls)
                    throw new Exception("554 completed sweep was not cached");
            }
        }
        // A cross-body obstacle must still reject an edge after rails pass,
        // including when the check is interrupted before the footprint phase.
        var cross=new Terrain { CrossWall=true };var rejected=false;
        var crossCache=new WalkingTraversalCache();
        for(var frame=0;frame<30&&!rejected;frame++){
            cross.Remaining=80;
            try{
                if(crossCache.Check(cross,Vector3.Zero,new(2,0,0),cross.Blocked,out _))
                    throw new Exception("554 resumed footprint obstacle bypassed");
                rejected=true;
            }catch(NavigationSliceExpiredException){}
        }
        if(!rejected||crossCache.Failure!="Cross-body clearance or footprint support blocked")
            throw new Exception("554 interrupted footprint lost rejection");
        Console.WriteLine("554 resumable flat/slope sweeps, bounded replay, early walls and interrupted footprint rejection passed.");
    }
    private sealed class Terrain:IWalkingCollision
    {
        public float Slope;public bool Wall,CrossWall,InvalidFloor;
        public int Remaining=int.MaxValue,Calls;
        private void Query(){if(Remaining--<=0)throw new NavigationSliceExpiredException();Calls++;}
        public bool Ground(Vector3 p,out Vector3 ground){Query();ground=p with{Y=InvalidFloor&&p.X>.1f?float.NaN:p.X*Slope};return true;}
        public bool Blocked(Vector3 a,Vector3 b){
            Query();
            if(CrossWall&&MathF.Abs(b.Z-a.Z)>.5f)return true;
            return Wall&&MathF.Min(a.X,b.X)<1.8f&&MathF.Max(a.X,b.X)>=1.8f;
        }
        public bool Traverse(Vector3 a,Vector3 b)=>WalkingGroundTraversal.Check(this,a,b,Blocked,out _);
    }
}
