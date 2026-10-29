using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly ItemTradeRun itemTrade=new();
    private readonly GilTradeCompletionCapture itemTradeCompletion=new();
    private bool TraderRunning=>gilTrade.Running||itemTrade.Running;
    private bool itemTradeOwned;
    private DateTimeOffset itemTradePoll;
    private TradeBatchSlot? itemTradePending;
    private DateTimeOffset itemTradeOfferAt;
    private bool itemTradeMenuSelected,itemTradeQuantitySent;
    private nint itemTradeMenu;
    private TradeItemMenuSelection? itemTradeMenuSelection;
    private long itemTradeOfferedBefore;
    private unsafe TradeBagSlot[] ReadItemTradeBags()
    {
        var inv=InventoryManager.Instance();if(inv==null)throw new InvalidOperationException("Inventory unavailable.");
        var rows=new List<TradeBagSlot>();var sheet=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
        foreach(var type in new uint[]{0,1,2,3,2001}){
            var c=inv->GetInventoryContainer((InventoryType)type);
            if(c==null||!c->IsLoaded||c->Items==null||c->Size is <=0 or >200)throw new InvalidOperationException("Waiting for loaded bags.");
            for(var i=0;i<c->Size;i++){
                var s=c->Items[i];var id=type==2001?(uint)i+2:s.ItemId;var row=sheet.GetRowOrDefault(id);
                var limit=type==2001?9999:row.HasValue?(int)row.Value.StackSize:0;
                rows.Add(new(type,i,id,(s.Flags&InventoryItem.ItemFlags.HighQuality)!=0,s.Quantity,limit,
                    row.HasValue&&!row.Value.IsUntradable&&s.SpiritbondOrCollectability==0&&(s.Flags&InventoryItem.ItemFlags.Collectable)==0));
            }
        }
        return rows.ToArray();
    }
    private unsafe TradeItemAmount[] ReadItemTradeOffer(bool remote)
    {
        var inv=InventoryManager.Instance();if(inv==null)return [];
        var result=new List<TradeItemAmount>();
        for(var i=0;i<5;i++){
            var s=remote?inv->TradeItemsRemote[i]:inv->TradeItemsLocal[i];
            if(s.ItemId==0&&s.Quantity==0)continue;
            if(s.ItemId<=1||s.Quantity<=0)throw new InvalidOperationException("Malformed trade item.");
            result.Add(new(s.ItemId,(s.Flags&InventoryItem.ItemFlags.HighQuality)!=0,s.Quantity));
        }
        if(inv->TradeItemsLocal[5].ItemId>1||inv->TradeItemsRemote[5].ItemId>1)throw new InvalidOperationException("Unexpected currency slot item.");
        return result.ToArray();
    }
    private ItemTradeFrame ReadItemTradeFrame()
    {
        var context=ReadGilTradeFrame();var bags=ReadItemTradeBags();
        var local=context.TradeOpen&&context.TradeReady&&!context.SyncPending?ReadItemTradeOffer(false):Array.Empty<TradeItemAmount>();
        var remote=context.TradeOpen&&context.TradeReady&&!context.SyncPending?ReadItemTradeOffer(true):Array.Empty<TradeItemAmount>();
        context=context with{CompletionObserved=itemTradeCompletion.Completed(context.Character,itemTrade.Partner,1,DateTimeOffset.UtcNow)};
        return new(context,bags,local,remote,ItemTradePlan.CanReceive(remote,bags,id=>CrystalStorage.IsItem(id)?9999:OrganizerStackLimit(id)));
    }
    private unsafe void StartItemTrade()
    {
        gilTradeNotice="";
        try{
            if(TraderRunning||!config.EnableGilTrader)return;
            if(retainerGilRunning||organizeRunning||retrievalRun.Running||chestRunning||craftingWithdrawalPending)throw new InvalidOperationException("Finish the inventory operation first.");
            if(SellerAddon("ContextMenu")!=null||SellerAddon("InputNumeric")!=null)throw new InvalidOperationException("Close the item menu first.");
            gilTradePrompt=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Addon>().GetRowOrDefault(102223)?.Text.ExtractText()??"";
            if(gilTradePrompt.Length==0)throw new InvalidOperationException("Trade confirmation text unavailable.");
            PrepareTradeItemShortcutLabels();
            if(!gilReceive&&tradeItemLabelsTask?.IsCompletedSuccessfully!=true)throw new InvalidOperationException("Loading item trade actions. Try again in a moment.");
            gilSessionWorld=Player.CurrentWorld.RowId;gilSessionTerritory=Client.TerritoryType;
            var selected=itemTradeSelection.Select(x=>new TradeItemAmount(x.Key.Item,x.Key.Hq,x.Value)).ToArray();
            if(!itemTrade.Start(!gilReceive,selected,ReadItemTradeFrame(),DateTimeOffset.UtcNow)){gilTradeNotice=itemTrade.Status;TraceGilTrade("Items start: "+itemTrade.Status);return;}
            itemTradePending=null;itemTradeOwned=false;itemTradePoll=default;itemTradeCompletion.Clear();
            TraceGilTrade($"Items: {(gilReceive?"receive":"send")} with {gilPartnerName} @ {WorldName(gilPartnerWorld)}");
        }catch(Exception e){gilTradeNotice=e.Message;}
    }
    private unsafe void CancelOwnedItemTrade()
    {
        var inv=InventoryManager.Instance();
        if(itemTradeOwned&&Player.IsLoaded&&Player.ContentId==itemTrade.Character&&inv!=null&&inv->TradePartnerEntityId==itemTrade.Partner&&inv->TradePartnerNameString==gilPartnerName&&
            (SellerAddon("Trade")!=null||(int)inv->TradeLocalState==2))inv->RefuseTrade();
        itemTradeOwned=false;itemTradePending=null;itemTradeMenuSelection=null;
    }
    private void StopItemTrade(string reason)
    {
        if(!itemTrade.Running)return;itemTrade.Stop(reason);itemTradeCompletion.Clear();
        try{CancelOwnedItemTrade();}catch(Exception e){Log.Warning(e,"Item trade cancellation");}TraceGilTrade(itemTrade.Status);
    }
    private unsafe void BeginItemOffer(TradeBatchSlot slot,DateTimeOffset now)
    {
        var agent=AgentInventoryContext.Instance();var trade=SellerAddon("Trade");
        if(agent==null||trade==null||SellerAddon("ContextMenu")!=null||SellerAddon("InputNumeric")!=null)throw new InvalidOperationException("Close the existing item menu before sending.");
        var current=ReadItemTradeBags().SingleOrDefault(x=>x.Container==slot.Source.Container&&x.Slot==slot.Source.Slot);
        if(current!=slot.Source||!current.Tradable||slot.Quantity<=0||slot.Quantity>current.Quantity)throw new InvalidOperationException("Selected bag slot changed.");
        itemTradeOfferedBefore=ReadItemTradeOffer(false).Where(x=>x.Key==slot.Source.Key).Sum(x=>(long)x.Quantity);
        itemTradePending=slot;itemTradeOfferAt=now;itemTradeMenuSelected=itemTradeQuantitySent=false;itemTradeMenu=0;itemTradeMenuSelection=null;
        agent->OpenForItemSlot((InventoryType)slot.Source.Container,slot.Source.Slot,0,trade->Id);
    }
    private unsafe void CaptureItemTradeMenu(AddonArgs args)
    {
        if(itemTradePending is not {} slot||itemTradeMenuSelected||!itemTrade.Running)return;
        itemTradeMenuSelection=null;
        try{
            var pointer=args switch{AddonSetupArgs setup=>setup.AtkValues,AddonRefreshArgs refresh=>refresh.AtkValues,_=>0};
            var count=args switch{AddonSetupArgs setup=>setup.AtkValueCount,AddonRefreshArgs refresh=>refresh.AtkValueCount,_=>0u};
            var agent=AgentInventoryContext.Instance();var inv=InventoryManager.Instance();var trade=SellerAddon("Trade");
            if(pointer==0||count>72||agent==null||inv==null||trade==null||inv->TradePartnerEntityId!=itemTrade.Partner||Player.ContentId!=itemTrade.Character||
                (uint)agent->TargetInventoryId!=slot.Source.Container||agent->TargetInventorySlotId!=slot.Source.Slot)return;
            if(!TradeItemShortcutNative.CaptureItem(inv->GetInventorySlot((InventoryType)slot.Source.Container,slot.Source.Slot),agent->TargetInventorySlot,(InventoryType)slot.Source.Container,slot.Source.Slot,out var target)||
                target.ItemId!=slot.Source.Item||target.Quantity!=slot.Source.Quantity||((target.Flags&1)!=0)!=slot.Source.Hq)return;
            var labels=tradeItemLabelsTask?.IsCompletedSuccessfully==true?tradeItemLabelsTask.Result:null;if(labels==null)return;
            var native=TradeItemShortcutNative.NativeEntry(agent,labels);if(native<0)return;
            var label=TradeItemShortcutNative.Read(agent->EventParams[agent->ContexItemStartIndex+native].String.Value);
            var index=TradeItemShortcutNative.DisplayedEntry((AtkValue*)pointer,(int)count,label);
            TraceGilTrade($"Item Trade menu setup: native={native}, displayed={index}, setupValues={count}");
            if(index<0)return;
            target=target with{Character=itemTrade.Character,Partner=itemTrade.Partner,TradeAddon=(nint)trade};
            itemTradeMenuSelection=new(args.Addon.Address,target,label,index,Environment.TickCount64);
        }catch(Exception e){TraceGilTrade("Item Trade menu capture: "+e.GetType().Name);}
    }
    private unsafe void TickItemOffer(ItemTradeFrame frame,DateTimeOffset now)
    {
        var slot=itemTradePending!;var f=frame.Context;
        if(!f.Ready||f.Character!=itemTrade.Character||!f.PartnerNearby||f.PartnerEntity!=itemTrade.Partner||!f.TradeOpen||f.TradePartner!=itemTrade.Partner||f.OtherDialog||f.LocalGil!=0||f.RemoteGil!=0||frame.Remote.Length>0||f.LocalState!=3)
            throw new InvalidOperationException("Trade changed while adding an item.");
        if(now-itemTradeOfferAt>TimeSpan.FromSeconds(8))throw new InvalidOperationException("Timed out adding an item; no retry was sent.");
        if(!f.TradeReady||f.SyncPending)return;
        var observed=frame.Local.Where(x=>x.Key==slot.Source.Key).Sum(x=>(long)x.Quantity);
        if(observed==itemTradeOfferedBefore+slot.Quantity){
            var oldMenu=SellerAddon("ContextMenu");if(oldMenu!=null&&(nint)oldMenu==itemTradeMenu&&SellerAddon("InputNumeric")==null)oldMenu->Hide(false,true,0);
            itemTradePending=null;return;
        }
        if(observed!=itemTradeOfferedBefore)throw new InvalidOperationException("Unexpected item quantity in trade offer.");
        var agent=AgentInventoryContext.Instance();
        if(agent==null||(uint)agent->TargetInventoryId!=slot.Source.Container||agent->TargetInventorySlotId!=slot.Source.Slot||
            !TradeItemShortcutNative.CaptureItem(InventoryManager.Instance()->GetInventorySlot((InventoryType)slot.Source.Container,slot.Source.Slot),agent->TargetInventorySlot,(InventoryType)slot.Source.Container,slot.Source.Slot,out var target)||
            target.ItemId!=slot.Source.Item||target.Quantity!=slot.Source.Quantity||((target.Flags&1)!=0)!=slot.Source.Hq)
            throw new InvalidOperationException("Item menu no longer matches selected bag slot.");
        if(!itemTradeMenuSelected){
            if(now-itemTradeOfferAt<TimeSpan.FromMilliseconds(100))return;
            var menu=SellerAddon("ContextMenu");if(menu==null)return;
            if(itemTradeMenu==0)itemTradeMenu=(nint)menu;if((nint)menu!=itemTradeMenu)throw new InvalidOperationException("Item menu replaced.");
            var labels=tradeItemLabelsTask?.IsCompletedSuccessfully==true?tradeItemLabelsTask.Result:null;
            if(labels==null)return;
            var native=TradeItemShortcutNative.NativeEntry(agent,labels);if(native<0)throw new InvalidOperationException("The game's Trade action is unavailable for this item.");
            var label=TradeItemShortcutNative.Read(agent->EventParams[agent->ContexItemStartIndex+native].String.Value);
            target=target with{Character=itemTrade.Character,Partner=itemTrade.Partner,TradeAddon=(nint)SellerAddon("Trade")};
            var selection=itemTradeMenuSelection;
            if(!menu->IsReady||selection==null||!selection.Matches((nint)menu,target,label,Environment.TickCount64)){
                if(now-itemTradeOfferAt<TimeSpan.FromMilliseconds(2500))return;
                TraceGilTrade(TradeItemMenuDiagnostic());
                throw new InvalidOperationException("Could not verify Trade menu setup for the selected item. See Details.");
            }
            itemTradeMenuSelected=true;itemTradeMenuSelection=null;
            ((AddonContextMenu*)menu)->OnMenuSelected(selection.Index,0);return;
        }
        var numeric=SellerAddon("InputNumeric");
        if(numeric!=null&&!itemTradeQuantitySent){
            var value=new AtkValue{Type=AtkValueType.Int,Int=slot.Quantity};itemTradeQuantitySent=true;numeric->FireCallback(1,&value,true);
        }
    }
    private unsafe void UpdateItemTrader(DateTimeOffset now)
    {
        if(!itemTrade.Running||now<itemTradePoll)return;itemTradePoll=now.AddMilliseconds(150);
        try{
            if(!config.EnableGilTrader||!gilTraderWindow.IsOpen&&!minimizedLaunchers.Contains("Gil Trader")){StopItemTrade("Trader closed or disabled.");return;}
            var frame=ReadItemTradeFrame();var f=frame.Context;
            if(f.TradeOpen&&f.Character==itemTrade.Character&&f.TradePartner==itemTrade.Partner)itemTradeOwned=true;
            if(itemTradePending!=null){TickItemOffer(frame,now);return;}
            var trades=itemTrade.Trades;var action=itemTrade.Step(frame,now);
            if(trades!=itemTrade.Trades){if(itemTrade.Sending){itemTradeSelection.Clear();foreach(var remaining in itemTrade.Remaining)itemTradeSelection[remaining.Key]=remaining.Quantity;}itemTradeCompletion.Clear();TraceGilTrade($"Items: {itemTrade.Done} confirmed in {itemTrade.Trades} trades");itemTradeBagRefresh=default;}
            var inv=InventoryManager.Instance();if(action!=ItemTradeAction.None&&inv==null)throw new InvalidOperationException("Trade manager unavailable.");
            switch(action){
                case ItemTradeAction.Request:
                    var target=GilTradePartner()??throw new InvalidOperationException("Selected player moved away.");Targets.Target=target;itemTradeOwned=true;inv->SendTradeRequest(target.EntityId);break;
                case ItemTradeAction.AddItem:BeginItemOffer(itemTrade.NextItem!,now);break;
                case ItemTradeAction.LockOffer:
                    var addon=SellerAddon("Trade");if(!GilTradeNative.TradeButtonEvent(addon,out var click))throw new InvalidOperationException("Trade button unavailable.");
                    itemTradeCompletion.Arm(itemTrade.Character,itemTrade.Partner,1,now);var data=new AtkEventData();addon->ReceiveEvent(click.State.EventType,(int)click.Param,&click,&data);break;
                case ItemTradeAction.Confirm:
                    var dialog=(AddonSelectYesno*)SellerAddon("SelectYesno");
                    if(dialog==null||dialog->PromptText==null||dialog->YesButton==null||!dialog->YesButton->IsEnabled||!GilTradeNative.PromptMatches(dialog->PromptText->NodeText.ToString(),gilTradePrompt))throw new InvalidOperationException("Trade confirmation changed.");
                    dialog->FireCallbackInt(0);break;
            }
            if(!itemTrade.Running){TraceGilTrade("Items: "+itemTrade.Status);itemTradeCompletion.Clear();CancelOwnedItemTrade();PrintEquinox("Item Trader · "+itemTrade.Status);}
        }catch(Exception e){StopItemTrade("Stopped: "+e.Message);Log.Warning(e,"Item Trader stopped");}
    }
}


