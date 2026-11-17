using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private Dictionary<(uint Item,int Set),Queue<bool>> workshopDeliveryQualities=[];
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
        workshopDeliveryQualities=workshopPreparedQualities.ToDictionary(e=>e.Key,e=>new Queue<bool>(e.Value));
        workshopNeedsPreparation=false;
        if(workshopNqWithdraw.Length==0&&workshopHqWithdraw.Length==0)return false;
        var position=Objects.LocalPlayer?.Position??Vector3.Zero;
        float Distance(string name)=>Objects.Where(o=>o.IsTargetable&&string.Equals(o.Name.TextValue,name,StringComparison.OrdinalIgnoreCase)).Select(o=>Vector3.DistanceSquared(position,o.Position)).DefaultIfEmpty(float.PositiveInfinity).Min();
        var stock=retrievalStock.Select(s=>new WorkshopBulkStock(s.Item,s.Quantity,s.Hq,false))
            .Concat(WorkshopRecordedChest(0).Select(s=>new WorkshopBulkStock(s.Item,s.Quantity,s.Hq,true)));
        var batches=WorkshopBulkPolicy.Allocate(workshopNqWithdraw,workshopHqWithdraw,stock,Distance("Company Chest")<Distance("Summoning Bell"));
        workshopBulkBatches.Clear();foreach(var batch in batches)workshopBulkBatches.Enqueue(batch);
        workshopChestDeposit=false;
        workshopBulkRunning=true;workshopRunning=false;workshopWithdrawOnly=false;
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
            if(CrystalStorage.IsItem(entry.ItemId)){
                if(CrystalStorage.Withdrawable(entry.ItemId,RetrievalHeld(entry.ItemId),entry.Quantity)<entry.Quantity)return false;
                continue;
            }
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
            if(workshopWithdrawOnly){StopWorkshop(WorkshopWithdrawalSummary());return;}
            workshopCombinedStage=3;workshopCombinedDeadline=now.AddMinutes(2);
            workshopStatus="Queue materials received · returning to fabrication station";return;
        }
        workshopBulkCurrent=batch;
        workshopRetainerTargets.Clear();
        foreach(var part in batch.RetainerParts)foreach(var e in part.Items){
            var held=RetrievalHeld(e.ItemId,part.Quality);
            workshopRetainerTargets[(e.ItemId,part.Quality==RetrievalQuality.HqOnly)]=checked(held+CrystalStorage.Withdrawable(e.ItemId,held,e.Quantity));
        }
        if(workshopWithdrawOnly){
            var items=new List<RetrievalEntry>();
            foreach(var e in batch.Items){
                var quantity=CrystalStorage.Withdrawable(e.ItemId,RetrievalHeld(e.ItemId,batch.Quality),e.Quantity);
                workshopDeferredCrystals+=e.Quantity-quantity;
                if(quantity>0)items.Add(e with{Quantity=quantity});
            }
            if(items.Count==0){NextWorkshopBulkBatch(now);return;}
            workshopBulkCurrent=batch=batch with{Items=items.ToArray()};
        }
        workshopBulkTargets=batch.Items.ToDictionary(e=>e.ItemId,e=>checked(RetrievalHeld(e.ItemId,batch.Quality)+e.Quantity));
        if(batch.Chest){BeginWorkshopChest(now);workshopChestDeposit=false;}
        else {workshopCombinedItems=batch.Items;workshopCombinedQuality=batch.Quality;workshopCombinedStage=1;workshopCombinedDeadline=now.AddMinutes(2);}
        workshopStatus=$"Preparing queue materials · {batch.Items.Length} items from "+(batch.Chest?"FC chest":"retainers");
    }
    private void FinishWorkshopBulkBatch(DateTimeOffset now)
    {
        if(workshopBulkCurrent==null||workshopRetainerTargets.Any(e=>RetrievalHeld(e.Key.Item,e.Key.Hq?RetrievalQuality.HqOnly:RetrievalQuality.NqOnly)<e.Value)||!workshopBulkTargets.All(e=>RetrievalHeld(e.Key,workshopBulkCurrent.Quality)>=e.Value)){
            StopWorkshop("Queue preparation stopped: allocated materials did not all arrive. Check space / stock, then resume. No delivery was started.");return;
        }
        NextWorkshopBulkBatch(now);
    }
    private bool? WorkshopDeliveryQuality(WorkshopSupply row)
    {
        var nq=workshopQueueRun&&workshopWithdrawQuality==2?0:RetrievalHeld(row.ItemId,RetrievalQuality.NqOnly);
        var hq=workshopQueueRun&&workshopWithdrawQuality==1?0:RetrievalHeld(row.ItemId,RetrievalQuality.HqOnly);
        if(workshopQueueRun&&workshopDeliveryQualities.TryGetValue((row.ItemId,row.SetSize),out var choices)&&choices.TryPeek(out var high))
            return (high?hq:nq)>=row.SetSize?high:null;
        return WorkshopPolicy.ChooseHq(nq,hq,row.SetSize);
    }
    private void ResetWorkshopBulk()
    {
        workshopWithdrawOnly=false;workshopRetainerTargets.Clear();workshopDeliveryQualities.Clear();
        workshopPreparedThrough=int.MaxValue;workshopPreparedPhaseOnly=false;
        workshopBulkRunning=workshopNeedsPreparation=false;workshopBulkCurrent=null;workshopBulkBatches.Clear();workshopBulkTargets.Clear();
    }
}
