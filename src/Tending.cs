using System.Text.RegularExpressions;
namespace EquinoxCompanion;

public sealed record TendingRecord(string EventId, DateTimeOffset SelectedAt, DateTimeOffset ConfirmedAt,
    Actor Actor, Address Address, int Patch, int Bed);

public sealed record TendIntent(string EventId, DateTimeOffset At, GardenSnapshot Target, int Patch, int Bed)
{
    public static TendIntent? From(GardenMenu menu, string? option, DateTimeOffset at)
    {
        if (option != "Tend Crop" || menu.Target.Address is null || menu.Target.TargetId is null) return null;
        var match = Regex.Match(menu.Title, @"^([1-8])(?:st|nd|rd|th) Bed, ([1-3])(?:st|nd|rd|th) Patch$", RegexOptions.CultureInvariant);
        return match.Success ? new(Guid.NewGuid().ToString("N"), at, menu.Target,
            int.Parse(match.Groups[2].Value), int.Parse(match.Groups[1].Value)) : null;
    }
    public TendingRecord? Confirm(uint logId, DateTimeOffset at, GardenSnapshot? target)
    {
        if (logId != 4017 || at < At || at - At > TimeSpan.FromSeconds(3) || target is null ||
            target.Actor != Target.Actor || target.Address != Target.Address || target.TargetId != Target.TargetId) return null;
        return new(EventId, At, at, Target.Actor, Target.Address!, Patch, Bed);
    }
}
