namespace EquinoxCompanion;

// Derived display data only. Preserve the existing eligibility rules while restricting
// each lookup to that estate's history; never walk the full journal from DrawHousing.
public sealed record HousingEstateRow(SyncEvent Estate,DateTimeOffset? Entry,DateTimeOffset? OwnEntry);
public sealed record HousingCharacterRows(HousingEstateRow[] Estates,HouseObservation[] OtherVisits);
public sealed class HousingDisplayIndex
{
    public Dictionary<string,HousingCharacterRows> Characters { get; }=[];
    public static HousingDisplayIndex Build(IEnumerable<SyncEvent> discoveries,IEnumerable<HouseObservation> visits)
    {
        var houses=discoveries.Where(d=>d.Kind=="house.discovered"&&d.Address is not null).ToArray();
        var byHouse=houses.ToLookup(d=>d.Address!.HouseId);
        var visitArray=visits.ToArray();var visitsByHouse=visitArray.ToLookup(v=>v.Address.HouseId);
        var visitsByActor=visitArray.ToLookup(v=>v.Actor.ContentId);
        var owned=houses.Where(d=>d.House?.Evidence=="owned-estate-id").ToLookup(d=>d.Actor.ContentId);
        var result=new HousingDisplayIndex();
        foreach(var id in owned.Select(g=>g.Key).Concat(visitsByActor.Select(g=>g.Key)).Distinct())
        {
            var estates=owned[id].GroupBy(d=>d.Address!.HouseId).Select(g=>g.OrderByDescending(d=>d.At).First())
                .OrderBy(d=>d.House!.Type=="Private house"?0:d.House.Type=="Free Company house"?1:2)
                .Select(e=>new HousingEstateRow(e,HousingStatus.LastEligibleEntry(e,byHouse[e.Address!.HouseId],visitsByHouse[e.Address.HouseId]),
                    visitsByHouse[e.Address.HouseId].Where(v=>v.Actor.ContentId==id&&v.Kind=="house.entered").Select(v=>(DateTimeOffset?)v.ObservedAt).Max())).ToArray();
            var ids=estates.Select(e=>e.Estate.Address!.HouseId).ToHashSet();
            var other=visitsByActor[id].Where(v=>!ids.Contains(v.Address.HouseId)).GroupBy(v=>v.Address.HouseId)
                .Select(g=>g.OrderByDescending(v=>v.ObservedAt).First()).OrderByDescending(v=>v.ObservedAt).ToArray();
            result.Characters[id]=new(estates,other);
        }
        return result;
    }
}
