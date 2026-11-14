namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private Task<InventoryReadResult>? sharedInventoryTask;
    private string sharedInventoryTaskKey="",sharedInventoryStatusKey="";
    private string sharedInventoryStatus="",sharedInventoryError="";
    private DateTimeOffset nextInventoryRead;
    private void UpdateSharedInventory(DateTimeOffset now)
    {
        if(sharedInventoryStatusKey!=config.PairingKey){
            sharedInventoryStatusKey=config.PairingKey;sharedInventoryStatus="";sharedInventoryError="";nextInventoryRead=default;
        }
        if(sharedInventoryTask?.IsCompleted==true){
            if(sharedInventoryTaskKey==config.PairingKey){
                var result=sharedInventoryTask.IsCompletedSuccessfully?sharedInventoryTask.Result:
                    new InventoryReadResult(null,"Shared inventory refresh failed; keeping saved inventory.");
                sharedInventoryError=result.Error??"";
                sharedInventoryStatus=result.Error??"Shared inventory updated.";
                if(result.Locations is {} locations&&config.SharedRoster is {ItemStoragePaging:>=1} current){
                    // Only inventory is replaced. A newer house/profile response
                    // may have arrived while these inventory pages were loading.
                    config.SharedRoster=result.ApplyTo(current);SaveConfiguration();
                }
                if(result.Error is {} error)errorJournal.Record("shared-inventory",error);
                nextInventoryRead=now.AddSeconds(result.Error==null?60:30);
            }
            if(sharedInventoryTask.IsFaulted)_=sharedInventoryTask.Exception;
            sharedInventoryTask=null;
        }
        if(!config.SyncEnabled||config.PairingKey.Length!=64||sharedInventoryTask!=null||now<nextInventoryRead||
            rosterStatusKey!=config.PairingKey||rosterReceivedAt==null||config.SharedRoster is not {ItemStoragePaging:>=1} roster)return;
        if(!visible&&!(itemSearchWindow?.IsOpen??false)&&!(bidWindow?.IsOpen??false)&&!(adventureWindow?.IsOpen??false)&&
            !FastGardenSync&&!config.RefreshSharedInBackground&&!RegistrationNeedsRoster)return;
        sharedInventoryTaskKey=config.PairingKey;
        sharedInventoryStatus="Updating shared inventory; house profiles refresh independently.";
        sharedInventoryTask=sync.ReadInventory(sharedInventoryTaskKey,roster);
    }
}
