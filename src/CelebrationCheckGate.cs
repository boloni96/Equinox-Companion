namespace EquinoxCompanion;

// Coalesce changed evidence, but never run merely because a timer expired.
internal sealed class CelebrationCheckGate
{
    private bool pending;
    private DateTimeOffset due;
    internal void Request(DateTimeOffset now)
    {
        if(!pending)due=now.AddMilliseconds(500);
        pending=true;
    }
    internal bool Take(DateTimeOffset now,bool ready)
    {
        if(!pending||!ready||now<due)return false;
        pending=false;return true;
    }
}
