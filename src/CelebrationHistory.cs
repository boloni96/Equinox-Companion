namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private CelebrationHistoryIndex celebrationHistory=new();
    private Task<CelebrationHistoryIndex>? celebrationHistoryTask;
    private object? celebrationHistorySource,celebrationHistoryCatalogue;
    private int celebrationHistoryCount;
    private SyncEvent? celebrationHistoryTail;
    private double celebrationHistoryBuildMs;
    private IEnumerable<bool> PrepareCelebrationHistory()
    {
        // A cancelled character check can leave a safe managed worker finishing.
        while(celebrationHistoryTask is {IsCompleted:false})yield return true;
        if(celebrationHistoryTask is {} previous){
            celebrationHistoryTask=null;
            if(previous.IsCompletedSuccessfully)celebrationHistory=previous.Result;
            else{_=previous.Exception;celebrationHistorySource=null;}
        }
        var rows=config.Discoveries;var catalogue=collectionGroups??catalogueBase;
        var reset=!ReferenceEquals(celebrationHistorySource,rows)||!ReferenceEquals(celebrationHistoryCatalogue,catalogue)||rows.Count<celebrationHistoryCount||
            celebrationHistoryCount>0&&!ReferenceEquals(rows[celebrationHistoryCount-1],celebrationHistoryTail);
        var from=reset?0:celebrationHistoryCount;
        if(from==rows.Count&&!reset)yield break;
        // Copy references, not the contents of the inventory/history records.
        var delta=new SyncEvent[rows.Count-from];rows.CopyTo(from,delta,0,delta.Length);
        var rewards=celebrationOffers!.Select(o=>o.Item).ToArray();var rewardIds=rewards.ToHashSet();
        var links=(catalogue??[]).SelectMany(g=>g).Where(e=>rewardIds.Contains(e.ItemId)).Select(e=>new CelebrationRewardLink(e.Category,e.Id,e.ItemId)).ToArray();
        var prior=reset?new CelebrationHistoryIndex():celebrationHistory;
        celebrationHistorySource=rows;celebrationHistoryCatalogue=catalogue;celebrationHistoryCount=rows.Count;celebrationHistoryTail=rows.LastOrDefault();
        var watch=System.Diagnostics.Stopwatch.GetTimestamp();
        // Worker captures only managed immutable records; no game/plugin services.
        celebrationHistoryTask=Task.Run(()=>prior.Append(delta,rewards,links));
        while(!celebrationHistoryTask.IsCompleted)yield return true;
        var task=celebrationHistoryTask;celebrationHistoryTask=null;
        celebrationHistoryBuildMs=System.Diagnostics.Stopwatch.GetElapsedTime(watch).TotalMilliseconds;
        if(!task.IsCompletedSuccessfully){_=task.Exception;celebrationHistorySource=null;throw new InvalidOperationException("Completion history preparation failed.");}
        celebrationHistory=task.Result;
    }
}
