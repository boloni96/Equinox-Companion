using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private SaddlebagDepositReceipt? saddleDepositReceipt;
    private bool saddleDepositSelected;
    private TradeItemMenuSelection? saddleDepositMenu;
    private static readonly HashSet<string> SaddleDepositLabels=new(StringComparer.Ordinal){"Add All to Saddlebag"};
    private readonly Queue<object> saddleDepositHistory=[];
    private void TraceSaddleDeposit(string kind,object detail){
        while(saddleDepositHistory.Count>=80)saddleDepositHistory.Dequeue();
        saddleDepositHistory.Enqueue(new{at=DateTimeOffset.UtcNow,kind,detail});
    }
    private unsafe void CancelSaddleDepositMenu(){
        var agent=AgentInventoryContext.Instance();
        if(!saddleDepositSelected&&localStoragePending is {} move&&agent!=null&&(uint)agent->TargetInventoryId==move.Source.Container&&agent->TargetInventorySlotId==move.Source.Slot)
            CloseSellerAddon("ContextMenu");
        saddleDepositReceipt=null;saddleDepositSelected=false;saddleDepositMenu=null;
    }
    private unsafe bool ReadSaddleDepositTarget(out TradeItemShortcutTarget target)
    {
        target=default;
        if(!localStorageRunning||!localStorageDuplicates||localStoragePending is not {} move||saddleDepositSelected||Player.ContentId!=chestOwner||Player.CurrentWorld.RowId!=chestWorld)return false;
        var agent=AgentInventoryContext.Instance();var manager=InventoryManager.Instance();
        if(agent==null||manager==null||(uint)agent->TargetInventoryId!=move.Source.Container||agent->TargetInventorySlotId!=move.Source.Slot)return false;
        if(!TradeItemShortcutNative.CaptureItem(manager->GetInventorySlot((InventoryType)move.Source.Container,move.Source.Slot),agent->TargetInventorySlot,(InventoryType)move.Source.Container,move.Source.Slot,out target)||
            target.ItemId!=move.Source.Item||target.Quantity!=move.Source.Quantity||((target.Flags&1)!=0)!=move.Source.Hq)return false;
        target=target with{Character=chestOwner};return true;
    }
    private unsafe void CaptureSaddleDepositMenu(AddonArgs args)
    {
        if(!localStorageRunning||!localStorageDuplicates||localStoragePending==null||saddleDepositSelected)return;
        saddleDepositMenu=null;
        try{
            // ContextMenu does not retain all setup values in addon->AtkValues.
            // Read the final rendered entries during setup/refresh, then keep only
            // the index and exact item identity; never retain the temporary pointer.
            var pointer=args switch{AddonSetupArgs a=>a.AtkValues,AddonRefreshArgs a=>a.AtkValues,_=>0};
            var count=args switch{AddonSetupArgs a=>a.AtkValueCount,AddonRefreshArgs a=>a.AtkValueCount,_=>0u};
            if(pointer==0||count>72||!ReadSaddleDepositTarget(out var target))return;
            var agent=AgentInventoryContext.Instance();var native=TradeItemShortcutNative.NativeEntry(agent,SaddleDepositLabels);if(native<0)return;
            var label=TradeItemShortcutNative.Read(agent->EventParams[agent->ContexItemStartIndex+native].String.Value);
            var index=TradeItemShortcutNative.DisplayedEntry((AtkValue*)pointer,(int)count,label);
            TraceSaddleDeposit("menu captured",new{native,displayed=index,setupValues=count,label});
            if(index>=0)saddleDepositMenu=new(args.Addon.Address,target,label,index,Environment.TickCount64);
        }catch(Exception e){TraceSaddleDeposit("menu capture failed",new{error=e.Message});}
    }
    private static long SaddleItemTotal(IEnumerable<ChestStack> slots,ChestStack item)=>slots.Where(s=>s.Item==item.Item&&s.Hq==item.Hq).Sum(s=>(long)s.Quantity);
    private unsafe void TickSaddlebagDuplicateDeposit(ChestStack[] store,ChestStack[] bags,DateTimeOffset now,InventoryManager* manager)
    {
        if(now>chestDeadline)throw new InvalidOperationException("Saddlebag transfer did not settle; stopped without retrying the item.");
        if(localStoragePending is not {} move){
            if(SellerAddon("ContextMenu")!=null||SellerAddon("InputNumeric")!=null)throw new InvalidOperationException("Close the open item menu before adding duplicates.");
            RefreshGearSetProtection();
            var deposit=bags.Where(s=>!OrganizerGearProtected(s.Item)).Select(s=>s with{CanDeposit=true}).ToArray();
            var next=ChestTransferPlanner.Duplicates(store,deposit,0,exclusions:config.DuplicateDepositExclusions);
            if(next==null){StopChest($"Complete · {chestMoves} duplicate transfers confirmed; no further matching items fit in the saddlebag.");return;}
            var agent=AgentInventoryContext.Instance();var module=AgentModule.Instance();
            var inventory=module==null?null:module->GetAgentByInternalId(AgentId.Inventory);
            if(agent==null||inventory==null)throw new InvalidOperationException("Inventory menu unavailable.");
            localStoragePending=next;saddleDepositSelected=false;saddleDepositMenu=null;
            saddleDepositReceipt=new(SaddleItemTotal(bags,next.Source),SaddleItemTotal(store,next.Source),next.Source.Quantity);
            chestDeadline=now.AddSeconds(15);
            TraceSaddleDeposit("menu requested",new{source=next.Source,bags=SaddleItemTotal(bags,next.Source),saddle=SaddleItemTotal(store,next.Source)});
            chestStatus=$"{chestMoves} confirmed · Opening Add All to Saddlebag for {SearchItemName(next.Source.Item)}";
            agent->OpenForItemSlot((InventoryType)next.Source.Container,next.Source.Slot,0,inventory->GetAddonId());return;
        }
        if(!saddleDepositSelected){
            var agent=AgentInventoryContext.Instance();var menu=SellerAddon("ContextMenu");if(menu==null)return;
            var source=manager->GetInventorySlot((InventoryType)move.Source.Container,move.Source.Slot);
            if(agent==null||(uint)agent->TargetInventoryId!=move.Source.Container||agent->TargetInventorySlotId!=move.Source.Slot||source==null||
                source->ItemId!=move.Source.Item||source->Quantity!=move.Source.Quantity||((source->Flags&InventoryItem.ItemFlags.HighQuality)!=0)!=move.Source.Hq)
                throw new InvalidOperationException("Saddlebag deposit source changed before selecting its menu.");
            var native=TradeItemShortcutNative.NativeEntry(agent,SaddleDepositLabels);
            if(native<0){TraceSaddleDeposit("deposit unavailable",new{item=move.Source.Item,menu=TravelNodeText(&menu->UldManager)});throw new InvalidOperationException("The game's Add All to Saddlebag action is unavailable for this item.");}
            var label=TradeItemShortcutNative.Read(agent->EventParams[agent->ContexItemStartIndex+native].String.Value);
            if(!ReadSaddleDepositTarget(out var target)||saddleDepositMenu is not {} captured||!captured.Matches((nint)menu,target,label,Environment.TickCount64))
                throw new InvalidOperationException("Could not verify the displayed saddlebag deposit action from menu setup.");
            var displayed=captured.Index;
            saddleDepositSelected=true;chestDeadline=now.AddSeconds(15);chestNext=now.AddMilliseconds(750);
            TraceSaddleDeposit("deposit requested",new{source=move.Source,native,displayed,label});
            ((AddonContextMenu*)menu)->OnMenuSelected(displayed,0);
            chestStatus=$"{chestMoves} confirmed · Waiting for {SearchItemName(move.Source.Item)} to settle";return;
        }
        var bagTotal=SaddleItemTotal(bags,move.Source);var saddleTotal=SaddleItemTotal(store,move.Source);
        var result=saddleDepositReceipt!.Observe(bagTotal,saddleTotal,now);
        if(result==SaddlebagDepositReceipt.Result.Reverted){TraceSaddleDeposit("transfer reverted",new{source=move.Source,bagTotal,saddleTotal});throw new InvalidOperationException("The saddlebag change reverted or another inventory change interfered; stopped without retrying.");}
        if(result!=SaddlebagDepositReceipt.Result.Settled)return;
        var amount=saddleDepositReceipt.Quantity;
        TraceSaddleDeposit("transfer settled",new{source=move.Source,amount,bagTotal,saddleTotal});
        localStoragePending=null;saddleDepositReceipt=null;saddleDepositSelected=false;saddleDepositMenu=null;chestMoves++;
        ObserveStorage(now);myInventoryRefresh=default;RequestStorageRefresh();
        chestStatus=$"{chestMoves} duplicate transfers confirmed";chestDeadline=now.AddSeconds(15);chestNext=now.AddMilliseconds(750);
    }
}
