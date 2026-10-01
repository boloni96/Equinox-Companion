namespace EquinoxCompanion;
public static class HouseEntryNotice
{
    public static string? Format(HouseObservation visit, SyncEvent? estate, SharedRoster? roster = null)
    {
        if (visit.Kind != "house.entered" || visit.Address.Room != 0 || visit.Address.Apartment || visit.Address.Workshop || !SyncValidation.ActorReady(visit.Actor)) return null;
        var owned = estate?.Kind == "house.discovered" && estate.Actor.ContentId == visit.Actor.ContentId && estate.Address?.HouseId == visit.Address.HouseId && estate.House?.Evidence == "owned-estate-id";
        var label = owned ? estate!.House!.Type == "Free Company house" ? "your FC House" : "your Private House" : "an estate (ownership unconfirmed)";
        var name = owned && !string.IsNullOrWhiteSpace(estate!.House!.EstateName) ? estate.House.EstateName + " · " : "";
        if (!owned && roster is not null)
        {
            // Match a complete address; a ward/plot alone is not unique across worlds.
            var matches = roster.People.SelectMany(p => p.Characters).SelectMany(c => c.Houses)
                .Where(h => h.GameHouseId.Equals(visit.Address.HouseId, StringComparison.OrdinalIgnoreCase) ||
                    !string.IsNullOrWhiteSpace(visit.Address.WorldName) && !string.IsNullOrWhiteSpace(visit.Address.DistrictName) &&
                    string.Equals(h.World, visit.Address.WorldName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(h.District, visit.Address.DistrictName, StringComparison.OrdinalIgnoreCase) &&
                    h.Ward == visit.Address.Ward && h.Plot == visit.Address.Plot)
                .DistinctBy(h => h.Id).Take(2).ToArray();
            if (matches.Length == 1)
            {
                var h = matches[0];
                label = h.Type == "Free Company house" && !string.IsNullOrWhiteSpace(h.FcName)
                    ? $"{h.FcName}'s FC House" + (string.IsNullOrWhiteSpace(h.OwnerName) ? "" : $" (master: {h.OwnerName})")
                    : !string.IsNullOrWhiteSpace(h.OwnerName) ? $"{h.OwnerName}'s {h.Type}" : "a paired estate";
                name = string.IsNullOrWhiteSpace(h.Name) ? "" : h.Name + " · ";
            }
        }
        return $"[Equinox] You entered {label} — {name}W{visit.Address.Ward} P{visit.Address.Plot}.";
    }
}
