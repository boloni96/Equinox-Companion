namespace EquinoxCompanion;

public sealed record GardenMenu(nint AddonAddress, DateTimeOffset OpenedAt,
    GardenSnapshot Target, string Title, string[] Options)
{
    public (int Patch, int Bed)? ReadyLocation()
    {
        if (Target.Address is null || Target.Address.Apartment || Target.Address.Workshop || Target.Address.Room != 0 ||
            Target.TargetId is null || Target.TargetDetails?.DataId != 2003757 || !Options.Contains("Harvest Crop") ||
            Options.Contains("Tend Crop") || Options.Contains("Plant Seeds")) return null;
        var m = System.Text.RegularExpressions.Regex.Match(Title, @"^([1-8])(?:st|nd|rd|th) Bed, ([1-3])(?:st|nd|rd|th) Patch$");
        return m.Success ? (int.Parse(m.Groups[2].Value), int.Parse(m.Groups[1].Value)) : null;
    }
    public string? OptionAt(int? index) => index is >= 0 && index < Options.Length ? Options[index.Value] : null;
    public bool Matches(DateTimeOffset now, GardenSnapshot? current) =>
        now >= OpenedAt && now - OpenedAt <= TimeSpan.FromMinutes(1) &&
        current is not null && current.Actor == Target.Actor && current.Address is not null &&
        current.Address == Target.Address && current.TargetId == Target.TargetId;
}
