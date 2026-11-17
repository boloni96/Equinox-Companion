using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private enum WorkshopStage { Browse, ProjectList, ProjectConfirm, Request, Slot, Inserted, DeliveryConfirm, Receipt, MenuTransition, CollectConfirm, CollectionReceipt }
    private bool workshopRunning,workshopQueueRun,workshopHq,workshopHqConfirmed;
    private WorkshopStage workshopStage;
    private WorkshopQueueSave? workshopActive;
    private WorkshopPhase? workshopBefore;
    private WorkshopSupply? workshopSupply;
    private HashSet<uint> workshopSelection=[];
    private uint workshopRequestId;
    private DateTimeOffset workshopNext,workshopDeadline,workshopInteraction;
    private string workshopStatus="Ready · optional workshop delivery is disabled by default.";
    private HashSet<string>? workshopPause;
    private int workshopBagBefore;
    private string workshopMenuBefore="",workshopAcceptedPrompt="";
    private const string WorkshopPauseOwner="EquinoxCompanion.Workshop";
    private void PauseWorkshop(bool pause)
    {
        if(!pause){workshopPause?.Remove(WorkshopPauseOwner);workshopPause=null;return;}
        if(!Pi.InstalledPlugins.Any(p=>p.IsLoaded&&p.InternalName=="YesAlready"))return;
        var requests=Pi.GetOrCreateData<HashSet<string>>("YesAlready.StopRequests",()=>new());
        if(!ReferenceEquals(requests,workshopPause))workshopPause?.Remove(WorkshopPauseOwner);
        workshopPause=requests;requests.Add(WorkshopPauseOwner);
    }
    private unsafe string WorkshopHouse()
    {
        var h=HousingManager.Instance();return h==null||h->WorkshopTerritory==null?"":AddressOf(h->WorkshopTerritory->HouseId)?.HouseId??"";
    }
    private unsafe WorkshopPhase? ReadWorkshopPhase()
    {
        var a=SellerAddon("SubmarinePartsMenu");if(a==null||!a->IsReady||a->AtkValues==null||a->AtkValuesCount<156)return null;
        int Number(int i){var v=a->AtkValues[i];return v.Type==AtkValueType.Int?v.Int:v.Type==AtkValueType.UInt&&v.UInt<=int.MaxValue?(int)v.UInt:-1;}
        var n=Number(11);if(n is <1 or >12)return null;
        var rows=new List<WorkshopSupply>();
        for(var i=0;i<n;i++){
            var id=Number(12+i);if(id<=0)return null;
            rows.Add(new((uint)id,retrievalCatalog?.Names.GetValueOrDefault((uint)id,"Item "+id)??"Item "+id,Number(60+i),Number(120+i),Number(108+i)));
        }
        var result=Number(0);if(result<=0)return null;
        var p=new WorkshopPhase((uint)result,Number(6),Number(7),rows.ToArray());return WorkshopPolicy.Valid(p)?p:null;
    }
    private void StopWorkshop(string reason)
    {
        if(workshopWalking){workshopWalking=false;StopIndependentWalk("Workshop stopped");}
        ResetWorkshopBulk();
        workshopPreparationWithdrawal=false;workshopPreparationItems.Clear();
        ResetWorkshopCutscene();
        if(workshopCombinedStage!=0){if(workshopCombinedStage==5&&chestRunning)StopChest("Workshop stopped.");workshopCombinedStage=0;CancelWorkshopWithdrawal();StopIndependentWalk("Workshop stopped");}
        workshopRunning=false;workshopStatus=reason;PauseWorkshop(false);
        RecordTravelPrompt("Workshop stopped",new{reason,queue=workshopQueueRun,completed=workshopActive?.Completed});
    }
    private void WorkshopTransition(WorkshopStage stage,string status,DateTimeOffset now)
    {
        workshopStage=stage;workshopStatus=status;workshopDeadline=now.AddSeconds(45);
        RecordTravelPrompt("Workshop progress",new{stage=stage.ToString(),status,item=workshopSupply?.ItemId,project=workshopBefore?.ItemId});
    }
    private void StartWorkshop(bool queue,bool resumePrepared=false)
    {
        try{
            if(!config.EnableWorkshop||!Player.IsLoaded||WorkshopHouse().Length==0)throw new InvalidOperationException("Enable the tool and enter the company workshop first.");
            if(retrievalRun.Running||craftingWithdrawalPending||organizeRunning||chestRunning||gearRouting||armoireRunning||gardenCareRun?.Running==true||gardenAutoPlantPlan!=null||gardenTendRoute!=null||nativeWorldTrip!=null||nativeHousingTrip!=null)throw new InvalidOperationException("Stop the other active routine first.");
            if(VisibleFollowAddon("Request")||VisibleFollowAddon("SelectYesno"))throw new InvalidOperationException("Close the existing request or confirmation before starting.");
            var saved=config.WorkshopQueues.FirstOrDefault(q=>q.Character==Player.ContentId);
            if(queue&&(saved==null||WorkshopPolicy.Next(saved)==null&&saved.PendingCollection==0&&saved.PendingDeposit==0))throw new InvalidOperationException("Add projects to the queue first.");
            if(queue){WorkshopPolicy.BindForRun(saved!,Player.CurrentWorld.RowId,WorkshopHouse());SaveConfiguration();}
            if(!queue&&(ReadWorkshopPhase() is not {}||workshopSelection.Count==0))throw new InvalidOperationException("Open Material Delivery and select materials first.");
            workshopActive=queue?saved:new(){Character=Player.ContentId,World=Player.CurrentWorld.RowId,House=WorkshopHouse()};
            workshopSingleDelivery=false;workshopQueueRun=queue;
            if(queue&&(!resumePrepared||saved!.Completed>=workshopPreparedThrough))workshopNeedsPreparation=config.WorkshopWithdrawAndDeliver;
            if(queue&&saved!.PendingDeposit!=0&&config.WorkshopDepositParts){BeginWorkshopChest(DateTimeOffset.UtcNow);return;}
            workshopBefore=ReadWorkshopPhase();workshopSupply=null;workshopRequestId=0;workshopInteraction=default;
            if(queue&&saved!.PendingCollection!=0&&RetrievalHeld(saved.PendingCollection)<=saved.CollectionBefore){
                // No receipt yet: retain the verified project identity and retry only its collection menu.
                saved.PendingCollection=0;SaveConfiguration();
            }
            workshopAcceptedPrompt="";workshopRunning=true;PauseWorkshop(true);workshopNext=default;
            WorkshopTransition(queue&&saved!.PendingCollection!=0?WorkshopStage.CollectionReceipt:WorkshopStage.Browse,"Checking company workshop",DateTimeOffset.UtcNow);
        }catch(Exception e){StopWorkshop(e.Message);}
    }
    private unsafe void TickWorkshop(DateTimeOffset now)
    {
        if(now<workshopNext)return;workshopNext=now.AddMilliseconds(250);
        try{
            TickWorkshopWithdrawal();
            TickWorkshopCombined(now);if(workshopCombinedStage!=0)return;
            if(!VisibleFollowAddon("Request"))workshopManualRequestSeen=false;
            if(!workshopRunning){ObserveManualWorkshopDelivery(now);if(!workshopRunning)return;}
            if(workshopSingleDelivery&&!config.WorkshopManualDelivery){StopWorkshop("Manual turn-in assistance disabled.");return;}
            if(!config.EnableWorkshop||!config.EnableRetriever||!Player.IsLoaded||workshopActive==null||Player.ContentId!=workshopActive.Character||Player.CurrentWorld.RowId!=workshopActive.World||WorkshopHouse()!=workshopActive.House){StopWorkshop("Stopped: character, world, workshop or settings changed. Queue preserved.");return;}
            if(workshopQueueRun&&config.WorkshopWithdrawAndDeliver&&workshopActive.Completed>=workshopPreparedThrough)workshopNeedsPreparation=true;
            PauseWorkshop(true);
            if(TickWorkshopCutscene(now))return;
            if(HelperInCutscene()){workshopDeadline=now.AddSeconds(45);return;}
            if(now>workshopDeadline){StopWorkshop("Stopped: "+workshopStatus+" was not confirmed. Queue preserved; check the game window.");return;}
            if(Conditions[ConditionFlag.InCombat]){StopWorkshop("Stopped: combat.");return;}
            if(HelperInCutscene()||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51])return;
            if(retrievalRun.Running||organizeRunning||chestRunning||gardenCareRun?.Running==true){StopWorkshop("Stopped: another item or gardening routine started.");return;}
            if(workshopStage==WorkshopStage.CollectionReceipt){
                var q=workshopActive;
                if(q.PendingCollection==0){StopWorkshop("Collection record is unavailable; review the queue.");return;}
                if(RetrievalHeld(q.PendingCollection)>=q.CollectionBefore+1){q.Completed++;if(config.WorkshopDepositParts)q.PendingDeposit=q.PendingCollection;q.PendingCollection=0;q.ReadyToCollect=0;SaveConfiguration();workshopBefore=null;WorkshopTransition(WorkshopStage.Browse,"Collected project; checking next queued part",now);if(q.PendingDeposit!=0)BeginWorkshopChest(now);}
                return;
            }
            var yes=(AddonSelectYesno*)SellerAddon("SelectYesno");
            if(yes!=null){
                var prompt=WorkshopPolicy.NormalizePrompt(yes->PromptText==null?"":TravelMenuText(yes->PromptText->NodeText.StringPtr)??"");
                if(prompt==workshopAcceptedPrompt)return;
                var expected=workshopQueueRun?WorkshopPolicy.Next(workshopActive):null;
                var allowed=false;
                if(workshopStage==WorkshopStage.ProjectConfirm&&expected!=null)allowed=string.Equals(prompt,$"Craft a {expected.Name.ToLowerInvariant()}?",StringComparison.OrdinalIgnoreCase)||string.Equals(prompt,$"Craft an {expected.Name.ToLowerInvariant()}?",StringComparison.OrdinalIgnoreCase);
                else if(workshopStage==WorkshopStage.CollectConfirm&&expected!=null)allowed=string.Equals(prompt,$"Retrieve the {expected.Name.ToLowerInvariant()} from the company workshop?",StringComparison.OrdinalIgnoreCase);
                else if(workshopStage==WorkshopStage.DeliveryConfirm&&workshopSupply!=null){
                    if(prompt=="Do you really want to trade a high-quality item?"&&workshopHq&&!workshopHqConfirmed){workshopHqConfirmed=true;allowed=true;}
                    else if(WorkshopContributionPrompt(prompt,workshopSupply)&&(!workshopHq||workshopHqConfirmed)){
                        allowed=true;WorkshopTransition(WorkshopStage.Receipt,"Waiting for the delivered-set counter",now);
                    }
                }
                if(!allowed){StopWorkshop("Stopped at an unexpected confirmation: "+prompt);return;}
                if(!NativeEventButton((AtkUnitBase*)yes,yes->YesButton))return;
                workshopAcceptedPrompt=prompt;
                if(workshopStage==WorkshopStage.ProjectConfirm)WorkshopTransition(WorkshopStage.MenuTransition,"Project selected",now);
                else if(workshopStage==WorkshopStage.CollectConfirm)WorkshopTransition(WorkshopStage.CollectionReceipt,"Waiting for the finished item",now);
                return;
            }
            workshopAcceptedPrompt="";
            if(workshopStage is WorkshopStage.ProjectConfirm or WorkshopStage.CollectConfirm or WorkshopStage.DeliveryConfirm)return;
            if(workshopStage is WorkshopStage.Request or WorkshopStage.Slot or WorkshopStage.Inserted){TickWorkshopRequest(now);return;}
            var phase=ReadWorkshopPhase();
            if(workshopStage==WorkshopStage.Receipt){
                if(workshopBefore==null||workshopSupply==null)return;
                if(phase==null){
                    var completedMenu=SellerAddon("SelectString");
                    if(completedMenu==null||!WorkshopPolicy.FinalDeliveryMenuReceipt(workshopBefore,workshopSupply,workshopBagBefore,
                        RetrievalHeld(workshopSupply.ItemId,workshopHq?RetrievalQuality.HqOnly:RetrievalQuality.NqOnly),TransportChoices(completedMenu)))return;
                    phase=workshopBefore with{Rows=workshopBefore.Rows.Select(r=>r==workshopSupply?r with{SetsDelivered=r.SetsDelivered+1}:r).ToArray()};
                }
                if(phase.ItemId!=workshopBefore.ItemId||phase.Phase!=workshopBefore.Phase){StopWorkshop("Project or phase changed before delivery was verified.");return;}
                var row=phase.Rows.FirstOrDefault(r=>r.ItemId==workshopSupply.ItemId);
                if(row!=null&&row.SetsDelivered==workshopSupply.SetsDelivered+1&&RetrievalHeld(row.ItemId,workshopHq?RetrievalQuality.HqOnly:RetrievalQuality.NqOnly)==workshopBagBefore-row.SetSize){
                    if(workshopQueueRun&&workshopDeliveryQualities.TryGetValue((row.ItemId,row.SetSize),out var allocatedQuality))allocatedQuality.TryDequeue(out _);
                    workshopBefore=phase;workshopSupply=null;
                    if(workshopQueueRun&&phase.Phase==phase.TotalPhases-1&&phase.Rows.All(r=>r.RemainingSets==0)){workshopActive.ReadyToCollect=phase.ItemId;SaveConfiguration();}
                    if(workshopSingleDelivery){StopWorkshop("Turn-in complete · click the next material when ready.");return;}
                    WorkshopTransition(WorkshopStage.Browse,"Delivery confirmed",now);
                }
                return;
            }
            var menu=SellerAddon("SelectString");
            if(workshopStage==WorkshopStage.MenuTransition){
                // Do not click the old entry again while the server is processing it.
                if(!WorkshopPolicy.MenuArrived(phase!=null,SellerAddon("CompanyCraftRecipeNoteBook")!=null,menu!=null,
                    menu!=null&&string.Join('|',TransportChoices(menu))==workshopMenuBefore))return;
                ResetWorkshopCutscene();
                WorkshopTransition(WorkshopStage.Browse,"Checking next workshop step",now);
            }
            if(phase!=null){
                if(workshopQueueRun){var expected=WorkshopPolicy.Next(workshopActive);if(expected==null){StopWorkshop("Queue complete.");return;}if(phase.ItemId!=expected.ItemId){
                        if(!WorkshopPolicy.PrioritizeActive(workshopActive,phase.ItemId)){StopWorkshop("The active project is not among the remaining queued projects. Add it to the queue or use Deliver selected.");return;}
                        SaveConfiguration();RecordTravelPrompt("Workshop queue resumed active project",new{project=phase.ItemId,completed=workshopActive.Completed});
                    }}
                else if(workshopBefore!=null&&(phase.ItemId!=workshopBefore.ItemId||phase.Phase!=workshopBefore.Phase)){StopWorkshop("Selected-material delivery complete; phase changed.");return;}
                workshopBefore=phase;
                if(workshopPreparedPhaseOnly&&(phase.ItemId!=workshopPreparedProject||phase.Phase!=workshopPreparedPhase))workshopNeedsPreparation=true;
                var row=phase.Rows.FirstOrDefault(r=>r.RemainingSets>0&&(workshopQueueRun||workshopSelection.Contains(r.ItemId)));
                if(row==null){
                    if(!workshopQueueRun){StopWorkshop("Selected materials delivered.");return;}
                    if(phase.Phase==phase.TotalPhases-1){workshopActive.ReadyToCollect=phase.ItemId;SaveConfiguration();}
                    SellerAddon("SubmarinePartsMenu")->FireCallbackInt(-1);workshopMenuBefore="";WorkshopTransition(WorkshopStage.MenuTransition,"Opening the next production step",now);return;
                }
                if(workshopNeedsPreparation&&BeginWorkshopBulkPreparation(now))return;
                var quality=WorkshopDeliveryQuality(row);
                if(quality==null){if(BeginWorkshopBulkPreparation(now))return;StopWorkshop($"Need one complete set of {row.SetSize} NQ or HQ {row.Name}. Prepare / withdraw materials, then Resume.");return;}
                workshopSupply=row;workshopHq=quality.Value;workshopHqConfirmed=false;workshopRequestId=0;
                workshopBagBefore=RetrievalHeld(row.ItemId,workshopHq?RetrievalQuality.HqOnly:RetrievalQuality.NqOnly);
                var agent=AgentCompanyCraftMaterial.Instance();if(agent==null||!agent->IsAgentActive()||agent->ResultItem!=phase.ItemId)return;
                var index=Array.FindIndex(phase.Rows,r=>r==row);
                // Native material request payload: item ID in the tagged value, set quantity in its high word.
                var values=stackalloc AtkValue[4];for(var i=0;i<4;i++)values[i]=default;
                values[0].Type=AtkValueType.Int;values[0].Int=0;values[1].Type=AtkValueType.UInt;values[1].UInt=(uint)index;values[2].Type=AtkValueType.UInt;values[2].UInt=(uint)row.SetSize;
                values[3].Type=(AtkValueType)(row.ItemId%500000);values[3].Int64=(long)row.SetSize<<32;
                var result=new AtkValue();WorkshopTransition(WorkshopStage.Request,"Opening material request: "+row.Name,now);
                helperReplaying=true;try{agent->ReceiveEvent(&result,values,4,0);}finally{helperReplaying=false;}return;
            }
            if(!workshopQueueRun){StopWorkshop("Material Delivery closed; selected-material delivery stopped.");return;}
            var next=WorkshopPolicy.Next(workshopActive);if(next==null){StopWorkshop("Queue complete · all finished projects received.");return;}
            var book=SellerAddon("CompanyCraftRecipeNoteBook");
            if(book!=null){
                if(workshopNeedsPreparation&&BeginWorkshopBulkPreparation(now))return;
                if(retrievalCatalog==null||!retrievalCatalog.Projects.TryGetValue(next.ItemId,out var project)){StopWorkshop("Project no longer exists in the crafting log.");return;}
                if(!book->IsReady||book->AtkValues==null||book->AtkValuesCount<415)return;
                var args=stackalloc AtkValue[8];for(var i=0;i<8;i++)args[i]=default;
                args[0].Type=AtkValueType.Int;
                if(workshopStage!=WorkshopStage.ProjectList){
                    args[0].Int=2;
                    args[2].Type=AtkValueType.UInt;args[2].UInt=project.Category;
                    args[3].Type=AtkValueType.UInt;args[3].UInt=project.CraftType;
                    for(var i=4;i<=6;i++)args[i].Type=AtkValueType.UInt;
                    WorkshopTransition(WorkshopStage.ProjectList,"Opening crafting category for "+next.Name,now);
                }else{
                    var count=book->AtkValues[13].UInt;
                    if(count==0)return;
                    if(count>100){StopWorkshop("Crafting log list is unavailable; queue preserved.");return;}
                    var listed=false;
                    for(var i=0;i<(int)count;i++)
                        if(WorkshopPolicy.MatchesProject(book->AtkValues[14+4*i].UInt,book->AtkValues[15+4*i].UInt,project)){listed=true;break;}
                    if(!listed){StopWorkshop("The crafting log does not list "+next.Name+" in its category. Check unlocks; queue preserved.");return;}
                    args[0].Int=1;args[4].Type=AtkValueType.UInt;args[4].UInt=project.Sequence;
                    WorkshopTransition(WorkshopStage.ProjectConfirm,"Selecting "+next.Name,now);
                }
                helperReplaying=true;try{book->FireCallback(8,args,true);}finally{helperReplaying=false;}return;
            }
            if(menu!=null){
                var choices=TransportChoices(menu);var index=-1;
                if(workshopBefore!=null){
                    index=choices.FindIndex(s=>s.StartsWith("Contribute materials.",StringComparison.Ordinal));
                    if(index<0&&workshopBefore.Rows.All(r=>r.RemainingSets==0))index=choices.FindIndex(s=>s.StartsWith("Advance to the next phase of production.",StringComparison.Ordinal)||s.StartsWith("Complete the construction of the "+next.Name.ToLowerInvariant()+".",StringComparison.OrdinalIgnoreCase));
                }
                if(index<0&&choices.Contains("Collect finished product.")){
                    // Only collect a project whose identity and completed final phase were observed in this run.
                    if(workshopActive.ReadyToCollect!=next.ItemId){StopWorkshop("Finished project needs verification. Open Material Delivery before resuming, or collect manually and update the queue.");return;}
                    index=choices.IndexOf("Collect finished product.");workshopActive.PendingCollection=next.ItemId;workshopActive.CollectionBefore=RetrievalHeld(next.ItemId);SaveConfiguration();
                    WorkshopTransition(WorkshopStage.CollectConfirm,"Collecting "+next.Name,now);
                }
                if(index<0)index=choices.FindIndex(s=>s.StartsWith("Contribute materials.",StringComparison.Ordinal));
                if(index<0&&choices.Contains("View company crafting log.")&&!choices.Any(s=>s.StartsWith("Discontinue project.",StringComparison.Ordinal)))index=choices.IndexOf("View company crafting log.");
                if(index<0){StopWorkshop("Unrecognized workshop menu; queue preserved.");return;}
                workshopMenuBefore=string.Join('|',choices);
                if(workshopStage!=WorkshopStage.CollectConfirm)WorkshopTransition(WorkshopStage.MenuTransition,"Workshop: "+choices[index],now);
                if(choices[index].StartsWith("Advance to the next phase of production.",StringComparison.Ordinal)||choices[index].StartsWith("Complete the construction of the ",StringComparison.Ordinal)){
                    ResetWorkshopCutscene();workshopSceneArmed=true;UpdateHelperSceneHook();
                }
                SelectTravelChoice(menu,index);return;
            }
            if(now<workshopInteraction)return;
            var target=Objects.FirstOrDefault(o=>o.IsTargetable&&(o.ObjectKind is ObjectKind.EventObj or ObjectKind.EventNpc)&&
                (o.BaseId==2007821||string.Equals(o.Name.TextValue,"Fabrication Station",StringComparison.OrdinalIgnoreCase)));
            if(target==null){workshopStatus="Waiting for the fabrication station to load.";return;}
            if(!ApproachWorkshopTarget(target,4f,now))return;
            // The station's own geometry can fail the walking line-of-sight test.
            // Attempt native interaction at close range; only a loaded menu advances the workflow.
            if(HousingInteract(target)){workshopInteraction=now.AddSeconds(3);workshopStatus="Opening the fabrication station · waiting for its menu";}
        }catch(Exception e){StopWorkshop("Stopped: "+e.Message);}
    }
    private bool WorkshopContributionPrompt(string prompt,WorkshopSupply supply)
    {
        var item=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(supply.ItemId);
        return WorkshopPolicy.ContributionPrompt(prompt,supply.SetSize,supply.Name,item?.Plural.ToString()??"");
    }
    private unsafe void TickWorkshopRequest(DateTimeOffset now)
    {
        var request=(AddonRequest*)SellerAddon("Request");var agent=AgentNpcTrade.Instance();var ui=UIState.Instance();
        if(request==null||!request->IsReady||agent==null||!agent->IsAgentActive()||agent->GetAddonId()!=request->Id||ui==null||workshopSupply==null)return;
        var trade=&ui->NpcTrade;
        if(trade->Requests.Count!=1||request->EntryCount!=1||trade->Requests.Items[0].ItemId!=workshopSupply.ItemId||trade->Requests.Items[0].RequiredQuantity!=workshopSupply.SetSize||trade->Requests.Items[0].WantCollectible||trade->Requests.Items[0].WantMateriaFilledSlots!=0){StopWorkshop("Requested material changed; nothing submitted.");return;}
        if(workshopRequestId!=0&&workshopRequestId!=request->Id){StopWorkshop("Request window changed.");return;}workshopRequestId=request->Id;
        if(workshopStage==WorkshopStage.Request){if(agent->SelectedTurnInSlot>=0){StopWorkshop("An item selection is already active.");return;}WorkshopTransition(WorkshopStage.Slot,"Selecting the complete quality stack",now);agent->SelectTurnInSlot(0);return;}
        if(workshopStage==WorkshopStage.Slot){
            if(agent->SelectedTurnInSlot!=0||agent->SelectedTurnInSlotItemOptions is <1 or >140)return;
            var option=-1;
            for(var i=0;i<agent->SelectedTurnInSlotItemOptions;i++){
                var item=agent->SelectedTurnInSlotItemOptionValues[i].Value;
                if(item==null||item->IsSymbolic||item->ItemId!=workshopSupply.ItemId||item->Quantity<workshopSupply.SetSize||(item->Flags&InventoryItem.ItemFlags.HighQuality)!= (workshopHq?InventoryItem.ItemFlags.HighQuality:InventoryItem.ItemFlags.None))continue;
                if(item->Container is not (InventoryType.Inventory1 or InventoryType.Inventory2 or InventoryType.Inventory3 or InventoryType.Inventory4))continue;
                option=i;break;
            }
            if(option<0){StopWorkshop("No single matching NQ/HQ stack can supply this set. Consolidate the stacks, then Resume.");return;}
            var args=stackalloc AtkValue[4];for(var i=0;i<4;i++){args[i]=default;args[i].Type=AtkValueType.Int;}args[1].Int=option;var result=new AtkValue();
            WorkshopTransition(WorkshopStage.Inserted,"Material selected; handing over once",now);agent->ReceiveEvent(&result,args,4,1);return;
        }
        var button=request->HandOverButton;
        if(agent->SelectedTurnInSlot>=0||button==null||!button->IsEnabled)return;
        WorkshopTransition(WorkshopStage.DeliveryConfirm,"Waiting for material confirmation",now);
        if(!NativeEventButton((AtkUnitBase*)request,button))StopWorkshop("Could not activate the verified hand-over button.");
    }
}
