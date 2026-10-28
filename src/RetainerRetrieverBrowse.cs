using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private sealed record RetrieverStock(ulong Retainer,string Holder,uint Item,string Name,int Quantity,DateTimeOffset? At);
    private Dictionary<uint,int> retrievalBrowseBags=[];
    private RetrieverStock[] retrievalStock=[];private bool retrievalStockComplete;private string retrievalStockNotice="Refresh at a summoning bell to identify this character's retainers.";
    private string retrievalBrowseSearch="";private bool retrievalBrowseGroups=true;
    private void RefreshRetrievalStock()
    {
        var roster=ReadSellerRetainers();var owner=Player.ContentId.ToString();var stocks=new List<RetrieverStock>();var complete=roster.Length>0;
        var latest=config.Discoveries.Where(e=>e.Actor.ContentId==owner&&e.Storage?.Key.StartsWith("retainer:")==true).GroupBy(e=>e.Storage!.Key).ToDictionary(g=>g.Key,g=>g.MaxBy(e=>e.At)!);
        var allagan=Pi.InstalledPlugins.Any(x=>x.IsLoaded&&x.InternalName=="InventoryTools")&&Pi.GetIpcSubscriber<bool>("AllaganTools.IsInitialized").InvokeFunc()&&Pi.GetIpcSubscriber<ulong>("AllaganTools.CurrentCharacter").InvokeFunc()==Player.ContentId;
        foreach(var retainer in roster){
            var rows=latest.Values.Where(e=>e.Storage!.Key.StartsWith("retainer:"+retainer.Id+":")).Where(e=>int.TryParse(e.Storage!.Key.Split(':').Last(),out var t)&&(t is >=10000 and <=10006 or 12001)).ToArray();
            if(allagan){var raw=Pi.GetIpcSubscriber<ulong,HashSet<ulong[]>>("AllaganTools.GetCharacterItems").InvokeFunc(retainer.Id);var good=raw.Where(x=>x.Length>3&&(x[0] is >=10000 and <=10006 or 12001)&&x[2] is >0 and <=uint.MaxValue&&x[3] is >0 and <=RetainerRetrieval.MaxQuantity).ToArray();if(raw.Count==0)complete=false;foreach(var group in good.GroupBy(x=>(uint)x[2]))stocks.Add(new(retainer.Id,retainer.Name,group.Key,retrievalCatalog?.Names.GetValueOrDefault(group.Key)??group.Key.ToString(),checked((int)group.Sum(x=>(long)x[3])),null));}
            else{if(!Enumerable.Range(10000,7).Append(12001).All(t=>rows.Any(e=>e.Storage!.Key=="retainer:"+retainer.Id+":"+t)))complete=false;foreach(var group in rows.SelectMany(e=>(e.Storage!.Stacks??[]).Select(s=>(s,e.At))).GroupBy(x=>x.s.ItemId))stocks.Add(new(retainer.Id,retainer.Name,group.Key,group.First().s.Name,group.Sum(x=>x.s.Quantity),group.Min(x=>x.At)));}
        }
        retrievalStock=stocks.ToArray();retrievalBrowseBags=retrievalStock.Select(x=>x.Item).Distinct().ToDictionary(x=>x,RetrievalHeld);retrievalStockComplete=complete;retrievalStockNotice=(allagan?"Allagan Tools recorded inventory":"Companion recorded inventory")+" · "+roster.Length+" retainers · "+(complete?"all have recorded stock":"some stock is unknown")+" · live quantities checked during retrieval";
    }
    private void DrawRetrievalBrowse()
    {
        if(ImGui.Button("Refresh inventory"))RetrievalTry(RefreshRetrievalStock);ImGui.SameLine();ImGui.Checkbox("Group by retainer",ref retrievalBrowseGroups);ImGui.InputText("Filter inventory",ref retrievalBrowseSearch,100);
        ImGui.TextWrapped(retrievalStockNotice);ImGui.SetNextItemWidth(120);ImGui.InputInt("Add quantity",ref retrievalQuantity);retrievalQuantity=Math.Clamp(retrievalQuantity,1,RetainerRetrieval.MaxQuantity);
        var rows=retrievalStock.Where(x=>x.Name.Contains(retrievalBrowseSearch,StringComparison.OrdinalIgnoreCase));
        void Draw(IEnumerable<RetrieverStock> items,string key){if(!ImGui.BeginTable("Inventory##"+key,4,ImGuiTableFlags.RowBg|ImGuiTableFlags.Resizable))return;foreach(var t in new[]{"Item · click to add","Retainer stock","Bags at refresh","Holder"})ImGui.TableSetupColumn(t);ImGui.TableHeadersRow();foreach(var g in items.GroupBy(x=>x.Item).OrderBy(g=>g.First().Name).Take(200)){var item=g.First();ImGui.TableNextRow();ImGui.TableNextColumn();if(ImGui.Selectable(item.Name+"##"+item.Item))RetrievalTry(()=>AddRetrieval([new(item.Item,item.Name,retrievalQuantity)]));if(ImGui.IsItemHovered())ImGui.SetTooltip("NQ and HQ combined · "+(item.At.HasValue?"recorded "+item.At.Value.ToLocalTime().ToString("g"):"Allagan Tools cache; observation time unavailable"));ImGui.TableNextColumn();ImGui.TextUnformatted(g.Sum(x=>x.Quantity).ToString());ImGui.TableNextColumn();ImGui.TextUnformatted(retrievalBrowseBags.GetValueOrDefault(item.Item).ToString());ImGui.TableNextColumn();ImGui.TextUnformatted(string.Join(", ",g.Select(x=>x.Holder).Distinct()));}ImGui.EndTable();}
        if(retrievalBrowseGroups){foreach(var g in rows.GroupBy(x=>x.Retainer))if(ImGui.CollapsingHeader(g.First().Holder+"##"+g.Key))Draw(g,g.Key.ToString());}else Draw(rows,"all");
    }
}
