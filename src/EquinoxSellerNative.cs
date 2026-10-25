using System.Collections.Concurrent;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Network.Structures;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace EquinoxCompanion;

public sealed partial class Plugin
{
    [Dalamud.IoC.PluginService] internal static IMarketBoard MarketBoard { get; private set; }=null!;
    private SellerRun sellerRun=null!;
    private MarketLedger marketLedger=MarketLedger.Empty;
    private Task<MarketLedger>? marketLoad;
    private LatestSnapshotWriter<MarketLedger> marketWriter=null!;
    private bool marketDirty,marketLoadFailed,marketHooksEnabled,marketBuddyLocked;
    private DateTimeOffset marketSaveAt,marketPollAt,manualEditorAt,manualCompareAt;
    private string sellerNotice="",marketDataError="",sellerLiveError="";
    private readonly SellerComparisonCapture sellerComparison=new();
    private bool sellerDispatchingCompare;
    private ulong sellerInventoryRetainer;
    private DateTimeOffset sellerInventorySince;
    private MarketStock? manualEditor;
    private SellerDecision? manualDecision;
    private ulong manualRetainer;
    private bool manualAutoCompare,manualShowHistory,manualSuspendUntilClose;
    private InventoryType manualInventory;
    private DateTimeOffset sellerSelectRetry,marketOrderAt;
    private int? manualPastePrice;
    private readonly ConcurrentQueue<(uint Item,SellerSale[] Sales,DateTimeOffset At)> sellerHistories=new();
    private readonly ConcurrentQueue<(int Request,SellerReceivedOffer[] Rows,DateTimeOffset At)> sellerOfferings=new();
    private readonly Dictionary<(ulong Retainer,int Slot),(MarketStock Item,SellerDecision Decision,DateTimeOffset At)> sellerReviews=[];
    private unsafe delegate void SellerResultDelegate(InfoProxyItemSearch* proxy,byte count,int error);
    private Hook<SellerResultDelegate>? sellerResultHook;
    private string MarketDataPath=>Path.Combine(Pi.GetPluginConfigDirectory(),"market-ledger.json");

    private unsafe void InitializeEquinoxMarket()
    {
        config.Seller??=new();config.MarketJournal??=new();
        config.Seller.MinimumPrices??=[];config.Seller.ExcludedItems??=[];config.Seller.SelectedRetainers??=[];
        marketWriter=new(ledger=>{
            var target=MarketDataPath;Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var temp=target+".tmp";File.WriteAllText(temp,System.Text.Json.JsonSerializer.Serialize(ledger));
            if(File.Exists(target))File.Copy(target,target+".bak",true);
            File.Move(temp,target,true);
        });
        marketLoad=Task.Run(()=>{
            var ledger=File.Exists(MarketDataPath)?System.Text.Json.JsonSerializer.Deserialize<MarketLedger>(File.ReadAllText(MarketDataPath))??throw new IOException("Empty market ledger"):MarketLedger.Empty;
            if(ledger.Retainers==null||ledger.Income==null||ledger.Prices==null||ledger.Retainers.Any(x=>x.Stock==null))throw new IOException("Incomplete market ledger; original file preserved");
            return ledger;
        });
        sellerAutoRetainerPause=new(new SellerAutoRetainerPort());
        sellerRun=new(new SellerPort(this));
        InitializeMarketWindows();
        // Market hooks are installed only while the optional tool is enabled.
        if(config.Seller.Enabled)EnableMarketHooks();
    }
    private unsafe void EnableMarketHooks()
    {
        if(marketHooksEnabled)return;
        marketHooksEnabled=true; // Roll back every subscription if initialization is interrupted.
        try {
            try {
                sellerResultHook??=Interop.HookFromAddress<SellerResultDelegate>(InfoProxyItemSearch.MemberFunctionPointers.ProcessRequestResult,SellerRequestResult);
                sellerResultHook.Enable();sellerLiveError="";
            }catch(Exception ex){sellerLiveError="Live comparison confirmation is unavailable ("+ex.GetType().Name+"). Manual pricing remains available.";Log.Warning(ex,sellerLiveError);}
            EnableRetainerHistory();
            MarketBoard.HistoryReceived+=SellerHistoryReceived;
            MarketBoard.OfferingsReceived+=SellerOfferingsReceived;
            MarketBoard.PurchaseRequested+=MarketPurchaseRequested;
            MarketBoard.ItemPurchased+=MarketPurchased;
            Addons.RegisterListener(AddonEvent.PostSetup,"RetainerList",SellerBellOpened);
            Addons.RegisterListener(AddonEvent.PostSetup,"RetainerSell",SellerEditorOpened);
            Addons.RegisterListener(AddonEvent.PreReceiveEvent,"RetainerSell",SellerCompareOpening);
            Addons.RegisterListener(AddonEvent.PostSetup,"RetainerSellList",SellerListOpened);
            Addons.RegisterListener(AddonEvent.PostReceiveEvent,"ItemSearchResult",SellerPriceClicked);
            Addons.RegisterListener(AddonEvent.PostReceiveEvent,new[]{"RetainerSell","ItemSearchResult"},SellerNativeEvent);
            Addons.RegisterListener(AddonEvent.PreFinalize,new[]{"RetainerList","RetainerSellList"},MarketHighlightFinalizing);
            Chat.LogMessage+=ObserveMarketSale;
            marketHooksEnabled=true;sellerNotice="";
        } catch(Exception e){DisableMarketHooks();sellerNotice="Market tools unavailable: "+e.GetType().Name;Log.Error(e,"Equinox Market initialization failed");}
    }
    private void DisableMarketHooks()
    {
        sellerRun?.Stop("Market tools disabled; settings kept.");ReleaseSellerLocks();
        StopMarketWatch();
        RemoveMarketBar();
        if(!marketHooksEnabled)return;
        marketHooksEnabled=false;
        sellerResultHook?.Disable();marketHistoryHook?.Disable();
        MarketBoard.HistoryReceived-=SellerHistoryReceived;
        MarketBoard.OfferingsReceived-=SellerOfferingsReceived;
        MarketBoard.PurchaseRequested-=MarketPurchaseRequested;
        MarketBoard.ItemPurchased-=MarketPurchased;
        Addons.UnregisterListener(AddonEvent.PostSetup,"RetainerList",SellerBellOpened);
        Addons.UnregisterListener(AddonEvent.PostSetup,"RetainerSell",SellerEditorOpened);
        Addons.UnregisterListener(AddonEvent.PreReceiveEvent,"RetainerSell",SellerCompareOpening);
        Addons.UnregisterListener(AddonEvent.PostSetup,"RetainerSellList",SellerListOpened);
        Addons.UnregisterListener(AddonEvent.PostReceiveEvent,"ItemSearchResult",SellerPriceClicked);
        Addons.UnregisterListener(AddonEvent.PostReceiveEvent,new[]{"RetainerSell","ItemSearchResult"},SellerNativeEvent);
        Addons.UnregisterListener(AddonEvent.PreFinalize,new[]{"RetainerList","RetainerSellList"},MarketHighlightFinalizing);
        Chat.LogMessage-=ObserveMarketSale;
        manualEditor=null;marketActor=null;marketPurchase=null;sellerInventoryRetainer=0;RestoreMarketHighlights();
    }
    private unsafe void SetMarketEnabled(bool enabled)
    {
        config.Seller.Enabled=enabled;SaveConfiguration();
        if(enabled)EnableMarketHooks();else DisableMarketHooks();
    }
    private unsafe void SellerRequestResult(InfoProxyItemSearch* proxy,byte count,int error)
    {
        sellerResultHook!.Original(proxy,count,error);
        if(config.Seller.Enabled && proxy!=null && sellerComparison.Response(proxy->SearchItemId%1_000_000,count,error,DateTimeOffset.UtcNow))
            TraceSeller("Comparison response",$"Item {sellerComparison.Item} · {count} listings · error {error}");
    }
    private void SellerOfferingsReceived(IMarketBoardCurrentOfferings offerings)
    {
        if(!config.Seller.Enabled||sellerOfferings.Count>=32)return;
        var rows=offerings.ItemListings.Select(x=>new SellerReceivedOffer(x.ListingId,x.ItemId%1_000_000,new(x.RetainerId,x.PricePerUnit,x.IsHq,x.ItemQuantity,x.OnMannequin))).ToArray();
        sellerOfferings.Enqueue((offerings.RequestId,rows,DateTimeOffset.UtcNow));
    }
    private void DrainSellerComparison()
    {
        while(sellerOfferings.TryDequeue(out var packet))if(sellerComparison.ReceiveOffers(packet.Request,packet.Rows,packet.At))
            TraceSeller("Comparison listings",$"Item {sellerComparison.Item} · {sellerComparison.ReceivedCount}/{sellerComparison.Expected} fresh listings");
        while(sellerHistories.TryDequeue(out var h))if(sellerComparison.ReceiveHistory(h.Item,h.Sales,h.At))
            TraceSeller("Comparison history",$"Item {h.Item} · {h.Sales.Length} recent sales received");
    }
    private unsafe void SellerCompareOpening(AddonEvent e,AddonArgs args)
    {
        if(!config.Seller.Enabled||sellerDispatchingCompare||args is not AddonReceiveEventArgs ev||(int)ev.AtkEventType!=25||ev.EventParam!=4)return;
        var item=SellerEditorItem(out _,out _);
        if(item==null)return;
        ArmSellerComparison(item->ItemId%1_000_000,(item->Flags&InventoryItem.ItemFlags.HighQuality)!=0);
    }
    private void ArmSellerComparison(uint item,bool hq)
    {
        sellerComparison.Begin(item,Player.CurrentWorld.RowId,hq,DateTimeOffset.UtcNow);
        if(!sellerRun.Running){manualCompareAt=sellerComparison.RequestedAt;manualDecision=null;}
        TraceSeller("Comparison request",$"Item {item} · new listings and history request");
    }

