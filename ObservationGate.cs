namespace EquinoxCompanion;

// No game dependency: debounce transitions without turning plugin reloads into visits.
public sealed class ObservationGate
{
    private string? candidate;
    private DateTimeOffset candidateSince;
    private string? committed;
    private bool initialized;

    public string? Observe(string location, DateTimeOffset now)
    {
        if (candidate != location) { candidate = location; candidateSince = now; return null; }
        if (now - candidateSince < TimeSpan.FromSeconds(1) || committed == location) return null;
        var previous = committed;
        committed = location;
        var wasInitialized = initialized;
        initialized = true;
        if (location == "outside") return null;
        return wasInitialized && previous == "outside" ? "house.entered" : "house.observedInside";
    }

    public void Reset() { candidate = null; committed = null; initialized = false; }
}
