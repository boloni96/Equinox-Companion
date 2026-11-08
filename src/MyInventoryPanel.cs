using FFXIVClientStructs.FFXIV.Client.Game;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private string myInventorySearch="";
    private readonly HashSet<uint> myInventoryCategories=[];
    private readonly Dictionary<uint,int> myInventorySelected=[];
    private bool myInventoryAllCategories=true,myInventoryArmoury;
    private RetrievalQuality myInventoryQuality;private int myInventoryTradeFilter;
    private ulong myInventoryCharacter,myInventoryDestination=StorageAutoRouting.AllRetainers;
    private DateTimeOffset myInventoryRefresh;
    private ChestStack[] myInventoryStock=[];
    private readonly UiProjectionCache<(uint Id,string Name,int Quantity)[]> myInventoryRowsCache=new(250);
    private string MyInventoryName(uint id)=>retrievalCatalog?.Names.GetValueOrDefault(id)??id.ToString();
    private bool MyInventoryMatches(uint id)=>MyInventoryName(id).Contains(myInventorySearch,StringComparison.OrdinalIgnoreCase)&&(myInventoryAllCategories||(myInventoryDestination==GlamourStorageDestination?GearCategoryMatches(id,myInventoryCategories,false):myInventoryCategories.Contains(OrganizeCategory(id))))&&(myInventoryDestination!=GlamourStorageDestination||(GearStorageItemAllowed(id)&&GearSourceAllowed(id)));
    private unsafe bool InventoryTradeFilterAllows(ChestStack stack,int filter)
    {
        if(filter==0||stack.Item==0)return true;
        var manager=InventoryManager.Instance();if(manager==null)return false;
        var container=manager->GetInventoryContainer((InventoryType)stack.Container);
        if(container==null||!container->IsLoaded||container->Items==null||stack.Slot<0||stack.Slot>=container->Size)return false;
        var slot=container->Items[stack.Slot];
        return slot.ItemId==stack.Item&&RetrievalAllowsTradeFilter(slot,filter);
    }
    private unsafe ChestStack[] ReadMyInventory()=>OrganizerInventory(false,false).Concat(OrganizerInventory(false,true)).Concat(myInventoryDestination==GlamourStorageDestination&&myInventoryArmoury?ReadGearArmoury():Array.Empty<ChestStack>()).Where(s=>s.Item>0&&s.Quantity>0&&InventoryTradeFilterAllows(s,myInventoryTradeFilter)).ToArray();
    private Dictionary<uint,int> MyInventoryAmounts(IEnumerable<ChestStack> stock)=>TemporaryInventorySelection.Amounts(stock.Where(s=>!OrganizerGearProtected(s.Item)),myInventorySelected,myInventoryQuality,MyInventoryMatches);
    private unsafe void EntrustMyInventory()
    {
        RefreshGearSetProtection();
        if(myInventoryDestination==GlamourStorageDestination){StartGearStorage(ReadMyInventory().Where(s=>myInventorySelected.ContainsKey(s.Item)&&!OrganizerGearProtected(s.Item)&&MyInventoryMatches(s.Item)&&RetainerRetrieval.AllowsQuality(s.Hq,myInventoryQuality)).Select(s=>s.Item),false,myInventoryQuality,myInventoryTradeFilter);return;}
        var amounts=MyInventoryAmounts(ReadMyInventory());
        if(amounts.Count==0)throw new InvalidOperationException("Select matching items from your current inventory first.");
        StartStorageRouting([new(StorageRouting.Bags,myInventoryDestination,[],amounts.Keys.ToArray(),Quality:myInventoryQuality,Amounts:amounts)],true,true,myInventoryTradeFilter);
    }
    private void EnsureMyInventoryCharacter()
    {
        if(myInventoryCharacter==Player.ContentId)return;
        myInventoryCharacter=Player.ContentId;myInventorySelected.Clear();myInventoryStock=[];myInventoryRefresh=default;myInventoryDestination=StorageAutoRouting.AllRetainers;
    }
    private void DrawMyInventory()
    {
        if(!Player.IsLoaded){ImGui.TextDisabled("Log in to see your inventory.");return;}
        EnsureMyInventoryCharacter();
        if(DateTimeOffset.UtcNow>=myInventoryRefresh){myInventoryStock=ReadMyInventory();myInventoryRefresh=DateTimeOffset.UtcNow.AddSeconds(1);myInventorySelected.Keys.Where(id=>!myInventoryStock.Any(s=>s.Item==id)).ToList().ForEach(id=>myInventorySelected.Remove(id));}
        ImGui.BeginDisabled(gearRouting||armoireRunning||craftingWithdrawalPending||retrievalRun.Running||organizeRunning||chestRunning);
        if(ImGui.BeginTable("MyInventoryFilters",2,ImGuiTableFlags.SizingStretchSame)){
            ImGui.TableNextRow();ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);ImGui.InputTextWithHint("##MyInventorySearch","Search my bags…",ref myInventorySearch,100);
            ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);if(DrawStorageCategoryPicker("##MyInventoryCategories",myInventoryCategories,myInventoryAllCategories?"All categories":"None selected",myInventoryDestination==GlamourStorageDestination?GearCategoryGroups(false):null))myInventoryAllCategories=false;
            ImGui.TableNextRow();ImGui.TableNextColumn();var quality=(int)myInventoryQuality;ImGui.SetNextItemWidth(-1);if(ImGui.Combo("##MyInventoryQuality",ref quality,"Any quality\0NQ only\0HQ only\0"))myInventoryQuality=(RetrievalQuality)quality;
            ImGui.TableNextColumn();if(ImGui.SmallButton("Reset filters")){myInventorySearch="";myInventoryCategories.Clear();myInventoryAllCategories=true;myInventoryQuality=RetrievalQuality.Any;myInventoryTradeFilter=0;gearStorageKind=gearAppearance=0;gearSourceGroup=gearSourceSubgroup=0;gearOrigin="";myInventoryRefresh=default;}
            ImGui.TableNextRow();ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);if(ImGui.Combo("##MyInventoryTradeFilter",ref myInventoryTradeFilter,"Any items\0Tradable only\0Untradable only\0Bound only\0Unbound only\0"))myInventoryRefresh=default;
            MarketHelp("Filters actual copies, including bound gear and collectibles. Entrust selected uses this filter too.");ImGui.EndTable();
        }
        if(myInventoryDestination==GlamourStorageDestination){DrawGearAppearancePicker(ref gearAppearance);if(ImGui.CollapsingHeader("Storage eligibility / source filters"+((gearStorageKind!=0||gearSourceGroup!=0||gearSourceSubgroup!=0||gearOrigin.Length>0)?" · active":"")))DrawGearSourcePicker(false);}
        if(myInventoryRefresh==default){myInventoryStock=ReadMyInventory();myInventoryRefresh=DateTimeOffset.UtcNow.AddSeconds(1);}
        var rowStamp=$"{myInventorySearch}|{myInventoryQuality}|{myInventoryAllCategories}|{string.Join(',',myInventoryCategories.Order())}|{myInventoryDestination}|{gearStorageKind}|{gearSourceGroup}|{gearSourceSubgroup}|{gearOrigin}";
        var rows=myInventoryRowsCache.Get(myInventoryStock,rowStamp,Environment.TickCount64,()=>{
            var started=System.Diagnostics.Stopwatch.GetTimestamp();
            try{return myInventoryStock.Where(s=>RetainerRetrieval.AllowsQuality(s.Hq,myInventoryQuality)&&MyInventoryMatches(s.Item)).GroupBy(s=>s.Item).Select(g=>(Id:g.Key,Name:MyInventoryName(g.Key),Quantity:g.Sum(s=>s.Quantity))).OrderBy(x=>x.Name).ToArray();}
            finally{RecordPerformanceElapsed("Organizer: inventory display rows",started);}
        });
        if(ImGui.SmallButton("Select all"))foreach(var row in rows.Where(r=>!OrganizerGearProtected(r.Id)))myInventorySelected.TryAdd(row.Id,row.Quantity);ImGui.SameLine();
        if(ImGui.SmallButton("Clear selection"))myInventorySelected.Clear();ImGui.SameLine();if(ImGui.SmallButton("Refresh")){myInventoryRefresh=default;retrievalRefreshPending=true;RetrievalTry(RefreshRetrievalStock);}
        if(retrievalItems.Length>0&&ImGui.SmallButton("Select listed items in bags")){
            foreach(var item in retrievalItems)TemporaryInventorySelection.Pick(myInventorySelected,item.ItemId,1,myInventoryStock.Where(s=>!OrganizerGearProtected(s.Item)));
        }
        MarketHelp("Copies only listed items currently in your bags into the temporary selection, using available quantities. The plan stays unchanged; filters and protections still apply when entrusting.");
        var names=retrievalKnownRetainers.GetValueOrDefault(Player.ContentId)??new Dictionary<ulong,string>();
        ImGui.SetNextItemWidth(220);
        if(ImGui.BeginCombo("Destination",myInventoryDestination==GlamourStorageDestination?"Armoire first / Glamour dresser":myInventoryDestination==StorageAutoRouting.AllRetainers?(config.UseRetainerAssignments.Contains(Player.ContentId)?"Assignments":"All retainers"):(names.TryGetValue(myInventoryDestination,out var destinationName)?RetainerDestinationLabel(myInventoryDestination,destinationName):"Choose retainer"))){
            if(ImGui.Selectable("All retainers / Assignments",myInventoryDestination==StorageAutoRouting.AllRetainers))myInventoryDestination=StorageAutoRouting.AllRetainers;
            if(ImGui.Selectable("Armoire first / Glamour dresser",myInventoryDestination==GlamourStorageDestination)){myInventoryDestination=GlamourStorageDestination;myInventoryRefresh=default;}
            ImGui.Separator();
            foreach(var row in names.OrderBy(r=>!RetainerNameKnown(r.Key,r.Value)).ThenBy(r=>r.Value)){
                if(ImGui.Selectable(RetainerDestinationLabel(row.Key,row.Value)+"##"+row.Key,myInventoryDestination==row.Key))myInventoryDestination=row.Key;
                if(!RetainerNameKnown(row.Key,row.Value)&&ImGui.IsItemHovered())ImGui.SetTooltip("Retainer ID: "+row.Key+"\nOpen the summoning bell, then Refresh to record this retainer's name.");
            }
            ImGui.EndCombo();
        }
        if(myInventoryDestination==StorageAutoRouting.AllRetainers){DrawUseRetainerAssignments();ImGui.SameLine();DrawAssignmentEditor();DrawMissingEntrustAssignments(myInventoryDestination);}
        if(myInventoryDestination==GlamourStorageDestination&&ImGui.Checkbox("Include Armoury Chest",ref myInventoryArmoury))myInventoryRefresh=default;
        var amounts=myInventoryDestination==GlamourStorageDestination?myInventoryStock.Where(s=>myInventorySelected.ContainsKey(s.Item)&&!OrganizerGearProtected(s.Item)&&MyInventoryMatches(s.Item)&&RetainerRetrieval.AllowsQuality(s.Hq,myInventoryQuality)).Select(s=>s.Item).Distinct().ToDictionary(id=>id,id=>1):MyInventoryAmounts(myInventoryStock);
        ImGui.BeginDisabled(amounts.Count==0);if(ImGui.SmallButton(myInventoryDestination==GlamourStorageDestination?"Store selected gear":"Entrust selected"))RetrievalTry(EntrustMyInventory);ImGui.SameLine();
        if(ImGui.SmallButton("Add selected to plan"))RetrievalTry(()=>AddRetrieval(amounts.Select(x=>new RetrievalEntry(x.Key,MyInventoryName(x.Key),x.Value)).ToArray()));ImGui.EndDisabled();
        ImGui.TextDisabled($"{amounts.Count} selected matching items · temporary selection");
        MarketHelp("Entrust selected uses only checked items matching these filters and their quantities. Assignments choose destinations only. Your loaded plan is unchanged. Open the summoning bell to entrust. Shift-hover selects; Alt-hover removes.");
        ImGui.EndDisabled();
        if(organizeRunning&&ImGui.Button("Stop entrust"))StopOrganize("Stopped by you; an already-sent move may still complete.");
        if(organizeMoves.Length>0||organizeRunning)ImGui.TextWrapped(organizeStatus);
        ImGui.BeginDisabled(gearRouting||armoireRunning||craftingWithdrawalPending||retrievalRun.Running||organizeRunning||chestRunning);
        if(ImGui.BeginChild("MyInventoryRows",new(0,Math.Max(80,ImGui.GetContentRegionAvail().Y-8)),false)){
            if(ImGui.BeginTable("MyInventoryItems",3,ImGuiTableFlags.RowBg|ImGuiTableFlags.SizingStretchProp)){
                ImGui.TableSetupColumn("Item",ImGuiTableColumnFlags.WidthStretch,3);ImGui.TableSetupColumn("In bags");ImGui.TableSetupColumn("Entrust quantity");ImGui.TableHeadersRow();
                var height=Math.Max(ImGui.GetFrameHeight(),ImGui.GetTextLineHeight()+4)+ImGui.GetStyle().CellPadding.Y*2;
                var clip=ImGui.ImGuiListClipper();clip.Begin(rows.Length,height);
                while(clip.Step())for(var i=clip.DisplayStart;i<clip.DisplayEnd;i++){
                    var row=rows[i];ImGui.PushID((int)row.Id);ImGui.TableNextRow(ImGuiTableRowFlags.None,height);ImGui.TableNextColumn();
                    var protectedItem=OrganizerGearProtected(row.Id);ImGui.BeginDisabled(protectedItem);
                    ImGui.BeginGroup();var selected=myInventorySelected.ContainsKey(row.Id);if(ImGui.Checkbox("##Selected",ref selected)){if(selected)myInventorySelected[row.Id]=row.Quantity;else myInventorySelected.Remove(row.Id);}ImGui.SameLine();DrawRetrievalIcon(row.Id);ImGui.TextUnformatted(row.Name);ImGui.EndGroup();ImGui.EndDisabled();
                    if(protectedItem){ImGui.SameLine();ImGui.TextDisabled("[Gear set]");if(ImGui.IsItemHovered())ImGui.SetTooltip(OrganizerGearProtectionLabel(row.Id));}
                    if(!protectedItem&&ImGui.IsItemHovered()&&!ImGui.GetIO().WantTextInput){if(ImGui.GetIO().KeyShift&&!ImGui.GetIO().KeyAlt)myInventorySelected.TryAdd(row.Id,row.Quantity);else if(ImGui.GetIO().KeyAlt&&!ImGui.GetIO().KeyShift)myInventorySelected.Remove(row.Id);}
                    ImGui.TableNextColumn();ImGui.TextUnformatted(row.Quantity.ToString());ImGui.TableNextColumn();
                    if(myInventorySelected.TryGetValue(row.Id,out var quantity)){quantity=Math.Clamp(quantity,1,row.Quantity);ImGui.SetNextItemWidth(-1);ImGui.InputInt("##Amount",ref quantity,0,0);myInventorySelected[row.Id]=Math.Clamp(quantity,1,row.Quantity);}else ImGui.TextDisabled("—");ImGui.PopID();
                }
                clip.End();clip.Destroy();ImGui.EndTable();
            }
            if(rows.Length==0)ImGui.TextDisabled("No bag items match these filters.");
        }ImGui.EndChild();
        ImGui.EndDisabled();
    }
}