    private void SellerHistoryReceived(IMarketBoardHistory history)
    {
        if(!config.Seller.Enabled || sellerHistories.Count>=32)return;
        sellerHistories.Enqueue((history.ItemId%1_000_000,history.HistoryListings.Select(x=>new SellerSale(x.SalePrice,x.IsHq,x.Quantity,new DateTimeOffset(DateTime.SpecifyKind(x.PurchaseTime,DateTimeKind.Utc)),x.OnMannequin)).ToArray(),DateTimeOffset.UtcNow));
    }
    private void SellerBellOpened(AddonEvent e,AddonArgs args)
    {
        marketPollAt=DateTimeOffset.UtcNow;
        TraceSeller("Bell","Retainer list opened");
        if(config.Seller.Enabled && config.Seller.OpenAtBell && !sellerRun.Running)sellerWindow.IsOpen=true;
    }
    private void SellerListOpened(AddonEvent e,AddonArgs args){sellerInventoryRetainer=0;marketPollAt=DateTimeOffset.UtcNow.AddMilliseconds(300);}
    private void SellerEditorOpened(AddonEvent e,AddonArgs args)=>BeginManualSeller();
    private void BeginManualSeller()
    {
        if(!config.Seller.Enabled || sellerRun.Running)return;
        manualEditor=null;manualDecision=null;manualRetainer=0;manualEditorAt=DateTimeOffset.UtcNow;manualCompareAt=default;manualSuspendUntilClose=false;
        sellerComparison.Clear();manualInventory=default;marketPollAt=DateTimeOffset.UtcNow;
        manualAutoCompare=config.Seller.OpenCompare&&!Keys[Dalamud.Game.ClientState.Keys.VirtualKey.SHIFT];
        manualShowHistory=config.Seller.OpenHistory^(config.Seller.AltTogglesHistory&&Keys[Dalamud.Game.ClientState.Keys.VirtualKey.MENU]);
        manualPastePrice=null;
        if(config.Seller.CtrlPastePrice&&Keys[Dalamud.Game.ClientState.Keys.VirtualKey.CONTROL]){
            manualAutoCompare=false;
            if(int.TryParse(Dalamud.Bindings.ImGui.ImGui.GetClipboardText(),out var value)&&value is >0 and <=SellerPricing.MaxPrice)manualPastePrice=value;
            else sellerNotice="Clipboard does not contain a valid unit price.";
        }
        TraceSeller("Manual editor","Opened",new {manualAutoCompare,manualShowHistory,shift=Keys[Dalamud.Game.ClientState.Keys.VirtualKey.SHIFT],ctrl=Keys[Dalamud.Game.ClientState.Keys.VirtualKey.CONTROL],alt=Keys[Dalamud.Game.ClientState.Keys.VirtualKey.MENU]});
        AcquireMarketBuddyLock();
    }
    private unsafe void SellerPriceClicked(AddonEvent e,AddonArgs args)
    {
        if(!config.Seller.Enabled || args is not AddonReceiveEventArgs ev || (int)ev.AtkEventType!=35 || ev.AtkEventData==0)return;
        if(sellerRun.Running){TraceSeller("Price click ignored","Automatic run is active; stop it before manual editing.");return;}
        if(manualEditor is null){TraceSeller("Price click ignored","Manual item was not captured.",sellerLastNativeState);return;}
        if(manualSuspendUntilClose){TraceSeller("Price click ignored","Editor is held after stopping a run; close and reopen it.");return;}
        if(!AcquireMarketBuddyLock()){TraceSeller("Price click ignored",sellerNotice);return;}
        try {
            DrainSellerComparison();
            var data=(AtkEventData*)ev.AtkEventData;
            var proxy=InfoProxyItemSearch.Instance();var index=data->ListItemData.SelectedIndex;
            if(proxy==null || !sellerComparison.Ready(proxy->SearchItemId%1_000_000,Player.CurrentWorld.RowId,(int)proxy->ListingCount) || proxy->SearchItemId%1_000_000!=manualEditor.ItemId || index<0 || index>=Math.Min(100,proxy->ListingCount)){TraceSeller("Price click ignored","Comparison identity, loading state or row index is not ready.",new {index,item=manualEditor.ItemId,searchItem=proxy==null?0:proxy->SearchItemId,waiting=proxy!=null&&proxy->WaitingForListings,count=proxy==null?0:proxy->ListingCount,sellerComparison.Expected,sellerComparison.ReceivedCount,sellerComparison.Error});return;}
            var listing=proxy->Listings[index];
            var renderer=data->ListItemData.ListItemRenderer;
            if(renderer!=null&&renderer->UldManager.NodeList!=null&&renderer->UldManager.NodeListCount>=14){
                var text=renderer->UldManager.NodeList[10];
                if(text!=null&&text->Type==NodeType.Text&&SellerPricing.TryReadGil(((AtkTextNode*)text)->NodeText.ToString(),out var clickedPrice)){
                    var candidates=proxy->Listings[..(int)Math.Min(100,proxy->ListingCount)].ToArray().Where(x=>x.UnitPrice==clickedPrice&&x.ItemId%1_000_000==manualEditor.ItemId&&x.IsHqItem==manualEditor.Hq&&!x.IsMannequin).ToArray();
                    if(candidates.Length==0){TraceSeller("Price click ignored","Displayed price has no matching item/quality offer.",new {clickedPrice,manualEditor.ItemId,manualEditor.Hq});return;}listing=candidates[0];
                }else {TraceSeller("Price click ignored","Displayed unit price could not be read.",new {nodeType=text==null?-1:(int)text->Type,text=text!=null&&text->Type==NodeType.Text?((AtkTextNode*)text)->NodeText.ToString():""});return;}
            }else {TraceSeller("Price click ignored","Comparison row renderer is unavailable.",new {index,nodeCount=renderer==null?0:renderer->UldManager.NodeListCount});return;}
            if(!sellerComparison.Contains(listing.ListingId,new(listing.RetainerId,listing.UnitPrice,listing.IsHqItem,listing.Quantity,listing.IsMannequin)) || listing.UnitPrice is 0 or >SellerPricing.MaxPrice || listing.ItemId%1_000_000!=manualEditor.ItemId || listing.IsMannequin || listing.IsHqItem!=manualEditor.Hq){TraceSeller("Price click ignored","Offer did not match the current item and quality.");return;}
            var own=KnownSellerRetainers();
            // Sorted rows can share a displayed price. If a verified own offer
            // is at that same price, match it rather than undercut our other stack.
            var matchOwn=proxy->Listings[..(int)Math.Min(100,proxy->ListingCount)].ToArray().Any(x=>x.RetainerId!=0&&own.Contains(x.RetainerId)&&x.UnitPrice==listing.UnitPrice&&x.IsHqItem==manualEditor.Hq&&!x.IsMannequin&&
                sellerComparison.Contains(x.ListingId,new(x.RetainerId,x.UnitPrice,x.IsHqItem,x.Quantity,x.IsMannequin)));
            var price=SellerPricing.OfferPrice(listing.UnitPrice,matchOwn,config.Seller,config.Seller.MinimumPrices.GetValueOrDefault(manualEditor.ItemId,1));
            if(config.Seller.CopyPrice)Dalamud.Bindings.ImGui.ImGui.SetClipboardText(price.ToString());
            var applied=config.Seller.ClickSetsPrice&&SetManualSellerPrice(price,config.Seller.ClickConfirms);
            sellerNotice=$"Selected {price:N0} gil"+(matchOwn?" · own retainer match":"")+(config.Seller.CopyPrice?" · copied":"")+(config.Seller.ClickSetsPrice?(applied?" · applied to editor":" · editor changed or unavailable; not submitted"):"");
            TraceSeller("Price click",sellerNotice,new {price,applied,matchOwn,config.Seller.ClickSetsPrice,config.Seller.CopyPrice,config.Seller.ClickConfirms});
        }catch(Exception ex){sellerNotice="Could not use this price; no automatic retry.";TraceSeller("Price click failed",ex.GetType().Name,new {ex.Message,ex.StackTrace});Log.Debug(ex,sellerNotice);}
    }
    private HashSet<ulong> KnownSellerRetainers()=>marketLedger.Retainers.Select(x=>x.Id).Concat(ReadSellerRetainers().Select(x=>x.Id)).Where(x=>x!=0).ToHashSet();
    private static unsafe AtkUnitBase* SellerAddon(string name)
    {var a=(AtkUnitBase*)GardenGui.GetAddonByName(name).Address;return a!=null&&a->IsVisible&&a->IsReady?a:null;}
    private unsafe bool SellerContext(ulong cid,uint world)=>config.Seller.Enabled && Player.IsLoaded && Player.ContentId==cid && Player.CurrentWorld.RowId==world && Player.HomeWorld.RowId==world && !Conditions[ConditionFlag.BetweenAreas] && !Conditions[ConditionFlag.BetweenAreas51] && !Conditions[ConditionFlag.InCombat] && !HelperInCutscene();
    private static unsafe bool SellerActive(ulong id)
    {
        var rm=RetainerManager.Instance();
        if(id==0||rm==null||!rm->IsReady||rm->LastSelectedRetainerId!=id||!Conditions[ConditionFlag.OccupiedSummoningBell]||SellerAddon("RetainerList")!=null)return false;
        // AgentRetainer.IsAgentActive only describes its inventory UI, not
        // the summoned retainer's greeting or SelectString menu.
        var ui=UIModule.Instance();var order=ui==null?null:ui->GetItemOrderModule();
        if(order!=null&&order->ActiveRetainerId==id)return true;
        var talk=SellerAddon("Talk");var r=rm->GetActiveRetainer();
        return talk!=null&&r!=null&&talk->AtkValues!=null&&talk->AtkValuesCount>1&&
            ((int)talk->AtkValues[1].Type&15) is 8 or 10&&TravelMenuText(talk->AtkValues[1].String.Value)==r->NameString;
    }
    private static unsafe InventoryItem* SellerEditorItem(out InventoryType inventory,out int slot)
    {
        inventory=default;slot=-1;
        var agent=AgentRetainer.Instance();var manager=InventoryManager.Instance();
        if(agent==null||manager==null||SellerAddon("RetainerSell")==null)return null;
        inventory=agent->SellItemInventoryType;slot=agent->SellItemInventorySlot;
        var container=manager->GetInventoryContainer(inventory);
        if(container==null||!container->IsLoaded||slot<0||slot>=container->Size)return null;
        // SetSlotBlocked makes the original slot symbolic while its item is
        // held in BlockedItems. Reading raw ItemId would read a slot number.
        var item=SellerNativeInterop.Resolve(container->GetInventorySlot(slot));
        return item!=null&&item->ItemId>0&&item->Quantity>0?item:null;
    }
    private unsafe SellerRetainerRef[] ReadSellerRetainers()
    {
        var rm=RetainerManager.Instance();if(!Player.IsLoaded||rm==null||!rm->IsReady)return [];
        var result=new List<SellerRetainerRef>();
        for(uint i=0;i<Math.Min(10,(int)rm->GetRetainerCount());i++){var r=rm->GetRetainerBySortedIndex(i);if(r!=null&&r->RetainerId!=0&&r->Available)result.Add(new(r->RetainerId,r->NameString));}
        return result.ToArray();
    }
    private unsafe MarketStock[]? ReadSellerStock(ulong id)
    {
        if(!SellerActive(id)||SellerAddon("RetainerSellList")==null)return null;
        if(sellerInventoryRetainer!=id){sellerInventoryRetainer=id;sellerInventorySince=DateTimeOffset.UtcNow;return null;}
        if(DateTimeOffset.UtcNow-sellerInventorySince<TimeSpan.FromSeconds(1))return null;
        var manager=InventoryManager.Instance();var agent=AgentRetainer.Instance();
        var inventory=manager==null?null:manager->GetInventoryContainer(InventoryType.RetainerMarket);
        if(agent==null||inventory==null||!inventory->IsLoaded||inventory->Size!=20||inventory->Items==null||agent->SellListEntryCount is <0 or >20)return null;
        var rows=new List<MarketStock>();
        for(short i=0;i<20;i++){
            var slot=SellerNativeInterop.Resolve(inventory->GetInventorySlot(i));if(slot==null||slot->ItemId==0||slot->Quantity==0)continue;
            var price=manager->GetRetainerMarketPrice(i);if(price is 0 or >SellerPricing.MaxPrice)return null;
            rows.Add(new(i,slot->ItemId%1_000_000,SearchItemName(slot->ItemId%1_000_000),(slot->Flags&InventoryItem.ItemFlags.HighQuality)!=0,(uint)slot->Quantity,(int)price,DateTimeOffset.UtcNow));
        }
        // A stale inventory from the previous retainer must not become an empty snapshot.
        if(rows.Count!=agent->SellListEntryCount)return null;
        var entries=agent->SellListEntries[..agent->SellListEntryCount].ToArray();
        foreach(var row in rows)if(!entries.Any(x=>x.InventorySlot==row.Slot&&x.ItemId%1_000_000==row.ItemId&&x.Quantity==row.Quantity))return null;
        var list=SellerAddon("RetainerSellList");
        var order=Enumerable.Range(0,20).Select(i=>(Slot:ReadMarketDisplaySlot(list,i),Index:i)).Where(x=>x.Slot>=0).GroupBy(x=>x.Slot).ToDictionary(g=>g.Key,g=>g.First().Index);
        return rows.Select(x=>x with {DisplayOrder=order.GetValueOrDefault(x.Slot,x.Slot)}).OrderBy(x=>x.DisplayOrder).ToArray();
    }
    private unsafe bool SellerEditorMatches(ulong retainer,MarketStock item,bool checkPrice=true)
    {
        if(!SellerActive(retainer))return false;
        var edit=(AddonRetainerSell*)SellerAddon("RetainerSell");var agent=AgentRetainer.Instance();
        if(edit==null||edit->AskingPrice==null||edit->Quantity==null||agent==null||agent->SellItemInventoryType!=InventoryType.RetainerMarket||agent->SellItemInventorySlot!=item.Slot||edit->Quantity->Value!=item.Quantity)return false;
        var native=SellerEditorItem(out var type,out var slot);
        return native!=null&&type==InventoryType.RetainerMarket&&slot==item.Slot&&native->ItemId%1_000_000==item.ItemId&&
            ((native->Flags&InventoryItem.ItemFlags.HighQuality)!=0)==item.Hq&&native->Quantity==item.Quantity&&
            (!checkPrice||edit->AskingPrice->Value==item.Price);
    }
    private unsafe bool SellerButton(AtkUnitBase* addon,AtkComponentButton* button,uint nodeId)
    {
        var detail=new {addon=addon==null?"missing":addon->NameString,expectedNode=nodeId,actualNode=button==null||button->OwnerNode==null?0:button->OwnerNode->NodeId,enabled=button!=null&&button->IsEnabled,events=SellerButtonEvents(button)};
        if(!SellerNativeInterop.ButtonEvent(addon,button,nodeId,out var click)){TraceSeller("Button blocked","Native button identity/readiness check failed.",detail);return false;}
        TraceSeller("Button requested",$"{detail.addon} · node {nodeId} · event {(int)click.State.EventType}:{click.Param}",detail);
        var data=new AtkEventData();addon->ReceiveEvent(click.State.EventType,(int)click.Param,&click,&data);return true;
    }
    private unsafe bool BeginSellerComparison(uint item)
    {
        var edit=(AddonRetainerSell*)SellerAddon("RetainerSell");if(edit==null){TraceSeller("Compare blocked","Price editor is not visible and ready.");return false;}
        if(sellerRun.Running&&sellerResultHook?.IsEnabled!=true){TraceSeller("Compare blocked","Live result hook is unavailable.");return false;}
        if(SellerAddon("ItemSearchResult")!=null){
            // A native Compare click during the run was observed before the
            // request. Adopt that request instead of blocking on our own UI.
            if(sellerComparison.Item==item&&sellerComparison.World==Player.CurrentWorld.RowId)return true;
            TraceSeller("Compare blocked","A different comparison window is already open.");return false;
        }
        if(InfoProxyItemSearch.Instance()==null){TraceSeller("Compare blocked","Market search proxy is unavailable.");return false;}
        var slot=SellerEditorItem(out _,out _);
        if(slot==null||slot->ItemId%1_000_000!=item){TraceSeller("Compare blocked","Editor inventory item does not match the request.",new {expected=item,actual=slot==null?0:slot->ItemId});return false;}
        // WaitingForListings stays true even after completed searches in the
        // supplied game trace. The enabled native button accepts a new search.
        ArmSellerComparison(item,(slot->Flags&InventoryItem.ItemFlags.HighQuality)!=0);
        sellerDispatchingCompare=true;
        try {if(SellerButton((AtkUnitBase*)edit,edit->ComparePrices,4))return true;}
        finally {sellerDispatchingCompare=false;}
        sellerComparison.Clear();manualCompareAt=default;return false;
    }
    private unsafe SellerComparison? ReadSellerComparison()
    {
        DrainSellerComparison();
        var capture=sellerComparison;
        if(capture.Item==0||capture.Expected<0||capture.RespondedAt<capture.RequestedAt)return null;
        if(capture.Error!=0)return new(capture.Item,capture.World,capture.RequestedAt,false,[],false,[],"Game comparison failed ("+capture.Error+"); existing price kept.");
        var proxy=InfoProxyItemSearch.Instance();var result=(AddonItemSearchResult*)SellerAddon("ItemSearchResult");
        if(proxy==null||result==null||!capture.Ready(proxy->SearchItemId%1_000_000,Player.CurrentWorld.RowId,(int)proxy->ListingCount))return null;
        var rows=new List<SellerOffer>();
        for(var i=0;i<capture.Expected;i++){
            var x=proxy->Listings[i];var offer=new SellerOffer(x.RetainerId,x.UnitPrice,x.IsHqItem,x.Quantity,x.IsMannequin);
            if(x.ItemId%1_000_000!=capture.Item||!capture.Contains(x.ListingId,offer))return null;
            rows.Add(offer);
        }
        var emptyQuality=!rows.Any(x=>x.Hq==capture.Hq&&!x.Mannequin);
        var wantHistory=(sellerRun.Running?config.Seller.OpenHistory:manualShowHistory) || emptyQuality && config.Seller.UseHistory;
        if(wantHistory&&!capture.HistoryRequested)capture.HistoryRequested=SellerButton((AtkUnitBase*)result,result->History,23);
        if(wantHistory&&!capture.HistoryReceived)return null;
        return new(capture.Item,capture.World,capture.RequestedAt,true,rows.ToArray(),capture.HistoryReceived,capture.History);
    }

