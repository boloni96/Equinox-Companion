using System.Text.Json;
namespace EquinoxCompanion;

// A selection is deliberately different from a crafting/withdrawal quantity list.
public sealed record EntrustSelection(Dictionary<uint,int> Keep, Dictionary<uint,int> CategoryKeep, uint[] Protected)
{
    internal Dictionary<uint,int> Amounts(IEnumerable<uint> selected, IEnumerable<ChestStack> bags, RetrievalQuality quality, Func<uint,uint> category, IEnumerable<uint> categories)
    {
        var items=selected.ToHashSet();var cats=categories.ToHashSet();var protectedIds=Protected.ToHashSet();
        return bags.Where(s=>s.Item>0&&s.Quantity>0&&RetainerRetrieval.AllowsQuality(s.Hq,quality)&&!protectedIds.Contains(s.Item))
            .Where(s=>items.Contains(s.Item)||cats.Contains(category(s.Item))).GroupBy(s=>s.Item)
            .Select(g=>(Id:g.Key,Amount:Math.Max(0,g.Sum(s=>s.Quantity)-(items.Contains(g.Key)?Keep.GetValueOrDefault(g.Key):CategoryKeep.GetValueOrDefault(category(g.Key))))))
            .Where(x=>x.Amount>0).ToDictionary(x=>x.Id,x=>x.Amount);
    }
    internal static RetrievalPreset Import(JsonElement root,Func<string,RetrievalEntry?> resolve,Func<uint[]>? protectedItems)
    {
        bool Flag(string key)=>root.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.True;
        if(Flag("AllowEntrustFromArmory"))throw new ArgumentException("This plan includes Armoury Chest items. Disable that option in AutoRetainer before exporting; Equinox Entrust plans use bags and crystals.");
        if(Flag("Duplicates"))throw new ArgumentException("Export this plan with Entrust Duplicates off. Equinox's separate Entrust duplicates action is not imported as a selection rule.");
        var name=root.GetProperty("Name").GetString()??"";
        if(string.IsNullOrWhiteSpace(name)||name.Length>100)throw new ArgumentException("Plan name must be 1–100 characters.");
        var entries=root.GetProperty("EntrustItems").EnumerateArray().Select(x=>x.GetUInt32()).Distinct().Select(id=>resolve(id.ToString())??throw new ArgumentException("Unknown item ID: "+id)).ToArray();
        var keep=root.TryGetProperty("EntrustItemsAmountToKeep",out var values)?values.Deserialize<Dictionary<uint,int>>()??[]:[];
        var cats=new Dictionary<uint,int>();
        if(root.TryGetProperty("EntrustCategories",out var categories))foreach(var c in categories.EnumerateArray()){
            var id=c.GetProperty("ID").GetUInt32();cats[id]=c.TryGetProperty("AmountToKeep",out var k)?k.GetInt32():0;
        }
        if(keep.Values.Concat(cats.Values).Any(x=>x<0||x>RetainerRetrieval.MaxQuantity))throw new ArgumentException("Invalid keep amount.");
        var protection=Flag("ExcludeProtected")?(protectedItems??throw new ArgumentException("AutoRetainer protection list is required for this import."))():[];
        return new(name,RetainerRetrieval.Combine(entries),Mode:RetrievalMode.CompletedItems,DestinationRetainer:StorageAutoRouting.AllRetainers,SendCategories:cats.Keys.ToArray(),Entrust:new(keep,cats,protection.Distinct().ToArray()));
    }
    internal static uint[] ReadProtectionDirectory(string directory)
    {
        // EzConfig migrates the legacy sibling file to AutoRetainer/DefaultConfig.json.
        // An existing modern file is authoritative; never fall back to stale protection.
        var paths=new[]{Path.Combine(directory,"AutoRetainer","DefaultConfig.json"),Path.Combine(directory,"AutoRetainer.json")};
        var path=paths.FirstOrDefault(File.Exists)??throw new IOException("AutoRetainer protection settings were not found in AutoRetainer/DefaultConfig.json or AutoRetainer.json. Open AutoRetainer settings and save them, then import again.");
        if(new FileInfo(path).Length>32*1024*1024)throw new IOException("AutoRetainer protection settings exceed the 32 MB import limit.");
        return ReadProtection(File.ReadAllText(path));
    }
    internal static RetrievalPreset[] AddImportedPlan(IEnumerable<RetrievalPreset> saved,RetrievalPreset current,string? editing,RetrievalPreset imported)
    {
        var result=saved.ToList();
        string Unique(string name){var candidate=name;for(var n=2;result.Any(x=>x.Name==candidate);n++)candidate=name[..Math.Min(name.Length,85)]+" ("+n+")";return candidate;}
        if(current.Items.Length>0||current.SendCategories?.Length>0){
            var at=editing==null?-1:result.FindIndex(x=>x.Name==editing);
            if(at>=0)result[at]=current with{Name=editing!,PlanId=result[at].PlanId};
            else result.Add(current with{Name=Unique(string.IsNullOrWhiteSpace(current.Name)?"Saved working list":current.Name)});
        }
        result.Add(imported with{Name=Unique(imported.Name),PlanId=Guid.NewGuid().ToString("N")});
        if(result.Count>25)throw new ArgumentException("Maximum 25 presets. Remove an unused saved preset before importing; your current list has not changed.");
        return result.ToArray();
    }
    internal static uint[] ReadProtection(string json)
    {
        using var doc=JsonDocument.Parse(json);var ids=new HashSet<uint>();var found=false;
        void Visit(JsonElement value){
            if(value.ValueKind==JsonValueKind.Object)foreach(var p in value.EnumerateObject()){
                if(p.Name=="IMProtectList"){
                    found=true;var list=p.Value;
                    if(list.ValueKind==JsonValueKind.Object&&list.TryGetProperty("$values",out var arr))list=arr;
                    foreach(var id in list.EnumerateArray())ids.Add(id.GetUInt32());
                }else Visit(p.Value);
            }else if(value.ValueKind==JsonValueKind.Array)foreach(var child in value.EnumerateArray())Visit(child);
        }
        Visit(doc.RootElement);
        if(!found)throw new ArgumentException("No readable AutoRetainer protection list was found. Save AutoRetainer settings, then import again; protection has not been discarded.");
        return ids.ToArray();
    }
}
