using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class BackgroundPlanTests
{
    [ModuleInitializer]internal static void Run()
    {
        // A completed purchase invalidates ownership data. Neither a new purchase nor
        // a stop is permitted until the background refresh publishes its result.
        var states=new[]{(true,false),(false,true),(false,true),(true,false)};
        var expected=new[]{KeyboundPlanDecision.Proceed,KeyboundPlanDecision.Wait,KeyboundPlanDecision.Wait,KeyboundPlanDecision.Proceed};
        if(!states.Select(s=>KeyboundPlanWait.Decide(s.Item1,s.Item2)).SequenceEqual(expected))throw new Exception("Purchase-refresh-purchase transition must wait and resume.");
        if(KeyboundPlanWait.Decide(true,true)!=KeyboundPlanDecision.Wait)throw new Exception("A stale displayed plan cannot authorize spending while refresh is pending.");
        if(KeyboundPlanWait.Decide(false,false)!=KeyboundPlanDecision.Stop)throw new Exception("Real validation failure must still stop buying.");
        if(!KeyboundUsePolicy.RetryPlan(false,true))throw new Exception("Account routine must also wait for pending ownership data.");
        var now=DateTimeOffset.UtcNow;var request=new KeyboundStartRequest(true,"user-button","run",42,1,2,3,now.AddSeconds(45));
        if(!request.Valid(now,42,1,2,3)||!request.UseRewards||request.Run!="run")throw new Exception("Waiting must preserve buy-and-use intent and run identity.");
        if(request.Valid(now,43,1,2,3)||request.Valid(now,42,2,2,3)||request.Valid(now,42,1,3,3)||request.Valid(now,42,1,2,4)||request.Valid(now.AddSeconds(45),42,1,2,3))throw new Exception("Delayed start must not survive context changes or timeout.");
        Console.WriteLine("PASS437 ownership wait/resume, stale-plan rejection, true failures, delayed-start identity and timeout");
    }
}
