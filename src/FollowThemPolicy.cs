namespace EquinoxCompanion;

[Serializable]
public sealed class FollowThemSettings
{
    public string TargetName { get; set; } = "";
    public uint HomeWorld { get; set; }
    public bool ResumeNearby { get; set; } = true;
    public bool AcceptPartyTeleports { get; set; }
    public bool SharePortalTransitions { get; set; }
    public bool UseSharedPortals { get; set; }
}
public enum FollowPhase { Stopped, Waiting, Loading, Following }
public enum FollowAction { None, Start, Stop }

// Runtime state is deliberately not serialized or included in shared Journal records.
public sealed class FollowThemSession
{
    public bool Armed { get; private set; }
    public FollowPhase Phase { get; private set; }
    public bool MovementRequested { get; private set; }
    public void Arm() { Armed = true; Phase = FollowPhase.Waiting; }
    public FollowAction Stop()
    {
        var result = MovementRequested ? FollowAction.Stop : FollowAction.None;
        Armed = false; MovementRequested = false; Phase = FollowPhase.Stopped;
        return result;
    }
    public FollowAction Observe(bool enabled, bool loggedIn, bool loading, bool available, bool resume)
    {
        if (!enabled || !loggedIn || !Armed) return Stop();
        if (loading)
        {
            MovementRequested = false; Phase = FollowPhase.Loading;
            return FollowAction.None;
        }
        if (!available)
        {
            if (!resume) return Stop();
            var action = MovementRequested ? FollowAction.Stop : FollowAction.None;
            MovementRequested = false; Phase = FollowPhase.Waiting;
            return action;
        }
        Phase = FollowPhase.Following;
        if (MovementRequested) return FollowAction.None;
        MovementRequested = true;
        return FollowAction.Start;
    }
    public static bool Matches(string wanted, uint world, string actual, uint actualWorld) =>
        world != 0 && world == actualWorld && !string.IsNullOrWhiteSpace(wanted) &&
        string.Equals(wanted, actual, StringComparison.Ordinal);
    public static bool IsPartyTeleportPrompt(string text) =>
        text.Trim().StartsWith("Accept Teleport to ", StringComparison.OrdinalIgnoreCase) && text.Trim().EndsWith('?');
}
