namespace EquinoxCompanion;

public sealed record GardenMenu(nint AddonAddress, DateTimeOffset OpenedAt,
    GardenSnapshot Target, string Title, string[] Options)
{
    public string? OptionAt(int? index) => index is >= 0 && index < Options.Length ? Options[index.Value] : null;
    public bool Matches(DateTimeOffset now, GardenSnapshot? current) =>
        now >= OpenedAt && now - OpenedAt <= TimeSpan.FromMinutes(1) &&
        current is not null && current.Actor == Target.Actor && current.Address is not null &&
        current.Address == Target.Address && current.TargetId == Target.TargetId;
}
