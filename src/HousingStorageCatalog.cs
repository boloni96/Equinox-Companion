namespace EquinoxCompanion;
internal sealed record HousingStorageHome(string Id,string Label,bool Outside,SharedItemLocation[] Locations);
internal static class HousingStorageCatalog
{
    internal static string HouseId(string key)=>key.StartsWith("house:")&&key.Split(':') is {Length:3} parts?parts[1]:"";
    internal static bool Inside(string key)=>uint.TryParse(key.Split(':').LastOrDefault(),out var type)&&type is >=25002 and <=25014 or >=27001 and <=27011;
    internal static string Section(string key)=>Inside(key)?"Inside":"Outside";
    internal static HousingStorageHome[] ForCharacter(SharedRoster? roster,IEnumerable<SyncEvent> observations,string contentId)
    {
        var local=observations.ToArray();
        var matches=(roster?.People??[]).SelectMany(p=>p.Characters).Where(c=>c.ContentId==contentId).Take(2).ToArray();
        var character=matches.Length==1?matches[0]:null;
        var houses=character?.Houses??[];
        if(character is {FcId.Length:>0}){
            var fc=(roster?.People??[]).SelectMany(p=>p.Characters).SelectMany(c=>c.Houses).Concat(roster?.GardenHouses??[])
                .Where(h=>h.FcId==character.FcId&&h.Type.Contains("Company",StringComparison.OrdinalIgnoreCase));
            houses=houses.Concat(fc).DistinctBy(h=>h.Id).ToArray();
        }
        var allowed=houses.Select(h=>h.GameHouseId).Where(id=>!string.IsNullOrEmpty(id)).ToHashSet();
        // Storage capture already requires estate permissions. Local evidence
        // admits an unsynced/shared home only for the character who recorded it.
        allowed.UnionWith(local.Where(e=>e.Actor.ContentId==contentId&&e.Storage!=null).Select(e=>HouseId(e.Storage!.Key)).Where(id=>id.Length>0));
        var scoped=roster is null?null:roster with{People=[],GardenHouses=houses,ItemLocations=(roster.ItemLocations??[]).Where(l=>allowed.Contains(HouseId(l.Key))).ToArray()};
        // Keep freshest observations from any actor, but only for eligible homes.
        return Build(scoped,local.Where(e=>e.Storage!=null&&allowed.Contains(HouseId(e.Storage.Key))));
    }

    internal static HousingStorageHome[] Build(SharedRoster? roster,IEnumerable<SyncEvent> observations)
    {
        var houses=(roster?.People.SelectMany(p=>p.Characters).SelectMany(c=>c.Houses)??[]).Concat(roster?.GardenHouses??[])
            .GroupBy(h=>string.IsNullOrWhiteSpace(h.GameHouseId)?"unrecorded-"+h.Id:h.GameHouseId).ToDictionary(g=>g.Key,g=>g.First());
        var locations=new Dictionary<string,SharedItemLocation>();
        void Add(SharedItemLocation s){if(HouseId(s.Key).Length==0)return;if(!locations.TryGetValue(s.Key,out var old)||s.At>old.At)locations[s.Key]=s;}
        foreach(var s in roster?.ItemLocations??[])if(houses.ContainsKey(HouseId(s.Key)))Add(s);
        foreach(var e in observations.Where(e=>e.Storage!=null&&HouseId(e.Storage.Key).Length>0)){
            var s=e.Storage!;var id=HouseId(s.Key);
            Add(new("house:"+id,e.Actor.Name,s.Key,s.Name,e.At,s.Items,s.Stacks));
        }
        var ids=houses.Keys.Concat(locations.Keys.Select(HouseId)).Distinct();
        return ids.Select(id=>{
            var h=houses.GetValueOrDefault(id);var rows=locations.Values.Where(s=>HouseId(s.Key)==id).OrderBy(s=>s.Key).ToArray();
            var label=h==null?(rows.FirstOrDefault()?.Name.Split(" · Indoor")[0].Split(" · Outdoor")[0]??id):$"{(h.OwnerName.Length>0?h.OwnerName:h.FcName)} · {h.Name} · {h.World} · {h.District} W{h.Ward} P{h.Plot} · {h.Type}";
            var outside=h==null||h.Plot>0&&!h.Type.Contains("apartment",StringComparison.OrdinalIgnoreCase)&&!h.Type.Contains("chamber",StringComparison.OrdinalIgnoreCase);
            return new HousingStorageHome(id,label,outside,rows);
        }).OrderBy(h=>h.Label).ToArray();
    }
}
