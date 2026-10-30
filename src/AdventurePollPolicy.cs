namespace EquinoxCompanion;
public static class AdventurePollPolicy
{
    // Keep live automation responsive; reduce repeated roster projections while idle.
    public static int Seconds(bool windowOpen,bool automationRunning,bool failed)=>
        failed?5:windowOpen||automationRunning?3:15;
}
