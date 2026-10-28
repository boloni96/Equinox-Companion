using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly HashSet<uint> browseCategories=[],browseSelected=[];
    private bool browseAllCategories=true,browseDirty=true;
    private ulong browseOwner;
    private RetrieverStock[]? browseSource;
    private BrowseRow[] browseAllRows=[],browseMatchedRows=[];
    private (ulong Id,string Name,BrowseRow[] Rows)[] browseHolders=[];
    private Dictionary<uint,uint> browseItemCategories=[];
    private sealed record BrowseRow(uint Item,string Name,int Quantity,string Holders,int Hq);
    private static BrowseRow[] BrowseRows(IEnumerable<RetrieverStock> stocks)=>stocks.GroupBy(x=>x.Item)
        .Select(g=>new BrowseRow(g.Key,g.First().Name,g.Sum(x=>x.Quantity),string.Join(", ",g.Select(x=>x.Holder).Distinct()),g.Where(x=>x.Hq).Sum(x=>x.Quantity)))
        .OrderBy(x=>x.Name).ThenBy(x=>x.Item).ToArray();
    private RetrievalEntry[] BrowseSelectedEntries()=>InventoryBrowseSelection.Entries(browseAllRows.Select(r=>new RetrievalEntry(r.Item,r.Name,1)),browseSelected,retrievalQuantity);
    private void DrawRetrievalBrowse()
    {
        if(browseOwner!=Player.ContentId){browseOwner=Player.ContentId;browseSelected.Clear();browseSource=null;browseDirty=true;}
        if(ImGui.Button("Refresh inventory"))RetrievalTry(RefreshRetrievalStock);ImGui.SameLine();ImGui.Checkbox("Group by retainer",ref retrievalBrowseGroups);
        MarketHelp(retrievalStockNotice+". Selection uses item IDs; NQ/HQ and duplicate stacks share one selection.");
        ImGui.SetNextItemWidth(Math.Max(130,ImGui.GetContentRegionAvail().X-115));
        if(ImGui.InputText("##InventorySearch",ref retrievalBrowseSearch,100))browseDirty=true;
        MarketHelp("Filter inventory by item name.");ImGui.SameLine();
        if(ImGui.SmallButton("Reset filters")){retrievalBrowseSearch="";browseCategories.Clear();browseAllCategories=true;browseDirty=true;}
        if(DrawStorageCategoryPicker("Inventory categories",browseCategories,browseAllCategories?"All categories":"None selected")){browseAllCategories=false;browseDirty=true;}
        if(!ReferenceEquals(browseSource,retrievalStock)){
            browseSource=retrievalStock;browseAllRows=BrowseRows(retrievalStock);
            browseItemCategories=browseAllRows.ToDictionary(x=>x.Item,x=>OrganizeCategory(x.Item));
            browseSelected.IntersectWith(browseAllRows.Select(x=>x.Item));browseDirty=true;
        }
        if(browseDirty){
            var filtered=retrievalStock.Where(r=>InventoryBrowseSelection.Matches(r.Name,browseItemCategories.GetValueOrDefault(r.Item),retrievalBrowseSearch,browseCategories,browseAllCategories)).ToArray();
            browseMatchedRows=BrowseRows(filtered);browseHolders=filtered.GroupBy(x=>x.Retainer).Select(g=>(g.Key,g.First().Holder,BrowseRows(g))).ToArray();browseDirty=false;
        }
        if(ImGui.SmallButton("Select all matching"))browseSelected.UnionWith(browseMatchedRows.Select(x=>x.Item));
        MarketHelp("Selects every matching item, including collapsed retainers and rows below the scroll area. Existing selections remain selected.");ImGui.SameLine();
        if(ImGui.SmallButton("Clear selection"))browseSelected.Clear();ImGui.SameLine();ImGui.TextDisabled($"{browseSelected.Count} selected · {browseMatchedRows.Length} matching");
        MarketHelp("Selections survive filter changes. Clear selection removes all selections, including hidden ones. Reset filters does not clear your selection.");
        ImGui.SetNextItemWidth(95);ImGui.InputInt("Quantity each",ref retrievalQuantity);retrievalQuantity=Math.Clamp(retrievalQuantity,1,RetainerRetrieval.MaxQuantity);
        MarketHelp("Quantity per item when adding to a withdrawal/crafting list. Entrust selections move all matching bag quantities, respecting Keep amounts.");
        ImGui.BeginDisabled(browseSelected.Count==0);
        if(ImGui.SmallButton("Add selected to plan"))RetrievalTry(()=>{
            var entries=BrowseSelectedEntries();
            if(retrievalEntrust!=null)entries=entries.Where(e=>retrievalItems.All(x=>x.ItemId!=e.ItemId)).Select(e=>e with{Quantity=1}).ToArray();
            AddRetrieval(entries);
        });ImGui.SameLine();
        if(ImGui.SmallButton("New plan…"))ImGui.OpenPopup("InventoryNewPlan");
        if(ImGui.BeginPopup("InventoryNewPlan")){
            if(ImGui.MenuItem("Entrust selected items"))RetrievalTry(()=>SaveNewRetrievalPlan(InventoryBrowseSelection.Plan(BrowseSelectedEntries().Select(e=>e with{Quantity=1}).ToArray(),true,retrievalQuality)));
            MarketHelp("Save an Entrust selection: all matching quantities from bags, without recipe expansion. Preserves your current working plan separately.");
            if(ImGui.MenuItem("Withdraw selected items"))RetrievalTry(()=>SaveNewRetrievalPlan(InventoryBrowseSelection.Plan(BrowseSelectedEntries(),false,retrievalQuality)));
            MarketHelp("Save the listed items and Quantity each as a withdrawal plan; no ingredient expansion. Preserves your current working plan separately.");
            ImGui.EndPopup();
        }
        ImGui.EndDisabled();
        ImGui.BeginChild("InventoryBrowserRows",new(0,Math.Max(80,ImGui.GetContentRegionAvail().Y-8)),false);
        if(browseMatchedRows.Length==0)ImGui.TextDisabled("No inventory items match these filters.");
        if(retrievalBrowseGroups){foreach(var holder in browseHolders)if(ImGui.CollapsingHeader(holder.Name+"##"+holder.Id))DrawInventoryBrowseRows(holder.Rows,holder.Id.ToString());}
        else DrawInventoryBrowseRows(browseMatchedRows,"all");
        ImGui.EndChild();
    }
    private void DrawInventoryBrowseRows(BrowseRow[] rows,string key)
    {
        if(!ImGui.BeginTable("Inventory##"+key,4,ImGuiTableFlags.RowBg|ImGuiTableFlags.Resizable))return;
        foreach(var title in new[]{"Item · select","Retainer stock","Bags at refresh","Holder"})ImGui.TableSetupColumn(title);ImGui.TableHeadersRow();
        var clip=ImGui.ImGuiListClipper();clip.Begin(rows.Length);
        while(clip.Step())for(var i=clip.DisplayStart;i<clip.DisplayEnd;i++){
            var row=rows[i];ImGui.PushID((int)row.Item);ImGui.TableNextRow();ImGui.TableNextColumn();
            var selected=browseSelected.Contains(row.Item);if(ImGui.Checkbox("##Selected",ref selected)){if(selected)browseSelected.Add(row.Item);else browseSelected.Remove(row.Item);}ImGui.SameLine();DrawRetrievalIcon(row.Item);
            if(ImGui.Selectable(row.Name,selected)){if(selected)browseSelected.Remove(row.Item);else browseSelected.Add(row.Item);}
            MarketHelp($"{row.Quantity-row.Hq:N0} NQ · {row.Hq:N0} HQ · recorded inventory\nSelect once across all retainers. Plan quality settings control actual withdrawals.");
            ImGui.TableNextColumn();ImGui.TextUnformatted(row.Quantity.ToString());ImGui.TableNextColumn();ImGui.TextUnformatted(retrievalBrowseBags.GetValueOrDefault(row.Item).ToString());ImGui.TableNextColumn();ImGui.TextUnformatted(row.Holders);ImGui.PopID();
        }
        clip.End();clip.Destroy();ImGui.EndTable();
    }
}
