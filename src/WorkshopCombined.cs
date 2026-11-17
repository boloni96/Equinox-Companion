using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private int workshopCombinedStage;
    private DateTimeOffset workshopCombinedDeadline;
    private string workshopCombinedHouse="";
    private RetrievalEntry[] workshopCombinedItems=[];
    private RetrievalQuality workshopCombinedQuality;
    private bool BeginWorkshopRestock(WorkshopSupply row,DateTimeOffset now)
    {
        if(!config.WorkshopWithdrawAndDeliver||!workshopQueueRun)return false;
        if(Player.CurrentWorld.RowId!=Player.HomeWorld.RowId){StopWorkshop("Withdraw + turn in requires your home world for retainer access.");return true;}
        workshopChestDeposit=false;
        RefreshRetrievalStock();
        var nq=RetrievalHeld(row.ItemId,RetrievalQuality.NqOnly);var hq=RetrievalHeld(row.ItemId,RetrievalQuality.HqOnly);
        var rn=retrievalStock.Where(s=>s.Item==row.ItemId&&!s.Hq).Sum(s=>s.Quantity);var rh=retrievalStock.Where(s=>s.Item==row.ItemId&&s.Hq).Sum(s=>s.Quantity);
        var batch=WorkshopPolicy.Restock(row,nq,hq,rn,rh,workshopWithdrawQuality);
        if(batch==null){StopWorkshop($"No recorded complete {row.SetSize}-item NQ/HQ set of {row.Name} available for the selected withdrawal quality. Refresh retainer stock or prepare materials, then resume.");return true;}
        workshopCombinedItems=[new(row.ItemId,row.Name,batch.Value.Quantity)];workshopCombinedQuality=batch.Value.Hq?RetrievalQuality.HqOnly:RetrievalQuality.NqOnly;
        workshopWithdrawalCharacter=Player.ContentId;workshopWithdrawalWorld=Player.CurrentWorld.RowId;workshopCombinedHouse=WorkshopHouse();
        workshopCombinedStage=1;workshopCombinedDeadline=now.AddMinutes(2);workshopRunning=false;
        workshopStatus="Withdraw + turn in · going to the workshop summoning bell";
        return true;
    }
    private unsafe void TickWorkshopCombined(DateTimeOffset now)
    {
        if(workshopCombinedStage==0)return;
        try{
            if(!config.EnableWorkshop||!config.EnableRetriever||(!config.WorkshopWithdrawAndDeliver&&!workshopChestDeposit)||!Player.IsLoaded||Player.ContentId!=workshopWithdrawalCharacter||Player.CurrentWorld.RowId!=workshopWithdrawalWorld||WorkshopHouse()!=workshopCombinedHouse||Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51])throw new InvalidOperationException("Character, workshop, world or settings changed.");
            if(sellerRun.Running||TraderRunning||organizeRunning||chestRunning&&workshopCombinedStage!=5||retainerGilRunning||gearRouting||armoireRunning||gardenCareRun?.Running==true||gardenAutoPlantPlan!=null||gardenTendRoute!=null||nativeWorldTrip!=null||nativeHousingTrip!=null)throw new InvalidOperationException("Another routine took control.");
            if(now>workshopCombinedDeadline)throw new InvalidOperationException("Combined workshop step timed out. Queue preserved.");
            if(workshopCombinedStage is 4 or 5){TickWorkshopChest(now);return;}
            if(SellerAddon("FreeCompanyChest")!=null){CloseSellerAddon("FreeCompanyChest");return;}
            if(workshopCombinedStage==2)return; // Withdrawal receipt owns this stage.
            if(VisibleFollowAddon("Request")||VisibleFollowAddon("SelectYesno"))throw new InvalidOperationException("Close the unrelated request or confirmation before resuming.");
            if(SellerAddon("SubmarinePartsMenu")!=null){CloseSellerAddon("SubmarinePartsMenu");return;}
            var menu=SellerAddon("SelectString");
            if(menu!=null){
                var choices=TransportChoices(menu);var leave=choices.FindIndex(s=>s=="Nothing.");
                if(leave<0)throw new InvalidOperationException("Close the current conversation before resuming.");
                SelectTravelChoice(menu,leave);return;
            }
            if(SellerAddon("RetainerList")!=null){CloseSellerAddon("RetainerList");return;}
            var self=Objects.LocalPlayer;if(self==null)return;
            var bell=workshopCombinedStage==1;
            var target=Objects.Where(o=>o.IsTargetable&&(o.ObjectKind is ObjectKind.EventObj or ObjectKind.EventNpc)&&
                (bell?string.Equals(DataManager.GetExcelSheet<Lumina.Excel.Sheets.EObjName>(Dalamud.Game.ClientLanguage.English).GetRowOrDefault(o.BaseId)?.Singular.ToString(),"summoning bell",StringComparison.OrdinalIgnoreCase):
                o.BaseId==2011588||string.Equals(o.Name.TextValue,"Fabrication Station",StringComparison.OrdinalIgnoreCase)))
                .OrderBy(o=>Vector3.DistanceSquared(self.Position,o.Position)).FirstOrDefault();
            if(target==null)throw new InvalidOperationException(bell?"No summoning bell found in this workshop.":"Fabrication station not found.");
            var range=bell?4f:5f;
            if(Vector3.DistanceSquared(self.Position,target.Position)>range*range){
                IndependentWalk(target.Position,range,now,allowFlight:false);
                workshopStatus=(bell?"Walking to workshop bell · ":"Returning to fabrication station · ")+independentWalkStatus;return;
            }
            StopIndependentWalk("Workshop interaction reached");
            if(bell){
                workshopCombinedStage=2;workshopCombinedDeadline=now.AddMinutes(10);
                workshopWithdrawals.Clear();workshopWithdrawals.Enqueue((workshopCombinedItems,workshopCombinedQuality));workshopWithdrawalPending=true;
                StartNextWorkshopWithdrawal();
            }else{
                workshopCombinedStage=0;StartWorkshop(true);
            }
        }catch(Exception e){StopWorkshop("Withdraw + turn in stopped: "+e.Message);}
    }
}
