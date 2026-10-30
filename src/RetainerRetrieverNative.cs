using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private static bool RetrievalTradableItem(uint item)=>DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(item) is {} row&&!row.IsUntradable;
    private static bool RetrievalAllowsTradeFilter(InventoryItem slot,int filter){var row=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(slot.ItemId);return WithdrawalTradePolicy.Allows(filter,row.HasValue,row?.IsUntradable??true,slot.SpiritbondOrCollectability,(slot.Flags&InventoryItem.ItemFlags.Collectable)!=0);}
    private RetrievalRun retrievalRun=null!;
    private RetrieverPort retrievalPort=null!;
    private DateTimeOffset retrievalTick;private string retrievalLastStatus="";
    private unsafe int RetrievalHeld(uint item,RetrievalQuality quality=RetrievalQuality.Any,int tradeFilter=0){var m=InventoryManager.Instance();if(m==null)return 0;var count=0;foreach(var t in new[]{InventoryType.Inventory1,InventoryType.Inventory2,InventoryType.Inventory3,InventoryType.Inventory4,InventoryType.Crystals}){var c=m->GetInventoryContainer(t);if(c==null||!c->IsLoaded)continue;for(var i=0;i<c->Size;i++)if(c->Items[i].ItemId==item&&(tradeFilter==0||RetrievalAllowsTradeFilter(c->Items[i],tradeFilter))&&RetainerRetrieval.AllowsQuality((c->Items[i].Flags&InventoryItem.ItemFlags.HighQuality)!=0,quality))count=checked(count+c->Items[i].Quantity);}return count;}
    private void UpdateRetriever(DateTimeOffset now)
    {
        TickStorageRefresh(now);TickRetainerTools(now);TickRetainerGil(now);TickChest(now);TickOrganize(now);
        if(retrievalPort.Pause.RestorePending)retrievalPort.Pause.Release(now);
        if(!config.EnableRetriever){CancelCraftingWithdrawal("");if(retrievalRun.Running)retrievalRun.Stop("Retriever disabled.");retrieverWindow.IsOpen=false;ForgetOptionalLauncher("Equinox Organizer");return;}
        if(retrievalRun.Running&&retrievalRun.State!=RetrievalRun.Phase.Prepare&&retrievalPort.Pause.Held){var pause=retrievalPort.Pause.Check(now);if(!pause.Ready){retrievalRun.Stop(pause.Error.Length>0?pause.Error:"Another AutoRetainer task started; retrieval stopped.");return;}}
        if(now<retrievalTick)return;retrievalTick=now.AddMilliseconds(200);TickCraftingWithdrawal(now);retrievalRun.Tick(now);
        if(retrievalLastStatus!=retrievalRun.Status){if(!retrievalRun.Running)retrievalPreviewDirty=true;retrievalLastStatus=retrievalRun.Status;Log.Information("Equinox Organizer: {Status}",retrievalLastStatus);if(retrievalLastStatus.StartsWith("Stopped:"))errorJournal.Record("retainer-retriever",retrievalLastStatus);}
    }
    private sealed unsafe partial class RetrieverPort:IRetrievalPort
    {
        private readonly Plugin p;private readonly SellerPort navigation;
        internal readonly SellerAutoRetainerPause Pause=new(new SellerAutoRetainerPort());
        internal int TradeFilter;internal RetrievalQuality Quality;internal ulong Character;internal uint World;
        private readonly WithdrawalArmouryReceipt armouryReceipt=new();
        private bool captureAfterTransfer;
        private static readonly InventoryType[] ArmouryDestinations=[
            InventoryType.ArmoryMainHand,InventoryType.ArmoryOffHand,InventoryType.ArmoryHead,
            InventoryType.ArmoryBody,InventoryType.ArmoryHands,InventoryType.ArmoryLegs,
            InventoryType.ArmoryFeets,InventoryType.ArmoryEar,InventoryType.ArmoryNeck,
            InventoryType.ArmoryWrist,InventoryType.ArmoryRings];
        private int ArmouryHeld(uint item){
            var m=InventoryManager.Instance();if(m==null)throw new InvalidOperationException("Player inventory unavailable.");
            var count=0;
            foreach(var type in ArmouryDestinations){
                var c=m->GetInventoryContainer(type);
                if(c==null||!c->IsLoaded||c->Items==null)throw new InvalidOperationException("Armoury Chest is not loaded; open it and retry withdrawal.");
                for(var i=0;i<c->Size;i++){
                    var slot=c->Items[i];
                    if(slot.ItemId==item&&(TradeFilter==0||RetrievalAllowsTradeFilter(slot,TradeFilter))&&
                        RetainerRetrieval.AllowsQuality((slot.Flags&InventoryItem.ItemFlags.HighQuality)!=0,Quality))
                        count=checked(count+slot.Quantity);
                }
            }
            return count;
        }
        public void Begin(IEnumerable<uint> items){captureAfterTransfer=false;
            // Only gear can be redirected here. Material/crystal withdrawals keep their existing bag semantics.
            var gear=items.Distinct().Where(id=>DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(id) is {} row&&row.EquipSlotCategory.RowId>0);
            armouryReceipt.Begin(gear.ToDictionary(id=>id,ArmouryHeld));
        }
        private ulong inventoryRetainer;private DateTimeOffset inventoryReadyAt;
        private RetrievalSlot? pending;private int quantity;private bool contextSent,numericSent,prepared;private DateTimeOffset openedAt;
        internal RetrieverPort(Plugin plugin){p=plugin;navigation=new(plugin);}
        public bool ContextMatches=>p.config.EnableRetriever&&Player.IsLoaded&&Player.ContentId==Character&&Player.CurrentWorld.RowId==World&&Player.HomeWorld.RowId==World&&Conditions[ConditionFlag.OccupiedSummoningBell]&&!Conditions[ConditionFlag.InCombat]&&!Conditions[ConditionFlag.BetweenAreas]&&!Conditions[ConditionFlag.BetweenAreas51]&&!p.HelperInCutscene()&&!p.sellerRun.Running&&!p.TraderRunning;
        public bool Prepare(){var result=prepared?Pause.Check(DateTimeOffset.UtcNow):Pause.Acquire(DateTimeOffset.UtcNow);prepared=true;if(result.Error.Length>0)throw new InvalidOperationException(result.Error);if(Pi.InstalledPlugins.Any(x=>x.IsLoaded&&x.InternalName=="Artisan")&&Pi.GetIpcSubscriber<bool>("Artisan.IsBusy").InvokeFunc())throw new InvalidOperationException("Artisan is busy. Stop its current task first.");return result.Ready;}
        public bool Select(ulong id){
            var rm=RetainerManager.Instance();var current=rm==null?0:rm->LastSelectedRetainerId;
            if(current!=id&&RetrieverActive(current)&&InventoryOpen){Close(current);return false;}
            if(RetrieverActive(id)){InventoryStatus="Selected retainer confirmed by game menu";return true;}
            InventoryStatus="Waiting for retainer selection / menu identity";return navigation.SelectRetainer(new SellerRetainerRef(id,""));
        }
        private static bool InventoryOpen=>SellerAddon("InventoryRetainer")!=null||SellerAddon("InventoryRetainerLarge")!=null;
        private string inventoryStatus="Not opened";
        internal string InventoryStatus {get=>inventoryStatus;private set{inventoryStatus=value;p.TraceRetainerNavigation(value);}}
        private DateTimeOffset inventoryMenuNext;
        public bool OpenInventory(ulong id)
        {
            if(!RetrieverActive(id)){InventoryStatus="Waiting for selected retainer identity";return false;}
            if(AgentRetainer.Instance()!=null&&AgentRetainer.Instance()->IsAgentActive()&&InventoryOpen){
                if(inventoryRetainer!=id){inventoryRetainer=id;inventoryReadyAt=DateTimeOffset.UtcNow.AddSeconds(1);}
                InventoryStatus="Inventory open; waiting for stock";
                return DateTimeOffset.UtcNow>=inventoryReadyAt;
            }
            var menu=(AddonSelectString*)SellerAddon("SelectString");
            if(menu==null){InventoryStatus="Waiting for retainer menu / greeting";p.SellerRetainerTalk(id);return false;}
            var label=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Addon>().GetRow(2378).Text.ExtractText();
            var entries=new List<string>();
            for(var i=0;i<Math.Min(20,(int)menu->PopupMenu.PopupMenu.EntryCount);i++){
                var text=TravelMenuText(menu->PopupMenu.PopupMenu.EntryNames[i].Value)??"";entries.Add(text);
                if(!RetainerMenuPolicy.Matches(text,label))continue;
                InventoryStatus="Opening entrust / withdraw items";
                if(DateTimeOffset.UtcNow>=inventoryMenuNext){SelectTravelChoice((AtkUnitBase*)menu,i);inventoryMenuNext=DateTimeOffset.UtcNow.AddSeconds(1);}
                return false;
            }
            InventoryStatus="Inventory menu entry not found: "+string.Join(" | ",entries);
            return false;
        }
        public RetrievalSlot[]? Read(ulong id)
        {
            if(!RetrieverActive(id)||!InventoryOpen)return null;
            var m=InventoryManager.Instance();if(m==null)return null;
            var list=new List<RetrievalSlot>();
            foreach(var type in Enumerable.Range(10000,7).Append(12001)){
                var c=m->GetInventoryContainer((InventoryType)type);
                if(c==null||!c->IsLoaded||c->Items==null){InventoryStatus="Inventory open; waiting for container "+type;return null;}
                var crystalBags=type==12001?p.ReadChestContainer(2001):Array.Empty<ChestStack>();
                for(var i=0;i<c->Size;i++){
                    var s=c->Items[i];if(TradeFilter!=0&&!RetrievalAllowsTradeFilter(s,TradeFilter))continue;var hq=(s.Flags&InventoryItem.ItemFlags.HighQuality)!=0;
                    var amount=type==12001?Math.Min(s.Quantity,CrystalStorage.Room(s.ItemId,crystalBags)):s.Quantity;
                    if(s.ItemId>0&&amount>0&&RetainerRetrieval.AllowsQuality(hq,Quality))list.Add(new(s.ItemId,amount,type,i,hq));
                }
            }
            return list.ToArray();
        }
        public void Confirmed(ulong id,RetrievalSlot slot,int amount){captureAfterTransfer=true;p.retainerCaptureStability.ConfirmTransfer(Character,id);p.RequestStorageRefresh();p.ReportStorageTransfer("Withdrew",slot.ItemId,slot.Hq,amount,p.StorageRetainerName(id));}
        public int Held(uint item)=>armouryReceipt.Held(item,p.RetrievalHeld(item,Quality,TradeFilter),armouryReceipt.Tracks(item)?ArmouryHeld(item):0);
        public bool Withdraw(ulong id,RetrievalSlot slot,int amount)
        {
            if(!RetrieverActive(id))throw new InvalidOperationException("Selected retainer changed.");
            if(slot.Container==12001){
                var source=p.ReadChestContainer(12001).First(s=>s.Slot==slot.Slot);
                var crystalTarget=p.ReadChestContainer(2001).FirstOrDefault(s=>CrystalStorage.CanReceive(source,s));
                if(source.Item!=slot.ItemId||crystalTarget==null)throw new InvalidOperationException("Crystal inventory changed; rebuild the preview.");
                return TransferCrystals(id,new(source,crystalTarget,amount));
            }
            if(pending is null){
                if(SellerAddon("ContextMenu")!=null||SellerAddon("InputNumeric")!=null)throw new InvalidOperationException("Close the existing item menu before withdrawing.");
                var stock=Read(id);if(stock is null||!stock.Contains(slot))throw new InvalidOperationException("Retainer stock changed; rebuild the preview.");
                if(InventoryManager.Instance()->GetEmptySlotsInBag()==0)throw new InvalidOperationException("Free a bag slot before withdrawing.");
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
        public bool Close(ulong id){pending=null;inventoryRetainer=0;if(AgentRetainer.Instance()!=null&&AgentRetainer.Instance()->IsAgentActive()&&InventoryOpen){if(captureAfterTransfer&&RetrieverActive(id)&&!p.CaptureRetainerBeforeClose())return false;captureAfterTransfer=false;AgentRetainer.Instance()->Hide();return false;}var menu=(AddonSelectString*)SellerAddon("SelectString");if(RetrieverActive(id)&&menu!=null){var quit=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Addon>().GetRow(2383).Text.ExtractText();for(var i=0;i<Math.Min(20,(int)menu->PopupMenu.PopupMenu.EntryCount);i++)if(RetainerMenuPolicy.Matches(TravelMenuText(menu->PopupMenu.PopupMenu.EntryNames[i].Value),quit)){if(DateTimeOffset.UtcNow>=inventoryMenuNext){SelectTravelChoice((AtkUnitBase*)menu,i);inventoryMenuNext=DateTimeOffset.UtcNow.AddSeconds(1);}return false;}}return navigation.CloseRetainer(id);}
        public void Release(){p.RequestStorageRefresh();CancelCrystalTransfer();prepared=false;if(pending is {} s){var a=AgentInventoryContext.Instance();if(a!=null&&(int)a->TargetInventoryId==s.Container&&a->TargetInventorySlotId==s.Slot){CloseSellerAddon("ContextMenu");if(contextSent)CloseSellerAddon("InputNumeric");}}pending=null;Pause.Release(DateTimeOffset.UtcNow);}
    }
}
