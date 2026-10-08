namespace EquinoxCompanion;
public static class HelperInteractionPolicy
{
    public static DateTimeOffset Deadline(DateTimeOffset submitted)=>submitted.AddSeconds(12);
    public static bool Waiting(DateTimeOffset now,DateTimeOffset retryAt)=>now<retryAt;
}
// A committed interaction can continue into another scene (not another NPC click).
// Only a genuine new interaction or leader-session reset enables fallback capture again.
public sealed class HelperSceneRecoveryGate
{
    public bool Allowed { get; private set; }=true;
    public void Committed()=>Allowed=false;
    public void NewInteraction()=>Allowed=true;
}
