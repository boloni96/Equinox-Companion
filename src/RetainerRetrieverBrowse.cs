using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private sealed record RetrieverStock(ulong Retainer,string Holder,uint Item,string Name,int Quantity,DateTimeOffset? At);
    private Dictionary<uint,int> retrievalBrowseBags=[];
    private RetrieverStock[] retrievalStock=[];private bool retrievalStockComplete;private string retrievalStockNotice="Refresh at a summoning bell to identify this character's retainers.";
    private string retrievalBrowseSearch="";private bool retrievalBrowseGroups=true;
    private bool retrievalRefreshPending=true;
    private readonly Dictionary<ulong,Dictionary<ulong,string>> retrievalKnownRetainers=[];
    private void RefreshRetrievalStock()
    {
        if(!Player.IsLoaded)return;
        var owner=Player.ContentId;var ownerText=owner.ToString();var live=ReadSellerRetainers();
        if(!retrievalKnownRetainers.TryGetValue(owner,out var names))retrievalKnownRetainers[owner]=names=[];
        foreach(var r in marketLedger.Retainers.Where(r=>r.CharacterId==owner))names[r.Id]=r.Name;
        foreach(var r in live)names[r.Id]=r.Name;
        var latest=config.Discoveries.Where(e=>e.Actor.ContentId==ownerText&&e.Storage?.Key.StartsWith("retainer:")==true).GroupBy(e=>e.Storage!.Key).ToDictionary(g=>g.Key,g=>g.MaxBy(e=>e.At)!);
        foreach(var row in latest.Values){var parts=row.Storage!.Key.Split(':');if(parts.Length==3&&ulong.TryParse(parts[1],out var id)&&int.TryParse(parts[2],out var type)&&type is >=10000 and <=10006 or 12001){var label=row.Storage.Name.Split(" · ")[0];names.TryAdd(id,label.StartsWith("Retainer ")?label[9..]:label);}}
        var allagan=false;var notice="";var rawById=new Dictionary<ulong,HashSet<ulong[]>>();
        try
        {
            allagan=Pi.InstalledPlugins.Any(x=>x.IsLoaded&&x.InternalName=="InventoryTools")&&Pi.GetIpcSubscriber<bool>("AllaganTools.IsInitialized").InvokeFunc()&&Pi.GetIpcSubscriber<ulong>("AllaganTools.CurrentCharacter").InvokeFunc()==owner;
            if(allagan)
            {
                var ids=Pi.GetIpcSubscriber<bool,HashSet<ulong>>("AllaganTools.GetCharactersOwnedByActive").InvokeFunc(false);
                foreach(var id in ids.Union(names.Keys).ToArray())
                {
                    try{var raw=Pi.GetIpcSubscriber<ulong,HashSet<ulong[]>>("AllaganTools.GetCharacterItems").InvokeFunc(id);
                        if(raw.Any(x=>x.Length>3&&RetainerRetrieval.IsRetainerStorage(x[0]))){rawById[id]=raw;names.TryAdd(id,"Retainer "+id);}}
                    catch{notice=" · some Allagan records unavailable; using saved data";}
                }
            }
        }
        catch{allagan=false;notice=" · Allagan unavailable; using saved data";}
        var stocks=new List<RetrieverStock>();var complete=live.Length>0;
        foreach(var (id,name) in names)
        {
            var rows=latest.Values.Where(e=>e.Storage!.Key.StartsWith("retainer:"+id+":")).Where(e=>int.TryParse(e.Storage!.Key.Split(':').Last(),out var t)&&RetainerRetrieval.IsRetainerStorage((ulong)t)).ToArray();
            if(rawById.TryGetValue(id,out var raw))
            {
                var good=raw.Where(x=>x.Length>3&&RetainerRetrieval.IsRetainerStorage(x[0])&&x[2] is >0 and <=uint.MaxValue&&x[3] is >0 and <=RetainerRetrieval.MaxQuantity);
                foreach(var g in good.GroupBy(x=>(uint)x[2]))stocks.Add(new(id,name,g.Key,retrievalCatalog?.Names.GetValueOrDefault(g.Key)??g.Key.ToString(),checked((int)g.Sum(x=>(long)x[3])),null));
            }
            else
            {
                if(!Enumerable.Range(10000,7).Append(12001).All(t=>rows.Any(e=>e.Storage!.Key=="retainer:"+id+":"+t)))complete=false;
                var saved=rows.SelectMany(e=>(e.Storage!.Stacks??[]).Select(s=>(s,e.At))).ToArray();
                foreach(var g in saved.GroupBy(x=>x.s.ItemId))stocks.Add(new(id,name,g.Key,g.First().s.Name,g.Sum(x=>x.s.Quantity),g.Min(x=>x.At)));
                if(rows.Length==0)stocks.AddRange(retrievalStock.Where(s=>s.Retainer==id&&retrievalPreviewOwner==owner));
            }
        }
        retrievalStock=stocks.ToArray();retrievalBrowseBags=retrievalStock.Select(x=>x.Item).Distinct().ToDictionary(x=>x,RetrievalHeld);retrievalStockComplete=complete;
        retrievalStockNotice=(allagan?"Allagan Tools + saved inventory":"Companion saved inventory")+" · "+names.Count+" known retainers · "+(complete?"recorded stock":"partial recorded stock")+notice;
        retrievalRefreshPending=false;retrievalPreviewDirty=true;
    }
    private void DrawRetrievalBrowse()
    {
        if(ImGui.Button("Refresh inventory"))RetrievalTry(RefreshRetrievalStock);ImGui.SameLine();ImGui.Checkbox("Group by retainer",ref retrievalBrowseGroups);ImGui.InputText("Filter inventory",ref retrievalBrowseSearch,100);
        ImGui.TextWrapped(retrievalStockNotice);ImGui.SetNextItemWidth(120);ImGui.InputInt("Add quantity",ref retrievalQuantity);retrievalQuantity=Math.Clamp(retrievalQuantity,1,RetainerRetrieval.MaxQuantity);
        var rows=retrievalStock.Where(x=>x.Name.Contains(retrievalBrowseSearch,StringComparison.OrdinalIgnoreCase));
        void Draw(IEnumerable<RetrieverStock> items,string key){if(!ImGui.BeginTable("Inventory##"+key,4,ImGuiTableFlags.RowBg|ImGuiTableFlags.Resizable))return;foreach(var t in new[]{"Item · click to add","Retainer stock","Bags at refresh","Holder"})ImGui.TableSetupColumn(t);ImGui.TableHeadersRow();foreach(var g in items.GroupBy(x=>x.Item).OrderBy(g=>g.First().Name).Take(200)){var item=g.First();ImGui.TableNextRow();ImGui.TableNextColumn();DrawRetrievalIcon(item.Item);if(ImGui.Selectable(item.Name+"##"+item.Item))RetrievalTry(()=>AddRetrieval([new(item.Item,item.Name,retrievalQuantity)]));if(ImGui.IsItemHovered())ImGui.SetTooltip("NQ and HQ combined · "+(item.At.HasValue?"recorded "+item.At.Value.ToLocalTime().ToString("g"):"Allagan Tools cache; observation time unavailable"));ImGui.TableNextColumn();ImGui.TextUnformatted(g.Sum(x=>x.Quantity).ToString());ImGui.TableNextColumn();ImGui.TextUnformatted(retrievalBrowseBags.GetValueOrDefault(item.Item).ToString());ImGui.TableNextColumn();ImGui.TextUnformatted(string.Join(", ",g.Select(x=>x.Holder).Distinct()));}ImGui.EndTable();}
        if(retrievalBrowseGroups){foreach(var g in rows.GroupBy(x=>x.Retainer))if(ImGui.CollapsingHeader(g.First().Holder+"##"+g.Key))Draw(g,g.Key.ToString());}else Draw(rows,"all");
    }
}
