using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private const ulong GlamourStorageDestination=ulong.MaxValue-2;
    private bool gearRouting,gearFromRetainers,gearMoveOwned,gearLookupOwned,gearSkipDyed=true;
    private bool gearContextSent,gearConfirmationSent,gearPausePrepared;
    private int gearPhase,gearStored,gearSkipped,gearReserve=2;
    private ulong gearCharacter;
    private uint gearWorld,gearTerritory;
    private DateTimeOffset gearNext,gearDeadline,gearInteractAt;
    private readonly Queue<uint> gearRemaining=[];
    private readonly Queue<uint> gearDeliveries=[];
    private uint[] gearBatch=[];
    private uint gearItem;
    private RetrievalQuality gearQuality;
    private int gearTradeFilter;
    private ChestStack? gearCopy;
    private GearStorageDestination gearDestination;
    private string gearStatus="";
    private readonly HashSet<uint> gearSelection=[];
    private readonly Dictionary<uint,int> gearKeep=[];
    private readonly HashSet<uint> gearKnownDresser=[];
    private SellerAutoRetainerPause? gearPause;

    private void DrawGearStorageOptions()
    {
        if(!ImGui.SmallButton("Gear storage options…"))return;
        ImGui.OpenPopup("GearStorageOptions");
    }
    private void DrawGearStorageOptionsPopup()
    {
        if(!ImGui.BeginPopup("GearStorageOptions"))return;
        ImGui.BeginDisabled(gearRouting||armoireRunning);
        if(ImGui.Checkbox("Skip dyed gear",ref gearSkipDyed)){config.StorageGearSkipDyed=gearSkipDyed;SaveConfiguration();}
        ImGui.TextWrapped("Armoire first when eligible; otherwise glamour dresser. Dyed gear never goes into the armoire. Turning this filter off permits individual dresser storage only; outfit conversion remains protected.");
        ImGui.SetNextItemWidth(110);if(ImGui.SliderInt("Leave bag slots free",ref gearReserve,1,10)){config.StorageGearReserve=gearReserve;SaveConfiguration();}
        ImGui.TextWrapped("Uses your selected items and quality/tradability filters. Stores one missing copy of each selected item. Existing storage copies, equipped gear, materia and damaged gear are protected. Saved Keep exclusions in Armoire options apply here too. Retainer deliveries require a bell and dresser in this room; movement between them uses Lifestream.");
        ImGui.EndDisabled();ImGui.EndPopup();
    }
    private unsafe void StartGearStorage(IEnumerable<uint> selected,bool fromRetainers,RetrievalQuality quality,int tradeFilter=0,bool respectPlan=false)
    {
        if(gearPause?.RestorePending==true||gearRouting||armoireRunning||organizeRunning||retrievalRun.Running||chestRunning||retainerGilRunning||sellerRun.Running||TraderRunning||craftingWithdrawalPending)throw new InvalidOperationException("Finish the current storage operation first.");
        if(!config.EnableRetriever||!Player.IsLoaded)throw new InvalidOperationException("Enable Organizer and log in first.");
        if(followSession.Armed)throw new InvalidOperationException("Stop following before starting a gear storage route.");
        if(fromRetainers&&Player.CurrentWorld.RowId!=Player.HomeWorld.RowId)throw new InvalidOperationException("Use your retainers on your home world.");
        LoadArmoireCatalogue();gearKeep.Clear();
        var protectedItems=respectPlan?retrievalEntrust?.Protected??[]:Array.Empty<uint>();
        var ids=selected.Distinct().Where(id=>id>0&&DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(id)?.EquipSlotCategory.RowId>0&&!ArmoireExclusions().Contains(id)&&!protectedItems.Contains(id)).ToArray();
        if(respectPlan&&retrievalEntrust is {} plan)foreach(var id in ids)gearKeep[id]=Math.Max(plan.Keep.GetValueOrDefault(id),plan.CategoryKeep.GetValueOrDefault(OrganizeCategory(id)));
        if(ids.Length==0)throw new InvalidOperationException("Select gear using the existing inventory, retainer or plan selection first.");
        if(GearObject(false)==null||fromRetainers&&GearObject(true)==null)throw new InvalidOperationException("Use an inn room with a glamour dresser and summoning bell loaded nearby.");
        gearCharacter=Player.ContentId;gearWorld=Player.CurrentWorld.RowId;gearTerritory=Client.TerritoryType;
        gearFromRetainers=fromRetainers;gearQuality=quality;gearTradeFilter=tradeFilter;gearStored=gearSkipped=0;gearCopy=null;
        gearSelection.Clear();gearSelection.UnionWith(ids);gearRemaining.Clear();foreach(var id in ids)gearRemaining.Enqueue(id);gearDeliveries.Clear();gearBatch=[];
        gearPause=new(new SellerAutoRetainerPort());gearPausePrepared=false;gearRouting=true;gearNext=default;gearMoveOwned=gearLookupOwned=false;GearPhase(0,"Opening dresser / checking destinations");
    }
    private void GearPhase(int phase,string status){gearPhase=phase;gearDeadline=DateTimeOffset.UtcNow.AddSeconds(120);gearInteractAt=default;gearStatus=status;}
    private unsafe void StopGearStorage(string reason)
    {
        if(!gearRouting)return;
        gearRouting=false;
        if(retrievalRun.Running)retrievalRun.Stop("Gear storage route stopped.");
        retrievalPort.GearFilter=false;gearPause?.Release(DateTimeOffset.UtcNow);
        if(gearMoveOwned){try{Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch(Exception e){Log.Warning(e,"Could not stop gear storage movement");}gearMoveOwned=false;}
        if(gearLookupOwned){var a=AgentCabinetWithdraw.Instance();if(a!=null&&a->IsAgentActive())a->Hide();gearLookupOwned=false;}
        if(gearCopy is {} copy){var a=AgentInventoryContext.Instance();if(a!=null&&(uint)a->TargetInventoryId==copy.Container&&a->TargetInventorySlotId==copy.Slot)CloseSellerAddon("ContextMenu");}
        gearStatus=$"{reason} · {gearStored} stored · {gearSkipped} skipped. Any undelivered gear stays in bags / Armoury Chest.";
        gearRemaining.Clear();gearDeliveries.Clear();gearCopy=null;retrievalRefreshPending=true;myInventoryRefresh=default;
    }
    private Dalamud.Game.ClientState.Objects.Types.IGameObject? GearObject(bool bell)
    {
        if(Objects.LocalPlayer is not {} player)return null;
        var name=bell?"summoning bell":"glamour dresser";
        return Objects.Where(o=>o.IsTargetable&&o.ObjectKind==Dalamud.Game.ClientState.Objects.Enums.ObjectKind.EventObj&&Vector3.Distance(o.Position,player.Position)<60&&string.Equals(DataManager.GetExcelSheet<Lumina.Excel.Sheets.EObjName>(Dalamud.Game.ClientLanguage.English).GetRowOrDefault(o.BaseId)?.Singular.ToString(),name,StringComparison.OrdinalIgnoreCase)).OrderBy(o=>Vector3.DistanceSquared(o.Position,player.Position)).FirstOrDefault();
    }
    private unsafe bool GearOpen(bool bell,DateTimeOffset now)
    {
        if(bell?RetainerToolsContext()&&SellerAddon("RetainerList")!=null:ArmoireDresserReady())return true;
        if(gearInteractAt!=default)return false;
        if(Conditions[ConditionFlag.OccupiedSummoningBell]){var id=CurrentRetainerIdentity();if(RetrieverActive(id)){retrievalPort.Close(id);return false;}CloseSellerAddon("RetainerList");return false;}
        if(bell&&ArmoireDresserReady()){CloseSellerAddon("MiragePrismPrismBoxCrystallize");CloseSellerAddon("MiragePrismPrismBox");return false;}
        var obj=GearObject(bell)??throw new InvalidOperationException("Storage object is no longer nearby.");
        var self=Objects.LocalPlayer??throw new InvalidOperationException("Character unavailable.");
        if(Vector3.Distance(self.Position,obj.Position)>4){
            if(gearMoveOwned){if(!LifestreamBusy())throw new InvalidOperationException("Movement ended before reaching storage.");return false;}
            if(!Pi.InstalledPlugins.Any(p=>p.IsLoaded&&p.InternalName=="Lifestream"))throw new InvalidOperationException("Move within reach of storage, or enable Lifestream for the route.");
            if(LifestreamBusy())throw new InvalidOperationException("Lifestream is busy with another trip.");
            var delta=self.Position-obj.Position;delta.Y=0;var target=obj.Position+(delta.LengthSquared()>0.01f?Vector3.Normalize(delta)*2.5f:Vector3.Zero);target.Y=self.Position.Y;
            Pi.GetIpcSubscriber<List<Vector3>,bool?,float?,float?,object>("Lifestream.MoveEx").InvokeAction([target],true,.5f,.25f);gearMoveOwned=true;return false;
        }
        if(gearMoveOwned){Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();gearMoveOwned=false;return false;}
        TargetSystem.Instance()->InteractWithObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)obj.Address,false);gearInteractAt=now;return false;
    }
    private unsafe bool GearLoadCabinet()
    {
        var state=UIState.Instance();if(state==null)return false;
        if(state->Cabinet.IsCabinetLoaded()){
            if(gearLookupOwned){AgentCabinetWithdraw.Instance()->Hide();gearLookupOwned=false;}return true;
        }
        if(!gearLookupOwned){var a=AgentCabinetWithdraw.Instance();if(a==null)return false;if(a->IsAgentActive())throw new InvalidOperationException("Close the existing armoire window first.");a->Show();gearLookupOwned=true;}
        return false;
    }
    private unsafe ChestStack[] ReadGearArmoury()
    {
        var manager=InventoryManager.Instance();if(manager==null)return [];
        var result=new List<ChestStack>();
        foreach(var type in ArmoireContainers.Where(t=>(uint)t>3&&t!=InventoryType.EquippedItems)){
            var c=manager->GetInventoryContainer(type);if(c==null||!c->IsLoaded||c->Items==null)continue;
            for(var i=0;i<c->Size;i++){var s=c->Items[i];if(!s.IsSymbolic&&s.ItemId>0&&s.Quantity>0)result.Add(new((uint)type,i,s.ItemId%1000000,s.Quantity,(s.Flags&InventoryItem.ItemFlags.HighQuality)!=0,1));}
        }
        return result.ToArray();
    }
    private unsafe void RefreshGearDresser()
    {
        var m=MirageManager.Instance();if(m==null||!m->PrismBoxLoaded)throw new InvalidOperationException("Dresser data unavailable.");
        gearKnownDresser.Clear();
        for(var i=0;i<m->PrismBoxItemIds.Length;i++){
            var id=m->PrismBoxItemIds[i]%1000000;if(id==0)continue;
            if(armoireSets!.TryGetValue(id,out var set)){for(var j=0;j<set.Length;j++)if(set[j]>0&&m->IsSetSlotUnlocked((uint)i,j))gearKnownDresser.Add(set[j]);}
            else gearKnownDresser.Add(id);
        }
    }
    private unsafe bool GearInDresser(uint item)
    {
        var m=MirageManager.Instance();if(m==null||!m->PrismBoxLoaded)throw new InvalidOperationException("Dresser contents are unavailable.");
        for(var i=0;i<m->PrismBoxItemIds.Length;i++){
            var id=m->PrismBoxItemIds[i]%1000000;if(id==item)return true;
            if(armoireSets!.TryGetValue(id,out var pieces))for(var j=0;j<pieces.Length;j++)if(pieces[j]==item&&m->IsSetSlotUnlocked((uint)i,j))return true;
        }
        return false;
    }
    private unsafe GearStorageDestination GearDestination(uint item,bool dyed)
    {
        var state=UIState.Instance();if(state==null||!state->Cabinet.IsCabinetLoaded())throw new InvalidOperationException("Armoire contents are unavailable.");
        var eligible=armoireRows!.TryGetValue(item,out var row);
        return GearStoragePolicy.Destination(eligible,eligible&&state->Cabinet.IsItemInCabinet(row),gearKnownDresser.Contains(item),dyed,gearSkipDyed,ArmoireExclusions().Contains(item));
    }
    private unsafe bool GearSlotAllowed(InventoryItem item,InventoryType container)
    {
        if(!gearSelection.Contains(item.ItemId%1000000)||item.IsSymbolic)return false;
        if(((uint)container>=10000||!gearFromRetainers)&&gearKeep.GetValueOrDefault(item.ItemId%1000000) is >0 and var keep){
            var manager=InventoryManager.Instance();if(manager==null)return false;var count=0;
            var types=(uint)container>=10000?Enumerable.Range(10000,7).Select(i=>(InventoryType)i):ArmoireContainers.Where(t=>t!=InventoryType.EquippedItems);
            foreach(var type in types){var c=manager->GetInventoryContainer(type);if(c==null||!c->IsLoaded||c->Items==null)return false;for(var i=0;i<c->Size;i++){var slot=c->Items[i];if(!slot.IsSymbolic&&slot.ItemId%1000000==item.ItemId%1000000&&RetainerRetrieval.AllowsQuality((slot.Flags&InventoryItem.ItemFlags.HighQuality)!=0,gearQuality))count+=slot.Quantity;}}
            if(count<=keep)return false;
        }
        var materia=false;for(var j=0;j<5;j++)materia|=item.Materia[j]!=0;
        if(!GearStoragePolicy.SafeCopy(item.Quantity,container==InventoryType.EquippedItems,item.Stains[0],item.Stains[1],gearSkipDyed,materia,item.Condition))return false;
        if(!RetainerRetrieval.AllowsQuality((item.Flags&InventoryItem.ItemFlags.HighQuality)!=0,gearQuality)||gearTradeFilter!=0&&!RetrievalAllowsTradeFilter(item,gearTradeFilter))return false;
        return GearDestination(item.ItemId%1000000,item.Stains[0]!=0||item.Stains[1]!=0)!=GearStorageDestination.Skip;
    }
    private unsafe ChestStack? GearFindCopy(uint item)
    {
        var manager=InventoryManager.Instance();if(manager==null)return null;
        // Native armoire storage chooses by item ID. Never let it consume an
        // unselected duplicate, including an equipped or differently dyed copy.
        if(ArmoireInventory(item).Held!=1)return null;
        foreach(var type in ArmoireContainers){
            var c=manager->GetInventoryContainer(type);if(c==null||!c->IsLoaded||c->Items==null)continue;
            for(var i=0;i<c->Size;i++){var s=c->Items[i];if(s.ItemId%1000000==item&&GearSlotAllowed(s,type))return new((uint)type,i,item,1,(s.Flags&InventoryItem.ItemFlags.HighQuality)!=0,1);}
        }
        return null;
    }
    private unsafe InventoryItem* GearLiveCopy(ChestStack copy)
    {
        var m=InventoryManager.Instance();if(m==null)return null;var c=m->GetInventoryContainer((InventoryType)copy.Container);
        if(c==null||!c->IsLoaded||c->Items==null||copy.Slot<0||copy.Slot>=c->Size)return null;
        var p=&c->Items[copy.Slot];return p->ItemId%1000000==copy.Item&&GearSlotAllowed(*p,(InventoryType)copy.Container)?p:null;
    }
    private unsafe void TickGearStorage(DateTimeOffset now)
    {
        if(!gearRouting&&gearPause?.RestorePending==true)gearPause.Release(now);
        if(!gearRouting||now<gearNext)return;gearNext=now.AddMilliseconds(250);
        try{
            if(!config.EnableRetriever||!Player.IsLoaded||Player.ContentId!=gearCharacter||Player.CurrentWorld.RowId!=gearWorld||Client.TerritoryType!=gearTerritory||Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||HelperInCutscene()||followSession.Armed||sellerRun.Running||TraderRunning||organizeRunning||chestRunning||retainerGilRunning||armoireRunning||craftingWithdrawalPending)throw new InvalidOperationException("Character, location or active operation changed.");
            if(now>gearDeadline)throw new InvalidOperationException("Game did not confirm the current storage step in time.");
            if(FollowMovementKeysHeld())throw new InvalidOperationException("Stopped by your movement input.");
            var inventory=InventoryManager.Instance();if(inventory==null)return;
            if(gearPhase!=3)foreach(var operation in inventory->PendingOperations)if(!operation.IsEmpty)return;
            var pause=gearPausePrepared?gearPause!.Check(now):gearPause!.Acquire(now);gearPausePrepared=true;if(pause.Error.Length>0)throw new InvalidOperationException(pause.Error);if(!pause.Ready)return;
            if(gearPhase==0){if(!GearOpen(false,now)||!GearLoadCabinet())return;RefreshGearDresser();GearPhase(1,"Planning selected gear destinations");return;}
            if(gearPhase==1){
                if(gearRemaining.Count==0){StopGearStorage("Finished");return;}
                var capacity=gearFromRetainers?GearStoragePolicy.BatchCapacity(ArmoireInventory(0).Free,gearReserve):20;
                if(capacity==0)throw new InvalidOperationException("No staging space beyond your reserved bag slots.");
                var batch=new List<uint>();
                var dresserSlots=MirageManager.Instance()->PrismBoxItemIds.ToArray().Count(id=>id==0);
                var prisms=InventoryManager.Instance()->GetInventoryItemCount(21800);
                while(gearRemaining.Count>0&&batch.Count<capacity){
                    var id=gearRemaining.Dequeue();var destination=GearDestination(id,false);
                    if(destination==GearStorageDestination.Skip||gearFromRetainers&&ArmoireInventory(id).Held>0){gearSkipped++;continue;}
                    // Reserve destination resources before withdrawing a batch.
                    if(destination==GearStorageDestination.Dresser||!gearSkipDyed){
                        var newSlot=GearNeedsDresserSlot(id);
                        if(prisms<1||newSlot&&dresserSlots<1){gearSkipped++;continue;}
                        prisms--;if(newSlot) dresserSlots--;
                    }
                    batch.Add(id);
                }
                if(batch.Count==0)return;
                gearBatch=batch.ToArray();
                if(gearFromRetainers)GearPhase(2,"Going to retainer bell for this batch");
                else{foreach(var id in gearBatch)gearDeliveries.Enqueue(id);GearPhase(4,"Storing selected inventory gear");}return;
            }
            if(gearPhase==2){
                if(!GearOpen(true,now))return;
                retrievalPort.Character=gearCharacter;retrievalPort.World=gearWorld;retrievalPort.Quality=gearQuality;retrievalPort.TradeFilter=gearTradeFilter;retrievalPort.GearFilter=true;
                var retainers=ReadSellerRetainers().Select(r=>r.Id).ToArray();if(retainers.Length==0)return;
                retrievalRun.Start(retainers,gearBatch.Select(id=>new RetrievalEntry(id,SearchItemName(id),1)).ToArray(),false,now);
                GearPhase(3,"Withdrawing selected batch · dye and item filters checked live");return;
            }
            if(gearPhase==3){
                if(retrievalRun.Running){gearStatus=retrievalRun.Status;gearDeadline=now.AddSeconds(120);return;}
                retrievalPort.GearFilter=false;
                if(retrievalRun.Status.StartsWith("Stopped",StringComparison.Ordinal))throw new InvalidOperationException(retrievalRun.Status);
                foreach(var id in gearBatch){if(GearFindCopy(id)!=null)gearDeliveries.Enqueue(id);else gearSkipped++;}
                GearPhase(4,"Delivering batch to glamour storage");return;
            }
            if(gearPhase==4){
                if(!GearOpen(false,now)||!GearLoadCabinet())return;
                if(gearDeliveries.Count==0){GearPhase(1,"Reusing free inventory space for the next batch");return;}
                RefreshGearDresser();gearItem=gearDeliveries.Dequeue();gearCopy=GearFindCopy(gearItem);
                if(gearCopy==null){gearSkipped++;return;}
                var live=GearLiveCopy(gearCopy);if(live==null){gearSkipped++;gearCopy=null;return;}
                gearDestination=GearDestination(gearItem,live->Stains[0]!=0||live->Stains[1]!=0);
                if(gearDestination==GearStorageDestination.Armoire){
                    if(!UIState.Instance()->Cabinet.StoreCabinetItem(armoireRows![gearItem]))throw new InvalidOperationException("Game declined armoire storage; current item kept.");
                    GearPhase(6,"Waiting for armoire receipt");return;
                }
                if(gearDestination==GearStorageDestination.Skip){gearSkipped++;gearCopy=null;return;}
                if(InventoryManager.Instance()->GetInventoryItemCount(21800)<1)throw new InvalidOperationException("A glamour prism is needed for dresser storage.");
                if(TryStoreGearOutfit(*live,gearCopy)){GearPhase(6,"Waiting for dresser receipt");return;}
                // Ask the current game's own item menu for individual storage;
                // never substitute a guessed callback or an outfit that strips dye.
                if(SellerAddon("ContextMenu")!=null||SellerAddon("SelectYesno")!=null)throw new InvalidOperationException("Close the existing item menu or confirmation.");
                AgentInventoryContext.Instance()->OpenForItemSlot((InventoryType)gearCopy.Container,gearCopy.Slot,0,AgentMiragePrismPrismBox.Instance()->GetAddonId());
                gearContextSent=gearConfirmationSent=false;GearPhase(5,"Storing individual glamour");return;
            }
            if(gearPhase==5){if(!ArmoireDresserReady())throw new InvalidOperationException("Dresser closed.");GearStoreIndividual();return;}
            if(gearPhase==6){
                if(!ArmoireDresserReady())throw new InvalidOperationException("Dresser closed before confirming storage.");
                var present=gearDestination==GearStorageDestination.Armoire?UIState.Instance()->Cabinet.IsItemInCabinet(armoireRows![gearItem]):GearInDresser(gearItem);
                if(!GearStoragePolicy.DeliveryConfirmed(ArmoireInventory(gearItem).Held==0,present))return;
                gearKnownDresser.Add(gearItem);gearStored++;gearCopy=null;GearPhase(4,$"{gearStored} stored · delivering batch");
            }
        }catch(Exception e){StopGearStorage("Stopped: "+e.Message);Log.Warning(e,"Gear storage route stopped");}
    }
    private unsafe bool GearNeedsDresserSlot(uint item)
    {
        var m=MirageManager.Instance();if(m==null||!m->PrismBoxLoaded)return true;
        var sets=armoireSets!.Where(kv=>kv.Value.Contains(item)).Select(kv=>kv.Key).ToHashSet();
        foreach(var id in m->PrismBoxItemIds)if(sets.Contains(id%1000000))return false;
        return true;
    }
    private unsafe bool TryStoreGearOutfit(InventoryItem item,ChestStack copy)
    {
        if(item.Stains[0]!=0||item.Stains[1]!=0)return false;
        var sets=armoireSets!.Where(kv=>kv.Value.Contains(copy.Item)).OrderBy(kv=>kv.Key).ToArray();if(sets.Length==0)return false;
        var m=MirageManager.Instance();var set=sets[0].Key;int existing=-1;
        for(var i=0;i<m->PrismBoxItemIds.Length;i++)if(sets.Any(s=>s.Key==m->PrismBoxItemIds[i]%1000000)){existing=i;set=m->PrismBoxItemIds[i]%1000000;break;}
        if(existing<0&&!m->PrismBoxItemIds.Contains(0u))throw new InvalidOperationException("Glamour dresser has no free slot.");
        InventoryType* containers=stackalloc InventoryType[11];ushort* slots=stackalloc ushort[11];for(var i=0;i<11;i++){containers[i]=InventoryType.Invalid;slots[i]=0;}
        containers[0]=(InventoryType)copy.Container;slots[0]=(ushort)copy.Slot;
        var sent=existing>=0?m->StoreExistingOutfit((uint)existing,containers,slots):m->StoreNewOutfit(set,containers,slots);
        if(!sent)throw new InvalidOperationException("Game declined outfit storage; current item kept.");return true;
    }
    private unsafe void GearStoreIndividual()
    {
        if(gearCopy is not {} copy)throw new InvalidOperationException("Missing selected gear copy.");
        var agent=AgentInventoryContext.Instance();
        if(!gearContextSent){
            if(GearLiveCopy(copy)==null||agent==null||(uint)agent->TargetInventoryId!=copy.Container||agent->TargetInventorySlotId!=copy.Slot)throw new InvalidOperationException("Selected item menu changed.");
            var menu=SellerAddon("ContextMenu");if(menu==null)return;
            var english=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Addon>(Dalamud.Game.ClientLanguage.English);
            var labels=english.Where(r=>{var text=r.Text.ExtractText();return text.Contains("glamour dresser",StringComparison.OrdinalIgnoreCase)&&(text.Contains("store",StringComparison.OrdinalIgnoreCase)||text.Contains("convert",StringComparison.OrdinalIgnoreCase));}).Select(r=>DataManager.GetExcelSheet<Lumina.Excel.Sheets.Addon>().GetRow(r.RowId).Text.ExtractText()).ToHashSet();
            var index=0;var found=-1;foreach(var value in agent->EventParams){if(value.Type!=AtkValueType.String)continue;if(labels.Contains(TravelMenuText(value.String)??"")&&!agent->IsContextItemDisabled(index))found=index;index++;}
            if(found<0)throw new InvalidOperationException("The game did not offer individual dresser storage for this copy; item kept.");
            var values=stackalloc AtkValue[5];for(var i=0;i<5;i++){values[i]=default;values[i].Type=AtkValueType.Int;}values[1].Int=found;menu->FireCallback(5,values,true);gearContextSent=true;return;
        }
        if(GearInDresser(gearItem)&&ArmoireInventory(gearItem).Held==0){GearPhase(6,"Checking dresser receipt");return;}
        var confirm=SellerAddon("SelectYesno");if(confirm==null||gearConfirmationSent)return;
        var dresser=AgentMiragePrismPrismBox.Instance();
        if(dresser==null||dresser->Data==null||dresser->ContextAddonId!=confirm->Id||dresser->Data->CrystallizeSelectedItem.Inventory!=(InventoryType)copy.Container||dresser->Data->CrystallizeSelectedItem.Slot!=copy.Slot||dresser->Data->CrystallizeSelectedItem.ItemId%1000000!=copy.Item)
            throw new InvalidOperationException("The confirmation is not owned by the selected dresser operation.");
        var name=SearchItemName(gearItem);var matches=false;
        for(var i=0;i<Math.Min(20,(int)confirm->AtkValuesCount);i++)if(confirm->AtkValues[i].Type==AtkValueType.String&&(TravelMenuText(confirm->AtkValues[i].String)??"").Contains(name,StringComparison.Ordinal))matches=true;
        if(!matches||GearLiveCopy(copy)==null)throw new InvalidOperationException("Dresser confirmation does not match the selected item.");
        var yes=new AtkValue{Type=AtkValueType.Int,Int=0};confirm->FireCallback(1,&yes,true);gearConfirmationSent=true;GearPhase(6,"Waiting for individual dresser receipt");
    }
}
