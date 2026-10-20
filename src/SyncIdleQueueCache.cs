namespace EquinoxCompanion;

public sealed record SyncIdleQueueStamp(long Revision,int Houses,int Tending,int Planting,int Discoveries,int Sent,int Protocol,
    string PairingKey,string RegistrationScope,bool TrackGardens,bool CharacterDetails,bool Collections,bool AutoRetainer,bool Activities,bool HouseDetails);

// Only memoizes a queue with zero sendable records. Time-dependent validation is
// retried within 30 seconds; new observations explicitly invalidate immediately.
public sealed class SyncIdleQueueCache
{
    private SyncIdleQueueStamp? stamp;
    private DateTimeOffset captured,expires;
    public bool Unchanged(SyncIdleQueueStamp value,DateTimeOffset now)=>stamp==value&&now>=captured&&now<expires;
    public void Remember(SyncIdleQueueStamp value,DateTimeOffset now){stamp=value;captured=now;expires=now.AddSeconds(30);}
    public void Invalidate()=>stamp=null;
}
