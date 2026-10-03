namespace EquinoxCompanion;
public sealed record SharedRoster(long Revision, DateTimeOffset Updated, SharedPerson[] People, SharedVoyage[]? Voyages = null, int ProtocolVersion = 1, SharedGardenPlan[]? GardenPlans = null, SharedGardenCare[]? GardenCare = null, SharedGardenYield[]? GardenYields = null, SharedCachedVoyage[]? CachedVoyages = null, string GardenFrame = "wood", bool GardenCornerTrim = false, SharedGardenTarget[]? GardenTargets = null, bool GardenAnimateEffects = true);
public sealed record SharedVoyage(string FcId, string FcName, DateTimeOffset At, SubmarineDetails[] Submarines);
public sealed record SharedPerson(string Id, string Name, SharedCharacter[] Characters);
public sealed record SharedCharacter(string Id, string Name, string World, string Dc, string Region, string Account, SharedHouse[] Houses, string AccountId = "", bool? NeedsBoost = null, bool? FcMember = null, string FcId = "");
public sealed record SharedHouse(string Id, string GameHouseId, string Type, string Name, string World, string District, int Ward, int Plot, string Size, string OwnerName, string FcName, string FcTag, DateTimeOffset? LastEntry, bool Paused, string FcId = "");
public sealed record RosterResult(SharedRoster? Roster, string Status, bool NotModified = false, bool Unauthorized = false);

