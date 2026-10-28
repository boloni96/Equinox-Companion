namespace EquinoxCompanion;
public sealed class ItemSearchIndex
{
    public readonly (SharedItemLocation Location,StorageStack Stack,string Alias)[] Rows;
    public int LocationCount {get;}
    private ItemSearchIndex(int count,(SharedItemLocation,StorageStack,string)[] rows){LocationCount=count;Rows=rows;}
    public static ItemSearchIndex Build(SharedRoster? roster,SyncEvent[] observations,IReadOnlyDictionary<uint,string> names)
    {
        var chars=roster?.People.SelectMany(p=>p.Characters).ToArray()??[];
        var byContent=chars.Where(c=>c.ContentId.Length>0).GroupBy(c=>c.ContentId).ToDictionary(g=>g.Key,g=>g.First());
        var houseCharacters=chars.SelectMany(c=>c.Houses.Select(h=>(h.GameHouseId,c.Id))).GroupBy(x=>x.GameHouseId).ToDictionary(g=>g.Key,g=>g.Select(x=>x.Id).Distinct().ToArray());
        var locations=(roster?.ItemLocations??[]).GroupBy(x=>(x.OwnerId,x.Key)).ToDictionary(g=>g.Key,g=>g.MaxBy(x=>x.At)!);
        // Reduce retained history before projecting names and shared membership.
        var latest=observations.Where(e=>e.Kind=="storage.observed"&&e.Storage!=null).GroupBy(e=>(e.Actor.ContentId,e.Storage!.Key)).Select(g=>g.MaxBy(e=>e.At)!);
        foreach(var e in latest){
            if(!byContent.TryGetValue(e.Actor.ContentId,out var c))continue;
            var s=e.Storage!;var parts=s.Key.Split(':');var fc=s.Key.StartsWith("fc:")&&parts.Length>1?parts[1]:null;
            if(fc!=null&&(c.FcMember==false||c.FcId!=fc))continue;
            var housing=s.Key.StartsWith("house:")&&parts.Length>1;
            var owner=housing?"house:"+parts[1]:fc==null?c.Id:"fc:"+fc;var key=(owner,s.Key);
            if(locations.TryGetValue(key,out var old)&&old.At>=e.At)continue;
            locations[key]=new(owner,housing?"Housing · shared observation":fc==null?c.Name+" @ "+c.World:"Free Company",s.Key,s.Name,e.At,s.Items,s.Stacks,housing?(old?.CharacterIds??[]).Append(c.Id).Concat(houseCharacters.GetValueOrDefault(parts[1])??[]).Distinct().ToArray():null);
        }
        var rows=locations.Values.SelectMany(location=>(location.Stacks??location.Items.Select(id=>new StorageStack(id,0,false,names.GetValueOrDefault(id)??$"Item {id}")).ToArray())
            .Select(stack=>(location,stack with{Name=names.GetValueOrDefault(stack.ItemId)??stack.Name},stack.Name))).ToArray();
        return new(locations.Count,rows);
    }
    public (SharedItemLocation Location,StorageStack Stack)[] Search(string query,uint? exact)
    {
        query=query.Trim();if(query.Length==0)return [];
        return Rows.Where(r=>exact is {} id?r.Stack.ItemId==id:r.Stack.ItemId.ToString()==query||r.Stack.Name.Contains(query,StringComparison.OrdinalIgnoreCase)||r.Alias.Contains(query,StringComparison.OrdinalIgnoreCase)).Select(r=>(r.Location,r.Stack)).ToArray();
    }
}
