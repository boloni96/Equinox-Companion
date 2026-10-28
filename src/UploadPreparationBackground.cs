using System.Text.Json;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private sealed record UploadOptions(string Pairing,int Protocol,bool Enabled,bool Gardens,bool Characters,bool Collections,bool AutoRetainer,bool Activities,bool Houses,bool Market);
    private UploadOptions CurrentUploadOptions()=>new(config.PairingKey,config.SharedRoster?.ProtocolVersion??1,config.SyncEnabled,config.TrackGardens,config.SyncCharacterDetails,config.SyncCollections,config.SyncAutoRetainer,config.SyncActivities,config.SyncHouseDetails,config.Seller.Enabled&&config.Seller.SyncToJournal);
    private Task<PreparedUpload>? uploadPreparation;
    private UploadOptions? uploadOptions;
    private SyncIdleQueueStamp uploadIdleStamp=null!;
    private void PrepareUpload(SyncEvent[] candidates,SyncIdleQueueStamp stamp,DateTimeOffset now)
    {
        uploadOptions=CurrentUploadOptions();uploadIdleStamp=stamp;
        var characters=DiscoveryReads().Kind("character.updated").ToArray();var protocol=uploadOptions.Protocol;var history=syncHistoryFirst;
        uploadPreparation=Task.Run(()=>UploadPreparation.Build(candidates,characters,now,protocol,history,batch=>JsonSerializer.SerializeToUtf8Bytes(new{events=batch},json).Length));
        syncStatus="Preparing recorded changes for upload…";
    }
    // True means preparation still owns this turn. No network/UI work runs in its task.
    private bool CompleteUploadPreparation(DateTimeOffset now)
    {
        if(uploadPreparation is null)return false;
        if(!uploadPreparation.IsCompleted)return true;
        var task=uploadPreparation;uploadPreparation=null;
        if(task.IsFaulted){errorJournal.Record("upload preparation","Preparation failed; records kept for retry.",exceptionType:task.Exception?.GetBaseException().GetType().Name);nextSync=now.AddSeconds(5);return true;}
        if(!task.IsCompletedSuccessfully||uploadOptions!=CurrentUploadOptions()){nextSync=default;return false;}
        var result=task.Result;
        pendingSyncRecords=result.Pending.Length;heldSyncRecords=result.Held.Length;historicalHeldSyncRecords=result.HistoricalHeld;unsupportedSyncRecords=result.Unsupported;readySyncRecords=result.Eligible;
        foreach(var e in result.Held)if(reportedHeldRecords.Add(e.Id))errorJournal.Record("held-record",SyncValidation.HoldReason(e,now),e.Id,e.Kind);
        var events=result.Events;
        if(events.Length==0){
            if(result.Eligible==0)idleSyncQueue.Remember(uploadIdleStamp,now);
            if(result.Eligible>0)syncStatus="Eligible records exceed the upload size limit; kept locally. Export diagnostics.";
            else syncStatus=result.Unsupported>0?"Server update needed for some recorded actions. They are kept locally; supported actions continue syncing.":"No new eligible changes to upload.";
            nextSync=now.AddSeconds(FastGardenSync?1:30);return true;
        }
        syncHistoryFirst=!syncHistoryFirst;lastSyncBatchSize=events.Length;
        lastSyncBatchOldest=events.Min(e=>e.At);lastSyncBatchNewest=events.Max(e=>e.At);
        syncStatus=$"Sending {events.Length} events · recent actions and history recovery…";
        lastSyncAttempt=now;syncTaskKey=config.PairingKey;
        var key=syncTaskKey;syncTask=Task.Run(()=>sync.Send(key,events));
        return true;
    }
}
