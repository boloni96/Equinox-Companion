namespace EquinoxCompanion;

// Immutable copied values only: safe to capture from the log callback.
public sealed record GardenContext(GardenSnapshot Current, GardenSnapshot? RecentTarget)
{
    public static GardenContext Capture(GardenContext? previous, GardenSnapshot current)
    {
        var samePlace = previous is not null && current.Address is not null &&
            previous.Current.Address == current.Address && previous.Current.Actor == current.Actor;
        var recent = current.TargetId is not null ? current : samePlace ? previous!.RecentTarget : null;
        if (recent is not null && current.ObservedAt - recent.ObservedAt > TimeSpan.FromSeconds(2)) recent = null;
        return new(current, recent);
    }

    public GardenSnapshot? CandidateAt(DateTimeOffset at)
    {
        if (Current.Address is null || at < Current.ObservedAt ||
            at - Current.ObservedAt > TimeSpan.FromMilliseconds(500)) return null;
        var target = Current.TargetId is not null ? Current : RecentTarget;
        return target is not null && at >= target.ObservedAt &&
            at - target.ObservedAt <= TimeSpan.FromSeconds(2) ? target : null;
    }
}

public static class GardenSignals
{
    public static string Classify(uint id) => id switch
    {
        4015 => "planting.succeeded",
        4016 => "fertilizing.succeeded",
        4010 => "fertilizing.failed.noFertilizer",
        4011 => "fertilizing.failed",
        4012 => "fertilizing.failed.alreadyFertilized",
        4005 or 4006 or 4007 or 4008 or 4009 => "planting.failed",
        4013 or 4014 => "harvesting.failed",
        4017 => "crop.status.doingWell",
        4018 => "crop.removedWithered",
        4025 => "crop.removed",
        4026 => "crop.removalDenied",
        750 or 751 => "item.received",
        _ => "unclassified"
    };
}
