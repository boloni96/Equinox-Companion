using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private bool workshopNeedsPreparation,workshopBulkRunning,workshopPreparedPhaseOnly;
    private int workshopPreparedThrough=int.MaxValue,workshopPreparedPhase;
    private uint workshopPreparedProject;
    private readonly Queue<WorkshopBulkBatch> workshopBulkBatches=new();
    private WorkshopBulkBatch? workshopBulkCurrent;
    private Dictionary<uint,int> workshopBulkTargets=[];
    private bool BeginWorkshopBulkPreparation(DateTimeOffset now)
    {
        if(!config.WorkshopWithdrawAndDeliver||!workshopQueueRun)return false;
        if(Player.CurrentWorld.RowId!=Player.HomeWorld.RowId){StopWorkshop("Withdraw + turn in requires your home world.");return true;}
        PrepareWorkshopMaterials();
        if(workshopPreparation==null){StopWorkshop(workshopPreparationNotice);return true;}
        if(workshopPreparation.Steps.Length>0||workshopPreparation.Missing.Length>0){
            workshopView=2;StopWorkshop("Prepare the missing turn-ins first · see Craft / Missing and quality notes. "+workshopPreparationNotice);return true;
        }
        // Prefer the whole remaining queue. Capacity is the only reason to stage
        // fewer projects; reserve one bag slot for the completed part.
        var projects=WorkshopSaved().Items.Sum(e=>e.Quantity)-WorkshopSaved().Completed;
        workshopPreparedPhaseOnly=false;
        while(!WorkshopBulkFits()&&projects>1){projects=Math.Max(1,projects/2);PrepareWorkshopMaterials(projects);}
        if(!WorkshopBulkFits()&&ReadWorkshopPhase() is {} current){
            PrepareWorkshopMaterials(1,currentPhaseOnly:true);workshopPreparedPhaseOnly=true;workshopPreparedPhase=current.Phase;workshopPreparedProject=current.ItemId;
        }
        if(!WorkshopBulkFits()){StopWorkshop("Free bag space for at least the current production phase and one finished part, then resume.");return true;}
        workshopPreparedThrough=WorkshopSaved().Completed+projects;
        workshopNeedsPreparation=false;
        if(workshopNqWithdraw.Length==0&&workshopHqWithdraw.Length==0)return false;
        var position=Objects.LocalPlayer?.Position??Vector3.Zero;
        float Distance(string name)=>Objects.Where(o=>o.IsTargetable&&string.Equals(o.Name.TextValue,name,StringComparison.OrdinalIgnoreCase)).Select(o=>Vector3.DistanceSquared(position,o.Position)).DefaultIfEmpty(float.PositiveInfinity).Min();
        var stock=retrievalStock.Select(s=>new WorkshopBulkStock(s.Item,s.Quantity,s.Hq,false))
            .Concat(WorkshopRecordedChest(0).Select(s=>new WorkshopBulkStock(s.Item,s.Quantity,s.Hq,true)));
        var batches=WorkshopBulkPolicy.Allocate(workshopNqWithdraw,workshopHqWithdraw,stock,Distance("Company Chest")<Distance("Summoning Bell"));
        workshopBulkBatches.Clear();foreach(var batch in batches)workshopBulkBatches.Enqueue(batch);
        workshopBulkRunning=true;workshopRunning=false;workshopPreparationWithdrawal=false;
        workshopWithdrawalCharacter=Player.ContentId;workshopWithdrawalWorld=Player.CurrentWorld.RowId;workshopCombinedHouse=WorkshopHouse();
        PauseWorkshop(true);NextWorkshopBulkBatch(now);return true;
    }
    private unsafe bool WorkshopBulkFits()
    {
        var manager=InventoryManager.Instance();if(manager==null)return false;
        if(workshopPreparation==null)return false;
        var needed=0;
        foreach(var high in new[]{false,true})foreach(var entry in high?workshopHqWithdraw:workshopNqWithdraw){
            var stack=(int)(DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(entry.ItemId)?.StackSize??1);
            if(CrystalStorage.IsItem(entry.ItemId))continue;
            var room=0;
            foreach(var type in new[]{InventoryType.Inventory1,InventoryType.Inventory2,InventoryType.Inventory3,InventoryType.Inventory4}){
                var container=manager->GetInventoryContainer(type);if(container==null||!container->IsLoaded)return false;
                for(var i=0;i<container->Size;i++){var item=container->Items[i];if(item.ItemId==entry.ItemId&&((item.Flags&InventoryItem.ItemFlags.HighQuality)!=0)==high)room+=Math.Max(0,stack-item.Quantity);}
            }
            needed+=WorkshopBulkPolicy.NewSlots(entry.Quantity,room,stack);
        }
        return needed<manager->GetEmptySlotsInBag();
    }
    private void NextWorkshopBulkBatch(DateTimeOffset now)
    {
        if(!workshopBulkBatches.TryDequeue(out var batch)){
            workshopBulkCurrent=null;workshopBulkRunning=false;workshopBulkTargets.Clear();
            workshopCombinedStage=3;workshopCombinedDeadline=now.AddMinutes(2);
            workshopStatus="Queue materials received · returning to fabrication station";return;
        }
        workshopBulkCurrent=batch;
        workshopBulkTargets=batch.Items.ToDictionary(e=>e.ItemId,e=>checked(RetrievalHeld(e.ItemId,batch.Quality)+e.Quantity));
        if(batch.Chest){BeginWorkshopChest(now);workshopChestDeposit=false;}
        else {workshopCombinedItems=batch.Items;workshopCombinedQuality=batch.Quality;workshopCombinedStage=1;workshopCombinedDeadline=now.AddMinutes(2);}
        workshopStatus=$"Preparing queue materials · {batch.Items.Length} items from "+(batch.Chest?"FC chest":"retainers");
    }
    private void FinishWorkshopBulkBatch(DateTimeOffset now)
    {
        if(workshopBulkCurrent==null||!workshopBulkTargets.All(e=>RetrievalHeld(e.Key,workshopBulkCurrent.Quality)>=e.Value)){
            StopWorkshop("Queue preparation stopped: allocated materials did not all arrive. Check space / stock, then resume. No delivery was started.");return;
        }
        NextWorkshopBulkBatch(now);
    }
    private void ResetWorkshopBulk()
    {
        workshopPreparedThrough=int.MaxValue;workshopPreparedPhaseOnly=false;
        workshopBulkRunning=workshopNeedsPreparation=false;workshopBulkCurrent=null;workshopBulkBatches.Clear();workshopBulkTargets.Clear();
    }
}
