using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;
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
        private TradeItemMenuSelection? transferSortMenu;
        private ulong transferSortOwner;
        internal void MarkForSort(ulong id){transferSortDirty.Add(id);}
        private void ClearTransferSort()
        {
            if(transferSortItem is {} item){var agent=AgentInventoryContext.Instance();if(agent!=null&&(uint)agent->TargetInventoryId==item.Container&&agent->TargetInventorySlotId==item.Slot)CloseSellerAddon("ContextMenu");}
            transferSortMenu=null;transferSortOwner=0;transferSortItem=null;transferSortStage=0;transferSortDirty.Clear();SortAllowed=true;
        }
        private bool ReadTransferSortTarget(out TradeItemShortcutTarget target)
        {
            target=default;
            if(transferSortItem is not {} item||!ContextMatches||!RetrieverActive(transferSortOwner))return false;
            var agent=AgentInventoryContext.Instance();var inv=InventoryManager.Instance();
            if(agent==null||inv==null||(uint)agent->TargetInventoryId!=item.Container||agent->TargetInventorySlotId!=item.Slot)return false;
            if(!TradeItemShortcutNative.CaptureItem(inv->GetInventorySlot((InventoryType)item.Container,item.Slot),agent->TargetInventorySlot,(InventoryType)item.Container,item.Slot,out target)||target.ItemId!=item.Item||target.Quantity!=item.Quantity||((target.Flags&1)!=0)!=item.Hq)return false;
            target=target with{Character=Character,Partner=0,TradeAddon=(nint)transferSortOwner};return true;
        }
        internal void CaptureTransferSortMenu(AddonArgs args)
        {
            if(transferSortStage!=2)return;
            transferSortMenu=null;
            try{
                var pointer=args switch{AddonSetupArgs a=>a.AtkValues,AddonRefreshArgs a=>a.AtkValues,_=>0};
                var count=args switch{AddonSetupArgs a=>a.AtkValueCount,AddonRefreshArgs a=>a.AtkValueCount,_=>0u};
                if(pointer==0||count>72||p.organizeSortLabels?.IsCompletedSuccessfully!=true||!ReadTransferSortTarget(out var target))return;
                var agent=AgentInventoryContext.Instance();var native=TradeItemShortcutNative.NativeEntry(agent,p.organizeSortLabels.Result);if(native<0)return;
                var label=TradeItemShortcutNative.Read(agent->EventParams[agent->ContexItemStartIndex+native].String.Value);
                var index=TradeItemShortcutNative.DisplayedEntry((AtkValue*)pointer,(int)count,label);
                p.TraceRetainerNavigation($"Sort menu: native {native}, displayed {index}, setup values {count}");
                if(index>=0)transferSortMenu=new(args.Addon.Address,target,label,index,Environment.TickCount64);
            }catch(Exception e){p.TraceRetainerNavigation("Sort menu capture failed: "+e.Message);}
        }
        internal void TransferSortMenuClosed(nint menu){if(transferSortMenu?.Menu==menu)transferSortMenu=null;}
        private bool SkipTransferSort(ulong id,string reason)
        {
            // Sorting is optional; never mark it successful or retry a submitted
            // action. Only dismiss the item menu belonging to this attempt.
            var menu=SellerAddon("ContextMenu");
            if(menu!=null){
                if(!ReadTransferSortTarget(out _))throw new InvalidOperationException("Sort menu ownership changed; stopped before closing another menu.");
                CloseSellerAddon("ContextMenu");
            }
            if(SellerAddon("InputNumeric")!=null)throw new InvalidOperationException("Unexpected quantity prompt while sorting.");
            p.TraceRetainerNavigation($"Sort skipped at stage {transferSortStage}: {reason}; continuing transfers.");
            Log.Warning("Retainer {Retainer} sort skipped at stage {Stage}: {Reason}",id,transferSortStage,reason);
            transferSortDirty.Remove(id);transferSortStage=0;transferSortItem=null;transferSortMenu=null;captureAfterTransfer=true;
            return true;
        }
        private bool SortAfterTransfers(ulong id)
        {
            if(!p.config.SortItemsAfterOrganizing||!SortAllowed||!transferSortDirty.Contains(id))return true;
            if(!ContextMatches||!RetrieverActive(id))throw new InvalidOperationException("Retainer changed before sorting.");
            var now=DateTimeOffset.UtcNow;p.PrepareOrganizerSort();
            if(transferSortStage==0){transferSortOwner=id;transferSortMenu=null;transferSortDeadline=now.AddSeconds(12);transferSortStage=1;}
            if(now>transferSortDeadline){
                var order=ItemOrderModule.Instance();var timedSorter=order==null?null:order->GetActiveRetainerSorter();
                p.TraceRetainerNavigation($"Sort timeout: stage {transferSortStage}, menu {SellerAddon("ContextMenu")!=null}, active owner {(order==null?0:order->ActiveRetainerId)}, function {(timedSorter==null?-999:timedSorter->SortFunctionIndex)}, percent {(timedSorter==null?-1:timedSorter->PercentComplete)}");
                return SkipTransferSort(id,"Sort completion was not confirmed within 12 seconds");
            }
            InventoryStatus="Sorting after final transfer";
            if(transferSortStage==1){
                if(p.organizeSortLabels?.IsFaulted==true)return SkipTransferSort(id,"Sort labels unavailable");
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
                var native=TradeItemShortcutNative.NativeEntry(agent,p.organizeSortLabels!.Result);if(native<0)return SkipTransferSort(id,"Sort is unavailable in this item menu");
                var label=TradeItemShortcutNative.Read(agent->EventParams[agent->ContexItemStartIndex+native].String.Value);
                if(!ReadTransferSortTarget(out var target)||transferSortMenu is not {} captured||!captured.Matches((nint)menu,target,label,Environment.TickCount64))
                    return SkipTransferSort(id,"Could not verify Sort from menu setup");
                var displayed=captured.Index;
                transferSortStage=3;p.TraceRetainerNavigation("Sort action selected; waiting for completion");transferSortAt=now;((AddonContextMenu*)menu)->OnMenuSelected(displayed,0);return false;
            }
            if(now-transferSortAt<TimeSpan.FromMilliseconds(750)||SellerAddon("ContextMenu")!=null)return false;
            var module=ItemOrderModule.Instance();if(module==null||module->ActiveRetainerId!=id)return false;
            var sorter=module->GetActiveRetainerSorter();if(sorter==null||sorter->SortFunctionIndex!=-1||sorter->PercentComplete!=100)return false;
            p.TraceRetainerNavigation("Sort completed");transferSortDirty.Remove(id);transferSortStage=0;transferSortItem=null;transferSortMenu=null;captureAfterTransfer=true;
            p.RememberOrganizerInventory(id,p.OrganizerInventory(true,false).Concat(p.OrganizerInventory(true,true)).ToArray());return true;
        }
    }
}
