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

// Only locally proven owner/FC-member entries may advance a shared timer.
public static class SharedHouseLocalEntry
{
    public static SharedHouse Apply(SharedHouse house,IEnumerable<HousingEstateRow> rows,DateTimeOffset now)
    {
        var latest=house.LastEntry;
        foreach(var row in rows){
            var estate=row.Estate;
            if(row.Entry is not {} at||at>now||latest is {} old&&at<=old||
               estate.Kind!="house.discovered"||estate.House?.Evidence!="owned-estate-id"||
               estate.House.Type!=house.Type||estate.Address is not {} address||
               string.IsNullOrWhiteSpace(house.GameHouseId)||!string.Equals(address.HouseId,house.GameHouseId,StringComparison.OrdinalIgnoreCase))continue;
            var eligible=house.Type=="Private house"
                ? !string.IsNullOrWhiteSpace(house.OwnerName)&&string.Equals(estate.Actor.Name.Trim(),house.OwnerName.Trim(),StringComparison.OrdinalIgnoreCase)&&
                  string.Equals(estate.Actor.HomeWorldName,house.World,StringComparison.OrdinalIgnoreCase)
                : house.Type=="Free Company house"&&!string.IsNullOrWhiteSpace(house.FcId)&&estate.House.FreeCompany?.Id==house.FcId;
            if(eligible)latest=at;
        }
        return latest==house.LastEntry?house:house with{LastEntry=latest};
    }
}
