using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private long lastUploadedCursor;
    private DeliveryStatus? deliveryStatus;
    private DateTimeOffset? deliveryCheckedAt;
    private string deliveryError="",deliveryKey="",deliveryTaskKey="",receiptTaskKey="",pendingReceipt="",receiptInFlight="";
    private Task<DeliveryResult>? deliveryTask;
    private Task<DeliveryAckResult>? receiptTask;
    private DateTimeOffset nextDeliveryRead,nextReceiptAttempt;
    private void ResetDeliveryStatus()
    {
        deliveryKey=config.PairingKey;lastUploadedCursor=0;deliveryStatus=null;deliveryCheckedAt=null;deliveryError="";pendingReceipt="";nextDeliveryRead=default;nextReceiptAttempt=default;
    }
    private void QueueRosterReceipt(RosterResult result)
    {
        if(DeliveryPolicy.CanAcknowledge(result,config.SharedRoster)){pendingReceipt=result.Receipt;}
    }
    private void UpdateDeliveryStatus(DateTimeOffset now)
    {
        if(deliveryKey!=config.PairingKey)ResetDeliveryStatus();
        if(receiptTask?.IsCompleted==true){
            if(receiptTaskKey==config.PairingKey){
                var result=receiptTask.IsCompletedSuccessfully?receiptTask.Result:new DeliveryAckResult(false,"Receipt confirmation pending; retrying.");
                if(result.Success){if(pendingReceipt==receiptInFlight)pendingReceipt="";nextDeliveryRead=default;}
                else{deliveryError=result.Error;nextReceiptAttempt=now.AddSeconds(15);}
            }
            if(receiptTask.IsFaulted)_=receiptTask.Exception;receiptTask=null;
        }
        if(deliveryTask?.IsCompleted==true){
            if(deliveryTaskKey==config.PairingKey){
                var result=deliveryTask.IsCompletedSuccessfully?deliveryTask.Result:new DeliveryResult(null,"Delivery status unavailable; retrying.");
                deliveryError=result.Error;if(result.Status is not null){deliveryStatus=result.Status;deliveryCheckedAt=now;}
            }
            if(deliveryTask.IsFaulted)_=deliveryTask.Exception;deliveryTask=null;
        }
        if(!config.SyncEnabled||config.PairingKey.Length!=64||(config.SharedRoster?.ProtocolVersion??0)<24)return;
        // Delivery reporting is lower priority than pending gameplay uploads.
        if(syncTask is not null||readySyncRecords>0)return;
        if(receiptTask is null&&pendingReceipt.Length>0&&now>=nextReceiptAttempt){receiptTaskKey=config.PairingKey;receiptInFlight=pendingReceipt;receiptTask=sync.AcknowledgeRoster(receiptTaskKey,receiptInFlight);}
        if(!visible&&!(bidWindow?.IsOpen??false)&&!(adventureWindow?.IsOpen??false)&&!config.RefreshSharedInBackground)return;
        if(deliveryTask is null&&now>=nextDeliveryRead){nextDeliveryRead=now.AddSeconds(15);deliveryTaskKey=config.PairingKey;deliveryTask=sync.ReadDelivery(deliveryTaskKey);}
    }
    private void DrawDeliveryDetails()
    {
        if((config.SharedRoster?.ProtocolVersion??0)<24){ImGui.TextUnformatted("Detailed delivery confirmation needs the matching Journal update.");return;}
        if(deliveryError.Length>0)ImGui.TextUnformatted(deliveryError);
        if(deliveryStatus is not {} status){ImGui.TextUnformatted("Delivery stages: awaiting server confirmation.");return;}
        var target=lastUploadedCursor>0?lastUploadedCursor:status.UploadedCursor;
        ImGui.TextUnformatted((lastUploadedCursor>0?"Latest upload from this plugin":"Latest shared upload")+": "+(target>0?$"stored through #{target}":"none reported"));
        ImGui.TextUnformatted("Applied in Cloudflare: "+(target>0?(status.AppliedCursor>=target?"confirmed":"pending")+ $" · through #{status.AppliedCursor}":"no upload to compare"));
        var peers=status.Peers.Where(p=>p.InstallationId!=config.InstallationId).ToArray();
        if(peers.Length==0)ImGui.TextUnformatted("Paired plugin receipt: no other installation has reported yet.");
        foreach(var peer in peers){
            var received=DeliveryPolicy.Received(peer,target);
            var recent=peer.LastSeenAt is {} seen&&DateTimeOffset.UtcNow-seen<TimeSpan.FromSeconds(90);
            var name=string.IsNullOrWhiteSpace(peer.LastCharacterName)?$"Plugin {peer.InstallationId[..6]}":$"Plugin {peer.InstallationId[..6]} (last upload: {peer.LastCharacterName})";
            ImGui.TextUnformatted(name+": "+(target==0?"no upload to compare":received?"received this update":peer.ReceivedAt is null?"receipt not reported":"waiting for this update")+(recent?" · recently connected":" · not recently connected"));
            if(peer.ReceivedAt is {} at)ImGui.TextUnformatted($"Last receipt: {DisplayTime.Local(at)} · through #{peer.ReceivedCursor}");
        }
        if(deliveryCheckedAt is {} checkedAt)ImGui.TextUnformatted("Status checked: "+DisplayTime.Local(checkedAt));
        ImGui.TextUnformatted("Plugin IDs identify installations, not Persons. A receipt confirms the roster was accepted by that plugin; it does not confirm every record matched a character.");
    }
}
