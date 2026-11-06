using System.Runtime.CompilerServices;
using System.Text.Json;
using EquinoxCompanion;
internal static class Performance433Tests
{
    [ModuleInitializer]internal static void Run()
    {
        var history=new PerformanceHitchHistory();var now=DateTimeOffset.UtcNow;
        history.Record(1,"fast",1,1,now,null);history.Record(1,"invalid",double.NaN,1,now,null);
        if(history.Snapshot(1).Length!=0)throw new Exception("Fast/invalid samples must not allocate trace rows.");
        for(var i=0;i<300;i++)history.Record(i,"check",20,i,now,new {activity="Coffer",gc0=i});
        var snapshot=history.Snapshot(300);
        if(snapshot.Length!=256||snapshot[0].Frame!=44||snapshot[^1].Frame!=299||history.TotalRecorded!=300)throw new Exception("Hitch history must be bounded and chronological.");
        if(history.Snapshot(1000).Length!=0)throw new Exception("Old hitch rows must expire.");
        var json=JsonSerializer.Serialize(snapshot);
        if(!json.Contains("Coffer")||!json.Contains("Milliseconds"))throw new Exception("Export must retain timing and context.");
        history.Clear();if(history.TotalRecorded!=0||history.Snapshot(300).Length!=0)throw new Exception("Reset must clear trace.");
        if(snapshot.Length!=256||snapshot[0].Frame!=44)throw new Exception("Export snapshot must survive reset.");
        Console.WriteLine("PASS433 bounded hitch history, expiry, context export and reset isolation");
    }
}
