namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private readonly MarketWatchClient marketWatchClient=new();
    private readonly Dictionary<(uint World,uint Item),MarketWatchSnapshot> marketWatchCache=[];
    private readonly Dictionary<(uint World,uint Item),DateTimeOffset> marketWatchNext=[];
    private readonly Dictionary<(ulong Retainer,int Slot),(MarketStock Item,SellerDecision Decision)> marketWatchReviews=[];
    private Task<MarketWatchBatch>? marketWatchTask;
    private CancellationTokenSource? marketWatchCancel;
    private (uint World,uint[] Items) marketWatchRequest;
    private DateTimeOffset marketWatchPoll,marketWatchRebuildAt;
    private string marketWatchNotice="Background checks start after retainer listings are observed.";
    private MarketRetainer[]? marketWatchStock;

    private void StopMarketWatch()
    {
        marketWatchCancel?.Cancel();marketWatchReviews.Clear();marketWatchStock=null;
    }
    private void UpdateMarketWatch(DateTimeOffset now)
    {
        if(marketWatchTask is {IsCompleted:true}){
            if(marketWatchTask.IsCompletedSuccessfully){
                var batch=marketWatchTask.Result;marketWatchNotice=batch.Error.Length>0?batch.Error:$"Background prices checked {DisplayTime.Local(now)}";
                marketWatchPoll=now+batch.RetryAfter;
                foreach(var item in marketWatchRequest.Items)marketWatchNext[(marketWatchRequest.World,item)]=now.AddMinutes(batch.Error.Length>0?5:Math.Clamp(config.Seller.BackgroundIntervalMinutes,5,120));
                foreach(var item in batch.Items)marketWatchCache[(item.World,item.Item)]=item;
            }else{_ = marketWatchTask.Exception;marketWatchPoll=now.AddMinutes(1);}
            marketWatchTask=null;marketWatchCancel?.Dispose();marketWatchCancel=null;marketWatchStock=null;
        }
        if(!config.Seller.BackgroundChecks){if(marketWatchCancel!=null)StopMarketWatch();return;}
        if(!ReferenceEquals(marketWatchStock,marketLedger.Retainers)||now>=marketWatchRebuildAt){
            marketWatchStock=marketLedger.Retainers;marketWatchRebuildAt=now.AddSeconds(30);marketWatchReviews.Clear();
            var active=new HashSet<(uint World,uint Item)>();
            foreach(var group in marketLedger.Retainers.GroupBy(x=>x.WorldId)){
                var ownNames=group.Select(x=>x.Name).Where(x=>x.Length>0).ToHashSet(StringComparer.Ordinal);
                foreach(var retainer in group)foreach(var item in retainer.Stock){
                    active.Add((group.Key,item.ItemId));
                    marketWatchReviews[(retainer.Id,item.Slot)]=(item,MarketWatchPolicy.Assess(item,group.Key,marketWatchCache.GetValueOrDefault((group.Key,item.ItemId)),ownNames,config.Seller,now));
                }
            }
            foreach(var key in marketWatchNext.Keys.Where(x=>!active.Contains(x)).ToArray())marketWatchNext.Remove(key);
            foreach(var key in marketWatchCache.Keys.Where(x=>!active.Contains(x)).ToArray())marketWatchCache.Remove(key);
        }
        // Never share a queue with gameplay sync, and do not start extra work while it is uploading.
        if(marketWatchTask!=null||now<marketWatchPoll||!Player.IsLoaded||sellerRun.Running||syncTask!=null)return;
        marketWatchPoll=now.AddSeconds(10);
        var due=marketLedger.Retainers.SelectMany(r=>r.Stock.Where(x=>!config.Seller.ExcludedItems.Contains(x.ItemId)).Select(x=>(World:r.WorldId,Item:x.ItemId))).Where(x=>x.World!=0).Distinct()
            .Where(x=>marketWatchNext.GetValueOrDefault(x)<=now).OrderBy(x=>marketWatchNext.GetValueOrDefault(x)).ToArray();
        if(due.Length==0)return;
        var world=due[0].World;var ids=due.Where(x=>x.World==world).Take(50).Select(x=>x.Item).ToArray();
        marketWatchRequest=(world,ids);marketWatchCancel=new();marketWatchTask=marketWatchClient.Read(world,ids,marketWatchCancel.Token);
    }
}