    private unsafe bool CancelSellerEditor(ulong retainer,MarketStock item)
    {
        if(SellerAddon("RetainerSell")!=null&&!SellerEditorMatches(retainer,item))return false;
        if(SellerAddon("RetainerSell")==null){sellerComparison.Clear();return true;}
        // Do not dismiss an editor the player has changed or opened independently.
        CloseSellerAddon("ItemSearchHistory");
        CloseSellerAddon("ItemSearchResult");
        var edit=(AddonRetainerSell*)SellerAddon("RetainerSell");
        var cancelled=edit!=null&&SellerButton((AtkUnitBase*)edit,edit->Cancel,22);
        sellerComparison.Clear();return cancelled;
    }
    private static unsafe void CloseSellerAddon(string name){var a=SellerAddon(name);if(a!=null)a->Close(true);}
    private bool AcquireMarketBuddyLock()
    {
        if(marketBuddyLocked)return true;
        if(!Pi.InstalledPlugins.Any(x=>x.IsLoaded&&x.InternalName.Equals("Marketbuddy",StringComparison.OrdinalIgnoreCase)))return true;
        try{
            if(Pi.GetIpcSubscriber<string,bool>("Marketbuddy.IsLocked").InvokeFunc(null!)){sellerNotice="Another tool is using Marketbuddy’s pricing lock.";return false;}
            marketBuddyLocked=Pi.GetIpcSubscriber<string,bool>("Marketbuddy.Lock").InvokeFunc("EquinoxSeller");return marketBuddyLocked;
        }catch{sellerNotice="Disable Marketbuddy before using Seller: its pricing lock is unavailable.";return false;}
    }
    private void ReleaseMarketBuddyLock()
    {
        if(marketBuddyLocked){try{Pi.GetIpcSubscriber<string,bool>("Marketbuddy.Unlock").InvokeFunc("EquinoxSeller");}catch{}marketBuddyLocked=false;}
    }
    private void ReleaseSellerLocks()
    {
        ReleaseMarketBuddyLock();
        sellerAutoRetainerPause?.Release(DateTimeOffset.UtcNow);
        if(sellerAutoRetainerPause!=null)TraceAutoRetainerHandoff();
        sellerComparison.Clear();
    }
    private unsafe void StartSeller(bool apply)
    {
        if(!config.Seller.Enabled||!marketHooksEnabled||marketLoad!=null||marketLoadFailed){sellerNotice="Enable Market and wait for its saved records first.";return;}
        if(sellerResultHook?.IsEnabled!=true){sellerNotice=sellerLiveError.Length>0?sellerLiveError:"Live comparison confirmation is unavailable. Toggle Market & Seller off and on to retry.";return;}
        if(sellerRun.Running||SellerAddon("RetainerSell")!=null){sellerNotice="Close the current item editor before starting a run.";return;}
        if(!Player.IsLoaded||Player.HomeWorld.RowId!=Player.CurrentWorld.RowId){sellerNotice="Use your home world’s summoning bell.";return;}
        var all=ReadSellerRetainers();
        var current=RetainerManager.Instance()==null?0:RetainerManager.Instance()->LastSelectedRetainerId;
        var selected=sellerScope switch {0=>all.Where(x=>x.Id==current&&SellerActive(current)).ToArray(),1=>all.Where(x=>config.Seller.SelectedRetainers.Contains(x.Id)).ToArray(),_=>all};
        if(selected.Length==0){sellerNotice="Select a retainer or choose All retainers.";return;}
        if(SellerAddon("RetainerList")==null&&!SellerActive(current)){sellerNotice="Open the summoning-bell retainer list first.";return;}
        ReleaseMarketBuddyLock();
        var handoff=sellerAutoRetainerPause.Acquire(DateTimeOffset.UtcNow);TraceAutoRetainerHandoff();
        if(handoff.Error.Length>0){sellerNotice=handoff.Error;ReleaseSellerLocks();TraceSeller("Run blocked",sellerNotice);return;}
        sellerSelectRetry=default;manualEditor=null;manualSuspendUntilClose=false;sellerNotice="";
        TraceSeller("Run requested",apply?"Update prices":"Check prices",new {retainers=selected,character=Player.ContentId,world=Player.CurrentWorld.RowId});
        try{sellerRun.Start(selected,Player.ContentId,Player.CurrentWorld.RowId,apply,config.Seller,marketLedger.Retainers.Select(x=>x.Id).Concat(all.Select(x=>x.Id)),DateTimeOffset.UtcNow);}
        catch{ReleaseSellerLocks();throw;}
        if(!sellerRun.Running)ReleaseSellerLocks();
    }
    private unsafe void UpdateEquinoxMarket(DateTimeOffset now)
    {
        UpdateSellerAutoRetainer(now);
        if(marketLoad is {IsCompleted:true}) {
            if(marketLoad.IsCompletedSuccessfully)marketLedger=marketLoad.Result;
            else {marketLoadFailed=true;marketDataError="Saved market data could not be read. Existing file kept; tracking is paused.";Log.Error(marketLoad.Exception!,marketDataError);}
            marketLoad=null;
        }
        if(marketDirty&&now>=marketSaveAt&&!marketLoadFailed){marketWriter.Enqueue(marketLedger);marketDirty=false;}
        if(marketWriter.Failure is {} failure){marketDataError="Market save failed: "+failure.GetType().Name;if(!marketDirty){marketDirty=true;marketSaveAt=now.AddSeconds(5);}}
        else if(!marketLoadFailed)marketDataError="";
        if(!config.Seller.Enabled||marketLoad!=null||marketLoadFailed)return;
        try {
            DrainSellerComparison();
            DrainMarketSales();
            sellerRun.Tick(now);
            UpdateMarketJournal(now);
            UpdateMarketWatch(now);
            UpdateMarketBar(now);
            if(now<marketPollAt)return;marketPollAt=now.AddMilliseconds(500);
            ObserveSellerDiagnostics(now);
            if(!Player.IsLoaded){manualEditor=null;sellerInventoryRetainer=0;marketPurchase=null;ReleaseSellerLocks();RestoreMarketHighlights();return;}
            RememberMarketOrder();
            var rm=RetainerManager.Instance();var active=rm==null?0:rm->LastSelectedRetainerId;
            if(ReadSellerStock(active) is {} stock)RememberMarketStock(active,stock,now);
            RememberMarketVisit(active,now);
            if(!sellerRun.Running)UpdateManualSeller(active,now);
            UpdateMarketHighlights();
        }catch(Exception ex){sellerRun.Stop("Seller stopped after an unexpected response; check the last listing.");sellerNotice="Market tools paused: "+ex.GetType().Name;TraceSeller("Error",sellerNotice,new {message=ex.Message,stack=ex.StackTrace});throw;}
    }
    private unsafe void UpdateManualSeller(ulong active,DateTimeOffset now)
    {
        var edit=(AddonRetainerSell*)SellerAddon("RetainerSell");var agent=AgentRetainer.Instance();
        if(manualSuspendUntilClose){if(edit==null){manualSuspendUntilClose=false;manualEditor=null;}else return;}
        if(edit==null){manualEditor=null;manualEditorAt=default;sellerComparison.Clear();if(SellerAddon("RetainerList")==null&&SellerAddon("RetainerSellList")==null&&!SellerActive(active))ReleaseSellerLocks();else AcquireMarketBuddyLock();return;}
        if(agent==null||!SellerActive(active)){TraceSeller("Manual capture blocked",agent==null?"Retainer agent is unavailable.":"Active retainer identity has not been confirmed.",sellerLastNativeState);return;}
        if(manualEditorAt==default)BeginManualSeller();
        if(!AcquireMarketBuddyLock()){TraceSeller("Manual capture blocked",sellerNotice);return;}
        if(manualEditor is null && now-manualEditorAt>TimeSpan.FromMilliseconds(150)){
            var item=SellerEditorItem(out var type,out var slot);
            if(item!=null&&edit->AskingPrice!=null&&edit->Quantity!=null){
                var id=item->ItemId%1_000_000;
                if(type!=InventoryType.RetainerMarket&&config.Seller.LimitNewStacks){
                    var max=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(id)?.StackSize??1;
                    edit->Quantity->SetValue(SellerPricing.Stack(config.Seller.NewStackSize,item->Quantity,(int)max));
                }
                manualInventory=type;manualRetainer=active;
                manualEditor=new(slot,id,SearchItemName(id),(item->Flags&InventoryItem.ItemFlags.HighQuality)!=0,(uint)edit->Quantity->Value,edit->AskingPrice->Value,now);
                TraceSeller("Manual item","Captured",new {inventory=type.ToString(),manualRetainer,manualEditor});
                if(manualPastePrice is {} paste){sellerNotice=SetManualSellerPrice(paste,false)?$"Pasted {paste:N0} gil into the editor.":"The editor changed; clipboard price was not applied.";manualPastePrice=null;}
            }else if(now-manualEditorAt>TimeSpan.FromSeconds(3)){sellerNotice="The selected item is not ready yet. Diagnostics has the blocked item details.";TraceSeller("Manual capture blocked",sellerNotice,sellerLastNativeState);}
        }
        if(manualEditor!=null&&manualCompareAt==default&&manualAutoCompare){
            if(BeginSellerComparison(manualEditor.ItemId))manualCompareAt=now;
        }
        if(manualEditor!=null&&ReadSellerComparison() is {} comparison){
            var decision=SellerPricing.Decide(manualEditor.ItemId,Player.CurrentWorld.RowId,manualEditor.Hq,manualEditor.Price,comparison,KnownSellerRetainers(),config.Seller,now);
            if(decision!=manualDecision){manualDecision=decision;TraceSellerDecision(active,manualEditor,decision,false,"Manual suggestion");}
            sellerReviews[(active,manualEditor.Slot)]=(manualEditor,decision,now);
        }
    }
    private unsafe bool SetManualSellerPrice(int price,bool confirm)
    {
        if(price is <=0 or >SellerPricing.MaxPrice||manualEditor is not {} item || !SellerActive(manualRetainer))return false;
        var edit=(AddonRetainerSell*)SellerAddon("RetainerSell");var agent=AgentRetainer.Instance();
        if(edit==null||edit->AskingPrice==null||agent==null||agent->SellItemInventorySlot!=item.Slot)return false;
        var native=SellerEditorItem(out var type,out var slot);
        if(native==null||type!=manualInventory||slot!=item.Slot||native->ItemId%1_000_000!=item.ItemId||((native->Flags&InventoryItem.ItemFlags.HighQuality)!=0)!=item.Hq)return false;
        edit->AskingPrice->SetValue(price);
        if(edit->AskingPrice->Value!=price)return false;
        if(confirm){CloseSellerAddon("ItemSearchHistory");CloseSellerAddon("ItemSearchResult");return SellerButton((AtkUnitBase*)edit,edit->Confirm,21);}
        return true;
    }
    private unsafe void OpenMarketItem(MarketRetainer retainer,MarketStock item)
    {
        if(sellerRun.Running){sellerNotice="Stop the current price run before editing an item manually.";OpenEquinoxSeller();return;}
        if(!Player.IsLoaded||Player.ContentId!=retainer.CharacterId||!SellerActive(retainer.Id)||SellerAddon("RetainerSellList")==null){
            sellerNotice=$"Open {retainer.Name}'s selling list on {retainer.Character} to edit this listing.";OpenEquinoxSeller();return;
        }
        if(SellerAddon("RetainerSell")!=null){sellerNotice="Finish or cancel the current price editor first.";return;}
        var live=ReadSellerStock(retainer.Id)?.FirstOrDefault(x=>x.SameItem(item));
        if(live==null||live.Price!=item.Price){sellerNotice="This listing changed. Wait for the selling list to refresh.";return;}
        AgentRetainer.Instance()->OpenRetainerSell(InventoryType.RetainerMarket,(ushort)item.Slot);
    }
    private void RememberMarketOrder()
    {
        if(!Player.IsLoaded||DateTimeOffset.UtcNow<marketOrderAt)return;marketOrderAt=DateTimeOffset.UtcNow.AddSeconds(3);
        var order=ReadSellerRetainers();if(order.Length==0)return;
        var map=order.Select((r,i)=>(r.Id,i)).ToDictionary(x=>x.Id,x=>x.i);
        var changed=false;
        var retainers=marketLedger.Retainers.Select(r=>{
            if(r.CharacterId==Player.ContentId&&map.TryGetValue(r.Id,out var index)&&r.DisplayOrder!=index){changed=true;return r with {DisplayOrder=index};}return r;
        }).ToArray();
        if(changed){marketLedger=marketLedger with {Retainers=retainers};DirtyMarket();}
    }
    private unsafe void HandleSellerEscape()
    {
        if(!config.Seller.Enabled||!escapeRequested||escapePopupWasOpen||!(sellerWindow.IsFocused||equinoxMarketWindow.IsFocused||sellerToolbarFocused)||Dalamud.Bindings.ImGui.ImGui.IsAnyItemActive())return;
        if(!Conditions[ConditionFlag.OccupiedSummoningBell])return;
        foreach(var name in new[]{"ItemSearchHistory","ItemSearchResult","RetainerSell","RetainerSellList","RetainerHistory","SelectString","RetainerList"}){
            var addon=SellerAddon(name);if(addon==null)continue;
            if(sellerRun.Running)sellerRun.Stop("Stopped with Escape. Confirmed prices are kept.");
            if(name=="RetainerSell"){
                var editor=(AddonRetainerSell*)addon;if(!SellerButton(addon,editor->Cancel,22))return;
            }else addon->Close(true);
            // Consume once so neither the game nor our launcher closes a
            // second window on the same physical key press.
            escapeRequested=false;escapeMinimizedFrame=Dalamud.Bindings.ImGui.ImGui.GetFrameCount();Keys[Dalamud.Game.ClientState.Keys.VirtualKey.ESCAPE]=false;return;
        }
    }
    private void DirtyMarket(){if(!marketDirty)marketSaveAt=DateTimeOffset.UtcNow.AddSeconds(1);marketDirty=true;}
    private unsafe void RememberMarketVisit(ulong id,DateTimeOffset now)
    {
        if(!SellerActive(id)||(SellerAddon("SelectString")==null&&SellerAddon("RetainerSellList")==null))return;
        var old=marketLedger.Retainers.FirstOrDefault(x=>x.Id==id&&x.CharacterId==Player.ContentId);
        if(old==null||now-(old.LastVisit??old.At)<TimeSpan.FromMinutes(1))return;
        marketLedger=marketLedger with {Retainers=marketLedger.Retainers.Select(x=>x.Id==id?x with {LastVisit=now}:x).ToArray()};DirtyMarket();
    }
    private unsafe void RememberMarketStock(ulong id,MarketStock[] stock,DateTimeOffset now)
    {
        var rm=RetainerManager.Instance();var native=rm==null?null:rm->GetActiveRetainer();if(native==null||native->RetainerId!=id)return;
        var person=config.SharedRoster?.People.FirstOrDefault(p=>p.Characters.Any(c=>c.ContentId==Player.ContentId.ToString()));
        var character=person?.Characters.FirstOrDefault(c=>c.ContentId==Player.ContentId.ToString());
        var old=marketLedger.Retainers.FirstOrDefault(x=>x.Id==id);
        var fresh=MarketAccounting.PreserveListingDates(old,new MarketRetainer(id,native->NameString,Player.ContentId,Player.CharacterName,Player.HomeWorld.RowId,WorldName(Player.HomeWorld.RowId)??"",person?.Name??"",character?.Account??"",InventoryManager.Instance()->GetRetainerGil(),now,stock) with {DisplayOrder=Array.FindIndex(ReadSellerRetainers(),r=>r.Id==id)});
        if(old!=null&&old.Gil==fresh.Gil&&old.DisplayOrder==fresh.DisplayOrder&&old.Stock.SequenceEqual(fresh.Stock)&&now-old.At<TimeSpan.FromMinutes(1))return;
        var recovered=old==null?[]:MarketAccounting.ReconcileOffline(old,fresh,marketLedger.Income);
        marketLedger=marketLedger with {Retainers=[..marketLedger.Retainers.Where(x=>x.Id!=id),fresh with {LastVisit=now}],Income=recovered.Length==0?marketLedger.Income:[..marketLedger.Income,..recovered]};DirtyMarket();
    }
    private sealed class SellerPort(Plugin p):ISellerPort
    {
        public SellerPreparation Prepare()=>p.PrepareSellerAutomation();
        private (ulong Retainer,int Slot)? requestedEditor;
        private ulong requestedMarket;
        public bool ContextMatches(ulong c,uint w)=>p.SellerContext(c,w);
        public unsafe bool SelectRetainer(SellerRetainerRef r){
            if(SellerActive(r.Id))return true;
            var active=RetainerManager.Instance()==null?0:RetainerManager.Instance()->LastSelectedRetainerId;
            if(SellerActive(active)){CloseRetainer(active);return false;}
            if(DateTimeOffset.UtcNow<p.sellerSelectRetry)return false;
            var a=SellerAddon("RetainerList");var rm=RetainerManager.Instance();if(a==null||rm==null||!rm->IsReady)return false;
            for(uint i=0;i<Math.Min(10,(int)rm->GetRetainerCount());i++){var x=rm->GetRetainerBySortedIndex(i);if(x==null||x->RetainerId!=r.Id||!x->Available)continue;
                var v=stackalloc AtkValue[4];for(var n=0;n<4;n++)v[n]=default;v[0].Type=AtkValueType.Int;v[0].Int=2;v[1].Type=AtkValueType.UInt;v[1].UInt=i;
                a->FireCallback(4,v,true);p.sellerSelectRetry=DateTimeOffset.UtcNow.AddSeconds(2);return false;}return false;
        }
        public unsafe bool OpenMarket(ulong id){
            if(!SellerActive(id))return false;if(SellerAddon("RetainerSellList")!=null){requestedMarket=0;return true;}
            if(requestedMarket==id)return false;
            var menu=(AddonSelectString*)SellerAddon("SelectString");if(menu==null){p.SellerRetainerTalk(id);return false;}
            var label=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Addon>().GetRowOrDefault(2380)?.Text.ToString();if(string.IsNullOrEmpty(label))return false;
            for(var i=0;i<Math.Min(20,(int)menu->PopupMenu.PopupMenu.EntryCount);i++){
                var text=TravelMenuText(menu->PopupMenu.PopupMenu.EntryNames[i].Value);if(text!=label)continue;requestedMarket=id;menu->FireCallbackInt(i);return false;
            }return false;
        }
        public MarketStock[]? ReadStock(ulong r)=>p.ReadSellerStock(r);
        public unsafe bool OpenEditor(ulong r,MarketStock item){
            if(p.SellerEditorMatches(r,item))return true;
            if(SellerAddon("RetainerSell")!=null||requestedEditor==(r,item.Slot))return false;
            var stock=p.ReadSellerStock(r)?.FirstOrDefault(x=>x.SameItem(item));if(stock==null||stock.Price!=item.Price)return false;
            requestedEditor=(r,item.Slot);p.sellerReviews[(r,item.Slot)]=(item,new(SellerStatus.Checking,null,"Checking live market prices…","Live listings"),DateTimeOffset.UtcNow);
            AgentRetainer.Instance()->OpenRetainerSell(InventoryType.RetainerMarket,(ushort)item.Slot);return false;
        }
        public bool EditorMatches(ulong r,MarketStock item)=>p.SellerEditorMatches(r,item);
        public bool BeginComparison(uint item)=>p.BeginSellerComparison(item);
        public SellerComparison? Comparison=>p.ReadSellerComparison();
        public unsafe bool ApplyPrice(ulong r,MarketStock item,int price){
            if(!p.SellerEditorMatches(r,item)||price is <=0 or >SellerPricing.MaxPrice)return false;
            CloseSellerAddon("ItemSearchHistory");CloseSellerAddon("ItemSearchResult");
            var edit=(AddonRetainerSell*)SellerAddon("RetainerSell");if(edit==null)return false;
            edit->AskingPrice->SetValue(price);return p.SellerButton((AtkUnitBase*)edit,edit->Confirm,21);
        }
        public unsafe bool PriceConfirmed(ulong r,MarketStock item,int price)=>SellerAddon("RetainerSell")==null&&p.ReadSellerStock(r)?.Any(x=>x.SameItem(item)&&x.Price==price)==true;
        public bool CancelEditor(ulong r,MarketStock item){var ok=p.CancelSellerEditor(r,item);if(ok)requestedEditor=null;return ok;}
        public unsafe bool CloseRetainer(ulong id){
            if(SellerAddon("RetainerList")!=null)return true;
            if(!SellerActive(id))return false;
            var list=SellerAddon("RetainerSellList");if(list!=null){list->Close(true);return false;}
            var menu=(AddonSelectString*)SellerAddon("SelectString");if(menu!=null){
                var quit=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Addon>().GetRowOrDefault(2383)?.Text.ToString();
                for(var i=0;i<Math.Min(20,(int)menu->PopupMenu.PopupMenu.EntryCount);i++)if(TravelMenuText(menu->PopupMenu.PopupMenu.EntryNames[i].Value)==quit){menu->FireCallbackInt(i);break;}
                return false;
            }
            p.SellerRetainerTalk(id);return false;
        }
        public void Review(SellerRetainerRef r,MarketStock item,SellerDecision d,bool applied){
            p.TraceSellerDecision(r.Id,item,d,applied,"Run");
            p.sellerReviews[(r.Id,item.Slot)]=(applied?item with {Price=d.Price!.Value}:item,applied?d with {Status=SellerStatus.Competitive,Undercut=d.LowestCompetitor<d.Price,Reason="Price update observed in the selling list. "+d.Reason}:d,DateTimeOffset.UtcNow);
            var entry=new MarketPriceLog(DateTimeOffset.UtcNow,r.Id,r.Name,item.ItemId,item.Name,item.Hq,item.Price,d.Price,applied?"Updated":d.Status.ToString(),d.Reason);
            p.marketLedger=p.marketLedger with {Prices=[..p.marketLedger.Prices.TakeLast(1999),entry]};p.DirtyMarket();
        }
        public unsafe void Release(){requestedEditor=null;requestedMarket=0;if(SellerAddon("RetainerSell")!=null)p.manualSuspendUntilClose=true;p.ReleaseSellerLocks();}
    }
    private unsafe void SellerRetainerTalk(ulong id)
    {
        // Only advance the selected retainer's greeting/farewell, never general dialogue.
        if(!SellerActive(id))return;var talk=SellerAddon("Talk");if(talk==null)return;
        var r=ReadSellerRetainers().FirstOrDefault(x=>x.Id==id);
        if(talk->AtkValues==null||talk->AtkValuesCount<2||r==null)return;
        var speaker=talk->AtkValues[1];if(((int)speaker.Type&15) is not (8 or 10)||TravelMenuText(speaker.String.Value)!=r.Name)return;
        ClickVisibleTalk();
    }
    private void DisposeEquinoxMarket()
    {
        DisableMarketHooks();marketWatchClient.Dispose();sellerResultHook?.Dispose();marketHistoryHook?.Dispose();DisposeMarketWindows();
        if(sellerAutoRetainerPause.RestorePending){Log.Warning("AutoRetainer pause could not be released during Companion unload. Resume it from AutoRetainer's controls.");errorJournal.Record("seller-autoretainer","AutoRetainer pause release failed during Companion unload; resume from AutoRetainer controls.");}
        if(marketLoad==null&&!marketLoadFailed)try{marketWriter.Flush(marketLedger);}catch(Exception ex){Log.Error(ex,"Market ledger could not be saved");}
    }
}
