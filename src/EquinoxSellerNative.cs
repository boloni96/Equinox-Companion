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
    private bool marketDirty,marketLoadFailed,marketHooksEnabled,marketBuddyLocked,autoRetainerSuppressed;
    private DateTimeOffset marketSaveAt,marketPollAt,sellerRequestAt,sellerResultAt,manualEditorAt,manualCompareAt;
    private string sellerNotice="",marketDataError="";
    private uint sellerRequestItem,sellerRequestWorld;
    private int sellerExpected=-1,sellerError;
    private bool sellerHistoryReceived,sellerHistoryRequested;
    private bool sellerRequestHq,autoRetainerPreviouslySuppressed;
    private ulong sellerInventoryRetainer;
    private DateTimeOffset sellerInventorySince;
    private SellerSale[] sellerHistory=[];
    private MarketStock? manualEditor;
    private ulong manualRetainer;
    private bool manualAutoCompare,manualShowHistory,manualSuspendUntilClose;
    private int? manualPastePrice;
    private readonly ConcurrentQueue<(uint Item,SellerSale[] Sales,DateTimeOffset At)> sellerHistories=new();
    private readonly Dictionary<(ulong Retainer,int Slot),(MarketStock Item,SellerDecision Decision,DateTimeOffset At)> sellerReviews=[];
    private unsafe delegate void SellerResultDelegate(InfoProxyItemSearch* proxy,byte count,int error);
    private Hook<SellerResultDelegate>? sellerResultHook;
    private string MarketDataPath=>Path.Combine(Pi.GetPluginConfigDirectory(),"market-ledger.json");

    private unsafe void InitializeEquinoxMarket()
    {
        config.Seller??=new();
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
            sellerResultHook??=Interop.HookFromAddress<SellerResultDelegate>(InfoProxyItemSearch.MemberFunctionPointers.ProcessRequestResult,SellerRequestResult);
            sellerResultHook.Enable();
            MarketBoard.HistoryReceived+=SellerHistoryReceived;
            MarketBoard.PurchaseRequested+=MarketPurchaseRequested;
            MarketBoard.ItemPurchased+=MarketPurchased;
            Addons.RegisterListener(AddonEvent.PostSetup,"RetainerList",SellerBellOpened);
            Addons.RegisterListener(AddonEvent.PreSetup,"RetainerSell",SellerEditorOpened);
            Addons.RegisterListener(AddonEvent.PostSetup,"RetainerSellList",SellerListOpened);
            Addons.RegisterListener(AddonEvent.PostReceiveEvent,"ItemSearchResult",SellerPriceClicked);
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
        sellerResultHook?.Disable();
        MarketBoard.HistoryReceived-=SellerHistoryReceived;
        MarketBoard.PurchaseRequested-=MarketPurchaseRequested;
        MarketBoard.ItemPurchased-=MarketPurchased;
        Addons.UnregisterListener(AddonEvent.PostSetup,"RetainerList",SellerBellOpened);
        Addons.UnregisterListener(AddonEvent.PreSetup,"RetainerSell",SellerEditorOpened);
        Addons.UnregisterListener(AddonEvent.PostSetup,"RetainerSellList",SellerListOpened);
        Addons.UnregisterListener(AddonEvent.PostReceiveEvent,"ItemSearchResult",SellerPriceClicked);
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
        if(config.Seller.Enabled && sellerRequestItem!=0 && proxy!=null && proxy->SearchItemId%1_000_000==sellerRequestItem)
        {sellerExpected=Math.Min(100,(int)count);sellerError=error;sellerResultAt=DateTimeOffset.UtcNow;}
    }
    private void SellerHistoryReceived(IMarketBoardHistory history)
    {
        if(!config.Seller.Enabled || sellerHistories.Count>=32)return;
        sellerHistories.Enqueue((history.ItemId%1_000_000,history.HistoryListings.Select(x=>new SellerSale(x.SalePrice,x.IsHq,x.Quantity,new DateTimeOffset(DateTime.SpecifyKind(x.PurchaseTime,DateTimeKind.Utc)),x.OnMannequin)).ToArray(),DateTimeOffset.UtcNow));
    }
    private void SellerBellOpened(AddonEvent e,AddonArgs args)
    {
        if(config.Seller.Enabled && config.Seller.OpenAtBell && !sellerRun.Running)sellerWindow.IsOpen=true;
    }
    private void SellerListOpened(AddonEvent e,AddonArgs args)=>marketPollAt=DateTimeOffset.UtcNow.AddSeconds(1);
    private unsafe void SellerEditorOpened(AddonEvent e,AddonArgs args)
    {
        if(!config.Seller.Enabled || sellerRun.Running)return;
        manualEditor=null;manualRetainer=0;manualEditorAt=DateTimeOffset.UtcNow;manualCompareAt=default;
        manualAutoCompare=config.Seller.OpenCompare&&!Keys[Dalamud.Game.ClientState.Keys.VirtualKey.SHIFT];
        manualShowHistory=config.Seller.OpenHistory^(config.Seller.AltTogglesHistory&&Keys[Dalamud.Game.ClientState.Keys.VirtualKey.MENU]);
        manualPastePrice=null;
        if(config.Seller.CtrlPastePrice&&Keys[Dalamud.Game.ClientState.Keys.VirtualKey.CONTROL]){
            manualAutoCompare=false;
            if(int.TryParse(Dalamud.Bindings.ImGui.ImGui.GetClipboardText(),out var value)&&value is >0 and <=SellerPricing.MaxPrice)manualPastePrice=value;
            else sellerNotice="Clipboard does not contain a valid unit price.";
        }
        AcquireMarketBuddyLock();
    }
    private unsafe void SellerPriceClicked(AddonEvent e,AddonArgs args)
    {
        if(!config.Seller.Enabled || sellerRun.Running || manualEditor is null || args is not AddonReceiveEventArgs ev || (int)ev.AtkEventType!=35 || ev.AtkEventData==0)return;
        if(manualSuspendUntilClose||!AcquireMarketBuddyLock())return;
        try {
            var data=(AtkEventData*)ev.AtkEventData;
            var proxy=InfoProxyItemSearch.Instance();var index=data->ListItemData.SelectedIndex;
            if(proxy==null || proxy->WaitingForListings || proxy->SearchItemId%1_000_000!=manualEditor.ItemId || index<0 || index>=Math.Min(100,proxy->ListingCount))return;
            var listing=proxy->Listings[index];
            if(listing.UnitPrice is 0 or >SellerPricing.MaxPrice || listing.IsMannequin || listing.IsHqItem!=manualEditor.Hq)return;
            var price=SellerPricing.Adjust(listing.UnitPrice,config.Seller,config.Seller.MinimumPrices.GetValueOrDefault(manualEditor.ItemId,1));
            if(config.Seller.CopyPrice)Dalamud.Bindings.ImGui.ImGui.SetClipboardText(price.ToString());
            if(config.Seller.ClickSetsPrice)SetManualSellerPrice(price,config.Seller.ClickConfirms);
        }catch(Exception ex){sellerNotice="Could not use this price; no automatic retry.";Log.Debug(ex,sellerNotice);}
    }
    private static unsafe AtkUnitBase* SellerAddon(string name)
    {var a=(AtkUnitBase*)GardenGui.GetAddonByName(name).Address;return a!=null&&a->IsVisible&&a->IsReady?a:null;}
    private unsafe bool SellerContext(ulong cid,uint world)=>config.Seller.Enabled && Player.IsLoaded && Player.ContentId==cid && Player.CurrentWorld.RowId==world && Player.HomeWorld.RowId==world && !Conditions[ConditionFlag.BetweenAreas] && !Conditions[ConditionFlag.BetweenAreas51] && !Conditions[ConditionFlag.InCombat] && !HelperInCutscene();
    private static unsafe bool SellerActive(ulong id)
    {var rm=RetainerManager.Instance();var a=AgentRetainer.Instance();return id!=0&&rm!=null&&rm->IsReady&&rm->LastSelectedRetainerId==id&&a!=null&&a->IsAgentActive();}
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
        if(inventory==null||!inventory->IsLoaded||inventory->Size!=20||inventory->Items==null||agent->SellListEntryCount is <0 or >20)return null;
        var rows=new List<MarketStock>();
        for(short i=0;i<20;i++){
            var slot=inventory->GetInventorySlot(i);if(slot==null||slot->ItemId==0||slot->Quantity==0)continue;
            var price=manager->GetRetainerMarketPrice(i);if(price is 0 or >SellerPricing.MaxPrice)return null;
            rows.Add(new(i,slot->ItemId%1_000_000,SearchItemName(slot->ItemId%1_000_000),(slot->Flags&InventoryItem.ItemFlags.HighQuality)!=0,(uint)slot->Quantity,(int)price,DateTimeOffset.UtcNow));
        }
        // A stale inventory from the previous retainer must not become an empty snapshot.
        if(rows.Count!=agent->SellListEntryCount)return null;
        var entries=agent->SellListEntries[..agent->SellListEntryCount].ToArray();
        foreach(var row in rows)if(!entries.Any(x=>x.InventorySlot==row.Slot&&x.ItemId%1_000_000==row.ItemId&&x.Quantity==row.Quantity))return null;
        return rows.ToArray();
    }
    private unsafe bool SellerEditorMatches(ulong retainer,MarketStock item,bool checkPrice=true)
    {
        if(!SellerActive(retainer))return false;
        var edit=(AddonRetainerSell*)SellerAddon("RetainerSell");var agent=AgentRetainer.Instance();
        if(edit==null||edit->AskingPrice==null||edit->Quantity==null||agent==null||agent->SellItemInventoryType!=InventoryType.RetainerMarket||agent->SellItemInventorySlot!=item.Slot||edit->Quantity->Value!=item.Quantity)return false;
        var stock=ReadSellerStock(retainer)?.FirstOrDefault(x=>x.SameItem(item));
        return stock!=null&&stock.Price==item.Price&&(!checkPrice||edit->AskingPrice->Value==item.Price);
    }
    private static unsafe bool SellerButton(AtkUnitBase* addon,AtkComponentButton* button)
    {
        if(addon==null||button==null||!button->IsEnabled||button->OwnerNode==null)return false;
        var ev=button->OwnerNode->AtkEventManager.Event;var guard=0;
        while(ev!=null&&guard++<32){if(ev->State.EventType==AtkEventType.ButtonClick&&ev->Listener==(AtkEventListener*)addon){var copy=*ev;var data=new AtkEventData();addon->ReceiveEvent(copy.State.EventType,(int)copy.Param,&copy,&data);return true;}ev=ev->NextEvent;}
        return false;
    }
    private unsafe bool BeginSellerComparison(uint item)
    {
        var edit=(AddonRetainerSell*)SellerAddon("RetainerSell");if(edit==null||sellerResultHook?.IsEnabled!=true)return false;
        if(SellerAddon("ItemSearchResult")!=null||InfoProxyItemSearch.Instance()==null||InfoProxyItemSearch.Instance()->WaitingForListings)return false;
        sellerRequestItem=item;sellerRequestWorld=Player.CurrentWorld.RowId;sellerExpected=-1;sellerError=0;
        var agent=AgentRetainer.Instance();var manager=InventoryManager.Instance();
        var inv=agent!=null&&manager!=null?manager->GetInventoryContainer(agent->SellItemInventoryType):null;
        if(inv==null||!inv->IsLoaded||agent->SellItemInventorySlot<0||agent->SellItemInventorySlot>=inv->Size)return false;
        var slot=inv->GetInventorySlot(agent->SellItemInventorySlot);if(slot==null||slot->ItemId%1_000_000!=item)return false;
        sellerRequestHq=(slot->Flags&InventoryItem.ItemFlags.HighQuality)!=0;
        sellerRequestAt=DateTimeOffset.UtcNow;sellerResultAt=default;sellerHistory=[];sellerHistoryReceived=false;sellerHistoryRequested=false;
        return SellerButton((AtkUnitBase*)edit,edit->ComparePrices);
    }
    private unsafe SellerComparison? ReadSellerComparison()
    {
        if(sellerRequestItem==0||sellerExpected<0||sellerResultAt<sellerRequestAt)return null;
        if(sellerError!=0)return new(sellerRequestItem,sellerRequestWorld,sellerRequestAt,false,[],false,[],"Game comparison failed ("+sellerError+"); existing price kept.");
        var proxy=InfoProxyItemSearch.Instance();var result=(AddonItemSearchResult*)SellerAddon("ItemSearchResult");
        if(proxy==null||result==null||proxy->WaitingForListings||proxy->SearchItemId%1_000_000!=sellerRequestItem||proxy->ListingCount!=sellerExpected)return null;
        var rows=new List<SellerOffer>();
        for(var i=0;i<sellerExpected;i++){var x=proxy->Listings[i];if(x.ItemId%1_000_000!=sellerRequestItem||x.UnitPrice==0)return null;rows.Add(new(x.RetainerId,x.UnitPrice,x.IsHqItem,x.Quantity,x.IsMannequin));}
        var emptyQuality=!rows.Any(x=>x.Hq==sellerRequestHq&&!x.Mannequin);
        var wantHistory=(sellerRun.Running?config.Seller.OpenHistory:manualShowHistory) || emptyQuality && config.Seller.UseHistory;
        if(wantHistory && !sellerHistoryRequested){sellerHistoryRequested=SellerButton((AtkUnitBase*)result,result->History);}
        if(emptyQuality&&config.Seller.UseHistory&&!sellerHistoryReceived)return null;
        return new(sellerRequestItem,sellerRequestWorld,sellerRequestAt,true,rows.ToArray(),sellerHistoryReceived,sellerHistory);
    }
    private unsafe bool CancelSellerEditor(ulong retainer,MarketStock item)
    {
        if(SellerAddon("RetainerSell")!=null&&!SellerEditorMatches(retainer,item))return false;
        if(SellerAddon("RetainerSell")==null){sellerRequestItem=0;return true;}
        // Do not dismiss an editor the player has changed or opened independently.
        CloseSellerAddon("ItemSearchHistory");
        CloseSellerAddon("ItemSearchResult");
        var edit=(AddonRetainerSell*)SellerAddon("RetainerSell");
        var cancelled=edit!=null&&SellerButton((AtkUnitBase*)edit,edit->Cancel);
        sellerRequestItem=0;return cancelled;
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
    private void ReleaseSellerLocks()
    {
        if(marketBuddyLocked){try{Pi.GetIpcSubscriber<string,bool>("Marketbuddy.Unlock").InvokeFunc("EquinoxSeller");}catch{}marketBuddyLocked=false;}
        if(autoRetainerSuppressed){try{Pi.GetIpcSubscriber<bool,object>("AutoRetainer.SetSuppressed").InvokeAction(autoRetainerPreviouslySuppressed);}catch{}autoRetainerSuppressed=false;}
        sellerRequestItem=0;
    }
    private unsafe void StartSeller(bool apply)
    {
        if(!config.Seller.Enabled||!marketHooksEnabled||marketLoad!=null||marketLoadFailed){sellerNotice="Enable Market and wait for its saved records first.";return;}
        if(sellerRun.Running||SellerAddon("RetainerSell")!=null){sellerNotice="Close the current item editor before starting a run.";return;}
        if(!Player.IsLoaded||Player.HomeWorld.RowId!=Player.CurrentWorld.RowId){sellerNotice="Use your home world’s summoning bell.";return;}
        if(config.EnableFollowThem||config.AutoOpenCoffers){sellerNotice="Stop Helper and automatic coffer opening before starting Seller.";return;}
        var all=ReadSellerRetainers();
        var current=RetainerManager.Instance()==null?0:RetainerManager.Instance()->LastSelectedRetainerId;
        var selected=sellerScope switch {0=>all.Where(x=>x.Id==current&&SellerActive(current)).ToArray(),1=>all.Where(x=>config.Seller.SelectedRetainers.Contains(x.Id)).ToArray(),_=>all};
        if(selected.Length==0){sellerNotice="Select a retainer or choose All retainers.";return;}
        if(SellerAddon("RetainerList")==null&&!(selected.Length==1&&SellerActive(selected[0].Id))){sellerNotice="Open the summoning-bell retainer list first.";return;}
        if(Pi.InstalledPlugins.Any(x=>x.IsLoaded&&x.InternalName=="AutoRetainer"))try{
            if(Pi.GetIpcSubscriber<bool>("AutoRetainer.PluginState.IsBusy").InvokeFunc()){sellerNotice="Wait for AutoRetainer to finish before starting.";return;}
            autoRetainerPreviouslySuppressed=Pi.GetIpcSubscriber<bool>("AutoRetainer.GetSuppressed").InvokeFunc();
            Pi.GetIpcSubscriber<bool,object>("AutoRetainer.SetSuppressed").InvokeAction(true);autoRetainerSuppressed=true;
        }catch{ReleaseSellerLocks();sellerNotice="Disable AutoRetainer before using Seller: its pause connection is unavailable.";return;}
        if(!AcquireMarketBuddyLock()){ReleaseSellerLocks();return;}manualEditor=null;manualSuspendUntilClose=false;sellerNotice="";
        sellerRun.Start(selected,Player.ContentId,Player.CurrentWorld.RowId,apply,config.Seller,marketLedger.Retainers.Select(x=>x.Id).Concat(all.Select(x=>x.Id)),DateTimeOffset.UtcNow);
    }
    private unsafe void UpdateEquinoxMarket(DateTimeOffset now)
    {
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
            while(sellerHistories.TryDequeue(out var h))if(h.Item==sellerRequestItem&&h.At>=sellerRequestAt){sellerHistory=h.Sales;sellerHistoryReceived=true;}
            DrainMarketSales();
            sellerRun.Tick(now);
            UpdateMarketWatch(now);
            UpdateMarketBar(now);
            if(now<marketPollAt)return;marketPollAt=now.AddMilliseconds(500);
            if(!Player.IsLoaded){manualEditor=null;sellerInventoryRetainer=0;marketPurchase=null;ReleaseSellerLocks();RestoreMarketHighlights();return;}
            var rm=RetainerManager.Instance();var active=rm==null?0:rm->LastSelectedRetainerId;
            if(ReadSellerStock(active) is {} stock)RememberMarketStock(active,stock,now);
            RememberMarketVisit(active,now);
            if(!sellerRun.Running)UpdateManualSeller(active,now);
            UpdateMarketHighlights();
        }catch(Exception ex){sellerRun.Stop("Seller stopped after an unexpected response; check the last listing.");sellerNotice="Market tools paused: "+ex.GetType().Name;throw;}
    }
    private unsafe void UpdateManualSeller(ulong active,DateTimeOffset now)
    {
        var edit=(AddonRetainerSell*)SellerAddon("RetainerSell");var agent=AgentRetainer.Instance();
        if(manualSuspendUntilClose){if(edit==null){manualSuspendUntilClose=false;manualEditor=null;}else return;}
        if(edit==null){manualEditor=null;if(SellerAddon("RetainerList")==null&&SellerAddon("RetainerSellList")==null&&!SellerActive(active))ReleaseSellerLocks();else AcquireMarketBuddyLock();return;}
        if(agent==null||!SellerActive(active))return;
        if(!AcquireMarketBuddyLock())return;
        if(manualEditor is null && now-manualEditorAt>TimeSpan.FromMilliseconds(300)){
            if(agent->SellItemInventoryType==InventoryType.RetainerMarket)manualEditor=ReadSellerStock(active)?.FirstOrDefault(x=>x.Slot==agent->SellItemInventorySlot);
            else {
                var manager=InventoryManager.Instance();var container=manager==null?null:manager->GetInventoryContainer(agent->SellItemInventoryType);
                var item=container!=null&&container->IsLoaded&&agent->SellItemInventorySlot>=0&&agent->SellItemInventorySlot<container->Size?container->GetInventorySlot(agent->SellItemInventorySlot):null;
                if(item!=null&&item->ItemId>0&&edit->AskingPrice!=null&&edit->Quantity!=null){
                    var id=item->ItemId%1_000_000;var max=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(id)?.StackSize??1;
                    if(config.Seller.LimitNewStacks)edit->Quantity->SetValue(SellerPricing.Stack(config.Seller.NewStackSize,(int)item->Quantity,(int)max));
                    manualEditor=new(agent->SellItemInventorySlot,id,SearchItemName(id),(item->Flags&InventoryItem.ItemFlags.HighQuality)!=0,(uint)edit->Quantity->Value,edit->AskingPrice->Value,now);
                }
            }
            manualRetainer=active;
            if(manualEditor!=null&&manualPastePrice is {} paste){SetManualSellerPrice(paste,false);manualPastePrice=null;}
        }
        if(manualEditor!=null&&manualCompareAt==default&&manualAutoCompare){
            if(BeginSellerComparison(manualEditor.ItemId))manualCompareAt=now;
        }
        if(manualEditor!=null&&ReadSellerComparison() is {} comparison){
            var decision=SellerPricing.Decide(manualEditor.ItemId,Player.CurrentWorld.RowId,manualEditor.Hq,manualEditor.Price,comparison,marketLedger.Retainers.Select(x=>x.Id).ToHashSet(),config.Seller,now);
            sellerReviews[(active,manualEditor.Slot)]=(manualEditor,decision,now);
        }
    }
    private unsafe void SetManualSellerPrice(int price,bool confirm)
    {
        if(manualEditor is not {} item || !SellerActive(manualRetainer))return;
        var edit=(AddonRetainerSell*)SellerAddon("RetainerSell");var agent=AgentRetainer.Instance();
        if(edit==null||edit->AskingPrice==null||agent==null||agent->SellItemInventorySlot!=item.Slot)return;
        var inv=InventoryManager.Instance()->GetInventoryContainer(agent->SellItemInventoryType);
        var native=inv!=null&&inv->IsLoaded&&item.Slot<inv->Size?inv->GetInventorySlot(item.Slot):null;
        if(native==null||native->ItemId%1_000_000!=item.ItemId||((native->Flags&InventoryItem.ItemFlags.HighQuality)!=0)!=item.Hq)return;
        edit->AskingPrice->SetValue(price);
        if(confirm){CloseSellerAddon("ItemSearchHistory");CloseSellerAddon("ItemSearchResult");SellerButton((AtkUnitBase*)edit,edit->Confirm);}
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
        var fresh=MarketAccounting.PreserveListingDates(old,new(id,native->NameString,Player.ContentId,Player.CharacterName,Player.HomeWorld.RowId,WorldName(Player.HomeWorld.RowId)??"",person?.Name??"",character?.Account??"",InventoryManager.Instance()->GetRetainerGil(),now,stock));
        if(old!=null&&old.Gil==fresh.Gil&&old.Stock.SequenceEqual(fresh.Stock)&&now-old.At<TimeSpan.FromMinutes(1))return;
        var recovered=old==null?[]:MarketAccounting.ReconcileOffline(old,fresh,marketLedger.Income);
        marketLedger=marketLedger with {Retainers=[..marketLedger.Retainers.Where(x=>x.Id!=id),fresh with {LastVisit=now}],Income=recovered.Length==0?marketLedger.Income:[..marketLedger.Income,..recovered]};DirtyMarket();
    }
    private sealed class SellerPort(Plugin p):ISellerPort
    {
        private (ulong Retainer,int Slot)? requestedEditor;
        private ulong requestedMarket;
        public bool ContextMatches(ulong c,uint w)=>p.SellerContext(c,w);
        public unsafe bool SelectRetainer(SellerRetainerRef r){
            if(SellerActive(r.Id)&&SellerAddon("RetainerList")==null)return true;
            var a=SellerAddon("RetainerList");var rm=RetainerManager.Instance();if(a==null||rm==null||!rm->IsReady)return false;
            for(uint i=0;i<Math.Min(10,(int)rm->GetRetainerCount());i++){var x=rm->GetRetainerBySortedIndex(i);if(x==null||x->RetainerId!=r.Id||!x->Available)continue;
                var v=stackalloc AtkValue[4];for(var n=0;n<4;n++)v[n]=default;v[0].Type=AtkValueType.Int;v[0].Int=2;v[1].Type=AtkValueType.UInt;v[1].UInt=i;
                a->FireCallback(4,v,true);return true;}return false;
        }
        public unsafe bool OpenMarket(ulong id){
            if(!SellerActive(id))return false;if(SellerAddon("RetainerSellList")!=null)return true;
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
            edit->AskingPrice->SetValue(price);return SellerButton((AtkUnitBase*)edit,edit->Confirm);
        }
        public unsafe bool PriceConfirmed(ulong r,MarketStock item,int price)=>SellerAddon("RetainerSell")==null&&p.ReadSellerStock(r)?.Any(x=>x.SameItem(item)&&x.Price==price)==true;
        public bool CancelEditor(ulong r,MarketStock item){var ok=p.CancelSellerEditor(r,item);if(ok)requestedEditor=null;return ok;}
        public unsafe bool CloseRetainer(ulong id){
            if(SellerAddon("RetainerList")!=null)return true;
            if(!SellerActive(id))return false;
            var list=SellerAddon("RetainerSellList");if(list!=null){list->Close(true);return false;}
            var menu=SellerAddon("SelectString");if(menu!=null){menu->Close(true);return false;}
            p.SellerRetainerTalk(id);return false;
        }
        public void Review(SellerRetainerRef r,MarketStock item,SellerDecision d,bool applied){
            p.sellerReviews[(r.Id,item.Slot)]=(applied?item with {Price=d.Price!.Value}:item,applied?d with {Status=SellerStatus.Competitive,Reason="Price update observed in the selling list. "+d.Reason}:d,DateTimeOffset.UtcNow);
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
        DisableMarketHooks();marketWatchClient.Dispose();sellerResultHook?.Dispose();DisposeMarketWindows();
        if(marketLoad==null&&!marketLoadFailed)try{marketWriter.Flush(marketLedger);}catch(Exception ex){Log.Error(ex,"Market ledger could not be saved");}
    }
}
