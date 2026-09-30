using System.Text.RegularExpressions;
namespace EquinoxCompanion;

// Keep incomplete login/logout snapshots local; never let one block valid queued actions.
public static class SyncValidation
{
    private static bool Text(string? s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 100;
    public static bool ActorReady(Actor? a) => a is not null &&
        Regex.IsMatch(a.ContentId ?? "", @"^[1-9]\d{0,19}$") &&
        !string.IsNullOrWhiteSpace(a.Name) && a.Name.Length <= 80 &&
        a.HomeWorldId is > 0 and <= 65535 && a.CurrentWorldId is > 0 and <= 65535;
    private static bool FC(FreeCompanyDetails? f) => f is not null && Regex.IsMatch(f.Id ?? "", @"^[1-9]\d{0,19}$") && Text(f.Name) && f.Tag is not null && f.Tag.Length <= 10 && f.WorldId > 0;
    public static bool CharacterReady(CharacterDetails? c) => c is not null && c.JobId is >= 1 and <= 100 && Text(c.JobName) &&
        c.Level is >= 1 and <= 200 && c.HighestLevel is >= 1 and <= 200 && c.HighestBattleLevel is >= 0 and <= 200 &&
        new[] { c.Race, c.Tribe, c.Sex }.All(s => s is not null && s.Length <= 100) &&
        c.Jobs is not null && c.Jobs.Length <= 100 && c.Jobs.All(j => j is not null && j.Id is >= 1 and <= 100 && Text(j.Name) && j.Level is >= 1 and <= 200) && (c.FreeCompany is null || FC(c.FreeCompany));
    public static bool CanSend(SyncEvent e, DateTimeOffset now)
    {
        if (!Regex.IsMatch(e.Id ?? "", "^[a-f0-9]{32}$") || !ActorReady(e.Actor) || e.At.Year < 2020 || e.At > now.AddMinutes(5)) return false;
        if (e.Kind == "character.updated") return CharacterReady(e.Character);
        var h = e.Address;
        if (h is null || !Regex.IsMatch(h.HouseId ?? "", "^[a-fA-F0-9]{16}$") || h.WorldId == 0 || h.TerritoryTypeId == 0 || h.Ward is < 1 or > 60 || h.Plot is < 0 or > 60 || h.Room is < 0 or > 65535) return false;
        if (e.Kind == "house.entered") return true;
        if (e.Kind == "house.discovered") return h.Plot > 0 && h.Room == 0 && !h.Apartment && !h.Workshop && h.WorldId == e.Actor.HomeWorldId &&
            e.House is { Evidence: "owned-estate-id" } d && d.Size is "" or "Small" or "Medium" or "Large" &&
            (d.Type == "Private house" || d.Type == "Free Company house" && FC(d.FreeCompany) && d.FreeCompany!.WorldId == h.WorldId);
        if (e.Patch is not (>= 1 and <= 3) || e.Bed is not (>= 1 and <= 8)) return false;
        return e.Kind switch {
            "garden.tended" or "garden.ready" => true,
            "garden.observed" => e.Crop is { Ready: true, Evidence: "garden-menu-and-system-message" } c && Text(c.CropName) && !c.CropName.Contains('\n') && !c.CropName.Contains('\r'),
            "garden.planted" => e.Plant is { SeedId: >= 1 and <= 1000000, SoilId: >= 1 and <= 1000000 } p && Text(p.SeedName) && Text(p.SoilName),
            _ => false
        };
    }
}
