using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private string allStorageSearch="",allStorageKey="";
    private int allStorageFilter;
    private ulong allStorageOwner;
    private readonly HashSet<uint> allStorageSelected=[];
    private readonly UiProjectionCache<SyncEvent[]> allStorageCache=new(1000);
    private readonly UiProjectionCache<(SyncEvent Event,StorageStack[] Rows)[]> allStorageRowsCache=new(1000);
    private void DrawAllStorage()
    {
        if(!Player.IsLoaded){ImGui.TextDisabled("Log in to browse this character's recorded storage.");return;}
        if(allStorageOwner!=Player.ContentId){allStorageOwner=Player.ContentId;allStorageKey="";allStorageSelected.Clear();}
        var index=DiscoveryReads();
        var locations=allStorageCache.Get(config,$"{Player.ContentId}:{index.Revision}:{retrievalInventoryView}",Environment.TickCount64,()=>index.StorageFor(Player.ContentId.ToString()).Where(e=>e.Storage!=null&&LocalStoragePolicy.View(e.Storage.Key,retrievalInventoryView)).OrderBy(e=>e.Storage!.Name).ToArray());
        ImGui.SetNextItemWidth(-1);ImGui.InputTextWithHint("##AllStorageSearch","Search all recorded items…",ref allStorageSearch,100);
        ImGui.SetNextItemWidth(190);ImGui.Combo("##AllStorageFilter",ref allStorageFilter,"Any items\0Tradable only\0Untradable only\0Bound only\0Unbound only\0");
        MarketHelp("Bound means a non-collectible copy with spiritbond/binding data. Untradable includes bound copies and items that cannot be traded by design. Old observations with unknown copy state are excluded from these filters; reopen the storage to refresh.");
        ImGui.SameLine();if(ImGui.SmallButton("Refresh")){RetrievalTry(()=>ObserveStorage(DateTimeOffset.UtcNow));myInventoryRefresh=default;}
        ImGui.SetNextItemWidth(-1);
        if(ImGui.BeginCombo("##StorageLocation",locations.FirstOrDefault(e=>e.Storage!.Key==allStorageKey)?.Storage?.Name??"All recorded locations")){
            if(ImGui.Selectable("All recorded locations",allStorageKey.Length==0)){allStorageKey="";allStorageSelected.Clear();}
            foreach(var e in locations)if(ImGui.Selectable(e.Storage!.Name+"##"+e.Storage.Key,allStorageKey==e.Storage.Key)){allStorageKey=e.Storage.Key;allStorageSelected.Clear();}
            ImGui.EndCombo();
        }
        var chosen=locations.FirstOrDefault(e=>e.Storage!.Key==allStorageKey);
        if(chosen!=null){
            ImGui.BeginDisabled(chestRunning||organizeRunning||retrievalRun.Running||gearRouting||armoireRunning||craftingWithdrawalPending);
            if(ImGui.SmallButton("Select all matching"))foreach(var s in VisibleStorageStacks(chosen))allStorageSelected.Add(s.ItemId);
            ImGui.SameLine();if(ImGui.SmallButton("Clear"))allStorageSelected.Clear();
            var supported=TryLocalStorageKey(allStorageKey,out var type)&& (LocalStoragePolicy.Saddle(type)||LocalStoragePolicy.Storeroom(type));
            if(supported){
                ImGui.BeginDisabled(allStorageSelected.Count==0);if(ImGui.Button("Withdraw selected to bags"))RetrievalTry(()=>StartLocalStorage(allStorageKey,false));ImGui.EndDisabled();
                EnsureMyInventoryCharacter();ImGui.BeginDisabled(myInventorySelected.Count==0);if(ImGui.Button("Entrust temporary bag selection here"))RetrievalTry(()=>StartLocalStorage(allStorageKey,true));ImGui.EndDisabled();
                ImGui.TextDisabled("Transfers whole selected stacks; open this storage in game first.");
                if(ImGui.SmallButton($"Choose bag items · {myInventorySelected.Count} selected")){retrievalInventoryView=0;}
            }else ImGui.TextWrapped("Recorded view. Use Retainers, FC chest, or Armoire for their transfer tools. Equipped and placed furnishings are display-only here.");
            ImGui.EndDisabled();
        }
        if(localStorageRunning&&ImGui.Button("Stop storage transfer"))StopChest("Stopped by you; an already-sent move may still complete.");
        if(localStorageStatusShown)ImGui.TextWrapped(chestStatus);
        ImGui.BeginChild("AllStorageRows",new(0,Math.Max(80,ImGui.GetContentRegionAvail().Y-4)),false);
        var groups=allStorageRowsCache.Get(locations,$"{allStorageSearch}:{allStorageFilter}",Environment.TickCount64,()=>locations.Select(e=>(e,VisibleStorageStacks(e).ToArray())).ToArray());
        foreach(var group in groups.Where(g=>allStorageKey.Length==0||g.Event.Storage!.Key==allStorageKey)){
            var e=group.Event;var rows=group.Rows;
            if(!ImGui.CollapsingHeader($"{e.Storage!.Name} · {rows.Length} item stacks##{e.Storage.Key}",allStorageKey.Length>0?ImGuiTreeNodeFlags.DefaultOpen:ImGuiTreeNodeFlags.None))continue;
            ImGui.TextDisabled($"Recorded {e.At.ToLocalTime():g}");
            if(ImGui.SmallButton("Use this location##"+e.Storage.Key)){allStorageKey=e.Storage.Key;allStorageSelected.Clear();}
            var clip=ImGui.ImGuiListClipper();clip.Begin(rows.Length,ImGui.GetFrameHeightWithSpacing());
            while(clip.Step())for(var i=clip.DisplayStart;i<clip.DisplayEnd;i++){
                var s=rows[i];ImGui.PushID(e.Storage.Key+":"+i);
                ImGui.BeginDisabled(allStorageKey!=e.Storage.Key||chestRunning);
                var selected=allStorageSelected.Contains(s.ItemId);if(ImGui.Checkbox("##pick",ref selected)){if(selected)allStorageSelected.Add(s.ItemId);else allStorageSelected.Remove(s.ItemId);}ImGui.EndDisabled();ImGui.SameLine();
                ImGui.TextUnformatted($"{SearchItemName(s.ItemId)}{(s.Hq?" HQ":"")} · {(s.Quantity>0?s.Quantity.ToString():"quantity unknown")} · {(s.Bound==true?"Bound":s.Tradable==true?"Tradable":s.Tradable==false?"Untradable":"copy state unknown")}");
                ImGui.PopID();
            }clip.End();clip.Destroy();
        }
        if(locations.Length==0)ImGui.TextWrapped("No storage recorded yet. Open the saddlebag, housing furnishing/storeroom menu, or other storage to capture its contents.");
        ImGui.EndChild();
    }
    private IEnumerable<StorageStack> VisibleStorageStacks(SyncEvent e)=>(e.Storage!.Stacks??e.Storage.Items.Select(id=>new StorageStack(id,0,false,SearchItemName(id))).ToArray()).SelectMany(LocalStoragePolicy.Copies).Where(s=>LocalStoragePolicy.Filter(s,allStorageFilter)&&SearchItemName(s.ItemId).Contains(allStorageSearch,StringComparison.OrdinalIgnoreCase));
}
