namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly Queue<(RetrievalEntry[] Items,RetrievalQuality Quality)> workshopWithdrawals=new();
    private Dictionary<uint,int> workshopWithdrawalTargets=[];
    private RetrievalQuality workshopWithdrawalActiveQuality;
    private bool workshopWithdrawalPending;
    private ulong workshopWithdrawalCharacter;private uint workshopWithdrawalWorld;
    private void BeginWorkshopWithdrawal()
    {
        // Refresh the allocations immediately before transferring; the UI filter never mixes NQ/HQ sets.
        PrepareWorkshopMaterials();if(workshopPreparation==null)return;
        workshopWithdrawals.Clear();
        foreach(var batch in WorkshopPolicy.WithdrawalBatches(workshopNqWithdraw,workshopHqWithdraw,workshopWithdrawQuality))workshopWithdrawals.Enqueue(batch);
        if(workshopWithdrawals.Count==0){workshopPreparationNotice="No recorded turn-ins to withdraw for this quality filter.";return;}
        workshopWithdrawalCharacter=Player.ContentId;workshopWithdrawalWorld=Player.CurrentWorld.RowId;
        workshopWithdrawalPending=true;StartNextWorkshopWithdrawal();
    }
    private void StartNextWorkshopWithdrawal()
    {
        if(!workshopWithdrawals.TryDequeue(out var next)){workshopWithdrawalPending=false;
            if(workshopCombinedStage==2){workshopCombinedStage=3;workshopCombinedDeadline=DateTimeOffset.UtcNow.AddMinutes(2);workshopStatus="Materials received · returning to fabrication station";}
            else workshopStatus="Turn-in withdrawal complete · refresh materials before crafting.";return;}
        workshopWithdrawalActiveQuality=next.Quality;
        workshopWithdrawalTargets=next.Items.ToDictionary(e=>e.ItemId,e=>checked(RetrievalHeld(e.ItemId,next.Quality)+e.Quantity));
        try{WithdrawPreparation(next.Items,next.Quality);}
        catch(Exception e){workshopWithdrawals.Clear();workshopWithdrawalPending=false;StopWorkshop("Withdrawal stopped: "+e.Message);}
    }
    private void TickWorkshopWithdrawal()
    {
        if(!workshopWithdrawalPending)return;
        if(!Player.IsLoaded||Player.ContentId!=workshopWithdrawalCharacter||Player.CurrentWorld.RowId!=workshopWithdrawalWorld){CancelWorkshopWithdrawal();workshopStatus="Withdrawal stopped: character or world changed.";return;}
        if(retrievalRun.Running||craftingWithdrawalPending)return;
        if(retrievalRun.Status.StartsWith("Stopped",StringComparison.Ordinal)||!workshopWithdrawalTargets.All(e=>RetrievalHeld(e.Key,workshopWithdrawalActiveQuality)>=e.Value)){
            workshopWithdrawalPending=false;workshopWithdrawals.Clear();StopWorkshop("Withdrawal stopped before all allocated items arrived. "+retrievalRun.Status);return;
        }
        StartNextWorkshopWithdrawal();
    }
    private void CancelWorkshopWithdrawal()
    {
        workshopCombinedStage=0;workshopWalking=false;StopIndependentWalk("Workshop withdrawal stopped");PauseWorkshop(false);
        workshopWithdrawalPending=false;workshopWithdrawals.Clear();workshopWithdrawalTargets.Clear();
        CancelCraftingWithdrawal("Stopped by you.");if(retrievalRun.Running)retrievalRun.Stop("Stopped by you.");
        workshopStatus="Turn-in withdrawal stopped.";
    }
}
