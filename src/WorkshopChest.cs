using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private WorkshopSupply? workshopChestSupply;
    private bool workshopChestDeposit,workshopChestHq;
    private int workshopChestBagBefore;
    private DateTimeOffset workshopChestInteraction;
    private void BeginWorkshopChest(WorkshopSupply? supply,DateTimeOffset now)
    {
        if(Player.CurrentWorld.RowId!=Player.HomeWorld.RowId){StopWorkshop("FC chest workflow requires your home world.");return;}
        workshopChestSupply=supply;workshopChestDeposit=supply==null;workshopChestHq=workshopWithdrawQuality==2;
        workshopWithdrawalCharacter=Player.ContentId;workshopWithdrawalWorld=Player.CurrentWorld.RowId;workshopCombinedHouse=WorkshopHouse();
        workshopCombinedStage=4;workshopCombinedDeadline=now.AddMinutes(3);workshopChestInteraction=default;
        workshopRunning=false;PauseWorkshop(true);workshopStatus=workshopChestDeposit?"Entrusting finished part · opening FC chest":"Checking FC chest for workshop materials";
    }
    private unsafe void TickWorkshopChest(DateTimeOffset now)
    {
        if(workshopCombinedStage==5){
            if(chestRunning){workshopStatus="Workshop FC chest · "+chestStatus;return;}
            if(chestStatus.StartsWith("Stopped",StringComparison.Ordinal))throw new InvalidOperationException(chestStatus);
            if(workshopChestDeposit){
                var id=workshopActive!.PendingDeposit;
                if(!WorkshopPolicy.DepositReceived(workshopChestBagBefore,RetrievalHeld(id),chestGoals.GetValueOrDefault(id)))throw new InvalidOperationException("Finished part was not confirmed in the selected FC chest tab. Check capacity and permissions, then resume.");
                workshopActive.PendingDeposit=0;SaveConfiguration();
                if(WorkshopPolicy.Next(workshopActive)==null){CloseSellerAddon("FreeCompanyChest");StopWorkshop("Queue complete · all finished parts entrusted to FC chest.");return;}
                workshopCombinedStage=3;workshopCombinedDeadline=now.AddMinutes(2);return;
            }
            var row=workshopChestSupply!;
            var nq=RetrievalHeld(row.ItemId,RetrievalQuality.NqOnly);var hq=RetrievalHeld(row.ItemId,RetrievalQuality.HqOnly);
            if(WorkshopPolicy.ChooseHq(nq,hq,row.SetSize)!=null){workshopCombinedStage=3;workshopCombinedDeadline=now.AddMinutes(2);return;}
            if(workshopWithdrawQuality==0&&!workshopChestHq){workshopChestHq=true;workshopCombinedStage=4;return;}
            CloseSellerAddon("FreeCompanyChest");
            BeginWorkshopRestock(row,now,chestTried:true);return;
        }
        if(VisibleFollowAddon("Request")||VisibleFollowAddon("SelectYesno"))throw new InvalidOperationException("Close the current request or confirmation before resuming.");
        if(SellerAddon("SubmarinePartsMenu")!=null){CloseSellerAddon("SubmarinePartsMenu");return;}
        var menu=SellerAddon("SelectString");
        if(menu!=null){var leave=TransportChoices(menu).FindIndex(s=>s=="Nothing.");if(leave<0)throw new InvalidOperationException("Close the current conversation before resuming.");SelectTravelChoice(menu,leave);return;}
        if(SellerAddon("RetainerList")!=null){CloseSellerAddon("RetainerList");return;}
        if(SellerAddon("FreeCompanyChest")!=null){
            var quality=workshopChestDeposit?RetrievalQuality.Any:workshopChestHq?RetrievalQuality.HqOnly:RetrievalQuality.NqOnly;
            var id=workshopChestDeposit?workshopActive!.PendingDeposit:workshopChestSupply!.ItemId;
            workshopChestBagBefore=RetrievalHeld(id,quality);
            if(workshopChestDeposit&&workshopChestBagBefore<1)throw new InvalidOperationException("Pending finished part is not in your bags; review the queue before resuming.");
            var goals=new Dictionary<uint,int>{{id,workshopChestDeposit?1:workshopChestSupply!.RemainingQuantity}};
            var pages=workshopChestDeposit?new[]{(uint)(19999+Math.Clamp(config.WorkshopChestTab,1,5))}:new uint[]{20000,20001,20002,20003,20004};
            retrievalError="";StartChest(workshopChestDeposit?4:0,explicitGoals:goals,explicitPages:pages,explicitQuality:quality);
            if(!chestRunning)throw new InvalidOperationException(retrievalError.Length>0?retrievalError:"FC chest transfer could not start.");
            workshopCombinedStage=5;workshopCombinedDeadline=now.AddMinutes(5);return;
        }
        var self=Objects.LocalPlayer;if(self==null)return;
        var target=Objects.Where(o=>o.IsTargetable&&o.ObjectKind==ObjectKind.EventObj&&
            (string.Equals(o.Name.TextValue,"Company Chest",StringComparison.OrdinalIgnoreCase)||string.Equals(DataManager.GetExcelSheet<Lumina.Excel.Sheets.EObjName>(Dalamud.Game.ClientLanguage.English).GetRowOrDefault(o.BaseId)?.Singular.ToString(),"company chest",StringComparison.OrdinalIgnoreCase)))
            .OrderBy(o=>Vector3.DistanceSquared(o.Position,self.Position)).FirstOrDefault();
        if(target==null)throw new InvalidOperationException("No FC chest found in this workshop.");
        if(Vector3.DistanceSquared(self.Position,target.Position)>16){IndependentWalk(target.Position,4,now,allowFlight:false);workshopStatus="Walking to workshop FC chest · "+independentWalkStatus;return;}
        StopIndependentWalk("Workshop FC chest reached");if(now<workshopChestInteraction)return;
        if(HousingInteract(target))workshopChestInteraction=now.AddSeconds(3);
    }
}
