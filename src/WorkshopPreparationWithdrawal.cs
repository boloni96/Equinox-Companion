namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private bool workshopPreparationWithdrawal;
    private readonly Queue<(RetrievalEntry Item,RetrievalQuality Quality)> workshopPreparationItems=new();
    private RetrievalQuality workshopPreparationQuality;
    private int WorkshopActiveQualityFilter=>workshopPreparationWithdrawal?(workshopPreparationQuality==RetrievalQuality.NqOnly?1:workshopPreparationQuality==RetrievalQuality.HqOnly?2:0):workshopWithdrawQuality;
    private int WorkshopPreparationHeld(uint id)=>RetrievalHeld(id,workshopPreparationQuality);
    private uint workshopPreparationItem;
    private int workshopPreparationTarget,workshopPreparationLastCount;
    private void BeginWorkshopPreparationWithdrawal(RetrievalEntry[] items,RetrievalQuality quality=RetrievalQuality.Any,RetrievalEntry[]? hqItems=null)
    {
        if(WorkshopUiBusy)throw new InvalidOperationException("Stop the active operation first.");
        if(!config.EnableWorkshop||!config.WorkshopWithdrawAndDeliver||WorkshopHouse().Length==0)throw new InvalidOperationException("Enable workshop automation and Withdraw + turn in, then enter your workshop.");
        if(items.Length==0&&(hqItems?.Length??0)==0)throw new InvalidOperationException("No recorded ingredient stock to withdraw.");
        workshopPreparationItems.Clear();foreach(var item in items)workshopPreparationItems.Enqueue((item,quality));
        foreach(var item in hqItems??[])workshopPreparationItems.Enqueue((item,RetrievalQuality.HqOnly));
        workshopActive=WorkshopSaved();workshopQueueRun=true;workshopPreparationWithdrawal=true;
        NextWorkshopPreparationItem(DateTimeOffset.UtcNow);
    }
    private void NextWorkshopPreparationItem(DateTimeOffset now)
    {
        if(!workshopPreparationItems.TryDequeue(out var next)){StopWorkshop("Ingredient withdrawal complete · ready for Artisan.");return;}
        var item=next.Item;workshopPreparationQuality=next.Quality;
        workshopPreparationItem=item.ItemId;workshopPreparationLastCount=WorkshopPreparationHeld(item.ItemId);workshopPreparationTarget=checked(workshopPreparationLastCount+item.Quantity);
        // Preparation allows mixed quality; each native transfer still uses one quality.
        BeginWorkshopRestock(new(item.ItemId,item.Name,1,workshopPreparationTarget),now);
    }
    private unsafe void FinishWorkshopPreparationItem(DateTimeOffset now)
    {
        if(SellerAddon("FreeCompanyChest")!=null){CloseSellerAddon("FreeCompanyChest");return;}
        if(SellerAddon("RetainerList")!=null){CloseSellerAddon("RetainerList");return;}
        var count=WorkshopPreparationHeld(workshopPreparationItem);
        if(count<workshopPreparationTarget){
            if(count<=workshopPreparationLastCount){StopWorkshop("Ingredient withdrawal incomplete · stock changed or capacity unavailable. Refresh materials before retrying.");return;}
            workshopPreparationLastCount=count;
            BeginWorkshopRestock(new(workshopPreparationItem,SearchItemName(workshopPreparationItem),1,workshopPreparationTarget),now,chestTried:true);return;
        }
        NextWorkshopPreparationItem(now);
    }
}
