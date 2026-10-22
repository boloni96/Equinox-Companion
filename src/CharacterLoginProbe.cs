namespace EquinoxCompanion;

internal sealed record LoginProbeTarget(string Id, string PersonId, string AccountId, string ContentId, string Name, string World);
internal sealed record LoginProbeEntry(string ContentId, string Name, string World, bool Locked);
internal enum LoginProbeResult { Waiting, Verified, NotFound, Cancelled, TimedOut }

// Only an explicitly requested title-screen check owns this state. Saved account
// links and previous launches never stand in for the current ready native list.
internal sealed class CharacterLoginProbe
{
    public LoginProbeTarget? Target { get; private set; }
    public DateTimeOffset Deadline { get; private set; }
    private string pairing = "";
    private DateTimeOffset matchedAt;
    public bool Active => Target is not null;

    public bool Begin(LoginProbeTarget target, string pairingKey, DateTimeOffset now)
    {
        if (Active || pairingKey.Length != 64 || !pairingKey.All(char.IsAsciiHexDigit)
            || string.IsNullOrWhiteSpace(target.Id) || string.IsNullOrWhiteSpace(target.PersonId)
            || string.IsNullOrWhiteSpace(target.AccountId) || !CharacterTravelPolicy.ValidTarget(target.Name, target.World)) return false;
        Target = target; pairing = pairingKey; Deadline = now.AddSeconds(45); matchedAt = default;
        return true;
    }

    public LoginProbeResult Inspect(DateTimeOffset now, string pairingKey, LoginProbeTarget? currentTarget,
        bool loggedIn, bool busy, bool ready, IReadOnlyList<LoginProbeEntry> entries)
    {
        if (Target is null || pairingKey != pairing || currentTarget != Target || loggedIn) return LoginProbeResult.Cancelled;
        if (now >= Deadline) return LoginProbeResult.TimedOut;
        if (busy || !ready || entries.Count == 0) { matchedAt = default; return LoginProbeResult.Waiting; }
        var matches = entries.Where(e => !e.Locked && ExactCharacter(Target, e)).Take(2).ToArray();
        if (matches.Length != 1) { matchedAt = default; return LoginProbeResult.NotFound; }
        // A second ready sample prevents a transient loading list from initiating
        // login. The native list is checked again immediately before the IPC call.
        if (matchedAt == default) { matchedAt = now; return LoginProbeResult.Waiting; }
        return now - matchedAt >= TimeSpan.FromMilliseconds(200) ? LoginProbeResult.Verified : LoginProbeResult.Waiting;
    }

    public static bool ExactCharacter(LoginProbeTarget target, LoginProbeEntry entry)
    {
        if (!string.Equals(target.Name, entry.Name, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(target.World, entry.World, StringComparison.OrdinalIgnoreCase)) return false;
        return string.IsNullOrEmpty(target.ContentId)
            ? ulong.TryParse(entry.ContentId, out var id) && id != 0
            : target.ContentId == entry.ContentId;
    }

    public void Cancel() { Target = null; pairing = ""; Deadline = default; matchedAt = default; }
}
