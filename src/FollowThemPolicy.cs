namespace EquinoxCompanion;

[Serializable]
public sealed class FollowThemSettings
{
    public bool QuestHelper {get;set;}
    public bool ShareQuestActions {get;set;}
    public bool SkipLeaderCutscenes {get;set;}
    public bool VerifiedCutsceneSkip {get;set;} // Fresh opt-in; never inherit the pre-crash setting.
    public bool PreferRightSide {get;set;} = true;
    public string TargetName { get; set; } = "";
    public uint HomeWorld { get; set; }
    public bool ChatMessages { get; set; } = true;
    public bool ResumeNearby { get; set; } = true;
    public bool AcceptPartyTeleports { get; set; }
    public bool SharePortalTransitions { get; set; }
    public bool UseSharedPortals { get; set; }
    public bool UseSharedTeleports { get; set; }
    public bool MeetAtTeleports { get; set; }
    public bool AcceptPartyInvites { get; set; }
    public bool AcceptDutyReady { get; set; }
    public bool FollowDismount { get; set; } = true;
    public bool LeaveDuties { get; set; }
    public bool FollowMount { get; set; } = true;
    public bool UseLifestream { get; set; }
    public bool FollowWorldVisits { get; set; }
    public bool FollowDataCenters { get; set; }
    public bool FollowTakeoff { get; set; } = true;
    public bool StopOnMovement { get; set; }
    public int StuckSeconds { get; set; } = 60;
    public int TeleportGilLimit { get; set; } = 5000;
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
    public FollowAction Pause() {var action=MovementRequested?FollowAction.Stop:FollowAction.None;MovementRequested=false;Phase=FollowPhase.Waiting;return action;}
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

// Require a stable playable interval after loading or an occupied event clears.
public sealed class FollowReadyGate
{
    private DateTimeOffset? readySince;
    public void Reset() => readySince = null;
    public bool Observe(DateTimeOffset now, bool playable)
    {
        if (!playable) { Reset(); return false; }
        readySince ??= now;
        return now - readySince.Value >= TimeSpan.FromMilliseconds(750);
    }
}
public sealed class FollowNoticeGate
{
    private string last = "";
    public bool Changed(string status)
    {
        if (status == last) return false;
        last = status;
        return true;
    }
}

public static class FollowCommandFeedback
{
    public static bool IsRejection(string text, string command) =>
        command == "/follow <t>" && text.Length <= 512 && text.Contains("<t>", StringComparison.Ordinal) && text.Contains("is not a valid target name", StringComparison.OrdinalIgnoreCase) ||
        command is "/follow" or "/follow <t>" or "/automove off" or "/automove" && text.Length <= 512 &&
        text.Contains(command.StartsWith("/follow", StringComparison.Ordinal) ? "/follow" : command, StringComparison.Ordinal) &&
        (text.Contains("unavailable", StringComparison.OrdinalIgnoreCase) ||
         command.StartsWith("/follow", StringComparison.Ordinal) && text.Contains("requires a valid target name", StringComparison.OrdinalIgnoreCase));
}

