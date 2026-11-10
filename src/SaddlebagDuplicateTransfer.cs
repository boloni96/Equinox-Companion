using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private SaddlebagDepositReceipt? saddleDepositReceipt;
    private bool saddleDepositSelected;
    private readonly Queue<object> saddleDepositHistory=[];
    private void TraceSaddleDeposit(string kind,object detail){
        while(saddleDepositHistory.Count>=80)saddleDepositHistory.Dequeue();
        saddleDepositHistory.Enqueue(new{at=DateTimeOffset.UtcNow,kind,detail});
    }
    private unsafe void CancelSaddleDepositMenu(){
        var agent=AgentInventoryContext.Instance();
        if(!saddleDepositSelected&&localStoragePending is {} move&&agent!=null&&(uint)agent->TargetInventoryId==move.Source.Container&&agent->TargetInventorySlotId==move.Source.Slot)
            CloseSellerAddon("ContextMenu");
        saddleDepositReceipt=null;saddleDepositSelected=false;
    }
    private static long SaddleItemTotal(IEnumerable<ChestStack> slots,ChestStack item)=>slots.Where(s=>s.Item==item.Item&&s.Hq==item.Hq).Sum(s=>(long)s.Quantity);
    private unsafe void TickSaddlebagDuplicateDeposit(ChestStack[] store,ChestStack[] bags,DateTimeOffset now,InventoryManager* manager)
    {
        if(now>chestDeadline)throw new InvalidOperationException("Saddlebag transfer did not settle; stopped without retrying the item.");
        if(localStoragePending is not {} move){
            if(SellerAddon("ContextMenu")!=null||SellerAddon("InputNumeric")!=null)throw new InvalidOperationException("Close the open item menu before adding duplicates.");
            RefreshGearSetProtection();
            var deposit=bags.Where(s=>!OrganizerGearProtected(s.Item)).Select(s=>s with{CanDeposit=true}).ToArray();
            var next=ChestTransferPlanner.Duplicates(store,deposit,0);
            if(next==null){StopChest($"Complete · {chestMoves} duplicate transfers confirmed; no further matching items fit in the saddlebag.");return;}
            var agent=AgentInventoryContext.Instance();var module=AgentModule.Instance();
            var inventory=module==null?null:module->GetAgentByInternalId(AgentId.Inventory);
            if(agent==null||inventory==null)throw new InvalidOperationException("Inventory menu unavailable.");
            localStoragePending=next;saddleDepositSelected=false;
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
            var labels=new HashSet<string>(StringComparer.Ordinal){"Add All to Saddlebag"};
            var native=TradeItemShortcutNative.NativeEntry(agent,labels);
            if(native<0){TraceSaddleDeposit("deposit unavailable",new{item=move.Source.Item,menu=TravelNodeText(&menu->UldManager)});throw new InvalidOperationException("The game's Add All to Saddlebag action is unavailable for this item.");}
            var label=TradeItemShortcutNative.Read(agent->EventParams[agent->ContexItemStartIndex+native].String.Value);
            var displayed=TradeItemShortcutNative.DisplayedEntry(menu->AtkValues,menu->AtkValuesCount,label);
            if(displayed<0)throw new InvalidOperationException("Could not verify the displayed saddlebag deposit action.");
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
        localStoragePending=null;saddleDepositReceipt=null;saddleDepositSelected=false;chestMoves++;
        ObserveStorage(now);myInventoryRefresh=default;RequestStorageRefresh();
        chestStatus=$"{chestMoves} duplicate transfers confirmed";chestDeadline=now.AddSeconds(15);chestNext=now.AddMilliseconds(750);
    }
}
