using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class OutdoorEntry485Tests
{
    [ModuleInitializer] internal static void Run(){
        var entry=new OutdoorPlotEntry(new(0,0,4));var point=Vector3.Zero;
        for(var i=0;i<6;i++){
            var next=entry.Next(point);
            if(MathF.Abs(Vector3.Distance(point,next)-3)>.001f||next.X!=0)throw new Exception("Estate entry must retain heading and bounded three-yalm steps.");
            point=next;
        }
        try{entry.Next(point);throw new Exception("Entry did not stop at its six-step bound.");}catch(InvalidOperationException){}
        var walk=new EntranceForwardProgress(Vector3.Zero,new(0,0,3),DateTimeOffset.UnixEpoch);
        if(walk.Observe(Vector3.Zero,DateTimeOffset.UnixEpoch.AddSeconds(2))!=EntranceProgress.Blocked)throw new Exception("Entry fallback must stop when the game blocks progress.");
        if(new EntranceForwardProgress(Vector3.Zero,new(0,0,3),DateTimeOffset.UnixEpoch).Observe(new(1,0,1),DateTimeOffset.UnixEpoch)!=EntranceProgress.OutsideCorridor)throw new Exception("Entry must not drift outside its narrow corridor.");
        Console.WriteLine("485 shared outdoor entry heading, six-step bound, blocked movement and corridor checks passed.");
    }
}
