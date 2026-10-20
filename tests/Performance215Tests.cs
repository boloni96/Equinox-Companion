using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Performance215Tests
{
    [ModuleInitializer]internal static void Run()
    {
        var p=new RollingPerformance();
        p.Record("Draw",100,100);p.Record("Draw",2,159);
        var r=p.Snapshot(160).Single();
        if(r.RecentSamples!=1||r.AverageMs!=2||r.RecentPeakMs!=2||r.LifetimePeakMs!=100)throw new Exception("Performance: old session peak must not describe current workload");
        p.Record("Draw",4,160);r=p.Snapshot(160).Single();
        if(r.RecentSamples!=2||r.AverageMs!=3||r.RecentPeakMs!=4||r.LastMs!=4)throw new Exception("Performance: recent aggregate");
        p.Record("Draw",double.NaN,160);p.Record("Draw",-1,160);r=p.Snapshot(221).Single();
        if(r.RecentSamples!=0||r.LifetimePeakMs!=100)throw new Exception("Performance: idle period retains only historical peak");
        p.Clear();if(p.Snapshot(221).Length!=0)throw new Exception("Performance: reset");
        Console.WriteLine("PASS215 recent performance window and distinct lifetime peaks");
    }
}
