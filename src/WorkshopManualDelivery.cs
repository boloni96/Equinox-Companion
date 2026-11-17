using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private bool workshopSingleDelivery,workshopManualRequestSeen;
    private unsafe void ObserveManualWorkshopDelivery(DateTimeOffset now)
    {
        var request=(AddonRequest*)SellerAddon("Request");
        if(request==null)workshopManualRequestSeen=false;
        var enabled=config.EnableRetriever&&config.EnableWorkshop&&config.WorkshopManualDelivery&&Player.IsLoaded;
        // Hold only our own YesAlready pause while the workshop material list is open.
        // This prevents another plugin from racing the verified item choice/confirmations.
        var ready=enabled&&SellerAddon("SubmarinePartsMenu")!=null&&WorkshopHouse().Length>0;
        PauseWorkshop(ready);
        if(!ready||request==null||!request->IsReady||workshopManualRequestSeen)return;
        if(retrievalRun.Running||craftingWithdrawalPending||organizeRunning||chestRunning||gearRouting||armoireRunning||gardenCareRun?.Running==true||gardenAutoPlantPlan!=null||gardenTendRoute!=null)return;
        var phase=ReadWorkshopPhase();var material=AgentCompanyCraftMaterial.Instance();var agent=AgentNpcTrade.Instance();var ui=UIState.Instance();
        if(phase==null||material==null||!material->IsAgentActive()||agent==null||!agent->IsAgentActive()||agent->GetAddonId()!=request->Id||ui==null)return;
        var trade=&ui->NpcTrade;
        if(request->EntryCount!=1||trade->Requests.Count!=1)return;
        var requested=trade->Requests.Items[0];
        if(!WorkshopPolicy.ManualRequestMatches(phase,material->ResultItem,material->SelectedSupplyItemIndex,requested.ItemId,requested.RequiredQuantity))return;
        workshopManualRequestSeen=true;
        if(agent->SelectedTurnInSlot>=0||VisibleFollowAddon("SelectYesno")||requested.WantCollectible||requested.MinCollectibility!=0||requested.WantMateriaFilledSlots!=0){workshopStatus="Manual item selection already started; finish this turn-in yourself.";return;}
        var row=phase.Rows[material->SelectedSupplyItemIndex];
        var quality=WorkshopPolicy.ChooseHq(RetrievalHeld(row.ItemId,RetrievalQuality.NqOnly),RetrievalHeld(row.ItemId,RetrievalQuality.HqOnly),row.SetSize);
        if(quality==null){workshopStatus=$"Need {row.SetSize} {row.Name} of one quality for this turn-in. NQ and HQ cannot be mixed.";return;}
        workshopSingleDelivery=true;workshopQueueRun=false;
        workshopActive=new(){Character=Player.ContentId,World=Player.CurrentWorld.RowId,House=WorkshopHouse()};
        workshopBefore=phase;workshopSupply=row;workshopHq=quality.Value;workshopHqConfirmed=false;workshopRequestId=0;
        workshopBagBefore=RetrievalHeld(row.ItemId,workshopHq?RetrievalQuality.HqOnly:RetrievalQuality.NqOnly);
        workshopAcceptedPrompt="";workshopRunning=true;
        WorkshopTransition(WorkshopStage.Request,"Completing your selected turn-in: "+row.Name,now);
    }
}
