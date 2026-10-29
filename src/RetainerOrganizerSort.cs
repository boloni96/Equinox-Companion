using Dalamud.Game;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private bool organizeSortEnabled;
    private ulong[] organizeSortRetainers=[];
    private int organizeSortIndex,organizeSorted,organizeSortEmpty;
    private string organizeFinishReason="";
    private DateTimeOffset organizeSortAt;
    private ChestStack? organizeSortItem;
    private TradeItemMenuSelection? organizeSortMenu;
    private Task<HashSet<string>>? organizeSortLabels;
    private void PrepareOrganizerSort()
    {
        if(organizeSortLabels!=null&&!organizeSortLabels.IsFaulted)return;
        organizeSortLabels=Task.Run(()=>{
            var local=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Addon>();var labels=new HashSet<string>(StringComparer.Ordinal);
            foreach(var row in DataManager.GetExcelSheet<Lumina.Excel.Sheets.Addon>(ClientLanguage.English))
                if(row.Text.ExtractText()=="Sort"&&local.GetRowOrDefault(row.RowId) is {} translated){var text=translated.Text.ExtractText();if(text.Length>0)labels.Add(text);}
            return labels;
        });
    }
    private void FinishOrganize(string reason)
    {
        if(!organizeSortEnabled||organizeCarry>0||organizeSortRetainers.Length==0){StopOrganize(reason);return;}
        organizeFinishReason=reason;organizeSortIndex=organizeSorted=organizeSortEmpty=0;organizeSortItem=null;organizeSortMenu=null;
        organizeActiveRetainer=organizeSortRetainers[0];OrganizeStage(10);
    }
    private unsafe void ClearOrganizerSortMenu()
    {
        if(organizeSortMenu is {} selection&&Player.IsLoaded&&organizePort?.Character==Player.ContentId&&RetrieverActive(organizeActiveRetainer)){
            var menu=SellerAddon("ContextMenu");var agent=AgentInventoryContext.Instance();
            if(menu!=null&&(nint)menu==selection.Menu&&agent!=null&&(uint)agent->TargetInventoryId==selection.Target.Inventory&&agent->TargetInventorySlotId==selection.Target.Slot)menu->Hide(false,true,0);
        }
        organizeSortItem=null;organizeSortMenu=null;
    }
    private unsafe bool ReadOrganizerSortTarget(out TradeItemShortcutTarget target)
    {
        target=default;
        if(organizeSortItem is not {} item||organizePort==null||!organizePort.ContextMatches||!RetrieverActive(organizeActiveRetainer))return false;
        var addon=SellerAddon("InventoryRetainer");if(addon==null)addon=SellerAddon("InventoryRetainerLarge");var agent=AgentInventoryContext.Instance();var inv=InventoryManager.Instance();
        if(addon==null||agent==null||inv==null||(uint)agent->TargetInventoryId!=item.Container||agent->TargetInventorySlotId!=item.Slot)return false;
        if(!TradeItemShortcutNative.CaptureItem(inv->GetInventorySlot((InventoryType)item.Container,item.Slot),agent->TargetInventorySlot,(InventoryType)item.Container,item.Slot,out target)||target.ItemId!=item.Item||target.Quantity!=item.Quantity||((target.Flags&1)!=0)!=item.Hq)return false;
        target=target with{Character=organizePort.Character,TradeAddon=(nint)addon};return true;
    }
    private unsafe void CaptureOrganizerSortMenu(AddonArgs args)
    {
        if(!organizeRunning||organizeStage!=12||organizeSortItem==null)return;
        organizeSortMenu=null;
        try{
            var pointer=args switch{AddonSetupArgs a=>a.AtkValues,AddonRefreshArgs a=>a.AtkValues,_=>0};
            var count=args switch{AddonSetupArgs a=>a.AtkValueCount,AddonRefreshArgs a=>a.AtkValueCount,_=>0u};
            if(pointer==0||count>72||organizeSortLabels?.IsCompletedSuccessfully!=true||!ReadOrganizerSortTarget(out var target))return;
            var agent=AgentInventoryContext.Instance();var native=TradeItemShortcutNative.NativeEntry(agent,organizeSortLabels.Result);if(native<0)return;
            var label=TradeItemShortcutNative.Read(agent->EventParams[agent->ContexItemStartIndex+native].String.Value);
            var index=TradeItemShortcutNative.DisplayedEntry((AtkValue*)pointer,(int)count,label);
            if(index>=0)organizeSortMenu=new(args.Addon.Address,target,label,index,Environment.TickCount64);
        }catch(Exception e){Log.Warning(e,"Retainer Sort menu verification failed");}
    }
    private unsafe void TickOrganizerSort(DateTimeOffset now)
    {
        var port=organizePort!;
        switch(organizeStage){
            case 10:if(port.Select(organizeActiveRetainer))OrganizeStage(11);break;
            case 11:
                if(!port.OpenInventory(organizeActiveRetainer)||port.Read(organizeActiveRetainer)==null)return;
                if(organizeSortLabels?.IsFaulted==true)throw new InvalidOperationException("Sort action labels unavailable.");
                if(organizeSortLabels?.IsCompletedSuccessfully!=true)return;
                organizeSortItem=OrganizerInventory(true,false).FirstOrDefault(s=>s.Item>0&&s.Quantity>0);
                if(organizeSortItem==null){organizeSortEmpty++;OrganizeStage(14);return;}
                var addon=SellerAddon("InventoryRetainer");if(addon==null)addon=SellerAddon("InventoryRetainerLarge");var agent=AgentInventoryContext.Instance();
                if(addon==null||agent==null||SellerAddon("ContextMenu")!=null||SellerAddon("InputNumeric")!=null)throw new InvalidOperationException("Close the existing menu before sorting retainers.");
                var module=ItemOrderModule.Instance();if(module==null||module->ActiveRetainerId!=organizeActiveRetainer||module->GetActiveRetainerSorter()==null)throw new InvalidOperationException("Retainer sort state is not ready.");
                organizeSortAt=now;organizeSortMenu=null;OrganizeStage(12);
                agent->OpenForItemSlot((InventoryType)organizeSortItem.Container,organizeSortItem.Slot,0,addon->Id);break;
            case 12:
                if(now-organizeSortAt>TimeSpan.FromSeconds(8))throw new InvalidOperationException("Could not verify this retainer's Sort menu action.");
                if(now-organizeSortAt<TimeSpan.FromMilliseconds(150))return;
                var menu=SellerAddon("ContextMenu");if(menu==null||!menu->IsReady||organizeSortMenu is not {} selection||!ReadOrganizerSortTarget(out var target))return;
                var context=AgentInventoryContext.Instance();var native=TradeItemShortcutNative.NativeEntry(context,organizeSortLabels!.Result);if(native<0)return;
                var label=TradeItemShortcutNative.Read(context->EventParams[context->ContexItemStartIndex+native].String.Value);
                if(!selection.Matches((nint)menu,target,label,Environment.TickCount64))return;
                // Mark selected before dispatch: setup/finalize events may run synchronously.
                organizeSortAt=now;OrganizeStage(13);((AddonContextMenu*)menu)->OnMenuSelected(selection.Index,0);organizeSortMenu=null;break;
            case 13:
                if(!RetrieverActive(organizeActiveRetainer))throw new InvalidOperationException("Retainer changed during Sort.");
                if(now-organizeSortAt<TimeSpan.FromMilliseconds(750)||SellerAddon("ContextMenu")!=null)return;
                var order=ItemOrderModule.Instance();if(order==null||order->ActiveRetainerId!=organizeActiveRetainer)return;
                var sorter=order->GetActiveRetainerSorter();if(sorter==null||sorter->SortFunctionIndex!=-1||sorter->PercentComplete!=100)return;
                organizeSorted++;RememberOrganizerInventory(organizeActiveRetainer,OrganizerInventory(true,false).Concat(OrganizerInventory(true,true)).ToArray());OrganizeStage(14);break;
            case 14:
                if(!port.Close(organizeActiveRetainer))return;
                organizeSortItem=null;organizeSortMenu=null;
                if(++organizeSortIndex>=organizeSortRetainers.Length){StopOrganize(organizeFinishReason+$" · Sorted {organizeSorted} retainers"+(organizeSortEmpty>0?$" · {organizeSortEmpty} empty":"")+".");return;}
                organizeActiveRetainer=organizeSortRetainers[organizeSortIndex];OrganizeStage(10);break;
        }
    }
}
