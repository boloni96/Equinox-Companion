namespace EquinoxCompanion;
public sealed record SharedRoster(long Revision, DateTimeOffset Updated, SharedPerson[] People, SharedVoyage[]? Voyages = null, int ProtocolVersion = 1);
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
