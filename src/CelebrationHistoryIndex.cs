namespace EquinoxCompanion;
public sealed record CelebrationRewardLink(string Category,uint EntryId,uint ItemId);
public sealed class CelebrationHistoryIndex
{
    private readonly Dictionary<string,AccountEventEvidence> actors;
    public long IndexedRecords {get;}
    public CelebrationHistoryIndex():this(new(StringComparer.OrdinalIgnoreCase),0){}
    private CelebrationHistoryIndex(Dictionary<string,AccountEventEvidence> actors,long indexed){this.actors=actors;IndexedRecords=indexed;}
    private static string NameKey(string name,string? world)=>"name:"+name.Trim()+"\n"+(world??"").Trim();
    public AccountEventEvidence Read(SharedCharacter c)=>actors.GetValueOrDefault(!string.IsNullOrWhiteSpace(c.ContentId)?"id:"+c.ContentId:NameKey(c.Name,c.World))??new(false,[]);
    // Pure managed worker: immutable event snapshots in, immutable index out.
    public CelebrationHistoryIndex Append(SyncEvent[] events,uint[] rewards,CelebrationRewardLink[] links)
    {
        var rewardIds=rewards.ToHashSet();
        var lookup=links.GroupBy(l=>(l.Category,l.EntryId)).ToDictionary(g=>g.Key,g=>g.Select(l=>l.ItemId).Distinct().ToArray());
        var state=actors.ToDictionary(p=>p.Key,p=>(Quest:p.Value.QuestComplete,Items:p.Value.Items.ToHashSet()),StringComparer.OrdinalIgnoreCase);
        var aliases=new Dictionary<Actor,string[]>();
        foreach(var e in events){
            if(e.Kind is not ("collection.observed" or "storage.observed"))continue;
            var found=new HashSet<uint>();var quest=false;
            if(e.Collection is {} collection){
                foreach(var id in collection.Unlocked){
                    if(collection.Category=="quest"&&QuestToolsPolicy.ShortId(id)==QuestToolsPolicy.KeywardBound)quest=true;
                    if(lookup.TryGetValue((collection.Category,id),out var items))found.UnionWith(items);
                }
                foreach(var id in collection.Obtained)if(lookup.TryGetValue((collection.Category,id),out var items))found.UnionWith(items);
            }
            if(e.Storage is {} storage&&(storage.Key.StartsWith("bag:")||storage.Key.StartsWith("retainer:")||storage.Key is "armoire" or "dresser"))
                foreach(var item in storage.Items)if(rewardIds.Contains(item))found.Add(item);
            if(!quest&&found.Count==0)continue;
            if(!aliases.TryGetValue(e.Actor,out var keys))aliases[e.Actor]=keys=["id:"+e.Actor.ContentId,NameKey(e.Actor.Name,e.Actor.HomeWorldName)];
            foreach(var key in keys){
                if(!state.TryGetValue(key,out var old))old=(false,[]);
                old.Items.UnionWith(found);state[key]=(old.Quest||quest,old.Items);
            }
        }
        return new(state.ToDictionary(p=>p.Key,p=>new AccountEventEvidence(p.Value.Quest,p.Value.Items.Order().ToArray()),StringComparer.OrdinalIgnoreCase),IndexedRecords+events.Length);
    }
}
