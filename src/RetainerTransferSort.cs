using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private sealed unsafe partial class RetrieverPort
    {
        private readonly HashSet<ulong> transferSortDirty=[];
        internal bool SortAllowed=true;
        private int transferSortStage;
        private DateTimeOffset transferSortDeadline,transferSortAt;
        private ChestStack? transferSortItem;
        internal void MarkForSort(ulong id){transferSortDirty.Add(id);}
        private void ClearTransferSort()
        {
            if(transferSortItem is {} item){var agent=AgentInventoryContext.Instance();if(agent!=null&&(uint)agent->TargetInventoryId==item.Container&&agent->TargetInventorySlotId==item.Slot)CloseSellerAddon("ContextMenu");}
            transferSortItem=null;transferSortStage=0;transferSortDirty.Clear();SortAllowed=true;
        }
        private bool SortAfterTransfers(ulong id)
        {
            if(!p.config.SortItemsAfterOrganizing||!SortAllowed||!transferSortDirty.Contains(id))return true;
            if(!ContextMatches||!RetrieverActive(id))throw new InvalidOperationException("Retainer changed before sorting.");
            var now=DateTimeOffset.UtcNow;p.PrepareOrganizerSort();
            if(transferSortStage==0){transferSortDeadline=now.AddSeconds(12);transferSortStage=1;}
            if(now>transferSortDeadline)throw new InvalidOperationException("Sort after transfer was not confirmed; transferred items remain stored.");
            InventoryStatus="Sorting after final transfer";
            if(transferSortStage==1){
                if(p.organizeSortLabels?.IsFaulted==true)throw new InvalidOperationException("Sort labels unavailable.");
                if(p.organizeSortLabels?.IsCompletedSuccessfully!=true)return false;
                transferSortItem=p.OrganizerInventory(true,false).FirstOrDefault(s=>s.Item>0&&s.Quantity>0);
                if(transferSortItem==null){transferSortDirty.Remove(id);transferSortStage=0;return true;}
                var addon=SellerAddon("InventoryRetainer");if(addon==null)addon=SellerAddon("InventoryRetainerLarge");var agent=AgentInventoryContext.Instance();
                if(addon==null||agent==null||SellerAddon("ContextMenu")!=null||SellerAddon("InputNumeric")!=null)throw new InvalidOperationException("Close the existing item menu before sorting.");
                var order=ItemOrderModule.Instance();if(order==null||order->ActiveRetainerId!=id||order->GetActiveRetainerSorter()==null)return false;
                transferSortStage=2;transferSortAt=now;agent->OpenForItemSlot((InventoryType)transferSortItem.Container,transferSortItem.Slot,0,addon->Id);return false;
            }
            if(transferSortStage==2){
                var menu=SellerAddon("ContextMenu");var agent=AgentInventoryContext.Instance();var item=transferSortItem!;
                if(menu==null||!menu->IsReady||agent==null||now-transferSortAt<TimeSpan.FromMilliseconds(150))return false;
                if((uint)agent->TargetInventoryId!=item.Container||agent->TargetInventorySlotId!=item.Slot)throw new InvalidOperationException("Sort item menu changed.");
                var actual=p.ReadChestContainer(item.Container).Single(s=>s.Slot==item.Slot);
                if(!OrganizerSlotMatches(item,actual))throw new InvalidOperationException("Sort target inventory changed.");
                var native=TradeItemShortcutNative.NativeEntry(agent,p.organizeSortLabels!.Result);if(native<0)throw new InvalidOperationException("Sort is not available in this item menu.");
                var label=TradeItemShortcutNative.Read(agent->EventParams[agent->ContexItemStartIndex+native].String.Value);
                var displayed=TradeItemShortcutNative.DisplayedEntry(menu->AtkValues,(int)menu->AtkValuesCount,label);
                if(displayed<0)throw new InvalidOperationException("Could not verify displayed Sort action.");
                transferSortStage=3;transferSortAt=now;((AddonContextMenu*)menu)->OnMenuSelected(displayed,0);return false;
            }
            if(now-transferSortAt<TimeSpan.FromMilliseconds(750)||SellerAddon("ContextMenu")!=null)return false;
            var module=ItemOrderModule.Instance();if(module==null||module->ActiveRetainerId!=id)return false;
            var sorter=module->GetActiveRetainerSorter();if(sorter==null||sorter->SortFunctionIndex!=-1||sorter->PercentComplete!=100)return false;
            transferSortDirty.Remove(id);transferSortStage=0;transferSortItem=null;captureAfterTransfer=true;
            p.RememberOrganizerInventory(id,p.OrganizerInventory(true,false).Concat(p.OrganizerInventory(true,true)).ToArray());return true;
        }
    }
}
