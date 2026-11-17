using System.Numerics;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private bool workshopWithdrawOnly;
    private long workshopDeferredCrystals;
    private Dictionary<(uint Item,bool Hq),int> workshopRetainerTargets=[];

    private void BeginWorkshopPreparationWithdrawal(bool turnIns,bool ingredients)
    {
        if(WorkshopUiBusy)throw new InvalidOperationException("Stop the active operation first.");
        if(!config.EnableWorkshop||!config.WorkshopWithdrawAndDeliver||WorkshopHouse().Length==0||Player.CurrentWorld.RowId!=Player.HomeWorld.RowId)
            throw new InvalidOperationException("Enable workshop automation and Withdraw + turn in, then enter your home-world workshop.");
        PrepareWorkshopMaterials();
        if(workshopPreparation==null)throw new InvalidOperationException(workshopPreparationNotice);
        var stock=retrievalStock.Select(s=>new WorkshopBulkStock(s.Item,s.Quantity,s.Hq,false))
            .Concat(WorkshopRecordedChest(0,true).Select(s=>new WorkshopBulkStock(s.Item,s.Quantity,s.Hq,true))).ToArray();
        var extras=ingredients?CraftingPreparation.IngredientWithdrawals(workshopPreparation,workshopNqWithdraw.Concat(workshopHqWithdraw)):[];
        var position=Objects.LocalPlayer?.Position??Vector3.Zero;
        float Distance(string name)=>Objects.Where(o=>o.IsTargetable&&o.Name.TextValue==name).Select(o=>Vector3.DistanceSquared(position,o.Position)).DefaultIfEmpty(float.PositiveInfinity).Min();
        var batches=WorkshopBulkPolicy.Preparation(workshopNqWithdraw,workshopHqWithdraw,turnIns,extras,stock,Distance("Company Chest")<Distance("Summoning Bell"),id=>RetrievalHeld(id),out workshopDeferredCrystals);
        if(batches.Length==0){
            workshopStatus=workshopDeferredCrystals>0?WorkshopWithdrawalSummary():"No recorded stock to withdraw for this selection.";
            return;
        }
        workshopBulkBatches.Clear();foreach(var batch in batches)workshopBulkBatches.Enqueue(batch);
        workshopActive=WorkshopSaved();workshopWithdrawOnly=true;workshopBulkRunning=true;workshopRunning=false;workshopChestDeposit=false;
        workshopWithdrawalCharacter=Player.ContentId;workshopWithdrawalWorld=Player.CurrentWorld.RowId;workshopCombinedHouse=WorkshopHouse();
        PauseWorkshop(true);NextWorkshopBulkBatch(DateTimeOffset.UtcNow);
    }
    private string WorkshopWithdrawalSummary()=>workshopDeferredCrystals>0
        ?$"Withdrawal pass complete · {workshopDeferredCrystals:N0} shards, crystals or clusters deferred at the carrying limit. Craft to free room, then withdraw again."
        :"Workshop withdrawal complete.";
    private int RemainingWorkshopRetainerStock(RetrievalSlot slot)=>
        workshopRetainerTargets.TryGetValue((slot.ItemId,slot.Hq),out var target)
            ?Math.Max(0,target-RetrievalHeld(slot.ItemId,slot.Hq?RetrievalQuality.HqOnly:RetrievalQuality.NqOnly)):0;
}
