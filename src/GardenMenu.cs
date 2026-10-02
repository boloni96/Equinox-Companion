namespace EquinoxCompanion;

public sealed record GardenMenu(nint AddonAddress, DateTimeOffset OpenedAt,
    GardenSnapshot Target, string Title, string[] Options)
{
    // Mature menus retain the growing menu's count metadata, but expose two choices.
    public static int VisibleOptionCount(int declared, int available, string? first, string? second)
    {
        if (available >= 2 && (first == "Harvest Crop" || first == "Plant Seeds") && second == "Quit") return 2;
        return declared is > 0 and <= 16 && available >= declared ? declared : 0;
    }
    public (int Patch, int Bed)? ReadyLocation()
    {
        if (Target.Address is null || Target.Address.Apartment || Target.Address.Workshop || Target.Address.Room != 0 ||
            Target.TargetId is null || Target.TargetDetails?.DataId != 2003757 || !Options.Contains("Harvest Crop") ||
            Options.Contains("Tend Crop") || Options.Contains("Plant Seeds")) return null;
        var m = System.Text.RegularExpressions.Regex.Match(Title, @"^([1-8])(?:st|nd|rd|th) Bed, ([1-3])(?:st|nd|rd|th) Patch$");
        return m.Success ? (int.Parse(m.Groups[2].Value), int.Parse(m.Groups[1].Value)) : null;
    }
    public (int Patch, int Bed)? EmptyLocation()
    {
        if (Target.Address is null || Target.Address.Apartment || Target.Address.Workshop || Target.Address.Room != 0 ||
            Target.TargetId is null || Target.TargetDetails?.DataId != 2003757 ||
            !Options.Contains("Plant Seeds") || Options.Contains("Harvest Crop") || Options.Contains("Tend Crop")) return null;
        var match = System.Text.RegularExpressions.Regex.Match(Title, @"^([1-8])(?:st|nd|rd|th) Bed, ([1-3])(?:st|nd|th|rd) Patch$");
        return match.Success ? (int.Parse(match.Groups[2].Value), int.Parse(match.Groups[1].Value)) : null;
    }
    public string? OptionAt(int? index) => index is >= 0 && index < Options.Length ? Options[index.Value] : null;
    public bool Matches(DateTimeOffset now, GardenSnapshot? current) =>
        now >= OpenedAt && now - OpenedAt <= TimeSpan.FromMinutes(1) &&
        current is not null && current.Actor == Target.Actor && current.Address is not null &&
        current.Address == Target.Address && current.TargetId == Target.TargetId;
}
