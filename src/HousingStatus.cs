namespace EquinoxCompanion;
public enum HousingBand { Unknown, Recent, Warning, Urgent, Overdue }
public static class HousingStatus
{
    public static HousingBand Band(DateTimeOffset? entry, DateTimeOffset now)
    {
        if (entry is null || entry > now) return HousingBand.Unknown;
        var days = (now - entry.Value).TotalDays;
        if (days > 45) return HousingBand.Overdue;
        if (days >= 31) return HousingBand.Urgent;
        if (days >= 8) return HousingBand.Warning;
        return HousingBand.Recent;
    }
    public static DateTimeOffset? LastEligibleEntry(SyncEvent estate, IEnumerable<SyncEvent> discoveries, IEnumerable<HouseObservation> visits)
    {
        if (estate.Address is null || estate.House is null) return null;
        // An observed owned-estate association identifies an owner or FC member.
        var members = discoveries.Where(d => d.Kind == "house.discovered" && d.Address?.HouseId == estate.Address.HouseId &&
            d.House?.Type == estate.House.Type && SyncValidation.ActorReady(d.Actor) &&
            (estate.House.Type == "Private house" ? d.Actor.ContentId == estate.Actor.ContentId :
             estate.House.FreeCompany is not null && d.House.FreeCompany?.Id == estate.House.FreeCompany.Id))
            .Select(d => d.Actor.ContentId).ToHashSet();
        return visits.Where(v => v.Kind == "house.entered" && v.Address.HouseId == estate.Address.HouseId &&
            v.Address.Room == 0 && !v.Address.Apartment && !v.Address.Workshop && members.Contains(v.Actor.ContentId))
            .Select(v => (DateTimeOffset?)v.ObservedAt).Max();
    }
}
