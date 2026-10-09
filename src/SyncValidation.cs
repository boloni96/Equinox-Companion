using System.Text.RegularExpressions;
namespace EquinoxCompanion;

// Keep incomplete login/logout snapshots local; never let one block valid queued actions.
public static class SyncValidation
{
    // Keep old diagnostic records in the export, but a complete newer snapshot
    // of the same character makes an incomplete login snapshot obsolete.
    public static bool SupersededIncompleteCharacter(SyncEvent e, IEnumerable<SyncEvent> records, DateTimeOffset now) =>
        e.Kind == "character.updated" && !CanSend(e, now) &&
        !string.IsNullOrWhiteSpace(e.Actor?.ContentId) && records.Any(newer =>
            newer.Kind == "character.updated" && newer.Actor?.ContentId == e.Actor.ContentId &&
            newer.At > e.At && CanSend(newer, now));
    public static bool SupportedByWebsite(string kind, int version) => kind == "garden.batch-order" ? version >= 15 : kind == "character.registered" ? version >= 14 : kind == "submarines.supplies" ? version >= 13 : kind == "garden.plan" ? version >= 12 : kind is "garden.status" or "garden.status.unmapped" ? version >= 11 : kind is "garden.empty.unmapped" or "garden.mapped" ? version >= 10 : kind == "garden.dead" ? version >= 9 : kind == "submarines.cached" ? version >= 7 : kind == "garden.fertilized" ? version >= 6 : kind == "company.observed" ? version >= 4 : kind == "storage.observed" ? version >= 3 : version >= 2 ||
        kind is "house.entered" or "garden.tended" or "garden.ready" or "garden.observed" or "garden.planted" or "house.discovered" or "character.updated";
    private static bool Text(string? s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 100;
    public static bool ActorReady(Actor? a) => a is not null &&
        Regex.IsMatch(a.ContentId ?? "", @"^[1-9]\d{0,19}$") &&
        !string.IsNullOrWhiteSpace(a.Name) && a.Name.Length <= 80 &&
        a.HomeWorldId is > 0 and <= 65535 && a.CurrentWorldId is > 0 and <= 65535;
    private static bool FC(FreeCompanyDetails? f) => f is not null && CompanyProfileIdentity.ValidId(f.Id) && Regex.IsMatch(f.Id ?? "", @"^[1-9]\d{0,19}$") && Text(f.Name) && f.Tag is not null && f.Tag.Length <= 10 && f.WorldId > 0;
    public static bool CompanyReady(FreeCompanyDetails? f) => FC(f) && f!.MasterName.Length <= 80 && f.Profile is { Rank: >= 1 and <= 30, ActiveMembers: >= 1 and <= 512 } p &&
        p.Source is "company-profile" or "member-list" && Text(p.HomeWorld) &&
        new[] {p.Slogan,p.GrandCompany,p.Recruitment,p.Active,p.Focus,p.Seeking,p.EstateName}.All(x=>x is null || x.Length<=500 && !x.Any(char.IsControl)) &&
        (p.FormedAt is null || DateTimeOffset.TryParse(p.FormedAt,out var formed) && formed.Year>=2010 && formed<=DateTimeOffset.UtcNow);
    public static bool CharacterReady(CharacterDetails? c) => c is not null && c.JobId is >= 1 and <= 100 && Text(c.JobName) &&
        c.Level is >= 1 and <= 200 && c.HighestLevel is >= 1 and <= 200 && c.HighestBattleLevel is >= 0 and <= 200 &&
        new[] { c.Race, c.Tribe, c.Sex, c.Nameday, c.Guardian, c.CityState, c.GrandCompany }.All(s => s is not null && s.Length <= 100) &&
        c.Jobs is not null && c.Jobs.Length <= 100 && c.Jobs.All(j => j is not null && j.Id is >= 1 and <= 100 && Text(j.Name) && j.Level is >= 1 and <= 200) && (c.FreeCompany is null || FC(c.FreeCompany));
    public static string HoldReason(SyncEvent e, DateTimeOffset now)
    {
        if (!ActorReady(e.Actor))
        {
            if (e.Actor is null) return "Missing character information.";
            if (e.Actor.HomeWorldId == 0) return "Character home server was not loaded.";
            if (e.Actor.CurrentWorldId == 0) return "Character current server was not loaded.";
            return "Incomplete or invalid character identity.";
        }
        if (!Regex.IsMatch(e.Id ?? "", "^[a-f0-9]{32}$")) return "Invalid event identifier.";
        if (e.At.Year < 2020 || e.At > now.AddMinutes(5)) return "Invalid event timestamp or clock ahead.";
        if (e.Kind == "character.updated") return "Incomplete character/job/FC details.";
        if (e.Address is null) return "Missing estate address.";
        return "Incomplete or invalid estate/garden details for this event type.";
    }
    public static bool CanSend(SyncEvent e, DateTimeOffset now)
    {
        if (!Regex.IsMatch(e.Id ?? "", "^[a-f0-9]{32}$") || !ActorReady(e.Actor) || e.At.Year < 2020 || e.At > now.AddMinutes(5)) return false;
        if (e.Kind == "submarines.supplies") return e.Address is null && SubmarineSupplyStatus.Valid(e.Supplies);
        if (e.Kind == "submarines.cached") return e.Address is null && AutoRetainerCache.Valid(e.CachedVoyage,now);
        if (e.Kind == "company.observed") return e.Address is null && CompanyReady(e.Company);
        if (e.Kind == "character.updated") return CharacterReady(e.Character);
        if (e.Kind == "character.registered") return e.Address is null && CharacterReady(e.Character) && CharacterRegistrationPolicy.Valid(e.Registration) && !string.IsNullOrWhiteSpace(e.Actor.HomeWorldName);
        if (e.Kind == "storage.observed") return e.Storage is {} storage && Regex.IsMatch(storage.Key ?? "", @"^(bag:\d{1,5}|armoire|dresser|(?:retainer|fc):[1-9]\d{0,19}:\d{1,5})$") && Text(storage.Name) && storage.Items is { Length: <= 8000 } && storage.Items.All(i=>i is >0 and <1000000);
        if (e.Kind == "collection.observed") return e.Collection is { } collection &&
            new[] { "mount", "minion", "orchestrion", "emote", "barding", "card", "ornament", "framerkit", "quest", "hairstyle" }.Contains(collection.Category) &&
            collection.Known is { Length: > 0 and <= 5000 } && collection.Unlocked is { Length: <= 5000 } && collection.Obtained is { Length: <= 5000 } && collection.Known.All(id=>id is > 0 and < 1000000) &&
            collection.Unlocked.All(collection.Known.Contains) && collection.Obtained.All(collection.Known.Contains) && EventQuestPolicy.Valid(collection);
        if (e.Kind == "fashion.observed") return e.Fashion is { Score: >= 0 and <= 100, Remaining: >= 0 and <= 4, ThemeId: >= 0 and <= 65535 } fashion && DateTimeOffset.TryParse(fashion.Cycle,out var cycle) && cycle <= e.At && e.At-cycle < TimeSpan.FromDays(7);
        if (e.Kind == "submarines.observed") return e.Voyage is { } voyage && Regex.IsMatch(voyage.FcId ?? "", @"^[1-9]\d{0,19}$") &&
            voyage.Submarines is { Length: > 0 and <= 4 } && voyage.Submarines.Select(s=>s?.Slot).Distinct().Count()==voyage.Submarines.Length && voyage.Submarines.All(s=>s is not null && s.Slot is >= 0 and <= 3 && Text(s.Name) && s.Rank is > 0 and <= 200 && s.Parts is { Length: 4 } && s.Route is { Length: <= 5 } && s.RegisterTime > 0 && s.RegisterTime <= now.ToUnixTimeSeconds() && s.Parts.All(p=>p<=65535) && s.ReturnTime >= 0 && s.ReturnTime < now.AddDays(30).ToUnixTimeSeconds());
        var h = e.Address;
        if (h is null || !Regex.IsMatch(h.HouseId ?? "", "^[a-fA-F0-9]{16}$") || h.WorldId == 0 || h.TerritoryTypeId == 0 || h.Ward is < 1 or > 60 || h.Plot is < 0 or > 60 || h.Room is < 0 or > 65535) return false;
        if (e.Kind == "garden.batch-order") return h.Plot>0&&h.Room==0&&!h.Apartment&&!h.Workshop&&GardenBatchOrdering.Valid(e.BatchOrder);
        if (e.Kind == "garden.plan") return h.Plot>0&&h.Room==0&&!h.Apartment&&!h.Workshop&&GardenPlanEditing.Valid(e.PlanEdit);
        if (e.Kind == "house.entered") return true;
        if (e.Kind == "house.placard") return h.Plot > 0 && h.Room == 0 && !h.Apartment && !h.Workshop &&
            e.House is { Evidence: "observed-placard" } placard && Text(placard.OwnerName) &&
            placard.Type is "Private house" or "Free Company house" && placard.Size is "Small" or "Medium" or "Large" &&
            (placard.FreeCompany is null || FC(placard.FreeCompany) && placard.FreeCompany.WorldId == h.WorldId);
        if (e.Kind == "house.discovered") return h.Plot > 0 && h.Room == 0 && !h.Apartment && !h.Workshop && h.WorldId == e.Actor.HomeWorldId &&
            e.House is { Evidence: "owned-estate-id" } d && d.Size is "" or "Small" or "Medium" or "Large" &&
            (d.Type == "Private house" || d.Type == "Free Company house" && FC(d.FreeCompany) && d.FreeCompany!.WorldId == h.WorldId);
        if(e.Kind is "garden.status" or "garden.status.unmapped")return !h.Apartment&&!h.Workshop&&h.Room==0&&h.Plot>0&&e.Crop is {Ready:false,Status:"growing" or "wilting" or "dead"} status&&Text(status.CropName)&&!status.CropName.Any(char.IsControl)&&
            (e.Kind=="garden.status"?e.Patch is >=1 and <=3&&e.Bed is >=1 and <=8&&status.Evidence=="garden-menu-and-system-message":e.GardenTarget is {} targetStatus&&float.IsFinite(targetStatus.X)&&float.IsFinite(targetStatus.Y)&&float.IsFinite(targetStatus.Z)&&status.Evidence=="garden-system-message-awaiting-calibration");
        if(e.Kind=="garden.empty.unmapped")return !h.Apartment&&!h.Workshop&&h.Room==0&&h.Plot>0&&e.GardenTarget is {} emptyTarget&&float.IsFinite(emptyTarget.X)&&float.IsFinite(emptyTarget.Y)&&float.IsFinite(emptyTarget.Z);
        if (e.Kind == "garden.unmapped") return e.GardenTarget is {} target && float.IsFinite(target.X) && float.IsFinite(target.Y) && float.IsFinite(target.Z) && e.Crop is { Ready: true, Evidence: "garden-system-message-awaiting-calibration" } crop && Text(crop.CropName) && !crop.CropName.Any(char.IsControl);
        if (e.Patch is not (>= 1 and <= 3) || e.Bed is not (>= 1 and <= 8)) return false;
        if(e.Kind=="garden.mapped")return !h.Apartment&&!h.Workshop&&h.Room==0&&h.Plot>0&&e.GardenTarget is {} mappedTarget&&float.IsFinite(mappedTarget.X)&&float.IsFinite(mappedTarget.Y)&&float.IsFinite(mappedTarget.Z);
        return e.Kind switch {
            "garden.tended" or "garden.dead" or "garden.ready" or "garden.empty" or "garden.fertilized" => !h.Apartment && !h.Workshop && h.Room == 0 && h.Plot > 0,
            "garden.observed" => e.Crop is { Ready: true, Evidence: "garden-menu-and-system-message" } c && Text(c.CropName) && !c.CropName.Contains('\n') && !c.CropName.Contains('\r'),
            "garden.planted" => e.Plant is { SeedId: >= 1 and <= 1000000, SoilId: >= 1 and <= 1000000 } p && Text(p.SeedName) && Text(p.SoilName),
            _ => false
        };
    }
}
