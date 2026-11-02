namespace EquinoxCompanion;
public static class AdventurePollPolicy
{
    // Keep live automation responsive; reduce repeated roster projections while idle.
    public static int Seconds(bool windowOpen,bool automationRunning,bool failed)=>
        failed?5:windowOpen||automationRunning?3:15;
}

public enum AdventureConnectionState { Ready, Waiting, Expired }
public sealed class AdventureConnectionRecovery
{
    public DateTimeOffset? WaitingSince {get;private set;}
    public TimeSpan ResumedAfter {get;private set;}
    public void Reset(){WaitingSince=null;ResumedAfter=TimeSpan.Zero;}
    public AdventureConnectionState Check(bool fresh,DateTimeOffset fetched,DateTimeOffset now)
    {
        ResumedAfter=TimeSpan.Zero;
        if(fresh&&fetched!=default&&now-fetched<=TimeSpan.FromSeconds(30)){
            if(WaitingSince is {} start)ResumedAfter=now-start;
            WaitingSince=null;return AdventureConnectionState.Ready;
        }
        WaitingSince??=now;
        return now-WaitingSince.Value>=TimeSpan.FromSeconds(90)?AdventureConnectionState.Expired:AdventureConnectionState.Waiting;
    }
}
