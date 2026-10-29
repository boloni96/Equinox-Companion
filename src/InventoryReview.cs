namespace EquinoxCompanion;

public sealed record InventoryReviewItem(uint ItemId,string Name,int? Quantity,bool? Hq,uint CategoryId,string Category,int? StackCount);
public sealed record InventoryReviewContainer(string CharacterId,string Character,string? World,string Key,string Name,DateTimeOffset ObservedAt,string Source,int? CapacitySlots,InventoryReviewItem[] Items);
public static class InventoryReview
{
    public static InventoryReviewContainer[] Build(IEnumerable<SyncEvent> observations,IEnumerable<RetainerInventoryRecord> records,IReadOnlyDictionary<ulong,string> retainers,IReadOnlyDictionary<uint,string> names,IReadOnlyDictionary<uint,uint> categories,IReadOnlyDictionary<uint,string> categoryNames)
    {
        var latest=new Dictionary<string,InventoryReviewContainer>();var actors=new Dictionary<string,(DateTimeOffset At,Actor Actor)>();
        InventoryReviewItem Item(uint id,string name,int? quantity,bool? hq,int? stacks){var category=categories.GetValueOrDefault(id);return new(id,names.GetValueOrDefault(id,name),quantity,hq,category,categoryNames.GetValueOrDefault(category,"Unknown"),stacks);}
        void Put(InventoryReviewContainer row){var key=row.Key.StartsWith("fc:")||row.Key.StartsWith("house:")?row.Key:row.CharacterId+":"+row.Key;if(!latest.TryGetValue(key,out var previous)||row.ObservedAt>=previous.ObservedAt)latest[key]=row;}
        foreach(var e in observations){
            if(!actors.TryGetValue(e.Actor.ContentId,out var actor)||e.At>actor.At)actors[e.Actor.ContentId]=(e.At,e.Actor);
            if(e.Storage is not {} storage)continue;
            var items=storage.Stacks is {} stacks?stacks.Where(s=>s.ItemId>0&&s.Quantity>0).Select(s=>Item(s.ItemId,s.Name,s.Quantity,s.Hq,s.Positions?.Length)).ToArray()
                :storage.Items.Where(id=>id>0).Distinct().Select(id=>Item(id,id.ToString(),null,null,null)).ToArray();
            Put(new(e.Actor.ContentId,e.Actor.Name,e.Actor.HomeWorldName,storage.Key,storage.Name,e.At,"Recorded storage observation",null,items));
        }
        foreach(var record in records){
            var actor=actors.GetValueOrDefault(record.Character.ToString()).Actor;
            foreach(var container in record.Slots.GroupBy(s=>s.Container)){
                var items=container.Where(s=>s.Item>0&&s.Quantity>0).GroupBy(s=>(s.Item,s.Hq)).Select(g=>Item(g.Key.Item,g.Key.Item.ToString(),g.Sum(s=>s.Quantity),g.Key.Hq,g.Count())).ToArray();
                var name=retainers.GetValueOrDefault(record.Retainer,"Retainer "+record.Retainer);
                Put(new(record.Character.ToString(),actor?.Name??"Character "+record.Character,actor?.HomeWorldName,$"retainer:{record.Retainer}:{container.Key}",name+" · "+(container.Key==12001?"Crystals":"Inventory "+(container.Key-9999)),record.At,"Organizer recorded slots",container.Count(),items));
            }
        }
        return latest.Values.OrderBy(x=>x.Character).ThenBy(x=>x.Key).ToArray();
    }
}
