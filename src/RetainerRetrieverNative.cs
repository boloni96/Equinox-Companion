using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private RetrievalRun retrievalRun=null!;
    private RetrieverPort retrievalPort=null!;
    private DateTimeOffset retrievalTick;private string retrievalLastStatus="";
    private unsafe int RetrievalHeld(uint item){var m=InventoryManager.Instance();if(m==null)return 0;var count=0;foreach(var t in new[]{InventoryType.Inventory1,InventoryType.Inventory2,InventoryType.Inventory3,InventoryType.Inventory4,InventoryType.Crystals}){var c=m->GetInventoryContainer(t);if(c==null||!c->IsLoaded)continue;for(var i=0;i<c->Size;i++)if(c->Items[i].ItemId==item)count=checked(count+c->Items[i].Quantity);}return count;}
    private void UpdateRetriever(DateTimeOffset now)
    {
        if(retrievalPort.Pause.RestorePending)retrievalPort.Pause.Release(now);
        if(!config.EnableRetriever){if(retrievalRun.Running)retrievalRun.Stop("Retriever disabled.");retrieverWindow.IsOpen=false;ForgetOptionalLauncher("Retainer Retriever");return;}
        if(retrievalRun.Running&&retrievalRun.State!=RetrievalRun.Phase.Prepare&&retrievalPort.Pause.Held){var pause=retrievalPort.Pause.Check(now);if(!pause.Ready){retrievalRun.Stop(pause.Error.Length>0?pause.Error:"Another AutoRetainer task started; retrieval stopped.");return;}}
        if(now<retrievalTick)return;retrievalTick=now.AddMilliseconds(200);retrievalRun.Tick(now);
        if(retrievalLastStatus!=retrievalRun.Status){if(!retrievalRun.Running)retrievalPreviewDirty=true;retrievalLastStatus=retrievalRun.Status;Log.Information("Retainer Retriever: {Status}",retrievalLastStatus);if(retrievalLastStatus.StartsWith("Stopped:"))errorJournal.Record("retainer-retriever",retrievalLastStatus);}
    }
    private sealed unsafe class RetrieverPort:IRetrievalPort
    {
        private readonly Plugin p;private readonly SellerPort navigation;
        internal readonly SellerAutoRetainerPause Pause=new(new SellerAutoRetainerPort());
        internal ulong Character;internal uint World;
        private ulong inventoryRetainer;private DateTimeOffset inventoryReadyAt;
        private RetrievalSlot? pending;private int quantity;private bool contextSent,numericSent,prepared;private DateTimeOffset openedAt;
        internal RetrieverPort(Plugin plugin){p=plugin;navigation=new(plugin);}
        public bool ContextMatches=>p.config.EnableRetriever&&Player.IsLoaded&&Player.ContentId==Character&&Player.CurrentWorld.RowId==World&&Player.HomeWorld.RowId==World&&Conditions[ConditionFlag.OccupiedSummoningBell]&&!Conditions[ConditionFlag.InCombat]&&!Conditions[ConditionFlag.BetweenAreas]&&!Conditions[ConditionFlag.BetweenAreas51]&&!p.HelperInCutscene()&&!p.sellerRun.Running&&!p.gilTrade.Running;
        public bool Prepare(){var result=prepared?Pause.Check(DateTimeOffset.UtcNow):Pause.Acquire(DateTimeOffset.UtcNow);prepared=true;if(result.Error.Length>0)throw new InvalidOperationException(result.Error);if(Pi.InstalledPlugins.Any(x=>x.IsLoaded&&x.InternalName=="Artisan")&&Pi.GetIpcSubscriber<bool>("Artisan.IsBusy").InvokeFunc())throw new InvalidOperationException("Artisan is busy. Stop its current task first.");return result.Ready;}
        public bool Select(ulong id)=>navigation.SelectRetainer(new SellerRetainerRef(id,""));
        private static bool InventoryOpen=>SellerAddon("InventoryRetainer")!=null||SellerAddon("InventoryRetainerLarge")!=null;
        public bool OpenInventory(ulong id){if(!SellerActive(id))return false;if(AgentRetainer.Instance()!=null&&AgentRetainer.Instance()->IsAgentActive()&&InventoryOpen){if(inventoryRetainer!=id){inventoryRetainer=id;inventoryReadyAt=DateTimeOffset.UtcNow.AddSeconds(1);}return DateTimeOffset.UtcNow>=inventoryReadyAt;}var menu=(AddonSelectString*)SellerAddon("SelectString");if(menu==null){p.SellerRetainerTalk(id);return false;}var label=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Addon>().GetRow(2378).Text.ToString();for(var i=0;i<Math.Min(20,(int)menu->PopupMenu.PopupMenu.EntryCount);i++)if(TravelMenuText(menu->PopupMenu.PopupMenu.EntryNames[i].Value)==label){menu->FireCallbackInt(i);return false;}return false;}
        public RetrievalSlot[]? Read(ulong id){if(!SellerActive(id)||!InventoryOpen)return null;var m=InventoryManager.Instance();if(m==null)return null;var list=new List<RetrievalSlot>();foreach(var type in Enumerable.Range(10000,7).Append(12001)){var c=m->GetInventoryContainer((InventoryType)type);if(c==null||!c->IsLoaded||c->Items==null)return null;for(var i=0;i<c->Size;i++){var s=c->Items[i];if(s.ItemId>0&&s.Quantity>0)list.Add(new(s.ItemId,s.Quantity,type,i));}}return list.ToArray();}
        public int Held(uint item)=>p.RetrievalHeld(item);
        public bool Withdraw(ulong id,RetrievalSlot slot,int amount)
        {
            if(!SellerActive(id))throw new InvalidOperationException("Selected retainer changed.");
            if(pending is null){
                if(SellerAddon("ContextMenu")!=null||SellerAddon("InputNumeric")!=null)throw new InvalidOperationException("Close the existing item menu before retrieving.");
                var stock=Read(id);if(stock is null||!stock.Contains(slot))throw new InvalidOperationException("Retainer stock changed; rebuild the preview.");
                if(InventoryManager.Instance()->GetEmptySlotsInBag()==0)throw new InvalidOperationException("Free a bag slot before retrieving.");
                pending=slot;quantity=amount;contextSent=numericSent=false;openedAt=DateTimeOffset.UtcNow;
                AgentInventoryContext.Instance()->OpenForItemSlot((InventoryType)slot.Container,slot.Slot,0,AgentRetainer.Instance()->GetAddonId());return false;
            }
            if(pending!=slot)throw new InvalidOperationException("Withdrawal target changed.");
            var target=AgentInventoryContext.Instance();if(target==null||(int)target->TargetInventoryId!=slot.Container||target->TargetInventorySlotId!=slot.Slot||target->TargetInventorySlot==null||target->TargetInventorySlot->ItemId!=slot.ItemId)throw new InvalidOperationException("Item menu target changed; retrieval stopped.");
            if(!contextSent){
                var menu=SellerAddon("ContextMenu");if(menu==null)return false;
                var text=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Addon>().GetRow(slot.Quantity==1||slot.ItemId<=19?98u:773u).Text.ToString();
                var agent=AgentInventoryContext.Instance();var index=0;var found=-1;
                foreach(var v in agent->EventParams){if(v.Type!=AtkValueType.String)continue;if(TravelMenuText(v.String)==text)found=index;index++;}
                if(found<0)throw new InvalidOperationException("Withdraw action not found.");
                var values=stackalloc AtkValue[5];for(var i=0;i<5;i++){values[i]=default;values[i].Type=AtkValueType.Int;}values[1].Int=found;menu->FireCallback(5,values,true);contextSent=true;
                if(slot.Quantity==1){pending=null;return true;}return false;
            }
            var numeric=SellerAddon("InputNumeric");if(numeric==null)return false;
            if(!numericSent){var value=new AtkValue{Type=AtkValueType.Int,Int=quantity};numeric->FireCallback(1,&value,true);numericSent=true;pending=null;return true;}return false;
        }
        public bool Close(ulong id){pending=null;inventoryRetainer=0;if(AgentRetainer.Instance()!=null&&AgentRetainer.Instance()->IsAgentActive()&&InventoryOpen){AgentRetainer.Instance()->Hide();return false;}return navigation.CloseRetainer(id);}
        public void Release(){prepared=false;if(pending is {} s){var a=AgentInventoryContext.Instance();if(a!=null&&(int)a->TargetInventoryId==s.Container&&a->TargetInventorySlotId==s.Slot){CloseSellerAddon("ContextMenu");if(contextSent)CloseSellerAddon("InputNumeric");}}pending=null;Pause.Release(DateTimeOffset.UtcNow);}
    }
}
