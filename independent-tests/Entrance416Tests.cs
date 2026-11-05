using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Entrance416Tests
{
    [ModuleInitializer] internal static void Run(){
        var now=DateTimeOffset.UtcNow;var start=Vector3.Zero;var goal=new Vector3(0,0,3);
        var stairs=new EntranceForwardProgress(start,goal,now);
        if(stairs.Observe(new(0,1,1.5f),now.AddSeconds(.5))!=EntranceProgress.Walking||
            stairs.Observe(new(0,2,2.8f),now.AddSeconds(1))!=EntranceProgress.Arrived)
            throw new Exception("Forward entrance must accept actual stair progress without requiring the starting floor height.");
        var blocked=new EntranceForwardProgress(start,goal,now);
        if(blocked.Observe(start,now.AddSeconds(1.5))!=EntranceProgress.Blocked)
            throw new Exception("Stationary gate attempt must stop after 1.5 seconds.");
        foreach(var p in new[]{new Vector3(1,0,1),new Vector3(0,-1,1),new Vector3(0,0,-1),new Vector3(float.NaN,0,0)})
            if(new EntranceForwardProgress(start,goal,now).Observe(p,now)!=EntranceProgress.OutsideCorridor)
                throw new Exception("Forward attempt must reject lateral wandering, drops, retreat and invalid positions.");
        if(new EntranceForwardProgress(start,goal,now).Observe(new(0,0,1),now.AddSeconds(8))!=EntranceProgress.Expired)
            throw new Exception("Slow progress must not extend the total entry budget.");
        try{new EntranceForwardProgress(start,new(0,0,10),now);throw new Exception("Unbounded forward attempt accepted.");}catch(InvalidOperationException){}
        Console.WriteLine("Observed forward estate entrance passed.");
    }
}
