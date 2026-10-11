namespace EquinoxCompanion;

public sealed record SubmarineDisplayIndex(Dictionary<string,SharedVoyage> Direct,
    Dictionary<string,SharedCachedVoyage> Caches,Dictionary<string,SharedSubmarineSupplies> Supplies)
{
    // Single history pass and indexed character matching; no history scan per event.
    public static SubmarineDisplayIndex Build(SharedRoster? roster,IEnumerable<SyncEvent> history)
    {
        var chars=(roster?.People??[]).SelectMany(p=>p.Characters).ToArray();
        static (string,string) Key(string name,string world)=>(name.ToUpperInvariant(),world.ToUpperInvariant());
        var actors=chars.GroupBy(c=>Key(c.Name,c.World)).Where(g=>g.Count()==1).ToDictionary(g=>g.Key,g=>g.First());
        var members=chars.Where(c=>c.FcMember!=false).Select(c=>(c.Id,c.FcId)).ToHashSet();
        var direct=new Dictionary<string,SharedVoyage>();var caches=new Dictionary<string,SharedCachedVoyage>();var supplies=new Dictionary<string,SharedSubmarineSupplies>();
        void Voyage(SharedVoyage v){if(!direct.TryGetValue(v.FcId,out var old)||v.At>old.At)direct[v.FcId]=v;}
        void Cache(SharedCachedVoyage v){if(members.Contains((v.CharacterId,v.Data.FcId))&&(!caches.TryGetValue(v.CharacterId,out var old)||v.ImportedAt>old.ImportedAt))caches[v.CharacterId]=v;}
        void Supply(SharedSubmarineSupplies v){if(members.Contains((v.CharacterId,v.Data.FcId))&&(!supplies.TryGetValue(v.CharacterId,out var old)||v.ObservedAt>old.ObservedAt))supplies[v.CharacterId]=v;}
        foreach(var v in roster?.Voyages??[])Voyage(v);
        foreach(var v in roster?.CachedVoyages??[])Cache(v);
        foreach(var v in roster?.SubmarineSupplies??[])Supply(v);
        var names=new Dictionary<string,string>();
        foreach(var e in history){
            if(e.Character?.FreeCompany is {} fc)names[fc.Id]=fc.Name;
            if(e.Voyage is {} v)Voyage(new(v.FcId,"Free Company",e.At,v.Submarines,v.Slots));
            if(e.CachedVoyage is null&&e.Supplies is null)continue;
            if(!actors.TryGetValue(Key(e.Actor.Name,e.Actor.HomeWorldName??""),out var c)||c.FcMember==false)continue;
            if(e.CachedVoyage is {} cv&&cv.FcId==c.FcId)Cache(new(c.Id,c.Name,c.World,c.FcId,e.At,cv));
            if(e.Supplies is {} sv&&sv.FcId==c.FcId)Supply(new(c.Id,e.At,sv));
        }
        foreach(var (id,v) in direct.ToArray())if(v.FcName=="Free Company"&&names.TryGetValue(id,out var name))direct[id]=v with {FcName=name};
        foreach(var (id,v) in caches.ToArray())if(v.FcName==v.Data.FcId&&names.TryGetValue(v.Data.FcId,out var name))caches[id]=v with {FcName=name};
        return new(direct,caches,supplies);
    }
}
