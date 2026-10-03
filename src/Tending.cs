using System.Text.RegularExpressions;
namespace EquinoxCompanion;

public sealed record TendingRecord(string EventId, DateTimeOffset SelectedAt, DateTimeOffset ConfirmedAt,
    Actor Actor, Address Address, int Patch, int Bed, string Kind = "garden.tended");

public sealed record TendIntent(string EventId, DateTimeOffset At, GardenSnapshot Target, int Patch, int Bed, string Kind = "garden.tended")
{
    public static TendIntent? From(GardenMenu menu, string? option, DateTimeOffset at)
    {
        if (option is not ("Tend Crop" or "Fertilize Crop") || menu.Target.Address is null || menu.Target.TargetId is null) return null;
        var match = Regex.Match(menu.Title, @"^([1-8])(?:st|nd|rd|th) Bed, ([1-3])(?:st|nd|rd|th) Patch$", RegexOptions.CultureInvariant);
        return match.Success ? new(Guid.NewGuid().ToString("N"), at, menu.Target,
            int.Parse(match.Groups[2].Value), int.Parse(match.Groups[1].Value), option == "Fertilize Crop" ? "garden.fertilized" : "garden.tended") : null;
    }
    public TendingRecord? Confirm(uint logId, DateTimeOffset at, GardenSnapshot? target)
    {
        if (logId != (Kind == "garden.fertilized" ? 4016u : 4017u) || at < At || at - At > TimeSpan.FromSeconds(Kind == "garden.fertilized" ? 15 : 3) || target is null ||
            target.Actor != Target.Actor || target.Address != Target.Address || target.TargetId != Target.TargetId) return null;
        return new(EventId, At, at, Target.Actor, Target.Address!, Patch, Bed, Kind);
    }
}

// Removal is an empty-bed observation, never a successful harvest or received yield.
public sealed record RemoveIntent(string EventId, DateTimeOffset At, GardenSnapshot Target, int Patch, int Bed)
{
    public static RemoveIntent? From(GardenMenu menu,string? option,DateTimeOffset at)
    {
        if(option!="Remove Crop"||!menu.Options.Contains(option)||menu.Target.Address is not {} address||address.Apartment||address.Workshop||address.Room!=0||menu.Target.TargetId is null||menu.Target.TargetDetails?.DataId!=2003757)return null;
        var match=Regex.Match(menu.Title,@"^([1-8])(?:st|nd|rd|th) Bed, ([1-3])(?:st|nd|rd|th) Patch$",RegexOptions.CultureInvariant);
        return match.Success?new(Guid.NewGuid().ToString("N"),at,menu.Target,int.Parse(match.Groups[2].Value),int.Parse(match.Groups[1].Value)):null;
    }
    public TendingRecord? Confirm(uint logId,DateTimeOffset at,GardenSnapshot? target)
    {
        if(logId is not (4018 or 4025)||at<At||at-At>TimeSpan.FromSeconds(30)||target is null||target.Actor!=Target.Actor||target.Address!=Target.Address||target.TargetId!=Target.TargetId)return null;
        return new(EventId,At,at,Target.Actor,Target.Address!,Patch,Bed,"garden.empty");
    }
}
