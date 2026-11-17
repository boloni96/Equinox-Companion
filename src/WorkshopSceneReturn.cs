namespace EquinoxCompanion;
// A production scene returning to gameplay is a transition receipt even when
// the game closes every station window. A missing menu alone is not a receipt.
internal sealed class WorkshopSceneReturn
{
    internal bool Seen { get; private set; }
    private DateTimeOffset? returnedAt;
    internal bool Observe(bool inScene,bool loading,DateTimeOffset now)
    {
        if(inScene){Seen=true;returnedAt=null;return false;}
        if(!Seen||loading){returnedAt=null;return false;}
        returnedAt??=now;
        return now-returnedAt.Value>=TimeSpan.FromMilliseconds(500);
    }
    internal void Reset(){Seen=false;returnedAt=null;}
}
