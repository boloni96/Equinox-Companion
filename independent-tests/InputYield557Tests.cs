using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class InputYield557Tests
{
    [ModuleInitializer] internal static void Run(){
        var t=DateTimeOffset.Parse("2026-11-15T20:16:17Z");var yield=new WalkingInputYield();
        void Check(bool yes,string why){if(!yes)throw new Exception("557 "+why);}
        Check(!yield.Waiting(t)&&!yield.Expired(t),"new route held");
        yield.Conflict(t);Check(yield.Waiting(t.AddMilliseconds(499)),"transient input not respected");
        Check(!yield.Waiting(t.AddMilliseconds(500)),"transient input permanently stopped route");
        yield.Conflict(t.AddSeconds(29));Check(!yield.Expired(t.AddSeconds(29)),"early competing-input failure");
        yield.Conflict(t.AddSeconds(29.8));Check(yield.Expired(t.AddSeconds(30)),"continuous conflict never bounded");
        yield.Clear();yield.Conflict(t);Check(!yield.Expired(t.AddMinutes(1)),"old isolated input expired during planning");
        yield.Clear();Check(!yield.Expired(t.AddMinutes(1))&&!yield.Waiting(t),"successful input did not clear contention");
        Check(!WalkingInputLease.CanSupplyGround(0,1,0)&&!WalkingInputLease.CanSupplyGround(0,0,.1f),"competing input overwritten");
        Console.WriteLine("557 transient input yields, bounded contention and preserved input priority passed.");
    }
}
