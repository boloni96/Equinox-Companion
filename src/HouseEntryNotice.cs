namespace EquinoxCompanion;
public static class HouseEntryNotice
{
    public static string? Format(HouseObservation visit, SyncEvent? estate)
    {
        if (visit.Kind != "house.entered" || visit.Address.Room != 0 || visit.Address.Apartment || visit.Address.Workshop || !SyncValidation.ActorReady(visit.Actor)) return null;
        var owned = estate?.Kind == "house.discovered" && estate.Actor.ContentId == visit.Actor.ContentId && estate.Address?.HouseId == visit.Address.HouseId && estate.House?.Evidence == "owned-estate-id";
        var label = owned ? estate!.House!.Type == "Free Company house" ? "your FC House" : "your Private House" : "an estate (ownership unconfirmed)";
        var name = owned && !string.IsNullOrWhiteSpace(estate!.House!.EstateName) ? estate.House.EstateName + " · " : "";
        return $"[Equinox] You entered {label} — {name}W{visit.Address.Ward} P{visit.Address.Plot}.";
    }
}