public static class SharedHousingEligibility
{
    public static bool CountsForCharacter(SharedCharacter character, SharedHouse house, IEnumerable<SharedCharacter> roster)
    {
        if (!string.Equals(character.World.Trim(), house.World.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
        // FC houses are already linked to members by the website projection.
        if (house.Type == "Free Company house") return true;
        if (house.Type != "Private house" || string.IsNullOrWhiteSpace(house.OwnerName)) return false;
        bool Matches(SharedCharacter c) => string.Equals(c.Name.Trim(), house.OwnerName.Trim(), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(c.World.Trim(), house.World.Trim(), StringComparison.OrdinalIgnoreCase);
        if (!Matches(character)) return false;
        return roster.Where(Matches).Select(c => c.Id).Distinct().Take(2).Count() == 1;
    }
}

public static class SharedCharacterGrouping
{
    public static string Group(SharedCharacter character)
    {
        var needsBoost = character.NeedsBoost;
        if (needsBoost is null && character.Name.TrimStart().StartsWith("~", StringComparison.Ordinal)) needsBoost = true;
        if (needsBoost is null) return "Pending sync";
        if (needsBoost == false) return "Regulars";
        return character.FcMember is true ? "Floaters" : character.FcMember is false ? "Empty" : "Pending sync";
    }
    public static string AccountKey(SharedCharacter character) => string.IsNullOrWhiteSpace(character.AccountId) ? character.Account : character.AccountId;
}

public static class SharedHousePresentation
{
    public static string Label(SharedCharacter character, SharedHouse house, bool privateOwner)
    {
        if (!string.Equals(character.World.Trim(), house.World.Trim(), StringComparison.OrdinalIgnoreCase)) return "Shared";
        if (house.Type == "Private house") return privateOwner ? "Private" : "Shared";
        if (house.Type != "Free Company house") return "Shared";
        if (!string.IsNullOrWhiteSpace(character.FcId) && !string.IsNullOrWhiteSpace(house.FcId))
            return character.FcId == house.FcId ? "FC" : "Shared";
        // Old cached rosters may lack IDs. Only a matching FC master proves this relation.
        return !string.IsNullOrWhiteSpace(house.OwnerName) && string.Equals(character.Name.Trim(), house.OwnerName.Trim(), StringComparison.OrdinalIgnoreCase) ? "FC" : "Shared";
    }
    public static int Order(string label) => label == "Private" ? 0 : label == "FC" ? 1 : 2;
}

public sealed record SharedGardenPlan(string HouseId, string HouseName, string World, string District, int Ward, int Plot, int Batch, string Target, DateTimeOffset At, SharedGardenBed[] Beds, string GameHouseId = "", int Capacity = 1, DateTimeOffset? CompletedAt = null, int PhysicalPatch = 0);
public sealed record SharedGardenBed(int Bed, string Crop, string Soil, string Status, string ActualCrop, string ActualSoil, DateTimeOffset? Planted, DateTimeOffset? Watered, double Days, bool Ready, DateTimeOffset? NextTend = null, DateTimeOffset? HarvestAt = null, int Order = 0, int ReplantOrder = 0, string StarterSoil = "", bool CheckExisting = false, string PlantEvent = "", DateTimeOffset? ObservedAt = null, DateTimeOffset? LastClearedAt = null, string TendedBy = "", double? WiltHours = null, DateTimeOffset? LastFertilized = null, DateTimeOffset? DeadConfirmedAt = null, bool KeepMature = false, bool Queued = false, DateTimeOffset? WiltedAt = null, DateTimeOffset? GrowingObservedAt = null);

public static class SharedGardenLocation
{
    public static string? Match(Address? address, IEnumerable<SharedGardenPlan> plans)
    {
        if (address is null || address.Apartment || address.Room != 0 || string.IsNullOrWhiteSpace(address.WorldName) || string.IsNullOrWhiteSpace(address.DistrictName)) return null;
        static string Norm(string? value) => (value ?? "").Trim().Replace("The ", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
        var matches = plans.Where(p => Norm(p.World) == Norm(address.WorldName) && Norm(p.District) == Norm(address.DistrictName) && p.Ward == address.Ward && p.Plot == address.Plot &&
            (string.IsNullOrWhiteSpace(p.GameHouseId) || string.Equals(p.GameHouseId, address.HouseId, StringComparison.OrdinalIgnoreCase))).Select(p => p.HouseId).Distinct().Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }
}

public sealed record SharedGardenCare(string HouseId, string GameHouseId, string HouseName, string World, string District, int Ward, int Plot, int Batch, string[] CharacterIds, SharedGardenCareBed[] Beds, int PhysicalPatch = 0);
public sealed record SharedGardenCareBed(int Bed, bool Ready, bool KeepMature, DateTimeOffset? Watered, DateTimeOffset? NextTend, DateTimeOffset? HarvestAt, DateTimeOffset? DeathAt = null, double? WiltHours = null, string Crop = "", string Soil = "", DateTimeOffset? Planted = null, DateTimeOffset? ObservedAt = null, string TendedBy = "", DateTimeOffset? DeadConfirmedAt = null, bool Empty = false, DateTimeOffset? LastClearedAt = null, DateTimeOffset? LastFertilized = null, DateTimeOffset? WiltedAt = null, DateTimeOffset? GrowingObservedAt = null);
public sealed record GardenChatNotice(string HouseId,string HouseName,int Batch,string Kind,DateTimeOffset? DeathAt);
public static class GardenCareStatus
{
    public static string WebsiteUrl(string houseId,int batch) => "https://equinoxjournal.pages.dev/#house="+Uri.EscapeDataString(houseId)+"&section=gardening&batch="+Math.Clamp(batch,1,3);
    private static string Label(string kind) => kind switch {"dead"=>"Dead crop confirmed","harvest"=>"Ready to harvest","wilt"=>"Wilting · tend urgently","tend"=>"Tending due","check maturity"=>"Check maturity",_=>"Check tending"};
    // Current UIColor rows: 43 green, 37 blue, 32 orange, 17 red.
    public static ushort ChatColor(string kind,DateTimeOffset? deathAt,DateTimeOffset now) => kind=="dead"?(ushort)17:kind=="harvest"?(ushort)43:kind is "tend" or "check care"?(deathAt is {} deadline&&deadline<=now.AddHours(4)?(ushort)32:(ushort)37):(ushort)32;
    public static (string Text,ushort Color)[] ChatSummary(IEnumerable<GardenChatNotice> values,DateTimeOffset now)
    {
        return values.GroupBy(x=>(x.Kind,Color:ChatColor(x.Kind,x.DeathAt,now)))
            .OrderBy(g=>g.Key.Kind=="dead"?0:g.Key.Color==32?1:g.Key.Kind=="harvest"?2:3)
            .SelectMany(g=>g.GroupBy(x=>x.HouseId).Select(h=>(BatchMessage(h.First().HouseName,g.Key.Kind,h.Select(x=>(x.Batch,x.DeathAt)),now).Replace("[Equinox] ",""),g.Key.Color))).ToArray();
    }
    public static string HouseLabel(SharedHouse? house, SharedGardenCare care)
    {
        var owner=house?.Type=="Free Company house"
            ? "FC"+(string.IsNullOrWhiteSpace(house.FcName)?"":": "+house.FcName)
            : !string.IsNullOrWhiteSpace(house?.OwnerName)?"Owner: "+house.OwnerName:"Owner not recorded";
        return $"{owner} · {care.World} · {care.District} W{care.Ward} P{care.Plot}";
    }
    public static string Message(string houseName, string kind, IEnumerable<int> batches, DateTimeOffset? deathAt = null, DateTimeOffset? now = null)
    {
        var label = Label(kind);
        var countdown = "";
        if (now is not null && kind is "tend" or "check care")
        {
            var minutes = deathAt is null ? -1 : (int)Math.Ceiling((deathAt.Value-now.Value).TotalMinutes);
            countdown = deathAt is null ? " [death timer unknown]" : minutes <= 0 ? " [death risk - check now]" : $" [~{minutes/60:00}h:{minutes%60:00}m to die]";
        }
        return $"[Equinox] {label} at '{houseName}': Batch {string.Join(", ", batches.Distinct().Order())}.{countdown}";
    }

    public static string BatchMessage(string houseName, string kind, IEnumerable<(int Batch, DateTimeOffset? DeathAt)> batches, DateTimeOffset now)
    {
        var label = Label(kind);
        var parts = batches.GroupBy(x => x.Batch).OrderBy(x => x.Key).Select(group =>
        {
            var deadline = group.Any(x => x.DeathAt is null) ? null : group.Min(x => x.DeathAt);
            var minutes = deadline is null ? -1 : (int)Math.Ceiling((deadline.Value-now).TotalMinutes);
            var time = deadline is null ? "timer unknown" : minutes <= 0 ? "death risk - check now" : $"~{minutes/60:00}h:{minutes%60:00}m";
            return $"Batch {group.Key}" + (kind is "tend" or "check care" ? $" [{time}]" : "");
        });
        return $"[Equinox] {label} at '{houseName}': {string.Join(", ", parts)}.";
    }
    public static string? Due(SharedGardenCareBed bed, DateTimeOffset now)
    {
        if (bed.Empty) return null;
        if (bed.Ready) return bed.KeepMature ? null : "harvest";
        if(bed.DeadConfirmedAt is {} dead&&dead<=now&&(bed.Watered is null||bed.Watered<=dead)&&(bed.Planted is null||bed.Planted<=dead))return "dead";
        if(bed.WiltedAt is {} wilt&&wilt<=now&&(bed.Watered is null||bed.Watered<=wilt)&&(bed.Planted is null||bed.Planted<=wilt))return "wilt";
        var next = bed.Watered?.AddHours(12) ?? bed.NextTend;
        if (next is null) return "check care";
        if (next <= now) return "tend";
        return bed.HarvestAt <= now ? "check maturity" : null;
    }
}

public sealed record SharedGardenYield(string HouseId, int Batch, string Actual, string Planned, string Seeds);
