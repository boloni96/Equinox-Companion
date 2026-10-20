namespace EquinoxCompanion;

// Runtime indexes, rebuilt from the durable observation history after restart.
// No observations are discarded and acknowledgements are still saved in config.
public sealed class ObservationIndex
{
    private readonly Dictionary<string,SyncEvent> pending=[];
    private readonly Dictionary<DiscoveryKey,SyncEvent> latest=[];
    private readonly Dictionary<string,List<SyncEvent>> characters=[];
    private readonly Dictionary<(string,string,int?,int?),DateTimeOffset> planted=[];
    private readonly HashSet<string> sent=[];
    private object? housesSource,tendsSource,plantsSource,discoveriesSource,sentSource;
    private int housesCount,tendsCount,plantsCount,discoveriesCount,sentCount;
    private string? pairing;
    private bool invalid=true;
    public long IndexedRecords {get;private set;}
    public IEnumerable<SyncEvent> Pending=>pending.Values;
    public int PendingCount=>pending.Count;
    public bool Acknowledged(string id)=>sent.Contains(id);
    public SyncEvent? LastDiscovery(SyncEvent e)=>latest.GetValueOrDefault(Key(e));
    public IEnumerable<SyncEvent> CharacterUpdates(string id)=>characters.TryGetValue(id,out var rows)?rows:[];
    public bool PlantedAfter(SyncEvent e,DateTimeOffset at)=>planted.TryGetValue((e.Actor.ContentId,e.Address?.HouseId??"",e.Patch,e.Bed),out var value)&&value>at;
    public void Invalidate()=>invalid=true;
    private sealed record DiscoveryKey(string Actor,string Kind,string? House,int? Patch,int? Bed,string? Category,int? Segment,uint? Argument,string? Storage,string? Company,string? Source);
    private static DiscoveryKey Key(SyncEvent e)=>new(e.Actor.ContentId,e.Kind,e.Address?.HouseId,e.Patch,e.Bed,e.Collection?.Category,e.Collection?.Segment,e.GardenTarget?.Argument,e.Storage?.Key,e.Company?.Id,e.Company?.Profile?.Source);
    public void Refresh(IReadOnlyList<HouseObservation> houses,IReadOnlyList<TendingRecord> tends,IReadOnlyList<PlantingRecord> plants,IReadOnlyList<SyncEvent> discoveries,IReadOnlyList<string> acknowledgements,string scope)
    {
        if(invalid||pairing!=scope||!ReferenceEquals(housesSource,houses)||!ReferenceEquals(tendsSource,tends)||!ReferenceEquals(plantsSource,plants)||!ReferenceEquals(discoveriesSource,discoveries)||!ReferenceEquals(sentSource,acknowledgements)||houses.Count<housesCount||tends.Count<tendsCount||plants.Count<plantsCount||discoveries.Count<discoveriesCount||acknowledgements.Count<sentCount)
        {
            pending.Clear();latest.Clear();characters.Clear();planted.Clear();sent.Clear();
            housesCount=tendsCount=plantsCount=discoveriesCount=sentCount=0;
            housesSource=houses;tendsSource=tends;plantsSource=plants;discoveriesSource=discoveries;sentSource=acknowledgements;pairing=scope;invalid=false;
        }
        for(;sentCount<acknowledgements.Count;sentCount++){var id=acknowledgements[sentCount];sent.Add(id);pending.Remove(id);}
        for(;housesCount<houses.Count;housesCount++){var h=houses[housesCount];if(h.Kind=="house.entered")Add(new(h.EventId,h.Kind,h.ObservedAt,h.Actor,h.Address));}
        for(;tendsCount<tends.Count;tendsCount++){var t=tends[tendsCount];Add(new(t.EventId,"garden.tended",t.ConfirmedAt,t.Actor,t.Address,t.Patch,t.Bed));}
        for(;plantsCount<plants.Count;plantsCount++)
        {
            var p=plants[plantsCount];Add(new(p.EventId,"garden.planted",p.ConfirmedAt,p.Actor,p.Address,p.Patch,p.Bed,p.Plant));
            var key=(p.Actor.ContentId,p.Address.HouseId,p.Patch,p.Bed);
            if(!planted.TryGetValue(key,out var previous)||p.ConfirmedAt>previous)planted[key]=p.ConfirmedAt;
        }
        for(;discoveriesCount<discoveries.Count;discoveriesCount++)
        {
            var e=discoveries[discoveriesCount];Add(e);latest[Key(e)]=e;
            if(e.Kind=="character.updated") {if(!characters.TryGetValue(e.Actor.ContentId,out var rows))characters[e.Actor.ContentId]=rows=[];rows.Add(e);}
        }
    }
    private void Add(SyncEvent e){IndexedRecords++;if(!sent.Contains(e.Id))pending[e.Id]=e;}
}
