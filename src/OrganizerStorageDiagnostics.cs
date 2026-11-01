namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private string organizerLastStorageRoute="";
    private ulong organizerLastStorageCharacter;
    private string OrganizerStorageStatus()=>Player.ContentId!=organizerLastStorageCharacter?"":armoireRunning||organizerLastStorageRoute=="armoire"?armoireStatus:gearStatus;
    private readonly Queue<object> gearStorageHistory=[];
    private void StorageTrace(string route,string action,uint item,string reason)
    {
        organizerLastStorageRoute=route;organizerLastStorageCharacter=Player.ContentId;
        gearStorageHistory.Enqueue(new {at=DateTimeOffset.UtcNow,character=Player.ContentId,route,action,item,name=item==0?"":SearchItemName(item),reason});
        while(gearStorageHistory.Count>100)gearStorageHistory.Dequeue();
    }
    private object GearStorageDiagnostics()=>new {
        armoire=new {running=armoireRunning,phase=armoirePhase,status=armoireStatus,moved=armoireMoved,skipped=armoireSkipped,pending=armoirePending,remaining=armoireQueue.Count,rechecks=armoireRechecks,deadline=armoireDeadline,preview=armoirePreview.Select(p=>new {item=p.Item,name=p.Name,status=p.Status}).ToArray()},
        gear=new {running=gearRouting,phase=gearPhase,status=gearStatus,item=gearItem,destination=gearDestination.ToString(),stored=gearStored,skipped=gearSkipped,remaining=gearRemaining.Count,deliveries=gearDeliveries.Count,rechecks=gearRechecks,deadline=gearDeadline},
        snapshot=config.OrganizerDresserSnapshots.GetValueOrDefault(Player.ContentId),history=gearStorageHistory.ToArray()};
}
