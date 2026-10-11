namespace EquinoxCompanion;

// New observations wake an idle queue without bypassing error backoff or
// uploading once per frame during a burst of collection/storage discoveries.
public static class SyncSchedule
{
    public static DateTimeOffset Wake(DateTimeOffset now, DateTimeOffset due, DateTimeOffset lastAttempt, int failures)
    {
        if (failures > 0) return due;
        var earliest = lastAttempt.AddSeconds(2);
        var wake = now.AddMilliseconds(500);
        if (wake < earliest) wake = earliest;
        return due < wake ? due : wake;
    }
}
