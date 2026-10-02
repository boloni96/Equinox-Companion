namespace EquinoxCompanion;
public sealed record SharedRoster(long Revision, DateTimeOffset Updated, SharedPerson[] People, SharedVoyage[]? Voyages = null, int ProtocolVersion = 1);
public sealed record SharedVoyage(string FcId, string FcName, DateTimeOffset At, SubmarineDetails[] Submarines);
public sealed record SharedPerson(string Id, string Name, SharedCharacter[] Characters);
public sealed record SharedCharacter(string Id, string Name, string World, string Dc, string Region, string Account, SharedHouse[] Houses, string AccountId = "", bool? NeedsBoost = null, bool? FcMember = null);
public sealed record SharedHouse(string Id, string GameHouseId, string Type, string Name, string World, string District, int Ward, int Plot, string Size, string OwnerName, string FcName, string FcTag, DateTimeOffset? LastEntry, bool Paused);
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
        var needsBoost = character.NeedsBoost ?? character.Name.TrimStart().StartsWith("~", StringComparison.Ordinal);
        var fcMember = character.FcMember ?? character.Houses.Any(h => h.Type == "Free Company house");
        return !needsBoost ? "Regulars" : fcMember ? "Floaters" : "Empty";
    }
    public static string AccountKey(SharedCharacter character) => string.IsNullOrWhiteSpace(character.AccountId) ? character.Account : character.AccountId;
}
