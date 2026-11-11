using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private string allStorageSearch="",allStorageKey="";
    private int allStorageFilter;
    private readonly HashSet<uint> allStorageCategories=[];
    private bool allStorageAllCategories=true;
    private ulong allStorageOwner;
    private readonly StorageInventorySelection allStorageSelected=new();
    private readonly UiProjectionCache<SharedItemLocation[]> allStorageCache=new(1000);
    private readonly UiProjectionCache<(SharedItemLocation Event,StorageStack[] Rows)[]> allStorageRowsCache=new(1000);
    private readonly UiProjectionCache<(SharedItemLocation Location,StorageStack Stack)[]> allStorageFlatCache=new(1000);
    private void DrawShowAllStorageOption()
    {
        var show=config.OrganizerShowAllStorage;
        if(ImGui.Checkbox("Show all",ref show)){config.OrganizerShowAllStorage=show;SaveConfiguration();}
        MarketHelp("Show matching items in one list with their storage location. Turn off to group by location. Search and filters still apply.");
    }
    private DateTimeOffset nextHousingStorageRead;
    private readonly UiProjectionCache<HousingStorageHome[]> housingStorageHomesCache=new(1000);
    private void DrawAllStorage()
    {
        if(!Player.IsLoaded){ImGui.TextDisabled("Log in to browse this character's recorded storage.");return;}
        if(allStorageOwner!=Player.ContentId){allStorageOwner=Player.ContentId;allStorageKey="";allStorageSelected.Clear();}
        var housingView=retrievalInventoryView is 2 or 4;
        if(housingView&&DateTimeOffset.UtcNow>=nextHousingStorageRead){
            nextHousingStorageRead=DateTimeOffset.UtcNow.AddSeconds(1);
            try{ObserveStorage(DateTimeOffset.UtcNow,housingOnly:true);}catch(Exception ex){housingStorageReadStatus="Housing read failed: "+ex.Message;}
        }
        var index=DiscoveryReads();
        var homes=housingView?housingStorageHomesCache.Get(config.SharedRoster,$"{Player.ContentId}:{index.Revision}",Environment.TickCount64,()=>HousingStorageCatalog.ForCharacter(config.SharedRoster,index.AllStorage(),Player.ContentId.ToString(System.Globalization.CultureInfo.InvariantCulture))):[];
        var locations=allStorageCache.Get(config.SharedRoster,$"{Player.ContentId}:{index.Revision}:{retrievalInventoryView}",Environment.TickCount64,()=>{
            var own=index.StorageFor(Player.ContentId.ToString()).Where(e=>e.Storage!=null&&!e.Storage.Key.StartsWith("house:")&&LocalStoragePolicy.View(e.Storage.Key,retrievalInventoryView))
                .Select(e=>new SharedItemLocation(e.Actor.ContentId,e.Actor.Name,e.Storage!.Key,e.Storage.Name,e.At,e.Storage.Items,e.Storage.Stacks));
            return own.Concat(homes.SelectMany(h=>h.Locations)).OrderBy(e=>e.Name).ToArray();
        });
        if(allStorageKey.Length>0&&!locations.Any(e=>e.Key==allStorageKey)){allStorageKey="";}
        if(housingView){ImGui.TextWrapped(config.SyncCollections?housingStorageReadStatus:"Storage recording is disabled in collection sync settings.");if(config.SharedRoster?.ItemLocationsTruncated==true)ImGui.TextWrapped("Shared storage results are partial; local records are also included.");}
        ImGui.SetNextItemWidth(-1);ImGui.InputTextWithHint("##AllStorageSearch","Search all recorded items…",ref allStorageSearch,100);
        ImGui.SetNextItemWidth(Math.Max(120,ImGui.GetContentRegionAvail().X-105));
        if(DrawStorageCategoryPicker("##AllStorageCategories",allStorageCategories,allStorageAllCategories?"All categories":"None selected")){allStorageAllCategories=false;allStorageSelected.Clear();}
        ImGui.SameLine();if(ImGui.SmallButton("Reset filters")){allStorageCategories.Clear();allStorageAllCategories=true;allStorageSearch="";allStorageFilter=0;allStorageSelected.Clear();}
        ImGui.SetNextItemWidth(190);ImGui.Combo("##AllStorageFilter",ref allStorageFilter,"Any items\0Tradable only\0Untradable only\0Bound only\0Unbound only\0");
        MarketHelp("Bound means a non-collectible copy with spiritbond/binding data. Untradable includes bound copies and items that cannot be traded by design. Old observations with unknown copy state are excluded from these filters; reopen the storage to refresh.");
        ImGui.SameLine();if(ImGui.SmallButton("Refresh")){RetrievalTry(()=>ObserveStorage(DateTimeOffset.UtcNow));myInventoryRefresh=default;}
        ImGui.SameLine();DrawShowAllStorageOption();
        ImGui.SetNextItemWidth(-1);
        if(ImGui.BeginCombo("##StorageLocation",locations.FirstOrDefault(e=>e.Key==allStorageKey)?.Name??"All recorded locations")){
            if(ImGui.Selectable("All recorded locations",allStorageKey.Length==0)){allStorageKey="";}
            foreach(var e in locations)if(ImGui.Selectable(e.Name+"##"+e.Key,allStorageKey==e.Key)){allStorageKey=e.Key;}
            ImGui.EndCombo();
        }
        var chosen=locations.FirstOrDefault(e=>e.Key==allStorageKey);
        ImGui.BeginDisabled(OrganizerSelectionBusy);
        if(ImGui.SmallButton("Select all matching"))
            foreach(var location in locations.Where(e=>allStorageKey.Length==0||e.Key==allStorageKey))
                foreach(var stack in VisibleStorageStacks(location).Where(s=>!OrganizerGearProtected(s.ItemId)))allStorageSelected.Set(location.Key,stack.ItemId,true);
        ImGui.SameLine();if(ImGui.SmallButton("Clear selection"))allStorageSelected.Clear(allStorageKey);
        ImGui.EndDisabled();
        ImGui.TextDisabled("Shift-hover selects · Alt-hover removes");
        if(chosen==null)ImGui.TextWrapped("Select across locations below. Choose a location to transfer its selected items.");
        if(chosen!=null){
            ImGui.BeginDisabled(OrganizerSelectionBusy);
            var supported=TryLocalStorageKey(allStorageKey,out var type)&& (LocalStoragePolicy.Saddle(type)||LocalStoragePolicy.Storeroom(type)||LocalStoragePolicy.Armoury(type));
            if(supported){
                ImGui.BeginDisabled(!allStorageSelected.Items(allStorageKey).Any());if(ImGui.Button("Withdraw selected to bags"))RetrievalTry(()=>StartLocalStorage(allStorageKey,false));ImGui.EndDisabled();
                EnsureMyInventoryCharacter();ImGui.BeginDisabled(myInventorySelected.Count==0||LocalStoragePolicy.Armoury(type));if(ImGui.Button("Entrust temporary bag selection here"))RetrievalTry(()=>StartLocalStorage(allStorageKey,true));ImGui.EndDisabled();
                ImGui.TextDisabled(LocalStoragePolicy.Armoury(type)?"Withdraw unequipped gear to bags; gear-set protection applies. Use the game to equip or store gear here.":"Transfers whole selected stacks; open this storage in game first.");
                if(ImGui.SmallButton($"Choose bag items · {myInventorySelected.Count} selected")){retrievalInventoryView=0;}
            }else ImGui.TextWrapped("Recorded view. Use Retainers, FC chest, or Armoire for their transfer tools. Equipped and placed furnishings are display-only here.");
            ImGui.EndDisabled();
        }
        if(localStorageRunning&&ImGui.Button("Stop storage transfer"))StopChest("Stopped by you; an already-sent move may still complete.");
        if(localStorageStatusShown)ImGui.TextWrapped(chestStatus);
        ImGui.BeginChild("AllStorageRows",new(0,Math.Max(80,ImGui.GetContentRegionAvail().Y-4)),false);
        var groups=allStorageRowsCache.Get(locations,$"{allStorageSearch}:{allStorageFilter}:{allStorageAllCategories}:{string.Join(',',allStorageCategories.Order())}",Environment.TickCount64,()=>locations.Select(e=>(e,VisibleStorageStacks(e).ToArray())).ToArray());
        var shown=groups.Where(g=>allStorageKey.Length==0||g.Event.Key==allStorageKey).ToArray();
        if(config.OrganizerShowAllStorage){
            var flat=allStorageFlatCache.Get(groups,allStorageKey,Environment.TickCount64,()=>shown.SelectMany(g=>g.Rows.Select(row=>(Location:g.Event,Stack:row))).OrderBy(x=>SearchItemName(x.Stack.ItemId)).ThenBy(x=>x.Location.Name).ToArray());
            DrawFlatStorageRows(flat);
        }else{
        foreach(var group in shown.Where(g=>!g.Event.Key.StartsWith("house:")))DrawStorageGroup(group.Event,group.Rows);
        foreach(var home in homes){
            if(allStorageKey.Length>0&&HousingStorageCatalog.HouseId(allStorageKey)!=home.Id)continue;
            if(!ImGui.TreeNodeEx(home.Label+"##house-storage-"+home.Id,allStorageKey.Length>0?ImGuiTreeNodeFlags.DefaultOpen:ImGuiTreeNodeFlags.None))continue;
            foreach(var inside in home.Outside?new[]{true,false}:new[]{true}){
                if(!ImGui.TreeNodeEx((inside?"Inside":"Outside")+"##"+home.Id,ImGuiTreeNodeFlags.DefaultOpen))continue;
                var section=shown.Where(g=>HousingStorageCatalog.HouseId(g.Event.Key)==home.Id&&HousingStorageCatalog.Inside(g.Event.Key)==inside).ToArray();
                if(section.Length==0)ImGui.TextWrapped("Not recorded yet. Open this house's furnishings/storeroom menu with a character who can use it.");
                foreach(var group in section)DrawStorageGroup(group.Event,group.Rows);
                ImGui.TreePop();
            }
            ImGui.TreePop();
        }
        }
        if(locations.Length==0&&homes.Length==0)ImGui.TextWrapped(retrievalInventoryView==4?"No housing storage known for this character. Open furnishings/storeroom at a house this character can use to record it.":"No storage recorded yet. Open the saddlebag, housing furnishing/storeroom menu, or other storage to capture its contents.");
        ImGui.EndChild();
    }
    private void DrawFlatStorageRows((SharedItemLocation Location,StorageStack Stack)[] rows)
    {
        if(rows.Length==0){ImGui.TextDisabled("No recorded items match these filters.");return;}
        if(!ImGui.BeginTable("FlatStorage",3,ImGuiTableFlags.RowBg|ImGuiTableFlags.Resizable))return;
        ImGui.TableSetupColumn("Item");ImGui.TableSetupColumn("Quantity");ImGui.TableSetupColumn("Location · choose to transfer");ImGui.TableHeadersRow();
        var height=Math.Max(ImGui.GetFrameHeight(),ImGui.GetTextLineHeight()+4)+ImGui.GetStyle().CellPadding.Y*2;
        var clip=ImGui.ImGuiListClipper();clip.Begin(rows.Length,height);
        while(clip.Step())for(var i=clip.DisplayStart;i<clip.DisplayEnd;i++){
            var (location,stack)=rows[i];ImGui.PushID(i);ImGui.TableNextRow(ImGuiTableRowFlags.None,height);ImGui.TableNextColumn();
            var protectedItem=OrganizerGearProtected(stack.ItemId);
            ImGui.BeginDisabled(OrganizerSelectionBusy||protectedItem);ImGui.BeginGroup();
            var selected=allStorageSelected.Contains(location.Key,stack.ItemId);
            if(ImGui.Checkbox("##pick",ref selected))allStorageSelected.Set(location.Key,stack.ItemId,selected);
            ImGui.SameLine();DrawRetrievalIcon(stack.ItemId);ImGui.TextUnformatted(SearchItemName(stack.ItemId)+(stack.Hq?" HQ":""));
            ImGui.EndGroup();var pick=OrganizerHoverSelectionAction(protectedItem);ImGui.EndDisabled();
            if(pick!=0)allStorageSelected.Set(location.Key,stack.ItemId,pick>0);
            MarketHelp(protectedItem?OrganizerGearProtectionLabel(stack.ItemId):$"Recorded {location.At.ToLocalTime():g} · "+(stack.Bound==true?"Bound":stack.Tradable==true?"Tradable":stack.Tradable==false?"Untradable":"copy state unknown"));
            ImGui.TableNextColumn();ImGui.TextUnformatted(stack.Quantity>0?stack.Quantity.ToString():"Unknown");
            ImGui.TableNextColumn();ImGui.BeginDisabled(OrganizerSelectionBusy);
            if(ImGui.Selectable(location.Name+"##location",allStorageKey==location.Key)){allStorageKey=location.Key;}
            ImGui.EndDisabled();ImGui.PopID();
        }
        clip.End();clip.Destroy();ImGui.EndTable();
    }
    private void DrawStorageGroup(SharedItemLocation e,StorageStack[] rows)
    {
            if(!ImGui.CollapsingHeader($"{e.Name} · {rows.Length} item stacks##{e.Key}",allStorageKey.Length>0?ImGuiTreeNodeFlags.DefaultOpen:ImGuiTreeNodeFlags.None))return;
            ImGui.TextDisabled($"Recorded {e.At.ToLocalTime():g}");
            if(ImGui.SmallButton("Use this location##"+e.Key)){allStorageKey=e.Key;}
            var clip=ImGui.ImGuiListClipper();clip.Begin(rows.Length,Math.Max(ImGui.GetFrameHeight(),ImGui.GetTextLineHeight()+4)+ImGui.GetStyle().ItemSpacing.Y);
            while(clip.Step())for(var i=clip.DisplayStart;i<clip.DisplayEnd;i++){
                var s=rows[i];ImGui.PushID(e.Key+":"+i);
                var protectedItem=OrganizerGearProtected(s.ItemId);
                ImGui.BeginDisabled(OrganizerSelectionBusy||protectedItem);ImGui.BeginGroup();
                var selected=allStorageSelected.Contains(e.Key,s.ItemId);if(ImGui.Checkbox("##pick",ref selected))allStorageSelected.Set(e.Key,s.ItemId,selected);ImGui.SameLine();
                DrawRetrievalIcon(s.ItemId);
                ImGui.TextUnformatted($"{SearchItemName(s.ItemId)}{(s.Hq?" HQ":"")} · {(s.Quantity>0?s.Quantity.ToString():"quantity unknown")} · {(s.Bound==true?"Bound":s.Tradable==true?"Tradable":s.Tradable==false?"Untradable":"copy state unknown")}");
                ImGui.EndGroup();var pick=OrganizerHoverSelectionAction(protectedItem);ImGui.EndDisabled();
                if(pick!=0)allStorageSelected.Set(e.Key,s.ItemId,pick>0);
                ImGui.PopID();
            }clip.End();clip.Destroy();
    }
    private bool AllStorageItemMatches(uint id)=>InventoryBrowseSelection.Matches(SearchItemName(id),OrganizeCategory(id),allStorageSearch,allStorageCategories,allStorageAllCategories);
    private IEnumerable<StorageStack> VisibleStorageStacks(SharedItemLocation e)=>(e.Stacks??e.Items.Select(id=>new StorageStack(id,0,false,SearchItemName(id))).ToArray()).SelectMany(LocalStoragePolicy.Copies).Where(s=>LocalStoragePolicy.Filter(s,allStorageFilter)&&AllStorageItemMatches(s.ItemId));
}
