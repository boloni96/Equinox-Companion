using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly HashSet<uint> browseCategories=[],browseSelected=[];
    private bool browseAllCategories=true,browseDirty=true,browseTransferAll;
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
    private void UseBrowseSelection(bool entrust)
    {
        if(craftingWithdrawalPrevious==null){
            craftingWithdrawalPrevious=new(retrievalName,retrievalItems.ToArray(),retrievalMultiplier,retrievalIncludesSubcrafts,Mode:retrievalMode,RecipeChoices:new(retrievalRecipeOverrides),Quality:retrievalQuality,DestinationRetainer:retrievalSendTo,SendCategories:retrievalSendCategories.ToArray(),CategoryWithdrawQuantity:retrievalCategoryWithdraw,CategoryKeepQuantity:retrievalCategoryKeep,FilterPlanCategories:retrievalFilterCategories,Entrust:retrievalEntrust);
            craftingWithdrawalPreviousEditing=retrievalEditingPreset;
        }
        SelectRetrievalPreset(InventoryBrowseSelection.Plan(BrowseSelectedEntries(),entrust,retrievalQuality));
        retrievalEditingPreset=null;retrievalTransferAll=browseTransferAll;
        if(entrust)SendRetrievalList(false);else StartRetrieval(false);
    }
    private int RetrievalTradeFilter=>config.WithdrawUntradableOnly?2:config.WithdrawTradableOnly?1:0;
    private void DrawWithdrawTradableOption()
    {
        var value=RetrievalTradeFilter;ImGui.SetNextItemWidth(165);
        if(ImGui.Combo("##WithdrawalTradeFilter",ref value,"Any items\0Tradable only\0Untradable only\0")){config.WithdrawTradableOnly=value==1;config.WithdrawUntradableOnly=value==2;retrievalPreviewDirty=true;SaveConfiguration();}
        MarketHelp("Withdrawal filter: Any items, Tradable only, or Untradable only. Checks actual stacks, including bound gear and collectibles. Applies to retainer withdrawals and matching bag counts; does not change Entrust. Your choice is remembered.");
    }
    private void DrawRetrievalBrowse()
    {
        if(browseOwner!=Player.ContentId){browseOwner=Player.ContentId;browseSelected.Clear();browseSource=null;browseDirty=true;}
        if(ImGui.BeginTable("BrowseFilters",2,ImGuiTableFlags.SizingStretchSame)){
            ImGui.TableNextRow();ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);
            if(ImGui.InputTextWithHint("##InventorySearch","Search inventory…",ref retrievalBrowseSearch,100))browseDirty=true;
            ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);
            if(DrawStorageCategoryPicker("##InventoryCategories",browseCategories,browseAllCategories?"All categories":"None selected")){browseAllCategories=false;browseDirty=true;}
            ImGui.TableNextRow();ImGui.TableNextColumn();var quality=(int)retrievalQuality;ImGui.SetNextItemWidth(-1);
            if(ImGui.Combo("##BrowseQuality",ref quality,"Any quality\0NQ only\0HQ only\0")){retrievalQuality=(RetrievalQuality)quality;retrievalPreviewDirty=true;}
            ImGui.TableNextColumn();DrawWithdrawTradableOption();ImGui.EndTable();
        }
        if(!ReferenceEquals(browseSource,retrievalStock)){
            browseSource=retrievalStock;browseAllRows=BrowseRows(retrievalStock);
            browseItemCategories=browseAllRows.ToDictionary(x=>x.Item,x=>OrganizeCategory(x.Item));
            browseSelected.IntersectWith(browseAllRows.Select(x=>x.Item));browseDirty=true;
        }
        if(browseDirty){
            var filtered=retrievalStock.Where(r=>InventoryBrowseSelection.Matches(r.Name,browseItemCategories.GetValueOrDefault(r.Item),retrievalBrowseSearch,browseCategories,browseAllCategories)).ToArray();
            browseMatchedRows=BrowseRows(filtered);browseHolders=filtered.GroupBy(x=>x.Retainer).Select(g=>(g.Key,g.First().Holder,BrowseRows(g))).ToArray();browseDirty=false;
        }
        ImGui.Checkbox("All available",ref browseTransferAll);ImGui.SameLine();ImGui.BeginDisabled(browseTransferAll);
        ImGui.SetNextItemWidth(70);ImGui.InputInt("##BrowseQuantity",ref retrievalQuantity,0,0);retrievalQuantity=Math.Clamp(retrievalQuantity,1,RetainerRetrieval.MaxQuantity);
        ImGui.EndDisabled();MarketHelp("Withdraw Quantity each, or All available selected items. Entrust moves matching bag quantities and respects Keep amounts.");ImGui.SameLine();
        if(ImGui.SmallButton("Select all"))browseSelected.UnionWith(browseMatchedRows.Select(x=>x.Item));ImGui.SameLine();
        if(ImGui.SmallButton("Clear"))browseSelected.Clear();ImGui.SameLine();
        if(ImGui.SmallButton("Options…"))ImGui.OpenPopup("BrowseOptions");
        if(ImGui.BeginPopup("BrowseOptions")){
            if(ImGui.SmallButton("Refresh inventory"))RetrievalTry(RefreshRetrievalStock);
            ImGui.Checkbox("Group by retainer",ref retrievalBrowseGroups);
            if(ImGui.SmallButton("Reset filters")){retrievalBrowseSearch="";browseCategories.Clear();browseAllCategories=true;browseDirty=true;}
            ImGui.TextWrapped(retrievalStockNotice);ImGui.EndPopup();
        }
        ImGui.BeginDisabled(browseSelected.Count==0);
        if(ImGui.SmallButton("Withdraw now"))RetrievalTry(()=>UseBrowseSelection(false));ImGui.SameLine();
        if(ImGui.SmallButton("Entrust now"))RetrievalTry(()=>UseBrowseSelection(true));
        MarketHelp("Use selected items immediately without saving a plan. Entrust moves matching bag quantities to all retainers; Withdraw uses Quantity each or All available and the current quality setting. Your previous list can be restored.");
        ImGui.SameLine();if(ImGui.SmallButton("Plan…"))ImGui.OpenPopup("BrowsePlanActions");
        if(ImGui.BeginPopup("BrowsePlanActions")){
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
        ImGui.EndPopup();}
        ImGui.EndDisabled();
        ImGui.SameLine();ImGui.TextDisabled($"{browseSelected.Count} / {browseMatchedRows.Length}");
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
        var rowHeight=Math.Max(ImGui.GetFrameHeight(),ImGui.GetTextLineHeight()+4)+ImGui.GetStyle().CellPadding.Y*2;
        var clip=ImGui.ImGuiListClipper();clip.Begin(rows.Length,rowHeight);
        while(clip.Step())for(var i=clip.DisplayStart;i<clip.DisplayEnd;i++){
            var row=rows[i];ImGui.PushID((int)row.Item);ImGui.TableNextRow(ImGuiTableRowFlags.None,rowHeight);ImGui.TableNextColumn();
            ImGui.BeginGroup();var selected=browseSelected.Contains(row.Item);if(ImGui.Checkbox("##Selected",ref selected)){if(selected)browseSelected.Add(row.Item);else browseSelected.Remove(row.Item);}ImGui.SameLine();DrawRetrievalIcon(row.Item);
            if(ImGui.Selectable(row.Name,selected)){if(selected)browseSelected.Remove(row.Item);else browseSelected.Add(row.Item);}
            ImGui.EndGroup();if(ImGui.IsItemHovered()&&!ImGui.GetIO().WantTextInput){var io=ImGui.GetIO();if(io.KeyShift&&!io.KeyAlt)browseSelected.Add(row.Item);else if(io.KeyAlt&&!io.KeyShift)browseSelected.Remove(row.Item);}
            MarketHelp($"Shift hover: select · Alt hover: remove\n{row.Quantity-row.Hq:N0} NQ · {row.Hq:N0} HQ · recorded inventory\nSelect once across all retainers. Plan quality settings control actual withdrawals.");
            ImGui.TableNextColumn();ImGui.TextUnformatted(row.Quantity.ToString());ImGui.TableNextColumn();ImGui.TextUnformatted(retrievalBrowseBags.GetValueOrDefault(row.Item).ToString());ImGui.TableNextColumn();ImGui.TextUnformatted(row.Holders);ImGui.PopID();
        }
        clip.End();clip.Destroy();ImGui.EndTable();
    }
}


