namespace EquinoxCompanion;
public sealed record LocalHousingProjection(HousingDisplayIndex Index,Actor[] Actors,HashSet<string> Owners)
{
    public static LocalHousingProjection Build(SyncEvent[] history,HouseObservation[] visits,TendingRecord[] tending,PlantingRecord[] planting)
    {
        var latest=new Dictionary<string,(DateTimeOffset At,Actor Actor)>();
        void Observe(DateTimeOffset at,Actor actor){if(SyncValidation.ActorReady(actor)&&(!latest.TryGetValue(actor.ContentId,out var old)||at>old.At))latest[actor.ContentId]=(at,actor);}
        var owners=new HashSet<string>();
        foreach(var e in history){
            Observe(e.At,e.Actor);
            if(e.Kind=="house.discovered"&&e.House?.Evidence=="owned-estate-id"&&e.Address!=null&&e.House.Type is "Private house" or "Free Company house")owners.Add(e.Actor.ContentId);
        }
        foreach(var v in visits)Observe(v.ObservedAt,v.Actor);
        foreach(var t in tending)Observe(t.ConfirmedAt,t.Actor);
        foreach(var p in planting)Observe(p.ConfirmedAt,p.Actor);
        return new(HousingDisplayIndex.Build(history,visits),latest.Values.Select(v=>v.Actor).OrderBy(a=>a.HomeWorldName).ThenBy(a=>a.Name).ToArray(),owners);
    }
}
