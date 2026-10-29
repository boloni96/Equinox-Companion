using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private string myInventorySearch="";
    private readonly HashSet<uint> myInventoryCategories=[];
    private readonly Dictionary<uint,int> myInventorySelected=[];
    private bool myInventoryAllCategories=true;
    private RetrievalQuality myInventoryQuality;
    private ulong myInventoryCharacter,myInventoryDestination=StorageAutoRouting.AllRetainers;
    private DateTimeOffset myInventoryRefresh;
    private ChestStack[] myInventoryStock=[];
    private string MyInventoryName(uint id)=>retrievalCatalog?.Names.GetValueOrDefault(id)??id.ToString();
    private bool MyInventoryMatches(uint id)=>MyInventoryName(id).Contains(myInventorySearch,StringComparison.OrdinalIgnoreCase)&&(myInventoryAllCategories||myInventoryCategories.Contains(OrganizeCategory(id)));
    private unsafe ChestStack[] ReadMyInventory()=>OrganizerInventory(false,false).Concat(OrganizerInventory(false,true)).Where(s=>s.Item>0&&s.Quantity>0).ToArray();
    private Dictionary<uint,int> MyInventoryAmounts(IEnumerable<ChestStack> stock)=>TemporaryInventorySelection.Amounts(stock,myInventorySelected,myInventoryQuality,MyInventoryMatches);
    private unsafe void EntrustMyInventory()
    {
        var amounts=MyInventoryAmounts(ReadMyInventory());
        if(amounts.Count==0)throw new InvalidOperationException("Select matching items from your current inventory first.");
        StartStorageRouting([new(StorageRouting.Bags,myInventoryDestination,[],amounts.Keys.ToArray(),Quality:myInventoryQuality,Amounts:amounts)],true,true);
    }
    private void DrawMyInventory()
    {
        if(!Player.IsLoaded){ImGui.TextDisabled("Log in to see your inventory.");return;}
        if(myInventoryCharacter!=Player.ContentId){myInventoryCharacter=Player.ContentId;myInventorySelected.Clear();myInventoryStock=[];myInventoryRefresh=default;myInventoryDestination=StorageAutoRouting.AllRetainers;}
        if(DateTimeOffset.UtcNow>=myInventoryRefresh){myInventoryStock=ReadMyInventory();myInventoryRefresh=DateTimeOffset.UtcNow.AddSeconds(1);myInventorySelected.Keys.Where(id=>!myInventoryStock.Any(s=>s.Item==id)).ToList().ForEach(id=>myInventorySelected.Remove(id));}
        if(ImGui.BeginTable("MyInventoryFilters",2,ImGuiTableFlags.SizingStretchSame)){
            ImGui.TableNextRow();ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);ImGui.InputTextWithHint("##MyInventorySearch","Search my bags…",ref myInventorySearch,100);
            ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);if(DrawStorageCategoryPicker("##MyInventoryCategories",myInventoryCategories,myInventoryAllCategories?"All categories":"None selected"))myInventoryAllCategories=false;
            ImGui.TableNextRow();ImGui.TableNextColumn();var quality=(int)myInventoryQuality;ImGui.SetNextItemWidth(-1);if(ImGui.Combo("##MyInventoryQuality",ref quality,"Any quality\0NQ only\0HQ only\0"))myInventoryQuality=(RetrievalQuality)quality;
            ImGui.TableNextColumn();if(ImGui.SmallButton("Reset filters")){myInventorySearch="";myInventoryCategories.Clear();myInventoryAllCategories=true;myInventoryQuality=RetrievalQuality.Any;}ImGui.EndTable();
        }
        var rows=myInventoryStock.Where(s=>RetainerRetrieval.AllowsQuality(s.Hq,myInventoryQuality)&&MyInventoryMatches(s.Item)).GroupBy(s=>s.Item).Select(g=>(Id:g.Key,Name:MyInventoryName(g.Key),Quantity:g.Sum(s=>s.Quantity))).OrderBy(x=>x.Name).ToArray();
        if(ImGui.SmallButton("Select all"))foreach(var row in rows)myInventorySelected.TryAdd(row.Id,row.Quantity);ImGui.SameLine();
        if(ImGui.SmallButton("Clear selection"))myInventorySelected.Clear();ImGui.SameLine();if(ImGui.SmallButton("Refresh"))myInventoryRefresh=default;
        var names=retrievalKnownRetainers.GetValueOrDefault(Player.ContentId)??new Dictionary<ulong,string>();
        ImGui.SetNextItemWidth(220);
        if(ImGui.BeginCombo("To retainer",myInventoryDestination==StorageAutoRouting.AllRetainers?(config.UseRetainerAssignments.Contains(Player.ContentId)?"Assignments":"All retainers"):names.GetValueOrDefault(myInventoryDestination,"Choose retainer"))){
            if(ImGui.Selectable("All retainers / Assignments",myInventoryDestination==StorageAutoRouting.AllRetainers))myInventoryDestination=StorageAutoRouting.AllRetainers;
            foreach(var row in names)if(ImGui.Selectable(row.Value+"##"+row.Key,myInventoryDestination==row.Key))myInventoryDestination=row.Key;
            ImGui.EndCombo();
        }
        if(myInventoryDestination==StorageAutoRouting.AllRetainers){DrawUseRetainerAssignments();ImGui.SameLine();DrawAssignmentEditor();}
        var amounts=MyInventoryAmounts(myInventoryStock);
        ImGui.BeginDisabled(amounts.Count==0);if(ImGui.SmallButton("Entrust selected"))RetrievalTry(EntrustMyInventory);ImGui.SameLine();
        if(ImGui.SmallButton("Add selected to plan"))RetrievalTry(()=>AddRetrieval(amounts.Select(x=>new RetrievalEntry(x.Key,MyInventoryName(x.Key),x.Value)).ToArray()));ImGui.EndDisabled();
        ImGui.TextDisabled($"{amounts.Count} selected matching items · bags + crystals · temporary selection");
        MarketHelp("Entrust selected uses only checked items matching these filters and their quantities. Assignments choose destinations only. Your loaded plan is unchanged. Open the summoning bell to entrust. Shift-hover selects; Alt-hover removes.");
        if(ImGui.BeginChild("MyInventoryRows",new(0,Math.Max(80,ImGui.GetContentRegionAvail().Y-8)),false)){
            if(ImGui.BeginTable("MyInventoryItems",3,ImGuiTableFlags.RowBg|ImGuiTableFlags.SizingStretchProp)){
                ImGui.TableSetupColumn("Item",ImGuiTableColumnFlags.WidthStretch,3);ImGui.TableSetupColumn("In bags");ImGui.TableSetupColumn("Entrust quantity");ImGui.TableHeadersRow();
                var height=Math.Max(ImGui.GetFrameHeight(),ImGui.GetTextLineHeight()+4)+ImGui.GetStyle().CellPadding.Y*2;
                var clip=ImGui.ImGuiListClipper();clip.Begin(rows.Length,height);
                while(clip.Step())for(var i=clip.DisplayStart;i<clip.DisplayEnd;i++){
                    var row=rows[i];ImGui.PushID((int)row.Id);ImGui.TableNextRow(ImGuiTableRowFlags.None,height);ImGui.TableNextColumn();
                    var selected=myInventorySelected.ContainsKey(row.Id);if(ImGui.Checkbox("##Selected",ref selected)){if(selected)myInventorySelected[row.Id]=row.Quantity;else myInventorySelected.Remove(row.Id);}ImGui.SameLine();DrawRetrievalIcon(row.Id);ImGui.TextUnformatted(row.Name);
                    if(ImGui.IsItemHovered()&&!ImGui.GetIO().WantTextInput){if(ImGui.GetIO().KeyShift&&!ImGui.GetIO().KeyAlt)myInventorySelected.TryAdd(row.Id,row.Quantity);else if(ImGui.GetIO().KeyAlt&&!ImGui.GetIO().KeyShift)myInventorySelected.Remove(row.Id);}
                    ImGui.TableNextColumn();ImGui.TextUnformatted(row.Quantity.ToString());ImGui.TableNextColumn();
                    if(myInventorySelected.TryGetValue(row.Id,out var quantity)){quantity=Math.Clamp(quantity,1,row.Quantity);ImGui.SetNextItemWidth(-1);ImGui.InputInt("##Amount",ref quantity,0,0);myInventorySelected[row.Id]=Math.Clamp(quantity,1,row.Quantity);}else ImGui.TextDisabled("—");ImGui.PopID();
                }
                clip.End();clip.Destroy();ImGui.EndTable();
            }
            if(rows.Length==0)ImGui.TextDisabled("No bag items match these filters.");
        }ImGui.EndChild();
    }
}
