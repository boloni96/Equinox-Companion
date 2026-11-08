namespace EquinoxCompanion;

// Append-only reads are incremental; replacement/pruning rebuilds from durable records.
// Only used on the framework/draw thread. Background workers receive copied arrays.
public sealed class DiscoveryReadIndex
{
    private IReadOnlyList<SyncEvent>? source;
    private int count;
    private readonly List<SyncEvent> garden=[];
    private readonly Dictionary<string,Dictionary<string,SyncEvent>> storage=[];
    public long GardenRevision {get;private set;}
    public IReadOnlyList<SyncEvent> GardenEvents=>garden;
    public IEnumerable<SyncEvent> StorageFor(string actor)=>storage.TryGetValue(actor,out var rows)?rows.Values:Array.Empty<SyncEvent>();
    private SyncEvent? tail;
    private readonly Dictionary<string,List<SyncEvent>> kinds=[];
    private readonly Dictionary<(string Kind,string Actor,string House,string Storage),SyncEvent> latest=[];
    private readonly Dictionary<(string House,int? Patch,int? Bed),DateTimeOffset> empty=[];
    public long Revision {get;private set;}
    public long IndexedRecords {get;private set;}
    public void Refresh(IReadOnlyList<SyncEvent> rows)
    {
        if(!ReferenceEquals(source,rows)||rows.Count<count||(count>0&&!ReferenceEquals(rows[count-1],tail)))
        {source=rows;count=0;tail=null;kinds.Clear();latest.Clear();empty.Clear();garden.Clear();storage.Clear();GardenRevision++;Revision++;}
        for(;count<rows.Count;count++)
        {
            var e=rows[count];IndexedRecords++;Revision++;
            if(e.Kind.StartsWith("garden.",StringComparison.Ordinal)){garden.Add(e);GardenRevision++;}
            if(e.Storage is {} box){
                if(!storage.TryGetValue(e.Actor.ContentId,out var boxes))storage[e.Actor.ContentId]=boxes=[];
                if(!boxes.TryGetValue(box.Key,out var previous)||e.At>previous.At)boxes[box.Key]=e;
            }
            if(!kinds.TryGetValue(e.Kind,out var group))kinds[e.Kind]=group=[];
            group.Add(e);
            latest[(e.Kind,e.Actor.ContentId,e.Address?.HouseId??"",e.Storage?.Key??"")]=e;
            if(e.Kind=="garden.empty"&&e.Address is {} address){var key=(address.HouseId,e.Patch,e.Bed);if(e.At>empty.GetValueOrDefault(key))empty[key]=e.At;}
        }
        tail=count>0?rows[count-1]:null;
    }
    public IReadOnlyList<SyncEvent> Kind(string kind)=>kinds.TryGetValue(kind,out var rows)?rows:Array.Empty<SyncEvent>();
    public SyncEvent? Last(string kind,string actor,string house="",string storage="")=>latest.GetValueOrDefault((kind,actor,house,storage));
    public DateTimeOffset EmptyAt(string house,int patch,int bed)=>empty.GetValueOrDefault((house,patch,bed),DateTimeOffset.MinValue);
}
