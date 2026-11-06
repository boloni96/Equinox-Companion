using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class CornerRecovery460Tests
{
    [ModuleInitializer] internal static void Run()
    {
        var current=new Vector3(157.1522f,-26,-20.1749f);
        // Same-corner report: forward motion west stalls; reverse the observed southward trail.
        var trail=new[]{current+new Vector3(0,0,-3),current+new Vector3(0,0,-2),current+new Vector3(0,0,-1),current};
        var retreat=WalkingBacktrack.Build(current,trail,(a,b)=>b.Z<a.Z);
        if(retreat.Length!=4||retreat[^1]!=trail[0])throw new Exception("Must backtrack the observed trail, not invent a sideways nudge.");
        if(WalkingBacktrack.Build(current,trail,(_,_)=>false).Length!=0)throw new Exception("Cannot backtrack through a newly blocked connector.");
        if(WalkingBacktrack.Build(current,[current+new Vector3(0,0,-5)],(_,_)=>true).Length!=0)throw new Exception("Cannot jump over missing trail samples.");
        var blocks=new RecentWalkingBlocks();var now=DateTimeOffset.UtcNow;blocks.Add(341,405,current,now);
        if(blocks.Get(341,405,now.AddSeconds(10)).Length!=1||blocks.Get(341,410,now).Length!=0||blocks.Get(641,405,now).Length!=0||blocks.Get(341,405,now.AddMinutes(6)).Length!=0)throw new Exception("Stall memory must survive retries, remain scoped and expire.");
        bool BackFace(Vector3 a,Vector3 b)=>a.X>0&&b.X<0;
        if(!WalkingClearance.SegmentBlocked(new(-1,0,0),new(1,0,0),BackFace))throw new Exception("A wall visible only from its reverse side must block the segment.");
        if(WalkingClearance.SegmentBlocked(new(-1,0,0),new(-2,0,0),BackFace))throw new Exception("Open segment incorrectly blocked.");
        Console.WriteLine("Observed-trail corner recovery, retry obstacle memory and reverse-facing walls passed.");
    }
}
