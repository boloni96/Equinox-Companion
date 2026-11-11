using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private bool organizerOpenArmoire,organizerOpenChest;
    private string bagStorageDestination="";
    private readonly UiProjectionCache<SharedItemLocation[]> bagStorageDestinations=new(1000);
    private StorageWithdrawalPage[] SelectedStoragePages(SharedItemLocation[] locations)
    {
        var groups=allStorageRowsCache.Get(locations,$"{allStorageSearch}:{allStorageFilter}:{allStorageAllCategories}:{string.Join(',',allStorageCategories.Order())}",Environment.TickCount64,()=>locations.Select(e=>(e,VisibleStorageStacks(e).ToArray())).ToArray());
        return StorageWithdrawalPlan.Build(groups.Where(g=>allStorageKey.Length==0||g.Event.Key==allStorageKey)
            .SelectMany(g=>g.Rows.Where(s=>allStorageSelected.Contains(g.Event.Key,s.ItemId)&&!OrganizerGearProtected(s.ItemId)).Select(s=>(g.Event.Key,s.ItemId))));
    }
    private void StartSelectedStorageWithdrawal(StorageWithdrawalPage[] pages)
    {
        if(OrganizerSelectionBusy)throw new InvalidOperationException("Finish the current inventory operation first.");
        if(pages.Length==0)throw new InvalidOperationException("Select matching items in a saddlebag, Armoury Chest or housing storeroom first.");
        // Validate every source before sending the first move. Remote houses and
        // unavailable premium bags must not silently become a partial transfer.
        foreach(var page in pages){if(!TryLocalStorageKey(page.Key,out var type))throw new InvalidOperationException("Unsupported storage location.");RequireLocalStorage(page.Key,type);}
        StartLocalStorage(pages[0].Key,false,pages[0].Items);
        localStorageBatchFilter=localStorageFilter;
        foreach(var page in pages.Skip(1))localStorageQueue.Enqueue(page);
    }
    private void DrawRecordedStorageActions(SharedItemLocation[] locations,SharedItemLocation? chosen)
    {
        var pages=SelectedStoragePages(locations);
        ImGui.BeginDisabled(OrganizerSelectionBusy);
        ImGui.BeginDisabled(pages.Length==0);
        if(ImGui.Button("Withdraw selected to inventory"))RetrievalTry(()=>StartSelectedStorageWithdrawal(pages));
        ImGui.EndDisabled();
        MarketHelp("Withdraw matching selections across saddlebag / Armoury / storeroom pages. Open the source in game first. Search, categories, tradability and gear-set protection apply. Each transfer must settle before the next.");
        if(pages.Length>0)ImGui.TextDisabled($"{pages.Sum(p=>p.Items.Length)} item selections · {pages.Length} source pages");
        if(chestOwner==Player.ContentId&&localStorageWithdrawn.Count>0){
            if(ImGui.SmallButton("Select withdrawn items in inventory"))RetrievalTry(()=>{
                EnsureMyInventoryCharacter();myInventorySearch="";myInventoryCategories.Clear();myInventoryAllCategories=true;
                myInventoryQuality=RetrievalQuality.Any;myInventoryTradeFilter=localStorageFilter;myInventoryDestination=StorageAutoRouting.AllRetainers;
                var stock=ReadMyInventory();myInventorySelected.Clear();
                foreach(var pair in localStorageWithdrawn){var held=stock.Where(x=>x.Item==pair.Key).Sum(x=>x.Quantity);if(held>0)myInventorySelected[pair.Key]=Math.Min(pair.Value,held);}
                myInventoryRefresh=default;retrievalInventoryView=0;
            });
            MarketHelp("Select up to the confirmed withdrawn quantity in your bags, ready for a storage destination or Gil & Item Trader. Review the selection before sending.");
        }
        if(chosen!=null&&TryLocalStorageKey(chosen.Key,out var type)&&(LocalStoragePolicy.Saddle(type)||LocalStoragePolicy.Storeroom(type))){
            EnsureMyInventoryCharacter();ImGui.BeginDisabled(myInventorySelected.Count==0);
            if(ImGui.Button("Entrust selected inventory items here"))RetrievalTry(()=>StartLocalStorage(chosen.Key,true));ImGui.EndDisabled();
        }
        if(ImGui.SmallButton("Choose inventory items / destination"))retrievalInventoryView=0;
        if(retrievalInventoryView is 2 or 8){ImGui.SameLine();if(ImGui.SmallButton("FC chest tools"))organizerOpenChest=true;}
        if(retrievalInventoryView is 2 or 7){ImGui.SameLine();if(ImGui.SmallButton("Armoire tools"))organizerOpenArmoire=true;}
        ImGui.EndDisabled();
        if(retrievalInventoryView==6)ImGui.TextWrapped("Unequip gear in game before moving it. Equipped items are protected here.");
        else if(retrievalInventoryView==7)ImGui.TextWrapped("Use Armoire tools for supported transfers. Restore individual dresser items through the game's dresser menu.");
        else if(retrievalInventoryView==4)ImGui.TextWrapped("Transfers use the current estate's storeroom. Placed furnishings and fixtures must be removed through the game's housing menu first.");
    }
    private void DrawBagStorageActions(IReadOnlyDictionary<uint,int> amounts)
    {
        var index=DiscoveryReads();var actor=Player.ContentId.ToString();
        var destinations=bagStorageDestinations.Get(config.SharedRoster,$"{actor}:{index.Revision}",Environment.TickCount64,()=>{
            var bags=index.StorageFor(actor).Where(e=>e.Storage!=null&&TryLocalStorageKey(e.Storage.Key,out var t)&&LocalStoragePolicy.Saddle(t))
                .Select(e=>new SharedItemLocation(e.Actor.ContentId,e.Actor.Name,e.Storage!.Key,e.Storage.Name,e.At,e.Storage.Items,e.Storage.Stacks));
            var houses=HousingStorageCatalog.ForCharacter(config.SharedRoster,index.AllStorage(),actor).SelectMany(h=>h.Locations)
                .Where(e=>TryLocalStorageKey(e.Key,out var t)&&LocalStoragePolicy.Storeroom(t));
            return bags.Concat(houses).DistinctBy(e=>e.Key).OrderBy(e=>e.Name).ToArray();
        });
        ImGui.Separator();ImGui.SetNextItemWidth(-1);
        if(ImGui.BeginCombo("##BagStorageDestination",destinations.FirstOrDefault(e=>e.Key==bagStorageDestination)?.Name??"Choose saddlebag / housing storeroom…")){
            foreach(var e in destinations)if(ImGui.Selectable(e.Name+"##"+e.Key,e.Key==bagStorageDestination))bagStorageDestination=e.Key;
            ImGui.EndCombo();
        }
        ImGui.BeginDisabled(amounts.Count==0||!destinations.Any(e=>e.Key==bagStorageDestination)||OrganizerSelectionBusy);
        if(ImGui.SmallButton("Entrust to selected storage"))RetrievalTry(()=>StartLocalStorage(bagStorageDestination,true));ImGui.EndDisabled();
        MarketHelp("Open the selected saddlebag or estate storeroom in game first. Deposits whole selected stacks from inventory. Indoor/outdoor furnishing eligibility and estate permissions are checked before moving.");
        ImGui.SameLine();ImGui.BeginDisabled(amounts.Count==0||OrganizerSelectionBusy);
        if(ImGui.SmallButton("Prepare trade"))RetrievalTry(()=>PrepareInventoryTrade(amounts));ImGui.EndDisabled();
        MarketHelp("Load matching tradable bag copies into Gil & Item Trader for review. Does not start a trade or send anything.");
        if(destinations.Length==0)ImGui.TextWrapped("Open a saddlebag or housing storeroom to record it as a destination.");
    }
    private void PrepareInventoryTrade(IReadOnlyDictionary<uint,int> amounts)
    {
        if(OrganizerSelectionBusy)throw new InvalidOperationException("Finish the current inventory operation first.");
        RefreshItemTradePicker();itemTradeBagOwner=Player.ContentId;itemTradeSelection.Clear();
        foreach(var pair in amounts){var remaining=pair.Value;
            foreach(var group in itemTradeBags.Where(s=>s.Item==pair.Key&&s.Tradable&&RetainerRetrieval.AllowsQuality(s.Hq,myInventoryQuality)).GroupBy(s=>s.Key).OrderBy(g=>g.Key.Hq)){
                var quantity=Math.Min(remaining,group.Sum(s=>s.Quantity));if(quantity>0)itemTradeSelection[group.Key]=quantity;remaining-=quantity;
            }
        }
        if(itemTradeSelection.Count==0)throw new InvalidOperationException("No selected tradable copies are available in your inventory.");
        itemTradeSearch="";itemTradeCategories.Clear();itemTradeSelectedOnly=true;gilItemsMode=true;gilReceive=false;
        gilTradeNotice=$"Prepared {itemTradeSelection.Count} item/quality selections from inventory. Review quantities and recipient before sending.";
        OpenGilTrader();
    }
}
